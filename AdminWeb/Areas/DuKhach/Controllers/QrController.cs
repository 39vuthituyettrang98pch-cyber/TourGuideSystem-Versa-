using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
public sealed class QrController : Controller
{
    private readonly AppDbContext _context;

    public QrController(AppDbContext context)
    {
        _context = context;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Scan(string? lang = null)
    {
        ViewData["Title"] = "Quét QR";
        ViewBag.SelectedLanguageCode = NormalizeLanguageCode(lang ?? Request.Cookies["versa.dukhach.lang"]);
        return View();
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Open(string? value, string? lang = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            TempData["DuKhachErrorMessage"] = "Không đọc được nội dung mã QR.";
            return RedirectToAction(nameof(Scan), new { lang });
        }

        var lookup = ExtractPoiLookup(value);

        if (lookup.PoiId == null && string.IsNullOrWhiteSpace(lookup.Token))
        {
            TempData["DuKhachErrorMessage"] = "Mã QR không đúng định dạng POI.";
            return RedirectToAction(nameof(Scan), new { lang });
        }

        var poi = lookup.PoiId.HasValue
            ? await _context.Pois
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == lookup.PoiId.Value && item.Status == "Approved", cancellationToken)
            : await _context.Pois
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.QrCodeToken == lookup.Token && item.Status == "Approved", cancellationToken);

        if (poi == null)
        {
            TempData["DuKhachErrorMessage"] = "POI không tồn tại, chưa được duyệt hoặc mã QR đã hết hiệu lực.";
            return RedirectToAction(nameof(Scan), new { lang });
        }

        var languageCode = NormalizeLanguageCode(lang ?? Request.Cookies["versa.dukhach.lang"]);
        var detailsUrl = Url.Action("Details", "Map", new { area = "DuKhach", id = poi.Id, lang = languageCode })
            ?? $"/DuKhach/Map/Details/{poi.Id}?lang={Uri.EscapeDataString(languageCode)}";

        return Redirect(detailsUrl);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> PoiImage(int id, string? lang = null, bool download = false, CancellationToken cancellationToken = default)
    {
        var poiExists = await _context.Pois
            .AsNoTracking()
            .AnyAsync(item => item.Id == id && item.Status == "Approved", cancellationToken);

        if (!poiExists)
            return NotFound();

        var languageCode = NormalizeLanguageCode(lang ?? Request.Cookies["versa.dukhach.lang"]);
        var payload = BuildPoiDetailsUrl(id, languageCode);

        using var generator = new QRCodeGenerator();
        using var qrData = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var image = qrCode.GetGraphic(20);

        return download
            ? File(image, "image/png", $"POI-{id}-WEB-QR.png")
            : File(image, "image/png");
    }

    private string BuildPoiDetailsUrl(int poiId, string languageCode)
    {
        return Url.Action(
                "Details",
                "Map",
                new { area = "DuKhach", id = poiId, lang = languageCode },
                Request.Scheme)
            ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}/DuKhach/Map/Details/{poiId}?lang={Uri.EscapeDataString(languageCode)}";
    }

    private static PoiQrLookup ExtractPoiLookup(string rawValue, int depth = 0)
    {
        if (depth > 2)
            return new PoiQrLookup();

        var value = Uri.UnescapeDataString(rawValue.Trim());

        if (int.TryParse(value, out var directId) && directId > 0)
            return new PoiQrLookup { PoiId = directId };

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            if (string.Equals(uri.Scheme, "versa", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(uri.Host, "poi", StringComparison.OrdinalIgnoreCase))
            {
                var tokenOrId = uri.AbsolutePath.Trim('/');

                if (int.TryParse(tokenOrId, out var appId) && appId > 0)
                    return new PoiQrLookup { PoiId = appId };

                if (LooksLikeToken(tokenOrId))
                    return new PoiQrLookup { Token = tokenOrId };
            }

            var query = QueryHelpers.ParseQuery(uri.Query);

            foreach (var key in new[] { "poiId", "id" })
            {
                if (query.TryGetValue(key, out var values) &&
                    int.TryParse(values.FirstOrDefault(), out var queryId) &&
                    queryId > 0)
                {
                    return new PoiQrLookup { PoiId = queryId };
                }
            }

            foreach (var key in new[] { "token", "qr", "value" })
            {
                if (query.TryGetValue(key, out var values))
                {
                    var nestedValue = values.FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(nestedValue))
                        return ExtractPoiLookup(nestedValue, depth + 1);
                }
            }

            if (!string.IsNullOrWhiteSpace(uri.Fragment))
            {
                var fragment = uri.Fragment.TrimStart('#');

                if (fragment.StartsWith("poi-", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(fragment["poi-".Length..], out var hashId) &&
                    hashId > 0)
                {
                    return new PoiQrLookup { PoiId = hashId };
                }
            }

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var segment in segments.Reverse())
            {
                if (int.TryParse(segment, out var pathId) && pathId > 0)
                    return new PoiQrLookup { PoiId = pathId };

                if (LooksLikeToken(segment))
                    return new PoiQrLookup { Token = segment };
            }
        }

        if (value.StartsWith("poi-", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(value["poi-".Length..], out var plainHashId) &&
            plainHashId > 0)
        {
            return new PoiQrLookup { PoiId = plainHashId };
        }

        if (LooksLikeToken(value))
            return new PoiQrLookup { Token = value };

        return new PoiQrLookup();
    }

    private static bool LooksLikeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var token = value.Trim();

        return token.Length is >= 8 and <= 128 &&
               token.All(character =>
                   char.IsLetterOrDigit(character) ||
                   character is '-' or '_' or '.');
    }

    private static string NormalizeLanguageCode(string? languageCode)
    {
        var value = (languageCode ?? "vi").Trim().ToLowerInvariant();

        return value.Length is >= 2 and <= 10 &&
               value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? value
            : "vi";
    }

    private sealed class PoiQrLookup
    {
        public int? PoiId { get; set; }
        public string? Token { get; set; }
    }
}