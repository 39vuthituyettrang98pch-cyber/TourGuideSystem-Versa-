using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services.MediaProcessing;

public sealed class MediaTaskBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MediaTaskBackgroundService> _logger;

    public MediaTaskBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<MediaTaskBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RecoverInterruptedTasksAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var request = await ClaimNextTaskAsync(stoppingToken);
                if (request == null)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                await ProcessRequestAsync(request, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Media task worker loop failed.");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task RecoverInterruptedTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var interruptedTasks = await dbContext.MediaTasks
            .Where(task => task.Status == MediaTaskStatus.Processing)
            .ToListAsync(cancellationToken);

        foreach (var task in interruptedTasks)
        {
            task.Status = MediaTaskStatus.Pending;
            task.LastError = "Ứng dụng đã dừng khi tác vụ đang xử lý. Tác vụ sẽ được chạy lại.";
            task.UpdatedAt = DateTime.UtcNow;
            task.CompletedAt = null;
        }

        if (interruptedTasks.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Recovered {Count} interrupted media tasks.",
                interruptedTasks.Count);
        }
    }

    private async Task<MediaProcessingRequest?> ClaimNextTaskAsync(
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await dbContext.MediaTasks
            .Where(item => item.Status == MediaTaskStatus.Pending)
            .OrderBy(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (task == null)
            return null;

        task.Status = MediaTaskStatus.Processing;
        task.ProgressPercentage = 0;
        task.TotalLanguages = 0;
        task.SucceededLanguages = 0;
        task.FailedLanguages = 0;
        task.LastError = null;
        task.AttemptCount++;
        task.StartedAt = DateTime.UtcNow;
        task.CompletedAt = null;
        task.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MediaProcessingRequest(task.Id, task.PoiId);
    }

    private async Task ProcessRequestAsync(
        MediaProcessingRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IMultilingualMediaProcessor>();
            var result = await processor.ProcessAsync(request, cancellationToken);

            var status = result.Errors.Count == 0
                ? MediaTaskStatus.Completed
                : result.SucceededLanguages > 0
                    ? MediaTaskStatus.CompletedWithErrors
                    : MediaTaskStatus.Failed;

            await FinishTaskAsync(
                request.MediaTaskId,
                status,
                result.TotalLanguages,
                result.SucceededLanguages,
                result.FailedLanguages,
                JoinErrors(result.Errors),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The task remains Processing and will be recovered on the next startup.
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Background processing failed for MediaTask {MediaTaskId}.",
                request.MediaTaskId);

            await FinishTaskAsync(
                request.MediaTaskId,
                MediaTaskStatus.Failed,
                null,
                null,
                null,
                exception.Message,
                CancellationToken.None);
        }
    }

    private async Task FinishTaskAsync(
        Guid mediaTaskId,
        MediaTaskStatus status,
        int? totalLanguages,
        int? succeededLanguages,
        int? failedLanguages,
        string? lastError,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await dbContext.MediaTasks.FindAsync([mediaTaskId], cancellationToken);

        if (task == null)
            return;

        task.Status = status;
        task.ProgressPercentage = 100;
        if (totalLanguages.HasValue)
            task.TotalLanguages = totalLanguages.Value;
        if (succeededLanguages.HasValue)
            task.SucceededLanguages = succeededLanguages.Value;
        if (failedLanguages.HasValue)
            task.FailedLanguages = failedLanguages.Value;
        task.LastError = Truncate(lastError, 4000);
        task.CompletedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? JoinErrors(IReadOnlyList<string> errors)
    {
        return errors.Count == 0
            ? null
            : Truncate(string.Join(Environment.NewLine, errors), 4000);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
            return value;

        return value[..maxLength];
    }
}
