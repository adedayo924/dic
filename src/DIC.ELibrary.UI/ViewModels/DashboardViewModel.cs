using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IStatsService _stats;
    private readonly IReaderService _reader;
    private readonly IBookService _books;
    private readonly ICurrentUser _currentUser;
    private readonly INavigationService _navigation;

    public DashboardViewModel(
        IStatsService stats,
        IReaderService reader,
        IBookService books,
        ICurrentUser currentUser,
        INavigationService navigation)
    {
        _stats = stats;
        _reader = reader;
        _books = books;
        _currentUser = currentUser;
        _navigation = navigation;
        Title = "Dashboard";
    }

    [ObservableProperty]
    private LibraryStats? stats;

    public ObservableCollection<BookSummary> ContinueReading { get; } = new();

    public ObservableCollection<BookSummary> Recent { get; } = new();

    public string Greeting
    {
        get
        {
            var name = _currentUser.Session?.DisplayName;
            return string.IsNullOrWhiteSpace(name) ? "Welcome back" : $"Welcome back, {name}";
        }
    }

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        Stats = await _stats.GetStatsAsync();
        OnPropertyChanged(nameof(Greeting));

        ContinueReading.Clear();
        if (_currentUser.UserId is { } userId)
        {
            foreach (var book in await _reader.GetHistoryAsync(userId, 6))
            {
                ContinueReading.Add(book);
            }
        }

        Recent.Clear();
        foreach (var book in await _books.GetRecentAsync(6))
        {
            Recent.Add(book);
        }
    });

    [RelayCommand]
    private Task BrowseAsync() => _navigation.NavigateToAsync<CatalogViewModel>();

    [RelayCommand]
    private Task OpenAsync(BookSummary? book)
    {
        if (book is not null)
        {
            return _navigation.NavigateToAsync<BookDetailViewModel>(book.Id);
        }
        return Task.CompletedTask;
    }
}
