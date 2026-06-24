using System.Net;
using System.Security.Claims;
using AdminWeb.Contracts.Api;
using AdminWeb.Data;
using AdminWeb.Models;
using AdminWeb.Services.Payments;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Controllers.Api;

[Route("api/menu")]
[ApiController]
public sealed class MenuOrderApiController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly VnPayPaymentService _vnPay;
    private readonly MomoPaymentService _momo;

    public MenuOrderApiController(
        AppDbContext context,
        VnPayPaymentService vnPay,
        MomoPaymentService momo)
    {
        _context = context;
        _vnPay = vnPay;
        _momo = momo;
    }

    [HttpGet("poi/{poiId:int}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuItemDto>>>> GetPoiMenu(
        int poiId,
        CancellationToken cancellationToken)
    {
        var poiExists = await _context.Pois
            .AsNoTracking()
            .AnyAsync(item => item.Id == poiId && item.Status == "Approved", cancellationToken);

        if (!poiExists)
            return NotFound(ApiResponse<IReadOnlyList<MenuItemDto>>.Fail("POI không tồn tại hoặc chưa được duyệt."));

        var itemsRaw = await _context.OwnerMenuItems
            .AsNoTracking()
            .Where(item => item.PoiId == poiId && item.Status == "Active")
            .ToListAsync(cancellationToken);

        var items = itemsRaw
            .OrderBy(item => item.Price)
            .ThenBy(item => item.Name)
            .Select(item => new MenuItemDto
            {
                Id = item.Id,
                PoiId = item.PoiId,
                Name = item.Name,
                Description = item.Description ?? string.Empty,
                Price = item.Price,
                Currency = item.Currency,
                ImageUrl = ToAbsoluteUrl(item.ImageUrl)
            })
            .ToList();

        return Ok(ApiResponse<IReadOnlyList<MenuItemDto>>.Ok(items));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("orders")]
    public async Task<ActionResult<ApiResponse<MenuOrderDto>>> CreateOrder(
        [FromBody] CreateMenuOrderRequest request,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var poi = await _context.Pois
            .Include(item => item.Translations)
            .Include(item => item.OwnerProfile)
            .FirstOrDefaultAsync(item => item.Id == request.PoiId && item.Status == "Approved", cancellationToken);

        if (poi == null)
            return NotFound(ApiResponse<MenuOrderDto>.Fail("POI không tồn tại hoặc chưa được duyệt."));

        if (!poi.OwnerProfileId.HasValue)
            return BadRequest(ApiResponse<MenuOrderDto>.Fail("POI này chưa có chủ gian hàng nên chưa thể đặt món."));

        var selected = request.Items
            .Where(item => item.Quantity > 0)
            .GroupBy(item => item.MenuItemId)
            .ToDictionary(group => group.Key, group => Math.Min(group.Sum(item => item.Quantity), 20));

        if (selected.Count == 0)
            return BadRequest(ApiResponse<MenuOrderDto>.Fail("Vui lòng chọn ít nhất 1 món/sản phẩm."));

        var menuIds = selected.Keys.ToList();
        var menuItems = await _context.OwnerMenuItems
            .Where(item => menuIds.Contains(item.Id) && item.PoiId == poi.Id && item.Status == "Active")
            .ToListAsync(cancellationToken);

        if (menuItems.Count == 0)
            return BadRequest(ApiResponse<MenuOrderDto>.Fail("Các món đã chọn không còn khả dụng."));

        var tourist = await _context.Tourists.AsNoTracking().FirstOrDefaultAsync(item => item.Id == touristId, cancellationToken);
        var customerName = string.IsNullOrWhiteSpace(request.CustomerName)
            ? tourist?.FullName?.Trim() ?? "Du khách"
            : request.CustomerName.Trim();
        var phone = request.CustomerPhone?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(phone))
            return BadRequest(ApiResponse<MenuOrderDto>.Fail("Vui lòng nhập số điện thoại để chủ gian hàng xác nhận đơn."));

        var order = new MenuOrder
        {
            OrderCode = CreateOrderCode(touristId),
            TouristId = touristId,
            OwnerProfileId = poi.OwnerProfileId.Value,
            PoiId = poi.Id,
            CustomerName = customerName.Length > 160 ? customerName[..160] : customerName,
            CustomerPhone = phone.Length > 40 ? phone[..40] : phone,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = "Pending",
            PaymentMethod = "PayAtCounter",
            PaymentStatus = "Unpaid",
            Currency = "VND",
            CreatedAt = DateTime.UtcNow
        };

        foreach (var menuItem in menuItems)
        {
            var quantity = selected.GetValueOrDefault(menuItem.Id);
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
            return BadRequest(ApiResponse<MenuOrderDto>.Fail("Các món đã chọn không hợp lệ."));

        order.Subtotal = order.Items.Sum(item => item.LineTotal);
        order.TotalAmount = order.Subtotal;
        order.Currency = order.Items.FirstOrDefault()?.Currency ?? "VND";

        _context.MenuOrders.Add(order);
        await _context.SaveChangesAsync(cancellationToken);

        var saved = await LoadOrderAsync(order.Id, touristId, cancellationToken);
        return Ok(ApiResponse<MenuOrderDto>.Ok(ToDto(saved!), "Đã tạo đơn. Chủ gian hàng sẽ xác nhận đơn."));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("orders/my")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<MenuOrderDto>>>> MyOrders(CancellationToken cancellationToken)
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

        return Ok(ApiResponse<IReadOnlyList<MenuOrderDto>>.Ok(orders.Select(ToDto).ToList()));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpGet("orders/{id:int}")]
    public async Task<ActionResult<ApiResponse<MenuOrderDto>>> GetOrder(int id, CancellationToken cancellationToken)
    {
        var order = await LoadOrderAsync(id, GetTouristId(), cancellationToken);
        if (order == null)
            return NotFound(ApiResponse<MenuOrderDto>.Fail("Không tìm thấy đơn."));

        return Ok(ApiResponse<MenuOrderDto>.Ok(ToDto(order)));
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("orders/{id:int}/checkout")]
    public async Task<ActionResult<ApiResponse<MenuOrderCheckoutDto>>> CheckoutOrder(
        int id,
        [FromBody] MenuOrderCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var method = NormalizePaymentMethod(request.PaymentMethod);

        var order = await _context.MenuOrders
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId, cancellationToken);

        if (order == null)
            return NotFound(ApiResponse<MenuOrderCheckoutDto>.Fail("Không tìm thấy đơn."));

        if (order.Status == "Cancelled")
            return BadRequest(ApiResponse<MenuOrderCheckoutDto>.Fail("Đơn đã hủy nên không thể thanh toán."));

        if (order.PaymentStatus == "Paid")
            return Ok(ApiResponse<MenuOrderCheckoutDto>.Ok(new MenuOrderCheckoutDto
            {
                OrderId = order.Id,
                PaymentMethod = order.PaymentMethod,
                Status = "Paid"
            }, "Đơn đã được thanh toán."));

        if (method == "PayAtCounter")
        {
            order.PaymentMethod = "PayAtCounter";
            order.PaymentStatus = "Unpaid";
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(ApiResponse<MenuOrderCheckoutDto>.Ok(new MenuOrderCheckoutDto
            {
                OrderId = order.Id,
                PaymentMethod = method,
                Status = "Unpaid"
            }, "Đơn sẽ thanh toán tại quầy / khi nhận hàng."));
        }

        if (method == "VNPay" && !_vnPay.IsConfigured)
            return BadRequest(ApiResponse<MenuOrderCheckoutDto>.Fail("VNPay chưa được cấu hình trên máy chủ."));
        if (method == "MoMo" && !_momo.CanCreatePayment)
            return BadRequest(ApiResponse<MenuOrderCheckoutDto>.Fail("MoMo chưa được cấu hình trên máy chủ."));

        var payment = CreateMenuOrderPayment(touristId, order, method);
        _context.PaymentTransactions.Add(payment);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            string checkoutUrl;

            if (method == "VNPay")
            {
                var returnUrl = AbsoluteUrl("/api/menu/orders/payments/vnpay-return");
                checkoutUrl = _vnPay.CreatePaymentUrl(payment, returnUrl, GetClientIpAddress());
                payment.GatewayStatus = "PENDING";
            }
            else if (_momo.IsDemoMode)
            {
                var token = Guid.NewGuid().ToString("N");
                payment.GatewayPaymentLinkId = token;
                payment.GatewayStatus = "MOMO_DEMO_PENDING";
                checkoutUrl = AbsoluteUrl($"/api/menu/orders/payments/demo/{payment.Id}?token={Uri.EscapeDataString(token)}");
            }
            else
            {
                var returnUrl = AbsoluteUrl("/api/menu/orders/payments/momo-return");
                var ipnUrl = AbsoluteUrl("/api/payment/momo/ipn");
                var result = await _momo.CreatePaymentAsync(payment, $"VERSA Menu Order {order.OrderCode}", returnUrl, ipnUrl, cancellationToken);
                checkoutUrl = result.PayUrl;
                payment.GatewayPaymentLinkId = result.RequestId;
                payment.GatewayStatus = "PENDING";
            }

            payment.CheckoutUrl = checkoutUrl;
            payment.GatewayOrderCode = order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            order.PaymentMethod = method;
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(ApiResponse<MenuOrderCheckoutDto>.Ok(new MenuOrderCheckoutDto
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                PaymentMethod = method,
                Status = payment.Status,
                CheckoutUrl = checkoutUrl
            }, "Mở cổng thanh toán an toàn."));
        }
        catch (Exception exception)
        {
            payment.Status = "Cancelled";
            payment.GatewayStatus = "CREATE_FAILED";
            payment.Note = "Không tạo được link thanh toán: " + exception.Message;
            await _context.SaveChangesAsync(cancellationToken);
            return BadRequest(ApiResponse<MenuOrderCheckoutDto>.Fail(exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpGet("orders/payments/demo/{id:int}")]
    public async Task<IActionResult> Demo(int id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var payment = await FindDemoMenuOrderPaymentAsync(id, token, cancellationToken);
        if (payment == null)
            return PaymentPage("Không tìm thấy giao dịch", "Liên kết thanh toán không hợp lệ hoặc đã hết hiệu lực.", false);

        var orderCode = WebUtility.HtmlEncode(payment.Note ?? "Đơn hàng");
        var action = $"/api/menu/orders/payments/demo/{payment.Id}/confirm?token={Uri.EscapeDataString(token)}";
        var body = $"<p>{orderCode}</p><p class='price'>{payment.Amount:N0} {WebUtility.HtmlEncode(payment.Currency)}</p><form method='post' action='{action}'><button type='submit'>Xác nhận thanh toán MoMo demo</button></form>";
        return PaymentPage("Thanh toán MoMo demo", body, true, bodyIsHtml: true);
    }

    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [HttpPost("orders/payments/demo/{id:int}/confirm")]
    public async Task<IActionResult> ConfirmDemo(int id, [FromQuery] string token, CancellationToken cancellationToken)
    {
        var payment = await FindDemoMenuOrderPaymentAsync(id, token, cancellationToken);
        if (payment == null)
            return PaymentPage("Không thể xác nhận", "Giao dịch không hợp lệ.", false);

        await MarkMenuOrderPaymentPaidAsync(payment, "MOMO_DEMO_PAID", cancellationToken);
        return PaymentPage("Thanh toán thành công", "Đơn hàng đã được ghi nhận thanh toán. Bạn có thể đóng trang này và quay lại ứng dụng.", true);
    }

    [AllowAnonymous]
    [HttpGet("orders/payments/vnpay-return")]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var transactionCode = _vnPay.GetTxnRef(Request.Query);
        var payment = await _context.PaymentTransactions
            .FirstOrDefaultAsync(item => item.TransactionCode == transactionCode && item.Purpose == "MenuOrder", cancellationToken);

        if (payment == null || !_vnPay.VerifySignature(Request.Query))
            return PaymentPage("Thanh toán chưa hoàn tất", "Không xác minh được giao dịch VNPay.", false);

        payment.GatewayStatus = _vnPay.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _vnPay.GetTransactionNo(Request.Query);

        if (_vnPay.IsSuccess(Request.Query))
        {
            await MarkMenuOrderPaymentPaidAsync(payment, "PAID", cancellationToken);
            return PaymentPage("Thanh toán thành công", "Đơn hàng đã được ghi nhận thanh toán. Hãy quay lại ứng dụng.", true);
        }

        if (payment.GatewayStatus == "24")
            payment.Status = "Cancelled";

        await _context.SaveChangesAsync(cancellationToken);
        return PaymentPage("Thanh toán chưa hoàn tất", "VNPay chưa xác nhận giao dịch thành công.", false);
    }

    [AllowAnonymous]
    [HttpGet("orders/payments/momo-return")]
    public async Task<IActionResult> MomoReturn(CancellationToken cancellationToken)
    {
        if (!_momo.VerifySignature(Request.Query))
            return PaymentPage("Thanh toán chưa hoàn tất", "Không xác minh được giao dịch MoMo.", false);

        var orderId = _momo.GetOrderId(Request.Query);
        var payment = await _context.PaymentTransactions
            .FirstOrDefaultAsync(item => item.TransactionCode == orderId && item.Purpose == "MenuOrder", cancellationToken);

        if (payment == null)
            return PaymentPage("Không tìm thấy giao dịch", "Vui lòng quay lại ứng dụng và kiểm tra đơn hàng.", false);

        payment.GatewayStatus = _momo.GetGatewayStatus(Request.Query);
        payment.GatewayPaymentLinkId = _momo.GetTransactionId(Request.Query);

        if (_momo.IsSuccess(Request.Query))
        {
            await MarkMenuOrderPaymentPaidAsync(payment, "PAID", cancellationToken);
            return PaymentPage("Thanh toán thành công", "Đơn hàng đã được ghi nhận thanh toán. Hãy quay lại ứng dụng.", true);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return PaymentPage("Thanh toán chưa hoàn tất", "MoMo chưa xác nhận giao dịch thành công.", false);
    }

    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [HttpPost("orders/{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<bool>>> CancelOrder(int id, CancellationToken cancellationToken)
    {
        var touristId = GetTouristId();
        var order = await _context.MenuOrders
            .FirstOrDefaultAsync(item => item.Id == id && item.TouristId == touristId, cancellationToken);

        if (order == null)
            return NotFound(ApiResponse<bool>.Fail("Không tìm thấy đơn."));

        if (order.Status is "Completed" or "Cancelled")
            return BadRequest(ApiResponse<bool>.Fail("Đơn này không thể hủy."));

        if (order.PaymentStatus == "Paid")
            return BadRequest(ApiResponse<bool>.Fail("Đơn đã thanh toán online. Vui lòng liên hệ chủ gian hàng để xử lý hoàn tiền/hủy đơn."));

        order.Status = "Cancelled";
        order.CancelledAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ApiResponse<bool>.Ok(true, "Đã hủy đơn."));
    }

    private async Task<MenuOrder?> LoadOrderAsync(int orderId, int touristId, CancellationToken cancellationToken)
    {
        return await _context.MenuOrders
            .AsNoTracking()
            .Include(item => item.Poi)
                .ThenInclude(poi => poi!.Translations)
            .Include(item => item.OwnerProfile)
            .Include(item => item.Items)
            .FirstOrDefaultAsync(item => item.Id == orderId && item.TouristId == touristId, cancellationToken);
    }

    private static MenuOrderDto ToDto(MenuOrder order)
    {
        var poiName = order.Poi?.Translations.FirstOrDefault(item => item.LanguageCode == "vi")?.Name
            ?? order.Poi?.Translations.FirstOrDefault()?.Name
            ?? $"POI #{order.PoiId}";

        return new MenuOrderDto
        {
            Id = order.Id,
            OrderCode = order.OrderCode,
            PoiId = order.PoiId,
            PoiName = poiName,
            OwnerName = order.OwnerProfile?.BusinessName ?? "Chủ gian hàng",
            CustomerName = order.CustomerName,
            CustomerPhone = order.CustomerPhone,
            Status = order.Status,
            PaymentMethod = order.PaymentMethod,
            PaymentStatus = order.PaymentStatus,
            TotalAmount = order.TotalAmount,
            Currency = order.Currency,
            Note = order.Note,
            CreatedAt = order.CreatedAt,
            Items = order.Items.Select(item => new MenuOrderItemDto
            {
                Id = item.Id,
                MenuItemId = item.OwnerMenuItemId,
                ItemName = item.ItemName,
                UnitPrice = item.UnitPrice,
                Quantity = item.Quantity,
                LineTotal = item.LineTotal,
                Currency = item.Currency
            }).ToList()
        };
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

    private async Task<PaymentTransaction?> FindDemoMenuOrderPaymentAsync(int id, string token, CancellationToken cancellationToken)
    {
        if (!_momo.IsDemoMode || string.IsNullOrWhiteSpace(token))
            return null;

        return await _context.PaymentTransactions.FirstOrDefaultAsync(item =>
            item.Id == id &&
            item.Purpose == "MenuOrder" &&
            item.PaymentMethod == "MoMo" &&
            item.GatewayPaymentLinkId == token &&
            item.Status != "Cancelled", cancellationToken);
    }

    private static PaymentTransaction CreateMenuOrderPayment(int touristId, MenuOrder order, string method) => new()
    {
        TransactionCode = NewTransactionCode("MNUAPP", touristId),
        PayerType = "Tourist",
        TouristId = touristId,
        Purpose = "MenuOrder",
        Amount = order.TotalAmount,
        Currency = order.Currency,
        PaymentMethod = method,
        Status = "Pending",
        GatewayStatus = "CREATING",
        GatewayOrderCode = order.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        Note = $"Thanh toán đơn {order.OrderCode}",
        CreatedAt = DateTime.UtcNow
    };

    private ContentResult PaymentPage(string title, string message, bool success, bool bodyIsHtml = false)
    {
        var safeTitle = WebUtility.HtmlEncode(title);
        var body = bodyIsHtml ? message : $"<p>{WebUtility.HtmlEncode(message)}</p>";
        var color = success ? "#16a36a" : "#d94a64";
        var icon = success ? "✓" : "!";
        var html = $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>{{safeTitle}}</title><style>body{margin:0;background:#f4f6fb;color:#172033;font-family:system-ui;display:grid;min-height:100vh;place-items:center;padding:20px}main{width:min(460px,100%);background:white;border:1px solid #e4e8f0;border-radius:28px;padding:30px;box-shadow:0 20px 60px #1720331c;text-align:center}.icon{width:64px;height:64px;border-radius:22px;background:{{color}};color:white;display:grid;place-items:center;margin:auto;font-size:30px}h1{font-size:25px}p{color:#68748a;line-height:1.6}.price{font-size:28px;color:#172033;font-weight:800}button{border:0;border-radius:16px;padding:14px 20px;background:#5b3fe4;color:white;font-weight:700;font-size:16px;width:100%}</style></head>
            <body><main><div class="icon">{{icon}}</div><h1>{{safeTitle}}</h1>{{body}}</main></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    private int GetTouristId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static string NormalizePaymentMethod(string? method)
    {
        if (string.Equals(method, "VNPay", StringComparison.OrdinalIgnoreCase))
            return "VNPay";
        if (string.Equals(method, "MoMo", StringComparison.OrdinalIgnoreCase))
            return "MoMo";
        return "PayAtCounter";
    }

    private string AbsoluteUrl(string path) => $"{Request.Scheme}://{Request.Host}{Request.PathBase}{path}";
    private string GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

    private static string CreateOrderCode(int touristId)
    {
        return $"MNU-{DateTime.UtcNow:yyyyMMddHHmmss}-{touristId}-{Guid.NewGuid().ToString("N")[..5]}".ToUpperInvariant();
    }

    private static string NewTransactionCode(string prefix, int touristId) =>
        $"{prefix}-{DateTime.UtcNow:yyyyMMddHHmmss}-{touristId}-{Guid.NewGuid().ToString("N")[..6]}".ToUpperInvariant();

    private string? ToAbsoluteUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
            return absoluteUri.ToString();

        var normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        if (normalizedPath.StartsWith("/uploads/demo/menu-items/", StringComparison.OrdinalIgnoreCase))
        {
            normalizedPath = normalizedPath.Contains('?')
                ? $"{normalizedPath}&v=29"
                : $"{normalizedPath}?v=29";
        }

        return $"{Request.Scheme}://{Request.Host}{Request.PathBase}{normalizedPath}";
    }
}
