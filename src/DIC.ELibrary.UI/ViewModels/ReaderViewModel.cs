using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Core.Options;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;
using Microsoft.Extensions.Options;

namespace DIC.ELibrary.UI.ViewModels;

public partial class ReaderViewModel : ViewModelBase
{
    private const int BaseWidth = 900;

    private readonly IBookService _books;
    private readonly IReaderService _reader;
    private readonly IPdfRenderer _renderer;
    private readonly IPdfService _pdf;
    private readonly ICurrentUser _currentUser;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly LibraryOptions _library;
    private Guid _bookId;
    private double _viewportWidth;
    private int _renderRequest;

    public ReaderViewModel(
        IBookService books,
        IReaderService reader,
        IPdfRenderer renderer,
        IPdfService pdf,
        ICurrentUser currentUser,
        IDialogService dialogs,
        INavigationService navigation,
        IOptions<LibraryOptions> library)
    {
        _books = books;
        _reader = reader;
        _renderer = renderer;
        _pdf = pdf;
        _currentUser = currentUser;
        _dialogs = dialogs;
        _navigation = navigation;
        _library = library.Value;
        Title = "Reader";
    }

    [ObservableProperty]
    private BookDetail? book;

    [ObservableProperty]
    private int currentPage = 1;

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    private BitmapImage? pageImage;

    [ObservableProperty]
    private double pageDisplayWidth = BaseWidth;

    [ObservableProperty]
    private double zoom = 1.0;

    [ObservableProperty]
    private bool isFavorite;

    [ObservableProperty]
    private string bookmarkLabel = string.Empty;

    [ObservableProperty]
    private bool areBookmarksVisible = true;

    public string BookmarksToggleLabel => AreBookmarksVisible ? "Hide panel" : "Show panel";

    partial void OnAreBookmarksVisibleChanged(bool value) => OnPropertyChanged(nameof(BookmarksToggleLabel));

    public ObservableCollection<BookmarkSummary> Bookmarks { get; } = new();

    [ObservableProperty]
    private string searchTerm = string.Empty;

    [ObservableProperty]
    private string? searchSummary;

    [ObservableProperty]
    private bool isSearching;

    [ObservableProperty]
    private PdfTextMatch? selectedSearchResult;

    public ObservableCollection<PdfTextMatch> SearchResults { get; } = new();

    partial void OnSelectedSearchResultChanged(PdfTextMatch? value)
    {
        if (value is not null && value.PageNumber >= 1 && value.PageNumber <= PageCount)
        {
            CurrentPage = value.PageNumber;
        }
    }

    public double ZoomPercent => Math.Round(Zoom * 100);

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        if (parameter is not Guid id)
        {
            return;
        }
        _bookId = id;
        Book = await _books.GetAsync(id);
        if (Book is null)
        {
            return;
        }
        Title = Book.Title;

        var path = _library.ToAbsolute(Book.FilePath);
        if (!File.Exists(path))
        {
            _dialogs.ShowError("The book file is missing from the library store.");
            return;
        }

        PageCount = Math.Max(1, await _renderer.GetPageCountAsync(path));

        if (_currentUser.UserId is { } userId)
        {
            var progress = await _reader.GetProgressAsync(userId, id);
            CurrentPage = Math.Clamp(progress?.LastPage ?? 1, 1, PageCount);
            IsFavorite = await _reader.IsFavoriteAsync(userId, id);
            await LoadBookmarksAsync(userId);
        }

        await LoadPageAsync();
    });

    partial void OnCurrentPageChanged(int value)
    {
        if (Book is not null && value >= 1 && value <= PageCount)
        {
            _ = RunAsync(async () =>
            {
                await LoadPageAsync();
                await SaveProgressAsync();
            });
        }
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercent));
        _ = LoadPageAsync();
    }

    /// <summary>Called by the view when the available page area changes so the page can be re-rendered to fit.</summary>
    public void SetViewportWidth(double viewportWidth)
    {
        var available = Math.Max(200, viewportWidth - 32);
        if (Math.Abs(available - _viewportWidth) < 2)
        {
            return;
        }

        _viewportWidth = available;
        if (Book is not null)
        {
            _ = LoadPageAsync();
        }
    }

    private async Task LoadPageAsync()
    {
        if (Book is null)
        {
            return;
        }

        var width = ComputeRenderWidth();
        var request = ++_renderRequest;
        var path = _library.ToAbsolute(Book.FilePath);
        var bytes = await _renderer.RenderPagePngAsync(path, CurrentPage, width);
        if (request != _renderRequest)
        {
            return;
        }

        PageDisplayWidth = width;
        PageImage = ImageLocator.FromBytes(bytes);
    }

    private int ComputeRenderWidth()
    {
        var baseWidth = _viewportWidth > 0 ? _viewportWidth : BaseWidth;
        return Math.Clamp((int)Math.Round(baseWidth * Zoom), 200, 6000);
    }

    private async Task SaveProgressAsync()
    {
        if (Book is null || _currentUser.UserId is not { } userId)
        {
            return;
        }
        var percent = PageCount <= 0 ? 0 : CurrentPage / (double)PageCount * 100.0;
        await _reader.SaveProgressAsync(userId, Book.Id, CurrentPage, percent);
    }

    private async Task LoadBookmarksAsync(Guid userId)
    {
        Bookmarks.Clear();
        foreach (var bookmark in await _reader.GetBookmarksAsync(userId, _bookId))
        {
            Bookmarks.Add(bookmark);
        }
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < PageCount)
        {
            CurrentPage++;
        }
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
        }
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(3.0, Math.Round(Zoom + 0.1, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(0.5, Math.Round(Zoom - 0.1, 2));

    [RelayCommand]
    private void ToggleBookmarks() => AreBookmarksVisible = !AreBookmarksVisible;

    [RelayCommand]
    private async Task AddBookmarkAsync()
    {
        if (_currentUser.UserId is not { } userId)
        {
            return;
        }
        var result = await _reader.AddBookmarkAsync(userId, _bookId, CurrentPage, string.IsNullOrWhiteSpace(BookmarkLabel) ? null : BookmarkLabel.Trim());
        if (result.Succeeded)
        {
            BookmarkLabel = string.Empty;
            await LoadBookmarksAsync(userId);
        }
    }

    [RelayCommand]
    private async Task RemoveBookmarkAsync(BookmarkSummary? bookmark)
    {
        if (bookmark is null || _currentUser.UserId is not { } userId)
        {
            return;
        }
        await _reader.RemoveBookmarkAsync(bookmark.Id);
        await LoadBookmarksAsync(userId);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (_currentUser.UserId is not { } userId)
        {
            return;
        }
        await _reader.ToggleFavoriteAsync(userId, _bookId);
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (Book is null)
        {
            return;
        }

        var term = SearchTerm?.Trim();
        if (string.IsNullOrEmpty(term))
        {
            SearchResults.Clear();
            SearchSummary = null;
            return;
        }

        var path = _library.ToAbsolute(Book.FilePath);
        if (!File.Exists(path))
        {
            _dialogs.ShowError("The book file is missing from the library store.");
            return;
        }

        IsSearching = true;
        try
        {
            var matches = await _pdf.SearchAsync(path, term);
            SearchResults.Clear();
            foreach (var match in matches)
            {
                SearchResults.Add(match);
            }

            SearchSummary = SearchResults.Count switch
            {
                0 => $"No matches for '{term}'.",
                1 => $"1 page matches '{term}'.",
                _ => $"{SearchResults.Count} pages match '{term}'."
            };

            if (SearchResults.Count > 0)
            {
                SelectedSearchResult = SearchResults[0];
            }
        }
        catch (OperationCanceledException)
        {
            // Navigation away cancels the search; no action required.
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchTerm = string.Empty;
        SearchResults.Clear();
        SearchSummary = null;
        SelectedSearchResult = null;
    }

    [RelayCommand]
    private Task BackAsync() => _navigation.GoBackAsync();
}
