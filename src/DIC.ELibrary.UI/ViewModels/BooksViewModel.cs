using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public sealed record BookStatusFilter(string Label, BookStatus? Status);

public partial class BooksViewModel : ViewModelBase
{
    private readonly IBookService _books;
    private readonly ICategoryService _categories;
    private readonly IModalService _modals;
    private readonly IDialogService _dialogs;

    public BooksViewModel(
        IBookService books,
        ICategoryService categories,
        IModalService modals,
        IDialogService dialogs)
    {
        _books = books;
        _categories = categories;
        _modals = modals;
        _dialogs = dialogs;
        Title = "Books";
        StatusFilters = new ObservableCollection<BookStatusFilter>
        {
            new("All statuses", null),
            new("Draft", BookStatus.Draft),
            new("Published", BookStatus.Published),
            new("Archived", BookStatus.Archived)
        };
        SelectedStatus = StatusFilters[0];
    }

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private Category? selectedCategory;

    [ObservableProperty]
    private BookStatusFilter selectedStatus;

    [ObservableProperty]
    private int page = 1;

    [ObservableProperty]
    private int totalPages = 1;

    [ObservableProperty]
    private int totalCount;

    public ObservableCollection<Category> Categories { get; } = new();

    public ObservableCollection<BookStatusFilter> StatusFilters { get; }

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

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(LoadAsync);

    [RelayCommand]
    private Task SearchAsync()
    {
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
    private async Task NewAsync()
    {
        if (await _modals.EditBookAsync(null))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task EditAsync(BookSummary? book)
    {
        if (book is null)
        {
            return;
        }
        if (await _modals.EditBookAsync(book.Id))
        {
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(BookSummary? book)
    {
        if (book is null || !_dialogs.Confirm($"Delete '{book.Title}' permanently?", "Delete book"))
        {
            return;
        }
        var result = await _books.DeleteAsync(book.Id);
        if (result.Succeeded)
        {
            await LoadAsync();
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to delete the book.");
        }
    }

    [RelayCommand]
    private async Task SetStatusAsync(BookSummary? book)
    {
        if (book is null)
        {
            return;
        }
        var next = book.Status == BookStatus.Published ? BookStatus.Archived : BookStatus.Published;
        var result = await _books.SetStatusAsync(book.Id, next);
        if (result.Succeeded)
        {
            await LoadAsync();
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to change status.");
        }
    }

    [RelayCommand]
    private async Task ImportCsvAsync()
    {
        var file = _dialogs.OpenFile("CSV files (*.csv)|*.csv|All files (*.*)|*.*", "Import metadata CSV");
        if (file is null)
        {
            return;
        }
        try
        {
            await using var stream = File.OpenRead(file);
            var result = await _books.ImportMetadataAsync(stream);
            if (result.Succeeded)
            {
                _dialogs.ShowInfo("Metadata import completed.", "Import");
                await LoadAsync();
            }
            else
            {
                _dialogs.ShowError(result.Error ?? "Import failed.");
            }
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }

    private async Task LoadAsync()
    {
        var query = new BookQuery
        {
            SearchText = SearchText,
            Status = SelectedStatus.Status,
            CategoryId = SelectedCategory is { Id: var id } && id != Guid.Empty ? id : null,
            SortBy = BookSortField.RecentlyAdded,
            SortDescending = true,
            Page = Page,
            PageSize = 25
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
