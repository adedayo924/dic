using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class MyLibraryViewModel : ViewModelBase
{
    private readonly ILoanService _loans;
    private readonly IReaderService _reader;
    private readonly ICurrentUser _currentUser;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public MyLibraryViewModel(
        ILoanService loans,
        IReaderService reader,
        ICurrentUser currentUser,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _loans = loans;
        _reader = reader;
        _currentUser = currentUser;
        _navigation = navigation;
        _dialogs = dialogs;
        Title = "My Library";
    }

    public ObservableCollection<LoanSummary> Loans { get; } = new();

    public ObservableCollection<BookSummary> Favorites { get; } = new();

    public ObservableCollection<BookSummary> History { get; } = new();

    public ObservableCollection<BookmarkSummary> Bookmarks { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        if (_currentUser.UserId is not { } userId)
        {
            return;
        }

        Loans.Clear();
        foreach (var loan in await _loans.GetForUserAsync(userId))
        {
            Loans.Add(loan);
        }

        Favorites.Clear();
        foreach (var book in await _reader.GetFavoritesAsync(userId))
        {
            Favorites.Add(book);
        }

        History.Clear();
        foreach (var book in await _reader.GetHistoryAsync(userId, 25))
        {
            History.Add(book);
        }

        Bookmarks.Clear();
        foreach (var bookmark in await _reader.GetBookmarksAsync(userId, null))
        {
            Bookmarks.Add(bookmark);
        }
    });

    [RelayCommand]
    private async Task ReturnAsync(LoanSummary? loan)
    {
        if (loan is null || _currentUser.UserId is not { } userId)
        {
            return;
        }
        if (!_dialogs.Confirm($"Return '{loan.BookTitle}'?", "Return book"))
        {
            return;
        }
        var result = await _loans.ReturnAsync(loan.Id);
        if (result.Succeeded)
        {
            await OnNavigatedToAsync(null);
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to return the book.");
        }
    }

    [RelayCommand]
    private Task OpenAsync(BookSummary? book)
        => book is null ? Task.CompletedTask : _navigation.NavigateToAsync<BookDetailViewModel>(book.Id);

    [RelayCommand]
    private async Task OpenBookmarkAsync(BookmarkSummary? bookmark)
    {
        if (bookmark is not null)
        {
            await _navigation.NavigateToAsync<ReaderViewModel>(bookmark.BookId);
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
        Bookmarks.Remove(bookmark);
    }
}
