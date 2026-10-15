using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class ReportsViewModel : ViewModelBase
{
    private readonly IStatsService _stats;
    private readonly IReportService _reports;
    private readonly IDialogService _dialogs;

    public ReportsViewModel(IStatsService stats, IReportService reports, IDialogService dialogs)
    {
        _stats = stats;
        _reports = reports;
        _dialogs = dialogs;
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

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        var file = _dialogs.SaveFile("CSV files (*.csv)|*.csv", "Export statistics report", "library-statistics.csv");
        if (file is null)
        {
            return;
        }

        try
        {
            var csv = await _reports.BuildStatisticsReportCsvAsync();
            File.WriteAllText(file, csv);
            _dialogs.ShowInfo($"Report saved to:{Environment.NewLine}{file}", "Export complete");
        }
        catch (Exception ex)
        {
            _dialogs.ShowError(ex.Message);
        }
    }
}
