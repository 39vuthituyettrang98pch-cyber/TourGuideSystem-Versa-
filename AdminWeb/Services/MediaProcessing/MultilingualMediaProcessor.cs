using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace AdminWeb.Services.MediaProcessing;

public sealed class MultilingualMediaProcessor : IMultilingualMediaProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MultilingualMediaProcessor> _logger;

    public MultilingualMediaProcessor(
        HttpClient httpClient,
        IConfiguration configuration,
        IWebHostEnvironment hostEnvironment,
        IServiceScopeFactory scopeFactory,
        ILogger<MultilingualMediaProcessor> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<MediaProcessingResult> ProcessAsync(
        MediaProcessingRequest request,
        CancellationToken cancellationToken)
    {
        var (taskType, sourceTranslation, languages, sourceAudioPath, sourceVideoPath) =
            await LoadTaskDataAsync(request, cancellationToken);
        await UpdateProgressAsync(
            request.MediaTaskId,
            0,
            cancellationToken,
            languages.Count,
            0,
            0);

        return taskType switch
        {
            MediaTaskType.TextToAudio => await ProcessTextToAudioAsync(
                    request,
                    sourceTranslation,
                    languages,
                    cancellationToken),

            MediaTaskType.VideoDubbing => await ProcessVideoDubbingAsync(
                    request,
                    sourceTranslation,
                    languages,
                    sourceAudioPath,
                    sourceVideoPath,
                    cancellationToken),

            _ => throw new ArgumentOutOfRangeException(
                nameof(taskType),
                taskType,
                "Unsupported media task type.")
        };
    }

    private async Task<MediaProcessingResult> ProcessTextToAudioAsync(
        MediaProcessingRequest request,
        SourceTranslation sourceTranslation,
        List<SupportedLanguage> languages,
        CancellationToken cancellationToken)
    {
        if (languages.Count == 0)
        {
            await UpdateProgressAsync(request.MediaTaskId, 100, cancellationToken);
            return MediaProcessingResult.Empty;
        }

        var attemptedLanguages = 0;
        var successfulLanguages = 0;
        var errors = new List<string>();

        foreach (var language in languages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var translation = await dbContext.PoiTranslations
                    .FirstOrDefaultAsync(
                        item => item.PoiId == request.PoiId &&
                                item.LanguageCode == language.LanguageCode,
                        cancellationToken);

                var isVietnamese = string.Equals(
                    language.LanguageCode,
                    "vi",
                    StringComparison.OrdinalIgnoreCase);

                TranslatedContent translatedContent;

                if (isVietnamese)
                {
                    translatedContent = new TranslatedContent(
                        sourceTranslation.Name,
                        sourceTranslation.ShortDescription,
                        sourceTranslation.FullDescription);
                }
                else if (translation != null &&
                         !string.IsNullOrWhiteSpace(translation.Name) &&
                         !string.IsNullOrWhiteSpace(translation.FullDescription))
                {
                    translatedContent = new TranslatedContent(
                        translation.Name,
                        translation.ShortDescription ?? string.Empty,
                        translation.FullDescription);
                }
                else
                {
                    translatedContent = await TranslateWithGeminiAsync(
                        sourceTranslation,
                        language,
                        cancellationToken);
                }

                if (translation == null)
                {
                    translation = new PoiTranslation
                    {
                        PoiId = request.PoiId,
                        LanguageCode = language.LanguageCode
                    };
                    dbContext.PoiTranslations.Add(translation);
                }

                translation.Name = translatedContent.Name;
                translation.ShortDescription = translatedContent.ShortDescription;
                translation.FullDescription = translatedContent.FullDescription;
                translation.TtsScript = translatedContent.FullDescription;
                translation.AudioUrl = null;
                translation.UpdatedAt = DateTime.UtcNow;

                // Save translated text even if Edge-TTS is unavailable or fails afterward.
                await dbContext.SaveChangesAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(language.EdgeTtsVoice))
                {
                    successfulLanguages++;
                    _logger.LogInformation(
                        "Saved text translation for POI {PoiId}, language {LanguageCode}. Edge-TTS voice is empty, so audio generation was skipped.",
                        request.PoiId,
                        language.LanguageCode);
                    continue;
                }

                var generatedAudio = await GenerateAudioAsync(
                    request.PoiId,
                    language,
                    translatedContent.FullDescription,
                    cancellationToken);

                translation.AudioUrl = generatedAudio.WebUrl;
                translation.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                successfulLanguages++;
                _logger.LogInformation(
                    "Generated translation and audio for POI {PoiId}, language {LanguageCode}.",
                    request.PoiId,
                    language.LanguageCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                errors.Add($"{language.LanguageCode}: {exception.Message}");
                _logger.LogError(
                    exception,
                    "Could not process POI {PoiId} in language {LanguageCode}. Continuing with the next language.",
                    request.PoiId,
                    language.LanguageCode);
            }
            finally
            {
                attemptedLanguages++;
                var progress = (int)Math.Floor(attemptedLanguages * 100d / languages.Count);

                try
                {
                    await UpdateProgressAsync(
                        request.MediaTaskId,
                        progress,
                        cancellationToken,
                        languages.Count,
                        successfulLanguages,
                        errors.Count,
                        errors.LastOrDefault());
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(
                        exception,
                        "Could not update progress for MediaTask {MediaTaskId}.",
                        request.MediaTaskId);
                }
            }
        }

        _logger.LogInformation(
            "Finished TextToAudio MediaTask {MediaTaskId}: {Successful}/{Total} languages succeeded.",
            request.MediaTaskId,
            successfulLanguages,
            languages.Count);

        return new MediaProcessingResult(
            languages.Count,
            successfulLanguages,
            errors);
    }

    private async Task<MediaProcessingResult> ProcessVideoDubbingAsync(
        MediaProcessingRequest request,
        SourceTranslation sourceTranslation,
        List<SupportedLanguage> languages,
        string? sourceAudioPath,
        string? sourceVideoPath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourceVideoPath) ||
            !File.Exists(sourceVideoPath))
        {
            throw new InvalidOperationException(
                $"Source video for MediaTask {request.MediaTaskId} does not exist.");
        }

        if (languages.Count == 0)
        {
            await UpdateProgressAsync(request.MediaTaskId, 100, cancellationToken);
            return MediaProcessingResult.Empty;
        }

        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "TourGuideSystem",
            request.MediaTaskId.ToString("N"));
        Directory.CreateDirectory(workingDirectory);

        try
        {
            var extractedWavPath = Path.Combine(workingDirectory, "source-vi.wav");
            await ExtractAudioFromVideoAsync(
                File.Exists(sourceAudioPath) ? sourceAudioPath : sourceVideoPath,
                extractedWavPath,
                cancellationToken);

            var vietnameseTranscript = await TranscribeVietnameseAsync(
                extractedWavPath,
                cancellationToken);

            var attemptedLanguages = 0;
            var successfulLanguages = 0;
            var errors = new List<string>();

            foreach (var language in languages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var translatedContent = await TranslateVideoContentWithGeminiAsync(
                        sourceTranslation,
                        vietnameseTranscript,
                        language,
                        cancellationToken);

                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var translation = await dbContext.PoiTranslations
                        .FirstOrDefaultAsync(
                            item => item.PoiId == request.PoiId &&
                                    item.LanguageCode == language.LanguageCode,
                            cancellationToken);

                    if (translation == null)
                    {
                        translation = new PoiTranslation
                        {
                            PoiId = request.PoiId,
                            LanguageCode = language.LanguageCode
                        };
                        dbContext.PoiTranslations.Add(translation);
                    }

                    translation.Name = translatedContent.Name;
                    translation.ShortDescription = translatedContent.ShortDescription;
                    translation.FullDescription = translatedContent.FullDescription;
                    translation.TtsScript = translatedContent.Narration;
                    translation.AudioUrl = null;
                    translation.VideoUrl = null;
                    translation.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    if (string.IsNullOrWhiteSpace(language.EdgeTtsVoice))
                    {
                        successfulLanguages++;
                        _logger.LogInformation(
                            "Saved video text/narration translation for POI {PoiId}, language {LanguageCode}. Edge-TTS voice is empty, so audio/video dubbing was skipped.",
                            request.PoiId,
                            language.LanguageCode);
                        continue;
                    }

                    var generatedAudio = await GenerateAudioAsync(
                        request.PoiId,
                        language,
                        translatedContent.Narration,
                        cancellationToken);

                    translation.AudioUrl = generatedAudio.WebUrl;
                    translation.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    var generatedVideo = await MixDubbedAudioIntoVideoAsync(
                        request.PoiId,
                        language,
                        sourceVideoPath,
                        generatedAudio.PhysicalPath,
                        cancellationToken);

                    translation.VideoUrl = generatedVideo.WebUrl;
                    translation.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    successfulLanguages++;
                    _logger.LogInformation(
                        "Generated dubbed video for POI {PoiId}, language {LanguageCode}.",
                        request.PoiId,
                        language.LanguageCode);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    errors.Add($"{language.LanguageCode}: {exception.Message}");
                    _logger.LogError(
                        exception,
                        "Could not dub POI {PoiId} in language {LanguageCode}. Continuing with the next language.",
                        request.PoiId,
                        language.LanguageCode);
                }
                finally
                {
                    attemptedLanguages++;
                    var progress = (int)Math.Floor(attemptedLanguages * 100d / languages.Count);

                    try
                    {
                        await UpdateProgressAsync(
                            request.MediaTaskId,
                            progress,
                            cancellationToken,
                            languages.Count,
                            successfulLanguages,
                            errors.Count,
                            errors.LastOrDefault());
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(
                            exception,
                            "Could not update progress for MediaTask {MediaTaskId}.",
                            request.MediaTaskId);
                    }
                }
            }

            _logger.LogInformation(
                "Finished VideoDubbing MediaTask {MediaTaskId}: {Successful}/{Total} languages succeeded.",
                request.MediaTaskId,
                successfulLanguages,
                languages.Count);

            return new MediaProcessingResult(
                languages.Count,
                successfulLanguages,
                errors);
        }
        finally
        {
            TryDeleteDirectory(workingDirectory);
        }
    }

    private async Task<(
        MediaTaskType TaskType,
        SourceTranslation Source,
        List<SupportedLanguage> Languages,
        string? SourceAudioPath,
        string? SourceVideoPath)> LoadTaskDataAsync(
        MediaProcessingRequest request,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var mediaTask = await dbContext.MediaTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(task => task.Id == request.MediaTaskId, cancellationToken)
            ?? throw new InvalidOperationException($"MediaTask {request.MediaTaskId} does not exist.");

        if (mediaTask.PoiId != request.PoiId)
            throw new InvalidOperationException("MediaTask PoiId does not match the queued request.");

        var source = await dbContext.PoiTranslations
            .AsNoTracking()
            .Where(translation =>
                translation.PoiId == request.PoiId &&
                translation.LanguageCode == "vi")
            .Select(translation => new SourceTranslation(
                translation.Name,
                translation.ShortDescription ?? string.Empty,
                translation.FullDescription ?? string.Empty,
                translation.AudioUrl,
                translation.VideoUrl))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                $"Vietnamese source translation for POI {request.PoiId} does not exist.");

        if (string.IsNullOrWhiteSpace(source.FullDescription))
            throw new InvalidOperationException(
                $"Vietnamese FullDescription for POI {request.PoiId} is empty.");

        var languages = await dbContext.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive)
            .OrderBy(language => language.LanguageCode)
            .ToListAsync(cancellationToken);

        return (
            mediaTask.TaskType,
            source,
            languages,
            ResolveLocalMediaPath(source.AudioUrl),
            ResolveLocalMediaPath(source.VideoUrl));
    }

    private async Task<TranslatedContent> TranslateWithGeminiAsync(
        SourceTranslation source,
        SupportedLanguage language,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini:ApiKey is not configured.");

        var model = _configuration["Gemini:Model"];
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Gemini:Model is not configured.");

        model = model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model["models/".Length..]
            : model;

        var sourceJson = JsonSerializer.Serialize(new
        {
            name = source.Name,
            shortDescription = source.ShortDescription,
            fullDescription = source.FullDescription
        });

        var prompt =
            $"""
             Translate the following Vietnamese POI content into {language.LanguageName}
             (language code: {language.LanguageCode}).

             Rules:
             - Preserve the original meaning and factual information.
             - Use natural language suitable for a tourism audio guide.
             - Translate the "name" field into the target language too. For well-known landmarks, use the common localized name in that language. Do not keep English/Vietnamese names for Chinese, Japanese or Korean unless it is a brand name with no localized form.
             - Do not add explanations or new facts.
             - Return only one JSON object with exactly these string fields:
               "name", "shortDescription", "fullDescription".

             Vietnamese source JSON:
             {sourceJson}
             """;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return ParseGeminiTranslation(responseBody);

            var canRetry = attempt < maxAttempts &&
                           ((int)response.StatusCode == 429 || (int)response.StatusCode == 503);

            if (!canRetry)
            {
                throw new InvalidOperationException(
                    $"Gemini API error for {language.LanguageCode}: " +
                    $"{(int)response.StatusCode} {responseBody}");
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
        }

        throw new InvalidOperationException(
            $"Gemini translation failed unexpectedly for {language.LanguageCode}.");
    }

    private static TranslatedContent ParseGeminiTranslation(string responseBody)
    {
        using var responseDocument = JsonDocument.Parse(responseBody);
        var candidateText = responseDocument.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(candidateText))
            throw new InvalidOperationException("Gemini returned an empty translation.");

        var json = ExtractFirstJsonObject(candidateText);
        var translation = JsonSerializer.Deserialize<TranslatedContent>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize Gemini translation.");

        if (string.IsNullOrWhiteSpace(translation.Name) ||
            string.IsNullOrWhiteSpace(translation.ShortDescription) ||
            string.IsNullOrWhiteSpace(translation.FullDescription))
        {
            throw new InvalidOperationException(
                "Gemini translation is missing one or more required fields.");
        }

        return translation;
    }

    private async Task<TranslatedVideoContent> TranslateVideoContentWithGeminiAsync(
        SourceTranslation source,
        string vietnameseTranscript,
        SupportedLanguage language,
        CancellationToken cancellationToken)
    {
        var sourceJson = JsonSerializer.Serialize(new
        {
            name = source.Name,
            shortDescription = source.ShortDescription,
            fullDescription = source.FullDescription,
            narration = vietnameseTranscript
        });

        var prompt =
            $"""
             Translate the following Vietnamese POI metadata and video narration into
             {language.LanguageName} (language code: {language.LanguageCode}).

             Rules:
             - Preserve all factual information and the original meaning.
             - Write natural narration suitable for a tourism dubbed video.
             - Translate the "name" field into the target language too. For well-known landmarks, use the common localized name in that language. Do not keep English/Vietnamese names for Chinese, Japanese or Korean unless it is a brand name with no localized form.
             - Do not add explanations or new facts.
             - Return only one JSON object with exactly these string fields:
               "name", "shortDescription", "fullDescription", "narration".

             Vietnamese source JSON:
             {sourceJson}
             """;

        var responseBody = await CallGeminiAsync(prompt, cancellationToken);
        using var responseDocument = JsonDocument.Parse(responseBody);
        var candidateText = responseDocument.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(candidateText))
            throw new InvalidOperationException("Gemini returned an empty video translation.");

        var json = ExtractFirstJsonObject(candidateText);
        var translation = JsonSerializer.Deserialize<TranslatedVideoContent>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not deserialize Gemini video translation.");

        if (string.IsNullOrWhiteSpace(translation.Name) ||
            string.IsNullOrWhiteSpace(translation.ShortDescription) ||
            string.IsNullOrWhiteSpace(translation.FullDescription) ||
            string.IsNullOrWhiteSpace(translation.Narration))
        {
            throw new InvalidOperationException(
                "Gemini video translation is missing one or more required fields.");
        }

        return translation;
    }

    private async Task<string> CallGeminiAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Gemini:ApiKey is not configured.");

        var model = _configuration["Gemini:Model"];
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("Gemini:Model is not configured.");

        model = model.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? model["models/".Length..]
            : model;

        var payload = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        var url =
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        const int maxAttempts = 3;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");
            using var response = await _httpClient.PostAsync(url, content, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                return responseBody;

            var canRetry = attempt < maxAttempts &&
                           ((int)response.StatusCode == 429 || (int)response.StatusCode == 503);

            if (!canRetry)
            {
                throw new InvalidOperationException(
                    $"Gemini API error: {(int)response.StatusCode} {responseBody}");
            }

            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken);
        }

        throw new InvalidOperationException("Gemini request failed unexpectedly.");
    }

    private async Task ExtractAudioFromVideoAsync(
        string sourceVideoPath,
        string outputWavPath,
        CancellationToken cancellationToken)
    {
        // Exact equivalent:
        // ffmpeg -y -i input.mp4 -vn -acodec pcm_s16le -ar 16000 -ac 1 output.wav
        await RunProcessAsync(
            ResolveFfmpegExecutable(),
            [
                "-y",
                "-i", sourceVideoPath,
                "-vn",
                "-acodec", "pcm_s16le",
                "-ar", "16000",
                "-ac", "1",
                outputWavPath
            ],
            "FFmpeg audio extraction",
            cancellationToken);

        EnsureOutputFileExists(outputWavPath, "FFmpeg did not create the extracted WAV file.");
    }

    private async Task<string> TranscribeVietnameseAsync(
        string wavPath,
        CancellationToken cancellationToken)
    {
        const long safeTranscriptionFileSize = 24L * 1024 * 1024;
        var fileInfo = new FileInfo(wavPath);

        if (fileInfo.Length <= safeTranscriptionFileSize)
            return await TranscribeAudioChunkAsync(wavPath, cancellationToken);

        var chunkDirectory = Path.Combine(
            Path.GetDirectoryName(wavPath) ?? Path.GetTempPath(),
            "transcription-chunks");
        Directory.CreateDirectory(chunkDirectory);
        var outputPattern = Path.Combine(chunkDirectory, "chunk-%03d.wav");

        await RunProcessAsync(
            ResolveFfmpegExecutable(),
            [
                "-y",
                "-i", wavPath,
                "-f", "segment",
                "-segment_time", "600",
                "-reset_timestamps", "1",
                "-c", "copy",
                outputPattern
            ],
            "FFmpeg transcription audio splitting",
            cancellationToken);

        var chunkPaths = Directory
            .EnumerateFiles(chunkDirectory, "chunk-*.wav")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (chunkPaths.Count == 0)
            throw new InvalidOperationException("FFmpeg did not create transcription audio chunks.");

        var transcripts = new List<string>(chunkPaths.Count);
        foreach (var chunkPath in chunkPaths)
        {
            if (new FileInfo(chunkPath).Length > safeTranscriptionFileSize)
            {
                throw new InvalidOperationException(
                    $"Transcription chunk {Path.GetFileName(chunkPath)} is still larger than 24 MB.");
            }

            transcripts.Add(await TranscribeAudioChunkAsync(chunkPath, cancellationToken));
        }

        return string.Join(Environment.NewLine, transcripts);
    }

    private async Task<string> TranscribeAudioChunkAsync(
        string audioPath,
        CancellationToken cancellationToken)
    {
        var apiKey = _configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OpenAI:ApiKey is not configured. Set it with user-secrets or OpenAI__ApiKey.");
        }

        var model = _configuration["OpenAI:TranscriptionModel"];
        if (string.IsNullOrWhiteSpace(model))
            model = "whisper-1";

        var endpoint = _configuration["OpenAI:TranscriptionEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
            endpoint = "https://api.openai.com/v1/audio/transcriptions";

        await using var fileStream = File.OpenRead(audioPath);
        using var multipartContent = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        multipartContent.Add(fileContent, "file", Path.GetFileName(audioPath));
        multipartContent.Add(new StringContent(model), "model");
        multipartContent.Add(new StringContent("vi"), "language");
        multipartContent.Add(new StringContent("json"), "response_format");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = multipartContent;

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"OpenAI transcription API error: {(int)response.StatusCode} {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var transcript = document.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(transcript))
            throw new InvalidOperationException("OpenAI transcription API returned empty text.");

        return transcript.Trim();
    }

    private async Task<GeneratedMediaFile> GenerateAudioAsync(
        int poiId,
        SupportedLanguage language,
        string text,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(language.EdgeTtsVoice))
            throw new InvalidOperationException(
                $"EdgeTtsVoice is empty for language {language.LanguageCode}.");

        var webRootPath = _hostEnvironment.WebRootPath
            ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        var audioDirectory = Path.Combine(webRootPath, "audio");
        Directory.CreateDirectory(audioDirectory);

        var languageCode = SanitizeFileNamePart(language.LanguageCode);
        var outputFileName = $"poi-{poiId}-{languageCode}-{Guid.NewGuid():N}.mp3";
        var outputPath = Path.Combine(audioDirectory, outputFileName);
        var executable = ResolveEdgeTtsExecutable();
        var textFilePath = Path.Combine(
            Path.GetTempPath(),
            $"tour-guide-tts-{Guid.NewGuid():N}.txt");

        try
        {
            await File.WriteAllTextAsync(textFilePath, text, Encoding.UTF8, cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("--voice");
            startInfo.ArgumentList.Add(language.EdgeTtsVoice);
            startInfo.ArgumentList.Add("--file");
            startInfo.ArgumentList.Add(textFilePath);
            startInfo.ArgumentList.Add("--write-media");
            startInfo.ArgumentList.Add(outputPath);

            await RunProcessAsync(startInfo, "Edge-TTS audio generation", cancellationToken);
            EnsureOutputFileExists(outputPath, "edge-tts did not create a valid MP3 file.");
        }
        finally
        {
            TryDeleteFile(textFilePath);
        }

        return new GeneratedMediaFile(outputPath, $"/audio/{outputFileName}");
    }

    private async Task<GeneratedMediaFile> MixDubbedAudioIntoVideoAsync(
        int poiId,
        SupportedLanguage language,
        string sourceVideoPath,
        string dubbedAudioPath,
        CancellationToken cancellationToken)
    {
        var webRootPath = _hostEnvironment.WebRootPath
            ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        var videoDirectory = Path.Combine(webRootPath, "video");
        Directory.CreateDirectory(videoDirectory);

        var languageCode = SanitizeFileNamePart(language.LanguageCode);
        var outputFileName = $"poi-{poiId}-{languageCode}-{Guid.NewGuid():N}.mp4";
        var outputPath = Path.Combine(videoDirectory, outputFileName);

        if (await HasAudioStreamAsync(sourceVideoPath, cancellationToken))
        {
            await RunProcessAsync(
                ResolveFfmpegExecutable(),
                [
                    "-y",
                    "-i", sourceVideoPath,
                    "-i", dubbedAudioPath,
                    "-filter_complex",
                    "[0:a:0]volume=0.15[background];[background][1:a:0]amix=inputs=2:duration=first:dropout_transition=2:normalize=0[mixed]",
                    "-map", "0:v:0",
                    "-map", "[mixed]",
                    "-c:v", "copy",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-movflags", "+faststart",
                    outputPath
                ],
                "FFmpeg video dubbing",
                cancellationToken);
        }
        else
        {
            await RunProcessAsync(
                ResolveFfmpegExecutable(),
                [
                    "-y",
                    "-i", sourceVideoPath,
                    "-i", dubbedAudioPath,
                    "-filter_complex", "[1:a:0]apad[dub]",
                    "-map", "0:v:0",
                    "-map", "[dub]",
                    "-c:v", "copy",
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-shortest",
                    "-movflags", "+faststart",
                    outputPath
                ],
                "FFmpeg video dubbing without source audio",
                cancellationToken);
        }

        EnsureOutputFileExists(outputPath, "FFmpeg did not create the dubbed video.");
        return new GeneratedMediaFile(outputPath, $"/video/{outputFileName}");
    }

    private string ResolveEdgeTtsExecutable()
    {
        var configuredExecutable = _configuration["EdgeTts:Executable"];

        if (!string.IsNullOrWhiteSpace(configuredExecutable) &&
            !string.Equals(configuredExecutable, "edge-tts", StringComparison.OrdinalIgnoreCase))
        {
            return configuredExecutable;
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var pythonRoot = Path.Combine(localAppData, "Programs", "Python");

            if (Directory.Exists(pythonRoot))
            {
                var installedExecutable = Directory
                    .EnumerateFiles(pythonRoot, "edge-tts.exe", SearchOption.AllDirectories)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (installedExecutable != null)
                    return installedExecutable;
            }
        }

        return string.IsNullOrWhiteSpace(configuredExecutable)
            ? "edge-tts"
            : configuredExecutable;
    }

    private string ResolveFfmpegExecutable()
    {
        var configuredExecutable = _configuration["Ffmpeg:Executable"];

        if (!string.IsNullOrWhiteSpace(configuredExecutable) &&
            !string.Equals(configuredExecutable, "ffmpeg", StringComparison.OrdinalIgnoreCase))
        {
            return configuredExecutable;
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            var wingetLink = Path.Combine(localAppData, "Microsoft", "WinGet", "Links", "ffmpeg.exe");

            if (File.Exists(wingetLink))
                return wingetLink;

            var wingetPackages = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(wingetPackages))
            {
                var installedExecutable = Directory
                    .EnumerateFiles(wingetPackages, "ffmpeg.exe", SearchOption.AllDirectories)
                    .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();

                if (installedExecutable != null)
                    return installedExecutable;
            }
        }

        return string.IsNullOrWhiteSpace(configuredExecutable)
            ? "ffmpeg"
            : configuredExecutable;
    }

    private string ResolveFfprobeExecutable()
    {
        var ffmpegExecutable = ResolveFfmpegExecutable();
        var directory = Path.GetDirectoryName(ffmpegExecutable);
        var fileName = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

        if (!string.IsNullOrWhiteSpace(directory))
        {
            var adjacentExecutable = Path.Combine(directory, fileName);
            if (File.Exists(adjacentExecutable))
                return adjacentExecutable;
        }

        return fileName;
    }

    private async Task<bool> HasAudioStreamAsync(
        string sourceVideoPath,
        CancellationToken cancellationToken)
    {
        var output = await RunProcessForOutputAsync(
            ResolveFfprobeExecutable(),
            [
                "-v", "error",
                "-select_streams", "a:0",
                "-show_entries", "stream=index",
                "-of", "csv=p=0",
                sourceVideoPath
            ],
            "FFprobe audio stream detection",
            cancellationToken);

        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        string operation,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        await RunProcessAsync(startInfo, operation, cancellationToken);
    }

    private static async Task RunProcessAsync(
        ProcessStartInfo startInfo,
        string operation,
        CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
            throw new InvalidOperationException($"Could not start {operation}.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{operation} exited with code {process.ExitCode}. " +
                $"Error: {standardError}. Output: {standardOutput}");
        }
    }

    private static async Task<string> RunProcessForOutputAsync(
        string executable,
        IEnumerable<string> arguments,
        string operation,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
            throw new InvalidOperationException($"Could not start {operation}.");

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);

            throw;
        }

        var standardOutput = await standardOutputTask;
        var standardError = await standardErrorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{operation} exited with code {process.ExitCode}. Error: {standardError}");
        }

        return standardOutput;
    }

    private static void EnsureOutputFileExists(string path, string errorMessage)
    {
        if (!File.Exists(path) || new FileInfo(path).Length == 0)
            throw new InvalidOperationException(errorMessage);
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not delete temporary directory {Path}.", path);
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not delete temporary file {Path}.", path);
        }
    }

    private string? ResolveLocalMediaPath(string? webUrl)
    {
        if (string.IsNullOrWhiteSpace(webUrl) ||
            Uri.TryCreate(webUrl, UriKind.Absolute, out _))
        {
            return null;
        }

        var webRootPath = _hostEnvironment.WebRootPath
            ?? Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        var normalizedRoot = Path.GetFullPath(webRootPath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var relativePath = webUrl
            .TrimStart('/', '\\')
            .Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(normalizedRoot, relativePath));

        return physicalPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
            ? physicalPath
            : null;
    }

    private async Task UpdateProgressAsync(
        Guid mediaTaskId,
        int progress,
        CancellationToken cancellationToken,
        int? totalLanguages = null,
        int? succeededLanguages = null,
        int? failedLanguages = null,
        string? lastError = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mediaTask = await dbContext.MediaTasks
            .FirstOrDefaultAsync(task => task.Id == mediaTaskId, cancellationToken);

        if (mediaTask == null)
            throw new InvalidOperationException($"MediaTask {mediaTaskId} does not exist.");

        mediaTask.ProgressPercentage = Math.Clamp(progress, 0, 100);
        if (totalLanguages.HasValue)
            mediaTask.TotalLanguages = totalLanguages.Value;
        if (succeededLanguages.HasValue)
            mediaTask.SucceededLanguages = succeededLanguages.Value;
        if (failedLanguages.HasValue)
            mediaTask.FailedLanguages = failedLanguages.Value;
        if (!string.IsNullOrWhiteSpace(lastError))
            mediaTask.LastError = lastError.Length <= 4000 ? lastError : lastError[..4000];
        mediaTask.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ExtractFirstJsonObject(string value)
    {
        var start = value.IndexOf('{');
        if (start < 0)
            throw new InvalidOperationException("Gemini response does not contain a JSON object.");

        var depth = 0;
        var insideString = false;
        var escaped = false;

        for (var index = start; index < value.Length; index++)
        {
            var character = value[index];

            if (insideString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    insideString = false;
                }

                continue;
            }

            if (character == '"')
            {
                insideString = true;
            }
            else if (character == '{')
            {
                depth++;
            }
            else if (character == '}' && --depth == 0)
            {
                return value.Substring(start, index - start + 1);
            }
        }

        throw new InvalidOperationException("Gemini response contains incomplete JSON.");
    }

    private static string SanitizeFileNamePart(string value)
    {
        var sanitized = new string(value
            .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private sealed record SourceTranslation(
        string Name,
        string ShortDescription,
        string FullDescription,
        string? AudioUrl,
        string? VideoUrl);

    private sealed record TranslatedContent(
        string Name,
        string ShortDescription,
        string FullDescription);

    private sealed record TranslatedVideoContent(
        string Name,
        string ShortDescription,
        string FullDescription,
        string Narration);

    private sealed record GeneratedMediaFile(
        string PhysicalPath,
        string WebUrl);
}
