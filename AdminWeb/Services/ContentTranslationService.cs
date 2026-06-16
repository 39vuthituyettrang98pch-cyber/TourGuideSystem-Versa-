using AdminWeb.Data;
using AdminWeb.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AdminWeb.Services;

public sealed class ContentTranslationService
{
    private readonly AppDbContext _context;
    private readonly IGeminiService _geminiService;
    private readonly ILogger<ContentTranslationService> _logger;

    public ContentTranslationService(
        AppDbContext context,
        IGeminiService geminiService,
        ILogger<ContentTranslationService> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _logger = logger;
    }

    public sealed record TranslationSummary(int Tours, int Categories)
    {
        public int Total => Tours + Categories;
    }

    public async Task<TranslationSummary> TranslateMissingContentForLanguageAsync(
        string languageCode,
        CancellationToken cancellationToken = default)
    {
        languageCode = NormalizeCode(languageCode);

        if (string.IsNullOrWhiteSpace(languageCode) || languageCode == "vi")
            return new TranslationSummary(0, 0);

        var tourCount = await TranslateMissingToursAsync(languageCode, cancellationToken);
        var categoryCount = await TranslateMissingCategoriesAsync(languageCode, cancellationToken);

        return new TranslationSummary(tourCount, categoryCount);
    }

    public async Task<TranslationSummary> TranslateMissingContentForAllActiveLanguagesAsync(
        CancellationToken cancellationToken = default)
    {
        var languages = await GetTargetLanguagesAsync(null, cancellationToken);
        var totalTours = 0;
        var totalCategories = 0;

        foreach (var language in languages)
        {
            totalTours += await TranslateMissingToursForLanguageAsync(language, cancellationToken);
            totalCategories += await TranslateMissingCategoriesForLanguageAsync(language, cancellationToken);
        }

        return new TranslationSummary(totalTours, totalCategories);
    }

    public async Task<int> TranslateMissingToursAsync(
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var languages = await GetTargetLanguagesAsync(languageCode, cancellationToken);
        var total = 0;

        foreach (var language in languages)
        {
            total += await TranslateMissingToursForLanguageAsync(language, cancellationToken);
        }

        return total;
    }

    public async Task<int> TranslateMissingCategoriesAsync(
        string? languageCode = null,
        CancellationToken cancellationToken = default)
    {
        var languages = await GetTargetLanguagesAsync(languageCode, cancellationToken);
        var total = 0;

        foreach (var language in languages)
        {
            total += await TranslateMissingCategoriesForLanguageAsync(language, cancellationToken);
        }

        return total;
    }

    private async Task<int> TranslateMissingToursForLanguageAsync(
        SupportedLanguage language,
        CancellationToken cancellationToken)
    {
        var targetCode = NormalizeCode(language.LanguageCode);

        var tours = await _context.Tours
            .AsNoTracking()
            .Include(tour => tour.Translations)
            .Where(tour => !tour.Translations.Any(translation => translation.LanguageCode == targetCode))
            .OrderBy(tour => tour.Id)
            .ToListAsync(cancellationToken);

        var items = tours
            .Select(tour =>
            {
                var source = PickSourceTranslation(tour.Translations);
                return source == null
                    ? null
                    : new TourSourceItem(tour.Id, source.Title, source.Description ?? string.Empty);
            })
            .Where(item => item != null)
            .Cast<TourSourceItem>()
            .ToList();

        if (items.Count == 0)
            return 0;

        var prompt =
            "Bạn là biên dịch viên nội dung du lịch cho hệ thống tour guide.\n" +
            $"Hãy dịch danh sách TOUR sang ngôn ngữ đích: {language.LanguageName} ({targetCode}).\n" +
            "Giữ nguyên id. Dịch tự nhiên, đúng văn phong du lịch, không bịa thêm dữ kiện mới.\n" +
            "Trả về DUY NHẤT JSON array, không markdown, không giải thích.\n" +
            "Schema bắt buộc: [{\"id\":1,\"title\":\"...\",\"description\":\"...\"}]\n" +
            "Dữ liệu nguồn:\n" +
            JsonSerializer.Serialize(items);

        var response = await _geminiService.GenerateTextAsync(prompt, cancellationToken);
        var translatedItems = ParseArray<TourTranslatedItem>(response)
            .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Title))
            .ToDictionary(item => item.Id, item => item);

        var existingIds = await _context.TourTranslations
            .AsNoTracking()
            .Where(translation => translation.LanguageCode == targetCode)
            .Select(translation => translation.TourId)
            .ToListAsync(cancellationToken);

        var existingSet = existingIds.ToHashSet();
        var nowItems = new List<TourTranslation>();

        foreach (var source in items)
        {
            if (existingSet.Contains(source.Id))
                continue;

            if (!translatedItems.TryGetValue(source.Id, out var translated))
                continue;

            nowItems.Add(new TourTranslation
            {
                TourId = source.Id,
                LanguageCode = targetCode,
                Title = translated.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(translated.Description)
                    ? null
                    : translated.Description.Trim()
            });
        }

        if (nowItems.Count == 0)
            return 0;

        await _context.TourTranslations.AddRangeAsync(nowItems, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return nowItems.Count;
    }

    private async Task<int> TranslateMissingCategoriesForLanguageAsync(
        SupportedLanguage language,
        CancellationToken cancellationToken)
    {
        var targetCode = NormalizeCode(language.LanguageCode);

        var categories = await _context.Categories
            .AsNoTracking()
            .Include(category => category.Translations)
            .Where(category => !category.Translations.Any(translation => translation.LanguageCode == targetCode))
            .OrderBy(category => category.Id)
            .ToListAsync(cancellationToken);

        var items = categories
            .Select(category =>
            {
                var source = PickSourceTranslation(category.Translations);
                return source == null
                    ? null
                    : new CategorySourceItem(category.Id, source.Name);
            })
            .Where(item => item != null)
            .Cast<CategorySourceItem>()
            .ToList();

        if (items.Count == 0)
            return 0;

        var prompt =
            "Bạn là biên dịch viên nội dung du lịch cho hệ thống tour guide.\n" +
            $"Hãy dịch danh sách DANH MỤC sang ngôn ngữ đích: {language.LanguageName} ({targetCode}).\n" +
            "Giữ nguyên id. Tên danh mục phải ngắn gọn, tự nhiên, phù hợp giao diện app/web.\n" +
            "Trả về DUY NHẤT JSON array, không markdown, không giải thích.\n" +
            "Schema bắt buộc: [{\"id\":1,\"name\":\"...\"}]\n" +
            "Dữ liệu nguồn:\n" +
            JsonSerializer.Serialize(items);

        var response = await _geminiService.GenerateTextAsync(prompt, cancellationToken);
        var translatedItems = ParseArray<CategoryTranslatedItem>(response)
            .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Id, item => item);

        var existingIds = await _context.CategoryTranslations
            .AsNoTracking()
            .Where(translation => translation.LanguageCode == targetCode)
            .Select(translation => translation.CategoryId)
            .ToListAsync(cancellationToken);

        var existingSet = existingIds.ToHashSet();
        var newItems = new List<CategoryTranslation>();

        foreach (var source in items)
        {
            if (existingSet.Contains(source.Id))
                continue;

            if (!translatedItems.TryGetValue(source.Id, out var translated))
                continue;

            newItems.Add(new CategoryTranslation
            {
                CategoryId = source.Id,
                LanguageCode = targetCode,
                Name = translated.Name.Trim()
            });
        }

        if (newItems.Count == 0)
            return 0;

        await _context.CategoryTranslations.AddRangeAsync(newItems, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return newItems.Count;
    }

    private async Task<List<SupportedLanguage>> GetTargetLanguagesAsync(
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(languageCode);

        var query = _context.SupportedLanguages
            .AsNoTracking()
            .Where(language => language.IsActive && language.LanguageCode != "vi");

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            query = query.Where(language => language.LanguageCode == normalized);
        }

        return await query
            .OrderBy(language => language.LanguageCode)
            .ToListAsync(cancellationToken);
    }

    private static TourTranslation? PickSourceTranslation(IEnumerable<TourTranslation> translations)
    {
        return translations.FirstOrDefault(item => item.LanguageCode == "vi" && !string.IsNullOrWhiteSpace(item.Title))
            ?? translations.FirstOrDefault(item => item.LanguageCode == "en" && !string.IsNullOrWhiteSpace(item.Title))
            ?? translations.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Title));
    }

    private static CategoryTranslation? PickSourceTranslation(IEnumerable<CategoryTranslation> translations)
    {
        return translations.FirstOrDefault(item => item.LanguageCode == "vi" && !string.IsNullOrWhiteSpace(item.Name))
            ?? translations.FirstOrDefault(item => item.LanguageCode == "en" && !string.IsNullOrWhiteSpace(item.Name))
            ?? translations.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Name));
    }

    private static List<T> ParseArray<T>(string text)
    {
        var json = ExtractFirstJsonArray(text);
        var items = JsonSerializer.Deserialize<List<T>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return items ?? [];
    }

    private static string ExtractFirstJsonArray(string text)
    {
        var cleaned = (text ?? string.Empty)
            .Replace("```json", "", StringComparison.OrdinalIgnoreCase)
            .Replace("```", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        var start = cleaned.IndexOf('[');
        if (start < 0)
            throw new InvalidOperationException("Gemini không trả về JSON array hợp lệ.");

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < cleaned.Length; i++)
        {
            var c = cleaned[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '[')
                depth++;
            else if (c == ']')
                depth--;

            if (depth == 0)
                return cleaned.Substring(start, i - start + 1);
        }

        throw new InvalidOperationException("Gemini trả về JSON array chưa hoàn chỉnh.");
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed record TourSourceItem(int Id, string Title, string Description);
    private sealed record CategorySourceItem(int Id, string Name);

    private sealed class TourTranslatedItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Description { get; set; }
    }

    private sealed class CategoryTranslatedItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
