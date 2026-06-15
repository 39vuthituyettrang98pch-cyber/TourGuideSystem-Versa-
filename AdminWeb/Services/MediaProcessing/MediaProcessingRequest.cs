namespace AdminWeb.Services.MediaProcessing;

public sealed record MediaProcessingRequest(
    Guid MediaTaskId,
    int PoiId);
