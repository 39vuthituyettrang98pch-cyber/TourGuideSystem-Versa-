using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers;

[Authorize(Roles = "Admin")]
public sealed class PaymentPlanController : Controller
{
    private readonly AppDbContext _context;

    public PaymentPlanController(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var plansRaw = await _context.PaymentPlans
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var plans = plansRaw
            .OrderBy(plan => plan.Audience)
            .ThenBy(plan => plan.Price)
            .ToList();

        return View(plans);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new PaymentPlan { Audience = "Owner", DurationDays = 30, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PaymentPlan plan, CancellationToken cancellationToken)
    {
        plan.PlanCode = (plan.PlanCode ?? string.Empty).Trim().ToUpperInvariant();
        plan.PlanName = (plan.PlanName ?? string.Empty).Trim();
        plan.Audience = string.IsNullOrWhiteSpace(plan.Audience) ? "Owner" : plan.Audience.Trim();

        if (string.IsNullOrWhiteSpace(plan.PlanCode) || string.IsNullOrWhiteSpace(plan.PlanName))
            ModelState.AddModelError(string.Empty, "Vui lòng nhập mã gói và tên gói.");

        if (!ModelState.IsValid)
            return View(plan);

        var exists = await _context.PaymentPlans.AnyAsync(item => item.PlanCode == plan.PlanCode, cancellationToken);
        if (exists)
        {
            ModelState.AddModelError(nameof(plan.PlanCode), "Mã gói đã tồn tại.");
            return View(plan);
        }

        _context.PaymentPlans.Add(plan);
        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã tạo gói dịch vụ.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var plan = await _context.PaymentPlans.FindAsync([id], cancellationToken);
        if (plan == null)
            return NotFound();

        return View(plan);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PaymentPlan model, CancellationToken cancellationToken)
    {
        var plan = await _context.PaymentPlans.FindAsync([id], cancellationToken);
        if (plan == null)
            return NotFound();

        plan.PlanCode = (model.PlanCode ?? string.Empty).Trim().ToUpperInvariant();
        plan.PlanName = (model.PlanName ?? string.Empty).Trim();
        plan.Audience = string.IsNullOrWhiteSpace(model.Audience) ? "Owner" : model.Audience.Trim();
        plan.Price = model.Price;
        plan.DurationDays = model.DurationDays <= 0 ? 30 : model.DurationDays;
        plan.Description = model.Description;
        plan.IsActive = model.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã cập nhật gói dịch vụ.";
        return RedirectToAction(nameof(Index));
    }
}
