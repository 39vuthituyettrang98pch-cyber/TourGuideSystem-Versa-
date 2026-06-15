using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class PoiController : AdminWeb.Controllers.PoiController
{
    public PoiController(AppDbContext context, IWebHostEnvironment environment)
        : base(context, environment)
    {
    }
}

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class PoiTranslationController : AdminWeb.Controllers.PoiTranslationController
{
    public PoiTranslationController(AppDbContext context, IWebHostEnvironment environment)
        : base(context, environment)
    {
    }
}

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class TourController : AdminWeb.Controllers.TourController
{
    public TourController(AppDbContext context)
        : base(context)
    {
    }
}

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class TourTranslationController : AdminWeb.Controllers.TourTranslationController
{
    public TourTranslationController(AppDbContext context)
        : base(context)
    {
    }
}

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class CategoryController : AdminWeb.Controllers.CategoryController
{
    public CategoryController(AppDbContext context)
        : base(context)
    {
    }
}

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class CategoryTranslationController : AdminWeb.Controllers.CategoryTranslationController
{
    public CategoryTranslationController(AppDbContext context)
        : base(context)
    {
    }
}

[Area("Editor")]
[Authorize(Roles = "Editor")]
public sealed class MediaController : AdminWeb.Controllers.MediaController
{
    public MediaController(AppDbContext context, IWebHostEnvironment environment)
        : base(context, environment)
    {
    }
}
