using System.Security.Claims;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
public sealed class PaymentsController : Controller
{
    private readonly AppDbContext _context;
    private readonly VnPayPaymentService _vnPay;
    private readonly MomoPaymentService _momo;
    private readonly PaymentActivationService _activation;

    public PaymentsController(
        AppDbContext context,
        VnPayPaymentService vnPay,
        MomoPaymentService momo,
        PaymentActivationService activation)
    {
        _context = context;
        _vnPay = vnPay;
        _momo = momo;
        _activation = activation;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Plans(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Gói dịch vụ";
        ViewBag.VnPayConfigured = _vnPay.IsConfigured;
        ViewBag.MomoConfigured = _momo.IsConfigured;
        ViewBag.MomoDemoMode = _momo.IsDemoMode;

        var visitorTouristId = await GetTouristIdFromTouristCookieAsync();
        ViewBag.HasActivePremium = visitorTouristId.HasValue && await HasActivePremiumAsync(visitorTouristId.Value, cancellationToken);

        // SQLite không hỗ trợ ORDER BY trực tiếp trên decimal.
        // Lấy danh sách trước rồi sắp xếp trên bộ nhớ để tránh lỗi runtime.
        var plans = (await _context.PaymentPlans
                .AsNoTracking()
                .Where(plan => plan.IsActive && (plan.Audience == "Tourist" || plan.Audience == "Both"))
                .ToListAsync(cancellationToken))
            .OrderBy(plan => plan.Price)
            .ToList();

        return View(plans);
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Thanh toán của tôi";

        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        ViewBag.ActiveSubscription = await _context.TouristSubscriptions
            .Include(item => item.PaymentPlan)
            .Where(item => item.TouristId == touristId.Value && item.Status == "Active")
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        var payments = await _context.PaymentTransactions
            .Include(payment => payment.PaymentPlan)
            .Where(payment => payment.TouristId == touristId.Value)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(payments);
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Purchase(int planId, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive, cancellationToken);
        if (plan == null)
            return NotFound();

        _context.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionCode = NewTransactionCode("USR", touristId.Value),
            PayerType = "Tourist",
            TouristId = touristId.Value,
            PaymentPlanId = plan.Id,
            Purpose = "TouristPremium",
            Amount = plan.Price,
            Currency = "VND",
            PaymentMethod = "Manual",
            Status = "Pending",
            GatewayStatus = "MANUAL_PENDING",
            Note = "Thanh toán thủ công. Admin xác nhận để mở quyền premium.",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        TempData["DuKhachSuccessMessage"] = "Đã tạo yêu cầu thanh toán thủ công. Vui lòng chờ hệ thống xác nhận.";
        return RedirectToAction(nameof(Plans));
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseVnPay(int planId, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        if (!_vnPay.IsConfigured)
        {
            TempData["DuKhachErrorMessage"] = "VNPay chưa cấu hình TmnCode/HashSecret nên chưa thể tạo link thanh toán. Vui lòng kiểm tra appsettings.json.";
            return RedirectToAction(nameof(Plans));
        }

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive, cancellationToken);
        if (plan == null)
            return NotFound();

        var payment = CreateTouristOnlinePayment(touristId.Value, plan, "VNPay", "Thanh toán online qua VNPay. Gói tự kích hoạt khi VNPay xác nhận thành công.");
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var returnUrl = $"{Request.Scheme}://{Request.Host}/DuKhach/Payments/VnPayReturn";
            var checkoutUrl = _vnPay.CreatePaymentUrl(payment, returnUrl, GetClientIpAddress());
            payment.CheckoutUrl = checkoutUrl;
            payment.GatewayOrderCode = payment.TransactionCode;
            payment.GatewayStatus = "PENDING";
            await _context.SaveChangesAsync(cancellationToken);
            return Redirect(checkoutUrl);
        }
        catch (Exception ex)
        {
            payment.Status = "Cancelled";
            payment.GatewayStatus = "CREATE_FAILED";
            payment.Note = "Không tạo được link VNPay: " + ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "Không tạo được link thanh toán VNPay: " + ex.Message;
            return RedirectToAction(nameof(Plans));
        }
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurchaseMomo(int planId, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        if (!_momo.CanCreatePayment)
        {
            TempData["DuKhachErrorMessage"] = "Chưa cấu hình MoMo hoặc chưa bật chế độ mô phỏng MoMo.";
            return RedirectToAction(nameof(Plans));
        }

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive, cancellationToken);
        if (plan == null)
            return NotFound();

        var payment = CreateTouristOnlinePayment(
            touristId.Value,
            plan,
            "MoMo",
            _momo.IsDemoMode
                ? "Thanh toán MoMo. Bấm xác nhận trên trang thanh toán để kích hoạt gói."
                : "Thanh toán online qua ví MoMo. Gói tự kích hoạt khi MoMo xác nhận thành công.");
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        if (_momo.IsDemoMode)
        {
            payment.CheckoutUrl = Url.Action(nameof(MomoDemo), "Payments", new { area = "DuKhach", id = payment.Id }, Request.Scheme);
            payment.GatewayOrderCode = payment.TransactionCode;
            payment.GatewayPaymentLinkId = $"MOMO-DEMO-{payment.Id}";
            payment.GatewayStatus = "MOMO_DEMO_PENDING";
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(MomoDemo), "Payments", new { area = "DuKhach", id = payment.Id });
        }

        try
        {
            var returnUrl = Url.Action(nameof(MomoReturn), "Payments", new { area = "DuKhach" }, Request.Scheme)!;
            var ipnUrl = $"{Request.Scheme}://{Request.Host}/api/payment/momo/ipn";
            var result = await _momo.CreatePaymentAsync(payment, $"VERSA Tourist {payment.Id}", returnUrl, ipnUrl, cancellationToken);
            payment.CheckoutUrl = result.PayUrl;
            payment.GatewayOrderCode = payment.TransactionCode;
            payment.GatewayPaymentLinkId = result.RequestId;
            payment.GatewayStatus = "PENDING";
            await _context.SaveChangesAsync(cancellationToken);
            return Redirect(result.PayUrl);
        }
        catch (Exception ex)
        {
            payment.Status = "Cancelled";
            payment.GatewayStatus = "CREATE_FAILED";
            payment.Note = "Không tạo được link MoMo: " + ex.Message;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "Không tạo được link thanh toán MoMo: " + ex.Message;
            return RedirectToAction(nameof(Plans));
        }
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> MomoDemo(int id, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode)
            return RedirectToAction(nameof(Plans));

        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId.Value, cancellationToken);

        if (payment == null)
            return NotFound();

        return View(payment);
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmMomoDemo(int id, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode)
            return RedirectToAction(nameof(Plans));

        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId.Value, cancellationToken);

        if (payment == null)
            return NotFound();

        if (payment.Status != "Paid")
        {
            payment.GatewayStatus = "MOMO_DEMO_PAID";
            payment.GatewayPaymentLinkId ??= $"MOMO-DEMO-{payment.Id}";
            await _activation.MarkPaymentPaidAsync(payment, "MOMO_DEMO_PAID", cancellationToken);
        }

        TempData["DuKhachSuccessMessage"] = "MoMo đã xác nhận thanh toán. Gói đã được kích hoạt.";
        return RedirectToAction(nameof(My));
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var txnRef = _vnPay.GetTxnRef(Request.Query);
        if (string.IsNullOrWhiteSpace(txnRef))
        {
            TempData["DuKhachErrorMessage"] = "Bạn đã hủy hoặc thoát khỏi trang thanh toán VNPay.";
            return RedirectToAction(nameof(My));
        }

        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach", returnUrl = $"/DuKhach/Payments/VnPayReturn{Request.QueryString}" });

        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == txnRef && item.TouristId == touristId.Value, cancellationToken);

        if (payment == null)
        {
            TempData["DuKhachErrorMessage"] = "Không tìm thấy giao dịch VNPay thuộc tài khoản du khách hiện tại.";
            return RedirectToAction(nameof(My));
        }

        if (!_vnPay.VerifySignature(Request.Query))
        {
            payment.GatewayStatus = "INVALID_SIGNATURE";
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "VNPay trả về chữ ký không hợp lệ.";
            return RedirectToAction(nameof(My));
        }

        var gatewayStatus = _vnPay.GetGatewayStatus(Request.Query);
        payment.GatewayStatus = gatewayStatus;
        payment.GatewayPaymentLinkId = _vnPay.GetTransactionNo(Request.Query);

        if (_vnPay.IsSuccess(Request.Query))
        {
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
            TempData["DuKhachSuccessMessage"] = "Thanh toán VNPay thành công. Gói đã được kích hoạt.";
        }
        else
        {
            if (gatewayStatus == "24")
            {
                payment.Status = "Cancelled";
                payment.Note = "Người dùng đã hủy thanh toán VNPay.";
                TempData["DuKhachErrorMessage"] = "Bạn đã hủy thanh toán VNPay. Gói chưa được kích hoạt.";
            }
            else
            {
                TempData["DuKhachErrorMessage"] = $"VNPay chưa xác nhận thanh toán thành công. Mã phản hồi: {gatewayStatus}.";
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(My));
    }

    [Authorize(Policy = "TouristAreaPolicy")]
    [HttpGet]
    public async Task<IActionResult> MomoReturn(CancellationToken cancellationToken)
    {
        if (!_momo.VerifySignature(Request.Query))
        {
            TempData["DuKhachErrorMessage"] = "MoMo trả về chữ ký không hợp lệ.";
            return RedirectToAction(nameof(My));
        }

        var touristId = GetTouristId();
        if (touristId == null)
            return RedirectToAction("Login", "Account", new { area = "DuKhach" });

        var orderId = _momo.GetOrderId(Request.Query);
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == orderId && item.TouristId == touristId.Value, cancellationToken);

        if (payment == null)
            return NotFound();

        payment.GatewayStatus = _momo.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _momo.GetTransactionId(Request.Query);

        if (_momo.IsSuccess(Request.Query))
        {
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
            TempData["DuKhachSuccessMessage"] = "Thanh toán MoMo thành công. Gói đã được kích hoạt.";
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "MoMo chưa xác nhận thanh toán thành công.";
        }

        return RedirectToAction(nameof(My));
    }

    private static PaymentTransaction CreateTouristOnlinePayment(int touristId, PaymentPlan plan, string method, string note) => new()
    {
        TransactionCode = NewTransactionCode("USR", touristId),
        PayerType = "Tourist",
        TouristId = touristId,
        PaymentPlanId = plan.Id,
        Purpose = "TouristPremium",
        Amount = plan.Price,
        Currency = "VND",
        PaymentMethod = method,
        Status = "Pending",
        GatewayStatus = "PENDING",
        Note = note,
        CreatedAt = DateTime.UtcNow
    };

    private int? GetTouristId()
    {
        var touristIdText = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(touristIdText, out var touristId) ? touristId : null;
    }

    private async Task<int?> GetTouristIdFromTouristCookieAsync()
    {
        var auth = await HttpContext.AuthenticateAsync("TouristScheme");
        if (!auth.Succeeded || auth.Principal == null)
            return null;

        var touristIdText = auth.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(touristIdText, out var touristId) ? touristId : null;
    }

    private async Task<bool> HasActivePremiumAsync(int touristId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await _context.TouristSubscriptions
            .AsNoTracking()
            .Include(item => item.PaymentPlan)
            .AnyAsync(item =>
                item.TouristId == touristId &&
                item.Status == "Active" &&
                item.ExpiresAt > now &&
                item.PaymentPlan != null &&
                (item.PaymentPlan.PlanCode == "USER_PREMIUM" || item.PaymentPlan.Audience == "Tourist" || item.PaymentPlan.Audience == "Both"),
                cancellationToken);
    }

    private string GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private static string NewTransactionCode(string prefix, int userId) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{userId}-{Guid.NewGuid().ToString("N")[..6]}";
}
