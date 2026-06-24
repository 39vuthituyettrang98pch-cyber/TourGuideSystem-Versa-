using AdminWeb.Data;
using AdminWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Areas.Reviewer.Controllers;

[Area("Reviewer")]
[Authorize(Policy = "ReviewerAreaPolicy")]
public sealed class AccountController : AdminWeb.Controllers.StaffPortalControllerBase
{
    private const string AuthenticationScheme = "ReviewerScheme";

    public AccountController(AppDbContext context, PasswordService passwordService)
        : base(context, passwordService)
    {
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        ConfigureLoginView("Reviewer");

        var redirect = await PrepareLoginPortalAsync("Reviewer");
        return redirect ?? View("Login");
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Login(string username, string password)
    {
        return LoginStaffAsync(username, password, "Reviewer", "Login");
    }

    [AllowAnonymous]
    [HttpPost("/Reviewer/Logout")]
    [HttpPost("/Reviewer/Account/Logout")]
    [HttpPost("/Areas/Reviewer/Logout")]
    [HttpPost("/Areas/Reviewer/Account/Logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthenticationScheme);
        return Redirect("/Areas/Reviewer/Login");
    }

    [AllowAnonymous]
    [HttpGet("/Reviewer/Logout")]
    [HttpGet("/Reviewer/Account/Logout")]
    [HttpGet("/Areas/Reviewer/Logout")]
    [HttpGet("/Areas/Reviewer/Account/Logout")]
    public async Task<IActionResult> LogoutByGet()
    {
        await HttpContext.SignOutAsync(AuthenticationScheme);
        return Redirect("/Areas/Reviewer/Login");
    }
}
