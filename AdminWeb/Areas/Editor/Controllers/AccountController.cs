using AdminWeb.Data;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Areas.Editor.Controllers;

[Area("Editor")]
[Authorize(Policy = "EditorAreaPolicy")]
public sealed class AccountController : AdminWeb.Controllers.StaffPortalControllerBase
{
    private const string AuthenticationScheme = "EditorScheme";

    public AccountController(AppDbContext context, PasswordService passwordService)
        : base(context, passwordService)
    {
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ConfigureLoginView("Editor");

        var redirect = await PrepareLoginPortalAsync("Editor");
        return redirect ?? View("Login");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Login(string username, string password)
    {
        return LoginStaffAsync(username, password, "Editor", "Login");
    }

    [AllowAnonymous]
    [HttpPost("/Editor/Logout")]
    [HttpPost("/Editor/Account/Logout")]
    [HttpPost("/Areas/Editor/Logout")]
    [HttpPost("/Areas/Editor/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthenticationScheme);
        return Redirect("/Areas/Editor/Login");
    }

    [AllowAnonymous]
    [HttpGet("/Editor/Logout")]
    [HttpGet("/Editor/Account/Logout")]
    [HttpGet("/Areas/Editor/Logout")]
    [HttpGet("/Areas/Editor/Account/Logout")]
    public async Task<IActionResult> LogoutByGet()
    {
        await HttpContext.SignOutAsync(AuthenticationScheme);
        return Redirect("/Areas/Editor/Login");
    }
}
