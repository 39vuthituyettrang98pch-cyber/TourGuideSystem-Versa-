namespace UserMobile.Models;

public sealed class CategoryCatalogDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int PoiCount { get; set; }
}

public sealed class TourCatalogDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public List<TourPoiCatalogDto> Pois { get; set; } = [];

    public string DurationText => DurationMinutes > 0
        ? $"{DurationMinutes} phút"
        : "Chưa xác định";

    public string PoiCountText => $"{Pois.Count} điểm tham quan";
}

public sealed class TourPoiCatalogDto
{
    public int Id { get; set; }
    public int SequenceOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string FullDescription { get; set; } = string.Empty;
    public string? AudioUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; }
}
