using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin,Editor")]
[Route("PoiAi")]
public sealed class PoiAiController : Controller
{
    private readonly IGeminiService _geminiService;

    public PoiAiController(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public sealed class OptimizeRequest
    {
        public string RawText { get; set; } = "";
    }

    [HttpPost("OptimizeContent")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OptimizeContent([FromBody] OptimizeRequest request, CancellationToken cancellationToken)

    {
        if (request == null || string.IsNullOrWhiteSpace(request.RawText))
            return BadRequest(new { success = false, message = "rawText is required" });

        try
        {
            var result = await _geminiService.OptimizePoiContentAsync(request.RawText, cancellationToken);
            var parts = result.Split(new[] { "|||" }, StringSplitOptions.None);

            var shortText = parts.Length > 0 ? parts[0] : "";
            var fullText = parts.Length > 1 ? parts[1] : "";

            return Ok(new { success = true, shortDescription = shortText, fullDescription = fullText });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
