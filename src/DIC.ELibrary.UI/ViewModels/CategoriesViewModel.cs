using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class CategoriesViewModel : ViewModelBase
{
    private readonly ICategoryService _categories;
    private readonly IModalService _modals;
    private readonly IDialogService _dialogs;
    private readonly List<Category> _all = new();

    public CategoriesViewModel(
        ICategoryService categories,
        IModalService modals,
        IDialogService dialogs)
    {
        _categories = categories;
        _modals = modals;
        _dialogs = dialogs;
        Title = "Categories";
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    public ObservableCollection<Category> Results { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(LoadAsync);

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadAsync);

    [RelayCommand]
    private async Task NewAsync()
    {
        if (await _modals.EditCategoryAsync(null))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(Category? category)
    {
        if (category is null)
        {
            return;
        }
        if (await _modals.EditCategoryAsync(category.Id))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Category? category)
    {
        if (category is null)
        {
            return;
        }
        if (!_dialogs.Confirm($"Delete category '{category.Name}'?", "Delete category"))
        {
            return;
        }

        var result = await _categories.DeleteAsync(category.Id);
        if (result.Succeeded)
        {
            await LoadAsync();
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to delete the category.");
        }
    }

    private async Task LoadAsync()
    {
        var categories = await _categories.GetAllAsync();
        _all.Clear();
        _all.AddRange(categories);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var term = SearchText?.Trim();
        IEnumerable<Category> query = _all;
        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(c =>
                c.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (c.Description?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        Results.Clear();
        foreach (var category in query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
        {
            Results.Add(category);
        }
    }
}
