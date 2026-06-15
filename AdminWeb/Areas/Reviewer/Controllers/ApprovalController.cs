using AdminWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AdminWeb.Areas.Reviewer.Controllers;

[Area("Reviewer")]
[Authorize(Roles = "Reviewer")]
public sealed class ApprovalController : AdminWeb.Controllers.ApprovalController
{
    public ApprovalController(AppDbContext context)
        : base(context)
    {
    }
}
