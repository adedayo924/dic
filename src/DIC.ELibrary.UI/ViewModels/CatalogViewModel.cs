using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public sealed record CatalogSortOption(string Label, BookSortField Field, bool Descending);

public partial class CatalogViewModel : ViewModelBase
{
    private readonly IBookService _books;
    private readonly ICategoryService _categories;
    private readonly INavigationService _navigation;

    public CatalogViewModel(IBookService books, ICategoryService categories, INavigationService navigation)
    {
        _books = books;
        _categories = categories;
        _navigation = navigation;
        Title = "Catalog";
        SortOptions = new ObservableCollection<CatalogSortOption>
        {
            new("Title (A–Z)", BookSortField.Title, false),
            new("Title (Z–A)", BookSortField.Title, true),
            new("Author (A–Z)", BookSortField.Author, false),
            new("Newest first", BookSortField.RecentlyAdded, true),
            new("Most borrowed", BookSortField.Popularity, true)
        };
        SelectedSort = SortOptions[0];
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private bool fullText;

    [ObservableProperty]
    private Category? selectedCategory;

    [ObservableProperty]
    private CatalogSortOption selectedSort;

    [ObservableProperty]
    private int page = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private int totalCount;

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<CatalogSortOption> SortOptions { get; }

    public ObservableCollection<BookSummary> Results { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        if (Categories.Count == 0)
        {
            Categories.Add(new Category { Id = Guid.Empty, Name = "All categories" });
            foreach (var category in await _categories.GetAllAsync())
            {
                Categories.Add(category);
            }
            SelectedCategory = Categories[0];
        }
        await LoadAsync();
    });

    partial void OnSelectedSortChanged(CatalogSortOption value)
    {
        if (IsInitialized)
        {
            _ = RunAsync(async () => { Page = 1; await LoadAsync(); });
        }
    }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (IsInitialized)
        {
            _ = RunAsync(async () => { Page = 1; await LoadAsync(); });
        }
    }

    private bool IsInitialized => Results.Count > 0 || !string.IsNullOrWhiteSpace(SearchText);

    [RelayCommand]
    private Task SearchAsync()
    {
        Page = 1;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private Task ClearAsync()
    {
        SearchText = string.Empty;
        FullText = false;
        Page = 1;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private Task NextAsync()
    {
        if (Page >= TotalPages)
        {
            return Task.CompletedTask;
        }
        Page++;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private Task PreviousAsync()
    {
        if (Page <= 1)
        {
            return Task.CompletedTask;
        }
        Page--;
        return RunAsync(LoadAsync);
    }

    [RelayCommand]
    private Task OpenAsync(BookSummary? book)
        => book is null ? Task.CompletedTask : _navigation.NavigateToAsync<BookDetailViewModel>(book.Id);

    private async Task LoadAsync()
    {
        var query = new BookQuery
        {
            SearchText = SearchText,
            FullText = FullText,
            Status = BookStatus.Published,
            SortBy = SelectedSort.Field,
            SortDescending = SelectedSort.Descending,
            Page = Page,
            PageSize = 20,
            CategoryId = SelectedCategory is { Id: var id } && id != Guid.Empty ? id : null
        };

        var result = await _books.QueryAsync(query);
        Results.Clear();
        foreach (var item in result.Items)
        {
            Results.Add(item);
        }
        TotalCount = result.TotalCount;
        TotalPages = Math.Max(1, result.TotalPages);
    }
}
