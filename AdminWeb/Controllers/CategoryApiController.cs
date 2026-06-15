using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/category")]
[ApiController]
public sealed class CategoryApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoryApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<CategoryDto>>>> GetCategories(
        [FromQuery] string lang = "vi",
        CancellationToken cancellationToken = default)
    {
        lang = NormalizeLanguage(lang);
        var categories = await _context.Categories
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.PoiCategories)
            .Where(item => item.Status == "active")
            .OrderBy(item => item.Id)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var data = categories.Select(category => MapCategory(category, lang)).ToList();
        return Ok(ApiResponse<IReadOnlyList<CategoryDto>>.Ok(data));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<CategoryDto>>> GetCategory(
        int id,
        [FromQuery] string lang = "vi",
        CancellationToken cancellationToken = default)
    {
        var category = await _context.Categories
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.PoiCategories)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.Id == id && item.Status == "active", cancellationToken);

        if (category == null)
            return NotFound(ApiResponse<CategoryDto>.Fail("Danh mục không tồn tại."));

        return Ok(ApiResponse<CategoryDto>.Ok(MapCategory(category, NormalizeLanguage(lang))));
    }

    private CategoryDto MapCategory(AdminWeb.Models.Category category, string languageCode)
    {
        var translation = category.Translations.FirstOrDefault(item => item.LanguageCode == languageCode)
            ?? category.Translations.FirstOrDefault(item => item.LanguageCode == "vi")
            ?? category.Translations.FirstOrDefault();

        return new CategoryDto
        {
            Id = category.Id,
            Name = translation?.Name ?? $"Danh mục #{category.Id}",
            IconUrl = ToAbsoluteUrl(category.IconUrl),
            PoiCount = category.PoiCategories.Count
        };
    }

    private static string NormalizeLanguage(string languageCode)
    {
        return string.IsNullOrWhiteSpace(languageCode)
            ? "vi"
            : languageCode.Trim().ToLowerInvariant();
    }

    private string? ToAbsoluteUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        var normalized = path.StartsWith('/') ? path : $"/{path}";
        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{normalized}";
    }
}
