using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AdminWeb.Areas.DuKhach.Controllers;

[Area("DuKhach")]
[Authorize(Policy = "TouristAreaPolicy")]
public sealed class MenuOrdersController : Controller
{
    private readonly AppDbContext _context;
    private readonly VnPayPaymentService _vnPay;
    private readonly MomoPaymentService _momo;

    public MenuOrdersController(
        AppDbContext context,
        VnPayPaymentService vnPay,
        MomoPaymentService momo)
    {
        _context = context;
        _vnPay = vnPay;
        _momo = momo;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int poiId, int? itemId, CancellationToken cancellationToken)
    {
        var poi = await _context.Pois
            .AsNoTracking()
            .Include(item => item.Translations)
            .Include(item => item.OwnerProfile)
            .FirstOrDefaultAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);

        if (poi == null)
            return NotFound();

        var itemsRaw = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => item.PoiId == poiId && item.Status == "Active")
            .ToListAsync(cancellationToken);

        var items = itemsRaw
            .OrderBy(item => item.Price)
            .ThenBy(item => item.Name)
            .ToList();

        ViewBag.Poi = poi;
        ViewBag.Tourist = await GetCurrentTouristAsync(cancellationToken);
        ViewBag.PreselectItemId = itemId;
        ViewData["Title"] = "Đặt món / mua sản phẩm";
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int poiId,
        string? customerName,
        string? customerPhone,
        string? note,
        Dictionary<int, int>? quantities,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var poi = await _context.Pois
            .Include(item => item.Translations)
            .Include(item => item.OwnerProfile)
            .FirstOrDefaultAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);

        if (poi == null)
            return NotFound();

        if (!poi.OwnerProfileId.HasValue)
        {
            TempData["DuKhachErrorMessage"] = "POI này chưa có chủ gian hàng nên chưa thể đặt món.";
            return RedirectToAction("Index", "PoiMenu", new { area = "DuKhach", poiId });
        }

        quantities ??= new Dictionary<int, int>();

        var selectedQuantities = quantities
            .Where(item => item.Value > 0)
            .ToDictionary(item => item.Key, item => Math.Min(item.Value, 20));

        if (selectedQuantities.Count == 0)
        {
            TempData["DuKhachErrorMessage"] = "Vui lòng chọn ít nhất 1 món/sản phẩm.";
            return RedirectToAction(nameof(Create), "MenuOrders", new { area = "DuKhach", poiId });
        }

        var menuItemIds = selectedQuantities.Keys.ToList();
        var menuItems = await _context.OwnerMenuItems
            .Where(item => menuItemIds.Contains(item.Id) && item.PoiId == poiId && item.Status == "Active")
            .ToListAsync(cancellationToken);

        if (menuItems.Count == 0)
        {
            TempData["DuKhachErrorMessage"] = "Các món đã chọn không còn khả dụng.";
            return RedirectToAction(nameof(Create), "MenuOrders", new { area = "DuKhach", poiId });
        }

        var tourist = await GetCurrentTouristAsync(cancellationToken);
        var normalizedName = string.IsNullOrWhiteSpace(customerName)
            ? tourist?.FullName?.Trim() ?? "Du khách"
            : customerName.Trim();
        var normalizedPhone = customerPhone?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedPhone))
        {
            TempData["DuKhachErrorMessage"] = "Vui lòng nhập số điện thoại để chủ gian hàng xác nhận đơn.";
            return RedirectToAction(nameof(Create), "MenuOrders", new { area = "DuKhach", poiId });
        }

        var order = new MenuOrder
        {
            OrderCode = CreateOrderCode(touristId),
            TouristId = touristId,
            OwnerProfileId = poi.OwnerProfileId.Value,
            PoiId = poi.Id,
            CustomerName = normalizedName.Length > 160 ? normalizedName[..160] : normalizedName,
            CustomerPhone = normalizedPhone.Length > 40 ? normalizedPhone[..40] : normalizedPhone,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Status = "Pending",
            PaymentMethod = "PayAtCounter",
            PaymentStatus = "Unpaid",
            Currency = "VND",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var menuItem in menuItems)
        {
            var quantity = selectedQuantities.GetValueOrDefault(menuItem.Id);
            if (quantity <= 0)
                continue;

            var lineTotal = menuItem.Price * quantity;
            order.Items.Add(new MenuOrderItem
            {
                OwnerMenuItemId = menuItem.Id,
                ItemName = menuItem.Name,
                UnitPrice = menuItem.Price,
                Quantity = quantity,
                LineTotal = lineTotal,
                Currency = menuItem.Currency
            });
        }

        if (order.Items.Count == 0)
        {
            TempData["DuKhachErrorMessage"] = "Các món đã chọn không hợp lệ.";
            return RedirectToAction(nameof(Create), "MenuOrders", new { area = "DuKhach", poiId });
        }

        order.Subtotal = order.Items.Sum(item => item.LineTotal);
        order.TotalAmount = order.Subtotal;
        order.Currency = order.Items.FirstOrDefault()?.Currency ?? "VND";

        _context.MenuOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        TempData["DuKhachSuccessMessage"] = $"Đã tạo đơn {order.OrderCode}. Bạn có thể thanh toán online hoặc chờ chủ gian hàng xác nhận.";
        return RedirectToAction(nameof(Details), "MenuOrders", new { area = "DuKhach", id = order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> My(CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var orders = await _context.MenuOrders
            .AsNoTracking()
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(item => item.OwnerProfile)
            .Include(item => item.Items)
            .Where(item => item.TouristId == touristId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        ViewData["Title"] = "Đơn hàng của tôi";
        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var order = await _context.MenuOrders
            .AsNoTracking()
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(item => item.OwnerProfile)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId, cancellationToken);

        if (order == null)
            return NotFound();

        ViewBag.VnPayConfigured = _vnPay.IsConfigured;
        ViewBag.MomoConfigured = _momo.CanCreatePayment;
        ViewBag.MomoDemoMode = _momo.IsDemoMode;
        ViewData["Title"] = $"Đơn {order.OrderCode}";
        return View(order);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayVnPay(int id, CancellationToken cancellationToken)
    {
        if (!_vnPay.IsConfigured)
        {
            TempData["DuKhachErrorMessage"] = "VNPay chưa được cấu hình.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var order = await FindMyPayableOrderAsync(id, cancellationToken);
        if (order == null)
            return NotFound();

        var payment = CreateMenuOrderPayment(GetTouristId(), order, "VNPay", "Thanh toán đơn hàng qua VNPay.");
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            var returnUrl = Url.Action(nameof(VnPayReturn), "MenuOrders", new { area = "DuKhach" }, Request.Scheme)!;
            var checkoutUrl = _vnPay.CreatePaymentUrl(payment, returnUrl, GetClientIpAddress());
            payment.CheckoutUrl = checkoutUrl;
            payment.GatewayStatus = "PENDING";
            order.PaymentMethod = "VNPay";
            await _context.SaveChangesAsync(cancellationToken);
            return Redirect(checkoutUrl);
        }
        catch (Exception exception)
        {
            payment.Status = "Cancelled";
            payment.GatewayStatus = "CREATE_FAILED";
            payment.Note = "Không tạo được link VNPay: " + exception.Message;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "Không tạo được link VNPay: " + exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PayMomo(int id, CancellationToken cancellationToken)
    {
        if (!_momo.CanCreatePayment)
        {
            TempData["DuKhachErrorMessage"] = "MoMo chưa được cấu hình hoặc chưa bật DemoMode.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var order = await FindMyPayableOrderAsync(id, cancellationToken);
        if (order == null)
            return NotFound();

        var payment = CreateMenuOrderPayment(GetTouristId(), order, "MoMo",
            _momo.IsDemoMode ? "Thanh toán đơn hàng bằng MoMo demo." : "Thanh toán đơn hàng bằng MoMo.");
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        if (_momo.IsDemoMode)
        {
            var token = Guid.NewGuid().ToString("N");
            payment.GatewayPaymentLinkId = token;
            payment.CheckoutUrl = Url.Action(nameof(MomoDemo), "MenuOrders", new { area = "DuKhach", id = payment.Id, token }, Request.Scheme);
            payment.GatewayStatus = "MOMO_DEMO_PENDING";
            order.PaymentMethod = "MoMo";
            await _context.SaveChangesAsync(cancellationToken);
            return RedirectToAction(nameof(MomoDemo), "MenuOrders", new { area = "DuKhach", id = payment.Id, token });
        }

        try
        {
            var returnUrl = Url.Action(nameof(MomoReturn), "MenuOrders", new { area = "DuKhach" }, Request.Scheme)!;
            var ipnUrl = $"{Request.Scheme}://{Request.Host}/api/payment/momo/ipn";
            var result = await _momo.CreatePaymentAsync(payment, $"VERSA Menu Order {order.OrderCode}", returnUrl, ipnUrl, cancellationToken);
            payment.CheckoutUrl = result.PayUrl;
            payment.GatewayPaymentLinkId = result.RequestId;
            payment.GatewayStatus = "PENDING";
            order.PaymentMethod = "MoMo";
            await _context.SaveChangesAsync(cancellationToken);
            return Redirect(result.PayUrl);
        }
        catch (Exception exception)
        {
            payment.Status = "Cancelled";
            payment.GatewayStatus = "CREATE_FAILED";
            payment.Note = "Không tạo được link MoMo: " + exception.Message;
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "Không tạo được link MoMo: " + exception.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MomoDemo(int id, string token, CancellationToken cancellationToken)
    {
        var payment = await FindDemoPaymentAsync(id, token, cancellationToken);
        if (payment == null)
            return NotFound();

        var safeNote = System.Net.WebUtility.HtmlEncode(payment.Note ?? payment.TransactionCode);
        var safeCurrency = System.Net.WebUtility.HtmlEncode(payment.Currency);
        var actionUrl = $"/DuKhach/MenuOrders/ConfirmMomoDemo/{payment.Id}?token={Uri.EscapeDataString(token)}";

        var html = $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>MoMo demo</title><style>body{font-family:system-ui;background:#f5f6fb;display:grid;min-height:100vh;place-items:center;margin:0;padding:20px}main{background:white;border-radius:28px;padding:30px;box-shadow:0 20px 60px #0002;max-width:440px;text-align:center}button{border:0;border-radius:16px;background:#a50064;color:white;padding:14px 18px;font-weight:800;width:100%}.price{font-size:28px;font-weight:950;color:#172033}</style></head>
            <body><main><h1>Thanh toán MoMo demo</h1><p>{{safeNote}}</p><p class="price">{{payment.Amount.ToString("N0")}} {{safeCurrency}}</p><form method="post" action="{{actionUrl}}"><button type="submit">Xác nhận đã thanh toán</button></form></main></body></html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost("DuKhach/MenuOrders/ConfirmMomoDemo/{id:int}")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ConfirmMomoDemo(int id, string token, CancellationToken cancellationToken)
    {
        var payment = await FindDemoPaymentAsync(id, token, cancellationToken);
        if (payment == null)
            return NotFound();

        await MarkMenuOrderPaymentPaidAsync(payment, "MOMO_DEMO_PAID", cancellationToken);
        TempData["DuKhachSuccessMessage"] = "Thanh toán MoMo demo thành công. Đơn đã được ghi nhận đã thanh toán.";
        return RedirectToAction(nameof(Details), new { id = int.Parse(payment.GatewayOrderCode ?? "0") });
    }

    [HttpGet]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var txnRef = _vnPay.GetTxnRef(Request.Query);
        var payment = await _context.PaymentTransactions
            .FirstOrDefaultAsync(item => item.TransactionCode == txnRef && item.TouristId == GetTouristId() && item.Purpose == "MenuOrder", cancellationToken);

        if (payment == null)
        {
            TempData["DuKhachErrorMessage"] = "Không tìm thấy giao dịch VNPay.";
            return RedirectToAction(nameof(My));
        }

        if (!_vnPay.VerifySignature(Request.Query))
        {
            payment.GatewayStatus = "INVALID_SIGNATURE";
            await _context.SaveChangesAsync(cancellationToken);
            TempData["DuKhachErrorMessage"] = "VNPay trả về chữ ký không hợp lệ.";
            return RedirectToAction(nameof(Details), new { id = int.Parse(payment.GatewayOrderCode ?? "0") });
        }

        payment.GatewayStatus = _vnPay.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _vnPay.GetTransactionNo(Request.Query);

        if (_vnPay.IsSuccess(Request.Query))
        {
            await MarkMenuOrderPaymentPaidAsync(payment, "PAID", cancellationToken);
            TempData["DuKhachSuccessMessage"] = "Thanh toán VNPay thành công. Đơn đã được ghi nhận đã thanh toán.";
        }
        else
        {
            if (payment.GatewayStatus == "24")
            {
                payment.Status = "Cancelled";
                TempData["DuKhachErrorMessage"] = "Bạn đã hủy thanh toán VNPay.";
            }
            else
            {
                TempData["DuKhachErrorMessage"] = $"VNPay chưa xác nhận thanh toán thành công. Mã: {payment.GatewayStatus}.";
            }

            await _context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id = int.Parse(payment.GatewayOrderCode ?? "0") });
    }

    [HttpGet]
    public async Task<IActionResult> MomoReturn(CancellationToken cancellationToken)
    {
        if (!_momo.VerifySignature(Request.Query))
        {
            TempData["DuKhachErrorMessage"] = "MoMo trả về chữ ký không hợp lệ.";
            return RedirectToAction(nameof(My));
        }

        var orderId = _momo.GetOrderId(Request.Query);
        var payment = await _context.PaymentTransactions
            .FirstOrDefaultAsync(item => item.TransactionCode == orderId && item.TouristId == GetTouristId() && item.Purpose == "MenuOrder", cancellationToken);

        if (payment == null)
            return NotFound();

        payment.GatewayStatus = _momo.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _momo.GetTransactionId(Request.Query);

        if (_momo.IsSuccess(Request.Query))
        {
            await MarkMenuOrderPaymentPaidAsync(payment, "PAID", cancellationToken);
            TempData["DuKhachSuccessMessage"] = "Thanh toán MoMo thành công. Đơn đã được ghi nhận đã thanh toán.";
        }
        else
        {
            TempData["DuKhachErrorMessage"] = "MoMo chưa xác nhận thanh toán thành công.";
            await _context.SaveChangesAsync(cancellationToken);
        }

        return RedirectToAction(nameof(Details), new { id = int.Parse(payment.GatewayOrderCode ?? "0") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var order = await _context.MenuOrders
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId, cancellationToken);

        if (order == null)
            return NotFound();

        if (order.Status is "Completed" or "Cancelled" || order.PaymentStatus == "Paid")
        {
            TempData["DuKhachErrorMessage"] = "Đơn này không thể hủy trực tiếp. Nếu đã thanh toán online, vui lòng liên hệ chủ gian hàng để xử lý hoàn tiền.";
            return RedirectToAction(nameof(Details), "MenuOrders", new { area = "DuKhach", id });
        }

        order.Status = "Cancelled";
        order.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        TempData["DuKhachSuccessMessage"] = "Đã hủy đơn hàng.";
        return RedirectToAction(nameof(Details), "MenuOrders", new { area = "DuKhach", id });
    }

    private async Task<MenuOrder?> FindMyPayableOrderAsync(int id, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var order = await _context.MenuOrders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId, cancellationToken);

        if (order == null)
            return null;

        if (order.Status == "Cancelled" || order.PaymentStatus == "Paid")
            return null;

        return order;
    }

    private async Task<PaymentTransaction?> FindDemoPaymentAsync(int id, string token, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode || string.IsNullOrWhiteSpace(token))
            return null;

        return await _context.PaymentTransactions.FirstOrDefaultAsync(item =>
            item.Id == id &&
            item.TouristId == GetTouristId() &&
            item.Purpose == "MenuOrder" &&
            item.PaymentMethod == "MoMo" &&
            item.GatewayPaymentLinkId == token &&
            item.Status != "Cancelled", cancellationToken);
    }

    private async Task MarkMenuOrderPaymentPaidAsync(PaymentTransaction payment, string gatewayStatus, CancellationToken cancellationToken)
    {
        payment.Status = "Paid";
        payment.GatewayStatus = string.IsNullOrWhiteSpace(gatewayStatus) ? "PAID" : gatewayStatus;
        payment.PaidAt ??= DateTime.UtcNow;

        if (int.TryParse(payment.GatewayOrderCode, out var orderId))
        {
            var order = await _context.MenuOrders.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
            if (order != null)
            {
                order.PaymentMethod = payment.PaymentMethod;
                order.PaymentStatus = "Paid";
                if (order.Status == "Pending")
                    order.Status = "Confirmed";
                order.ConfirmedAt ??= DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static PaymentTransaction CreateMenuOrderPayment(int touristId, MenuOrder order, string method, string note) => new()
    {
        TransactionCode = NewTransactionCode("MNUWEB", touristId),
        PayerType = "Tourist",
        TouristId = touristId,
        Purpose = "MenuOrder",
        Amount = order.TotalAmount,
        Currency = order.Currency,
        PaymentMethod = method,
        Status = "Pending",
        GatewayStatus = "CREATING",
        GatewayOrderCode = order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Note = $"{note} Đơn {order.OrderCode}",
        CreatedAt = DateTime.UtcNow
    };

    private int GetTouristId()
    {
        return int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }

    private async Task<Tourist?> GetCurrentTouristAsync(CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        return await _context.Tourists.AsNoTracking().FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);
    }

    private string GetClientIpAddress()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
    }

    private static string CreateOrderCode(int touristId)
    {
        return $"MNU-{DateTime.UtcNow:yyyyMMddHHmmss}-{touristId}-{Guid.NewGuid().ToString("N")[..5]}".ToUpperInvariant();
    }

    private static string NewTransactionCode(string prefix, int touristId) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{touristId}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();
}
