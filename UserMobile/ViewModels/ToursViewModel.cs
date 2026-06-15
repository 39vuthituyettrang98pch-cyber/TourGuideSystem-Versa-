using System.Collections.ObjectModel;
using UserMobile.Models;
using UserMobile.Services;

namespace UserMobile.ViewModels;

public sealed class ToursViewModel : BaseViewModel
{
    private readonly IExploreCatalogService _catalogService;
    private bool _isLoading;
    private bool _isEmpty;
    private string _message = string.Empty;
    private CategoryCatalogDto? _selectedCategory;

    public ObservableCollection<CategoryCatalogDto> Categories { get; } = [];
    public ObservableCollection<TourCatalogDto> Tours { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsEmpty
    {
        get => _isEmpty;
        set => SetProperty(ref _isEmpty, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public CategoryCatalogDto? SelectedCategory
    {
        get => _selectedCategory;
        set => SetProperty(ref _selectedCategory, value);
    }

    public ToursViewModel(IExploreCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public async Task LoadAsync(bool forceCategoryRefresh = false)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            Message = string.Empty;
            if (forceCategoryRefresh || Categories.Count == 0)
            {
                Categories.Clear();
                foreach (var category in await _catalogService.GetCategoriesAsync())
                    Categories.Add(category);
            }

            await LoadToursCoreAsync(SelectedCategory?.Id);
        }
        catch (Exception exception)
        {
            Tours.Clear();
            IsEmpty = true;
            Message = "Không thể tải danh mục và tour từ máy chủ.";
            System.Diagnostics.Debug.WriteLine($"Could not load tours: {exception.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectCategoryAsync(CategoryCatalogDto? category)
    {
        if (IsLoading)
            return;

        try
        {
            IsLoading = true;
            SelectedCategory = category;
            await LoadToursCoreAsync(category?.Id);
        }
        catch (Exception exception)
        {
            Tours.Clear();
            IsEmpty = true;
            Message = "Không thể tải tour theo danh mục đã chọn.";
            System.Diagnostics.Debug.WriteLine($"Could not filter tours: {exception.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadToursCoreAsync(int? categoryId)
    {
        Tours.Clear();
        foreach (var tour in await _catalogService.GetToursAsync(categoryId))
            Tours.Add(tour);

        IsEmpty = Tours.Count == 0;
        Message = IsEmpty
            ? "Chưa có tour phù hợp với danh mục này."
            : string.Empty;
    }
}
