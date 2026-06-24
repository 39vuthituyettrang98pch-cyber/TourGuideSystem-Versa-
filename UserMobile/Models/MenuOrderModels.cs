namespace UserMobile.Models;

public sealed class MenuItemDto
{
    public int Id { get; set; }
    public int PoiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "VND";
    public string? ImageUrl { get; set; }
}

public sealed class CreateMenuOrderRequest
{
    public int PoiId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? Note { get; set; }
    public List<CreateMenuOrderItemRequest> Items { get; set; } = [];
}

public sealed class CreateMenuOrderItemRequest
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
}

public sealed class MenuOrderDto
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "VND";
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MenuOrderItemDto> Items { get; set; } = [];
}

public sealed class MenuOrderItemDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public string Currency { get; set; } = "VND";
}


public sealed class MenuOrderCheckoutRequest
{
    public string PaymentMethod { get; set; } = "PayAtCounter";
}

public sealed class MenuOrderCheckoutResult
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CheckoutUrl { get; set; }
}
