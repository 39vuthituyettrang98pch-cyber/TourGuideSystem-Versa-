using System.Text.Json.Serialization;

namespace AdminWeb.ViewModels;

public sealed class PoiMapItemViewModel
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("latitude")]
    public double Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; init; }

    [JsonPropertyName("radius")]
    public int Radius { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = "";
}
