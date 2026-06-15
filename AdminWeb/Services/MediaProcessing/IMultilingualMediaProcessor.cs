namespace AdminWeb.Services.MediaProcessing;

public interface IMultilingualMediaProcessor
{
    Task<MediaProcessingResult> ProcessAsync(
        MediaProcessingRequest request,
        CancellationToken cancellationToken);
}
