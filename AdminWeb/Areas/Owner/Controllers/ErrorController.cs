using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[AllowAnonymous]
public sealed class ErrorController : Controller
{
    [HttpGet]
    public IActionResult HttpStatus(int code = 404)
    {
        Response.StatusCode = code;
        ViewData["Title"] = code == 404 ? "Không tìm thấy" : $"Lỗi {code}";
        ViewBag.StatusCode = code;
        return View();
    }
}
