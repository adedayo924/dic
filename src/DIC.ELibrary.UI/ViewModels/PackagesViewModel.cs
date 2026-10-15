using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class PackagesViewModel : ViewModelBase
{
    private readonly IPackageService _packages;
    private readonly IBookService _books;
    private readonly IDialogService _dialogs;

    public PackagesViewModel(IPackageService packages, IBookService books, IDialogService dialogs)
    {
        _packages = packages;
        _books = books;
        _dialogs = dialogs;
        Title = "Packages";
    }

    [ObservableProperty]
    private BookSummary? selectedBook;

    [ObservableProperty]
    private string? inspectionSummary;

    public ObservableCollection<BookSummary> Books { get; } = new();

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        var result = await _books.QueryAsync(new BookQuery { Page = 1, PageSize = 500, SortBy = BookSortField.Title });
        Books.Clear();
        foreach (var book in result.Items)
        {
            Books.Add(book);
        }
        SelectedBook = Books.FirstOrDefault();
    });

    [RelayCommand]
    private async Task BuildAsync()
    {
        if (SelectedBook is null)
        {
            _dialogs.ShowError("Select a book to package.");
            return;
        }
        var target = _dialogs.SaveFile("DIC package (*.dicpkg)|*.dicpkg", "Build package", $"{SelectedBook.Title}.dicpkg");
        if (target is null)
        {
            return;
        }
        var result = await _packages.BuildAsync(SelectedBook.Id, target);
        if (result.Succeeded)
        {
            _dialogs.ShowInfo("Package built successfully.", "Packages");
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Package build failed.");
        }
    }

    [RelayCommand]
    private async Task InspectAsync()
    {
        var file = _dialogs.OpenFile("DIC package (*.dicpkg)|*.dicpkg", "Inspect package");
        if (file is null)
        {
            return;
        }
        var result = await _packages.InspectAsync(file);
        if (result.Succeeded && result.Value is not null)
        {
            var inspection = result.Value;
            InspectionSummary = inspection.IsValid
                ? $"Valid package. Signature: {(inspection.SignatureVerified ? "verified" : "UNVERIFIED")}. " +
                  $"Title: {inspection.Manifest?.Title}. Pages: {inspection.Manifest?.PageCount}."
                : $"Invalid package: {inspection.Error}";
        }
        else
        {
            InspectionSummary = result.Error ?? "Unable to inspect package.";
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var file = _dialogs.OpenFile("DIC package (*.dicpkg)|*.dicpkg", "Install package");
        if (file is null)
        {
            return;
        }
        var result = await _packages.InstallAsync(file);
        if (result.Succeeded)
        {
            _dialogs.ShowInfo("Package installed into the library.", "Packages");
            await OnNavigatedToAsync(null);
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Install failed.");
        }
    }

    [RelayCommand]
    private async Task ExportCatalogAsync()
    {
        var target = _dialogs.SaveFile("DIC catalog (*.dicl)|*.dicl", "Export catalog", "catalog.dicl");
        if (target is null)
        {
            return;
        }
        var result = await _packages.ExportCatalogAsync(target);
        if (result.Succeeded)
        {
            _dialogs.ShowInfo("Catalog bundle exported.", "Packages");
        }
        else
        {
            _dialogs.ShowError(result.Error ?? "Export failed.");
        }
    }
}
