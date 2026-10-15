using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly IStatsService _stats;

    public ReportsViewModel(IStatsService stats)
    {
        _stats = stats;
        Title = "Reports";
    }

    [ObservableProperty]
    private LibraryStats? stats;

    public ObservableCollection<CategoryCount> BooksByCategory { get; } = new();

    public ObservableCollection<PopularBook> MostRead { get; } = new();

    public ObservableCollection<BookSummary> RecentBooks { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        Stats = await _stats.GetStatsAsync();
        if (Stats is null)
        {
            return;
        }

        BooksByCategory.Clear();
        foreach (var entry in Stats.BooksByCategory)
        {
            BooksByCategory.Add(entry);
        }

        MostRead.Clear();
        foreach (var entry in Stats.MostRead)
        {
            MostRead.Add(entry);
        }

        RecentBooks.Clear();
        foreach (var entry in Stats.RecentBooks)
        {
            RecentBooks.Add(entry);
        }
    });

    [RelayCommand]
    private Task RefreshAsync() => OnNavigatedToAsync(null);
}
