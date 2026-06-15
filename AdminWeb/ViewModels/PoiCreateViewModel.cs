using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace AdminWeb.ViewModels;

public sealed class PoiCreateViewModel
{
    public int? Id { get; set; }

    [Required]
    public decimal Latitude { get; set; } = 10.762622m;

    [Required]
    public decimal Longitude { get; set; } = 106.660172m;

    [Required]
    public int Radius { get; set; } = 50;

    // Tối ưu nội dung POI (vi)
    [Required]
    public string Name { get; set; } = "";

    [Required]
    public string ShortDescription { get; set; } = "";

    [Required]
    public string FullDescription { get; set; } = "";

    public string LanguageCode { get; set; } = "vi";

    public IFormFile? CoverImage { get; set; }
    public IFormFile? SourceAudio { get; set; }
    public IFormFile? SourceVideo { get; set; }
}

