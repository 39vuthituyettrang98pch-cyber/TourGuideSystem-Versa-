using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/languages")]
[ApiController]
public sealed class LanguageApiController : ControllerBase
{
    private readonly AppDbContext _context;

    public LanguageApiController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LanguageDto>>>> GetActiveLanguages(
        CancellationToken cancellationToken = default)
    {
        var languages = await _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageCode == "vi" ? 0 : language.LanguageCode == "en" ? 1 : 2)
            .ThenBy(language => language.LanguageName)
            .Select(language => new LanguageDto
            {
                Code = language.LanguageCode.Trim().ToLower(),
                Name = language.LanguageName,
                NativeName = language.LanguageName,
                EdgeTtsVoice = language.EdgeTtsVoice
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<IReadOnlyList<LanguageDto>>.Ok(languages));
    }
}
