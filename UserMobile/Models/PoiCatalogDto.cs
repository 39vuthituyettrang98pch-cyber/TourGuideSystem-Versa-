namespace UserMobile.Models;

public sealed class PoiCatalogDto
{
    public int Id { get; set; }
    public string QrCodeToken { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; }
    public double AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool IsFeatured { get; set; }
    public string? OwnerBusinessName { get; set; }
    public List<ReviewDto> RecentReviews { get; set; } = [];
    public List<int> CategoryIds { get; set; } = [];
    public List<PoiTranslationDto> Translations { get; set; } = [];
}

public sealed class PoiTranslationDto
{
    public string LanguageCode { get; set; } = string.Empty;
    public string LanguageName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }
}
