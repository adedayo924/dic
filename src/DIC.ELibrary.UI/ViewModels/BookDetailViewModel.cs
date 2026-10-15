using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class BookDetailViewModel : ViewModelBase
{
    private readonly IBookService _books;
    private readonly ILoanService _loans;
    private readonly IReaderService _reader;
    private readonly IPackageService _packages;
    private readonly ICurrentUser _currentUser;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;

    public BookDetailViewModel(
        IBookService books,
        ILoanService loans,
        IReaderService reader,
        IPackageService packages,
        ICurrentUser currentUser,
        INavigationService navigation,
        IDialogService dialogs)
    {
        _books = books;
        _loans = loans;
        _reader = reader;
        _packages = packages;
        _currentUser = currentUser;
        _navigation = navigation;
        _dialogs = dialogs;
        Title = "Book";
    }

    [ObservableProperty]
    private BookDetail? book;

    [ObservableProperty]
    private bool isFavorite;

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        if (parameter is not Guid id)
        {
            return;
        }
        Book = await _books.GetAsync(id);
        if (Book is not null)
        {
            Title = Book.Title;
            if (_currentUser.UserId is { } userId)
            {
                IsFavorite = await _reader.IsFavoriteAsync(userId, id);
            }
        }
    });

    [RelayCommand]
    private Task ReadAsync()
        => Book is null ? Task.CompletedTask : _navigation.NavigateToAsync<ReaderViewModel>(Book.Id);

    [RelayCommand]
    private async Task BorrowAsync()
    {
        if (Book is null || _currentUser.UserId is not { } userId)
        {
            return;
        }
        var result = await _loans.BorrowAsync(userId, Book.Id);
        if (result.Succeeded)
        {
            _dialogs.ShowInfo($"'{Book.Title}' has been borrowed.", "Loan created");
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Unable to borrow this book.");
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (Book is null || _currentUser.UserId is not { } userId)
        {
            return;
        }
        await _reader.ToggleFavoriteAsync(userId, Book.Id);
        IsFavorite = !IsFavorite;
    }

    [RelayCommand]
    private async Task ExportPackageAsync()
    {
        if (Book is null)
        {
            return;
        }
        var target = _dialogs.SaveFile("DIC package (*.dicpkg)|*.dicpkg", "Export package", $"{Book.Title}.dicpkg");
        if (target is null)
        {
            return;
        }
        var result = await _packages.BuildAsync(Book.Id, target);
        if (result.Succeeded)
        {
            _dialogs.ShowInfo("Package exported successfully.", "Export complete");
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Export failed.");
        }
    }

    [RelayCommand]
    private Task BackAsync() => _navigation.GoBackAsync();
}
