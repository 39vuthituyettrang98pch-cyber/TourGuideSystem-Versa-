using AdminWeb.Data;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class PoiController : AdminWeb.Controllers.PoiController
{
    public PoiController(AppDbContext context, IWebHostEnvironment environment)
        : base(context, environment)
    {
    }

    [HttpGet("/Areas/Editor/Poi/MapData")]
    public Task<IActionResult> AreaMapData()
    {
        return base.MapData();
    }
}

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class PoiTranslationController : AdminWeb.Controllers.PoiTranslationController
{
    public PoiTranslationController(
        AppDbContext context,
        IWebHostEnvironment environment,
        IGeminiService geminiService)
        : base(context, environment, geminiService)
    {
    }
}

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class TourController : AdminWeb.Controllers.TourController
{
    public TourController(
        AppDbContext context,
        ContentTranslationService translationService)
        : base(context, translationService)
    {
    }
}

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class TourTranslationController : AdminWeb.Controllers.TourTranslationController
{
    public TourTranslationController(AppDbContext context)
        : base(context)
    {
    }
}

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class CategoryController : AdminWeb.Controllers.CategoryController
{
    public CategoryController(
        AppDbContext context,
        ContentTranslationService translationService)
        : base(context, translationService)
    {
    }
}

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class CategoryTranslationController : AdminWeb.Controllers.CategoryTranslationController
{
    public CategoryTranslationController(AppDbContext context)
        : base(context)
    {
    }
}

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class MediaController : AdminWeb.Controllers.MediaController
{
    public MediaController(AppDbContext context, IWebHostEnvironment environment)
        : base(context, environment)
    {
    }
}
