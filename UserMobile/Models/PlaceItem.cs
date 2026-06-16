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
    public List<ReviewDto> Reviews { get; set; } = [];
    public double Distance { get; set; }
    public bool HasNarration { get; set; }
    public List<NarrationLanguage> NarrationLanguages { get; set; } = new();
}
