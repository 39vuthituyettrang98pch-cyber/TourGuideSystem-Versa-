namespace AdminWeb.Services.MediaProcessing;

public sealed record MediaProcessingResult(
    int TotalLanguages,
    int SucceededLanguages,
    IReadOnlyList<string> Errors)
{
    public int FailedLanguages => Errors.Count;

    public static MediaProcessingResult Empty { get; } =
        new(0, 0, Array.Empty<string>());
}
