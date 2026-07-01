using System.Security.Claims;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Areas.Owner.Controllers;

[Area("Owner")]
[Authorize(Policy = "OwnerAreaPolicy")]
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

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        // SQLite không hỗ trợ ORDER BY trực tiếp trên decimal.
        // Lấy danh sách trước rồi sắp xếp trên bộ nhớ để tránh lỗi runtime.
        ViewBag.Plans = (await _context.PaymentPlans
                .AsNoTracking()
                .Where(plan => plan.IsActive && (plan.Audience == "Owner" || plan.Audience == "Both"))
                .ToListAsync(cancellationToken))
            .OrderBy(plan => plan.Price)
            .ToList();

        ViewBag.ActiveSubscription = await _context.OwnerSubscriptions
            .Include(item => item.PaymentPlan)
            .Where(item => item.OwnerProfileId == owner.Id && item.Status == "Active")
            .OrderByDescending(item => item.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);

        ViewBag.VnPayConfigured = _vnPay.IsConfigured;
        ViewBag.MomoConfigured = _momo.IsConfigured;
        ViewBag.MomoDemoMode = _momo.IsDemoMode;

        var payments = await _context.PaymentTransactions
            .Include(payment => payment.PaymentPlan)
            .Where(payment => payment.OwnerProfileId == owner.Id)
            .OrderByDescending(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);

        return View(payments);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateFree(int planId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var plan = await _context.PaymentPlans
            .FirstOrDefaultAsync(item =>
                item.Id == planId &&
                item.IsActive &&
                item.Price <= 0 &&
                (item.Audience == "Owner" || item.Audience == "Both"),
                cancellationToken);

        if (plan == null)
            return NotFound();

        var payment = new PaymentTransaction
        {
            TransactionCode = NewTransactionCode("OWNFREE", owner.Id),
            PayerType = "Owner",
            OwnerProfileId = owner.Id,
            PaymentPlanId = plan.Id,
            PaymentPlan = plan,
            Purpose = "OwnerSubscription",
            Amount = 0,
            Currency = "VND",
            PaymentMethod = "Free",
            Status = "Pending",
            GatewayStatus = "FREE_PENDING",
            Note = "Kích hoạt gói miễn phí cho chủ gian hàng.",
            CreatedAt = DateTime.UtcNow
        };

        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);
        await _activation.MarkPaymentPaidAsync(payment, "FREE_ACTIVATED", cancellationToken);

        TempData["SuccessMessage"] = "Đã kích hoạt gói miễn phí cho chủ gian hàng.";
        return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int planId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive, cancellationToken);
        if (plan == null)
            return NotFound();

        _context.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionCode = NewTransactionCode("OWN", owner.Id),
            PayerType = "Owner",
            OwnerProfileId = owner.Id,
            PaymentPlanId = plan.Id,
            Purpose = "OwnerSubscription",
            Amount = plan.Price,
            Currency = "VND",
            PaymentMethod = "Manual",
            Status = "Pending",
            GatewayStatus = "MANUAL_PENDING",
            Note = "Thanh toán thủ công. Admin xác nhận đã thu tiền để kích hoạt gói.",
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        TempData["SuccessMessage"] = "Đã tạo yêu cầu thanh toán thủ công. Vui lòng chờ admin xác nhận.";
        return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateVnPay(int planId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        if (!_vnPay.IsConfigured)
        {
            TempData["ErrorMessage"] = "Chưa cấu hình VNPay. Hãy điền VnPay:TmnCode, VnPay:HashSecret và VnPay:PaymentUrl.";
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive, cancellationToken);
        if (plan == null)
            return NotFound();

        var payment = CreateOwnerOnlinePayment(owner, plan, "VNPay", "Thanh toán online qua VNPay. Gói tự kích hoạt khi VNPay xác nhận thành công.");
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var returnUrl = $"{Request.Scheme}://{Request.Host}/Owner/Payments/VnPayReturn";
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
            TempData["ErrorMessage"] = "Không tạo được link thanh toán VNPay: " + ex.Message;
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMomo(int planId, CancellationToken cancellationToken)
    {
        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Create", "Profile", new { area = "Owner" });

        if (!_momo.CanCreatePayment)
        {
            TempData["ErrorMessage"] = "Chưa cấu hình MoMo hoặc chưa bật chế độ mô phỏng MoMo.";
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }

        var plan = await _context.PaymentPlans.FirstOrDefaultAsync(item => item.Id == planId && item.IsActive, cancellationToken);
        if (plan == null)
            return NotFound();

        var payment = CreateOwnerOnlinePayment(
            owner,
            plan,
            "MoMo",
            _momo.IsDemoMode
                ? "Thanh toán MoMo. Bấm xác nhận trên trang thanh toán để kích hoạt gói."
                : "Thanh toán online qua ví MoMo. Gói tự kích hoạt khi MoMo xác nhận thành công.");
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        if (_momo.IsDemoMode)
        {
            payment.CheckoutUrl = Url.Action(nameof(MomoDemo), "Payments", new { area = "Owner", id = payment.Id }, Request.Scheme);
            payment.GatewayOrderCode = payment.TransactionCode;
            payment.GatewayPaymentLinkId = $"MOMO-DEMO-{payment.Id}";
            payment.GatewayStatus = "MOMO_DEMO_PENDING";
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(MomoDemo), "Payments", new { area = "Owner", id = payment.Id });
        }

        try
        {
            var returnUrl = Url.Action(nameof(MomoReturn), "Payments", new { area = "Owner" }, Request.Scheme)!;
            var ipnUrl = $"{Request.Scheme}://{Request.Host}/api/payment/momo/ipn";
            var result = await _momo.CreatePaymentAsync(payment, $"VERSA Owner {payment.Id}", returnUrl, ipnUrl, cancellationToken);
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
            TempData["ErrorMessage"] = "Không tạo được link thanh toán MoMo: " + ex.Message;
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MomoDemo(int id, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode)
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });

        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Login", "Account", new { area = "Owner" });

        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.Id == id && item.OwnerProfileId == owner.Id, cancellationToken);

        if (payment == null)
            return NotFound();

        return View(payment);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmMomoDemo(int id, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode)
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });

        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Login", "Account", new { area = "Owner" });

        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.Id == id && item.OwnerProfileId == owner.Id, cancellationToken);

        if (payment == null)
            return NotFound();

        if (payment.Status != "Paid")
        {
            payment.GatewayStatus = "MOMO_DEMO_PAID";
            payment.GatewayPaymentLinkId ??= $"MOMO-DEMO-{payment.Id}";
            await _activation.MarkPaymentPaidAsync(payment, "MOMO_DEMO_PAID", cancellationToken);
        }

        TempData["SuccessMessage"] = "MoMo đã xác nhận thanh toán. Gói đã được kích hoạt.";
        return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
    }

    [HttpGet]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var txnRef = _vnPay.GetTxnRef(Request.Query);
        if (string.IsNullOrWhiteSpace(txnRef))
        {
            TempData["ErrorMessage"] = "Bạn đã hủy hoặc thoát khỏi trang thanh toán VNPay.";
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }

        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Login", "Account", new { area = "Owner", returnUrl = $"/Owner/Payments/VnPayReturn{Request.QueryString}" });

        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == txnRef && item.OwnerProfileId == owner.Id, cancellationToken);

        if (payment == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy giao dịch VNPay thuộc tài khoản chủ gian hàng hiện tại.";
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }

        if (!_vnPay.VerifySignature(Request.Query))
        {
            payment.GatewayStatus = "INVALID_SIGNATURE";
            await _context.SaveChangesAsync(cancellationToken);
            TempData["ErrorMessage"] = "VNPay trả về chữ ký không hợp lệ.";
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }

        var gatewayStatus = _vnPay.GetGatewayStatus(Request.Query);
        payment.GatewayStatus = gatewayStatus;
        payment.GatewayPaymentLinkId = _vnPay.GetTransactionNo(Request.Query);

        if (_vnPay.IsSuccess(Request.Query))
        {
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
            TempData["SuccessMessage"] = "Thanh toán VNPay thành công. Gói đã được kích hoạt.";
        }
        else
        {
            if (gatewayStatus == "24")
            {
                payment.Status = "Cancelled";
                payment.Note = "Người dùng đã hủy thanh toán VNPay.";
                TempData["ErrorMessage"] = "Bạn đã hủy thanh toán VNPay. Gói chưa được kích hoạt.";
            }
            else
            {
                TempData["ErrorMessage"] = $"VNPay chưa xác nhận thanh toán thành công. Mã phản hồi: {gatewayStatus}.";
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
    }

    [HttpGet]
    public async Task<IActionResult> MomoReturn(CancellationToken cancellationToken)
    {
        if (!_momo.VerifySignature(Request.Query))
        {
            TempData["ErrorMessage"] = "MoMo trả về chữ ký không hợp lệ.";
            return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
        }

        var owner = await GetOwnerAsync(cancellationToken);
        if (owner == null)
            return RedirectToAction("Login", "Account", new { area = "Owner" });

        var orderId = _momo.GetOrderId(Request.Query);
        var payment = await _context.PaymentTransactions
            .Include(item => item.PaymentPlan)
            .FirstOrDefaultAsync(item => item.TransactionCode == orderId && item.OwnerProfileId == owner.Id, cancellationToken);

        if (payment == null)
            return NotFound();

        payment.GatewayStatus = _momo.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _momo.GetTransactionId(Request.Query);

        if (_momo.IsSuccess(Request.Query))
        {
            await _activation.MarkPaymentPaidAsync(payment, "PAID", cancellationToken);
            TempData["SuccessMessage"] = "Thanh toán MoMo thành công. Gói đã được kích hoạt.";
        }
        else
        {
            await _context.SaveChangesAsync(cancellationToken);
            TempData["ErrorMessage"] = "MoMo chưa xác nhận thanh toán thành công.";
        }

        return RedirectToAction(nameof(Index), "Payments", new { area = "Owner" });
    }

    private static PaymentTransaction CreateOwnerOnlinePayment(OwnerProfile owner, PaymentPlan plan, string method, string note) => new()
    {
        TransactionCode = NewTransactionCode("OWN", owner.Id),
        PayerType = "Owner",
        OwnerProfileId = owner.Id,
        PaymentPlanId = plan.Id,
        Purpose = "OwnerSubscription",
        Amount = plan.Price,
        Currency = "VND",
        PaymentMethod = method,
        Status = "Pending",
        GatewayStatus = "PENDING",
        Note = note,
        CreatedAt = DateTime.UtcNow
    };

    private async Task<OwnerProfile?> GetOwnerAsync(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        return await _context.OwnerProfiles
            .Include(owner => owner.User)
            .FirstOrDefaultAsync(owner =>
                (userId != null && owner.UserId.ToString() == userId) ||
                owner.User!.Username == username,
                cancellationToken);
    }

    private string GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private static string NewTransactionCode(string prefix, int ownerOrUserId) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{ownerOrUserId}-{Guid.NewGuid().ToString("N")[..6]}";
}
