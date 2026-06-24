namespace UserMobile.Models;

public class PlaceItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string QrCodeToken { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsDiscovered { get; set; }
    public string OwnerBusinessName { get; set; } = string.Empty;
    public bool IsFavorite { get; set; }
    public List<ReviewDto> Reviews { get; set; } = [];
    public double Distance { get; set; }
    public bool HasNarration { get; set; }
    public List<NarrationLanguage> NarrationLanguages { get; set; } = new();

    public bool HasImage => !string.IsNullOrWhiteSpace(ImageUrl);
    public bool HasNoImage => !HasImage;
    public bool IsSvgImage => HasImage && ImageUrl.Contains(".svg", StringComparison.OrdinalIgnoreCase);
    public bool IsRasterImage => HasImage && !IsSvgImage;

    public string RatingText => RatingCount > 0 ? $"★ {AverageRating:F1} ({RatingCount})" : "Chưa có đánh giá";
    public string FeaturedText => IsFeatured ? "♛ Nổi bật" : string.Empty;
    public string FeaturedBadgeText => IsFeatured
        ? string.IsNullOrWhiteSpace(OwnerBusinessName)
            ? "♛ POI nổi bật"
            : $"♛ Nổi bật · {OwnerBusinessName}"
        : string.Empty;
    public string ImageFallbackText => IsFeatured ? "POI nổi bật" : "VERSA Guide";
}
