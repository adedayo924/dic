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

public partial class IngestViewModel : ViewModelBase
{
    private readonly IBookService _books;
    private readonly ICategoryService _categories;
    private readonly IPdfService _pdf;
    private readonly IPdfRenderer _renderer;
    private readonly IOcrService _ocr;
    private readonly IDialogService _dialogs;

    public IngestViewModel(
        IBookService books,
        ICategoryService categories,
        IPdfService pdf,
        IPdfRenderer renderer,
        IOcrService ocr,
        IDialogService dialogs)
    {
        _books = books;
        _categories = categories;
        _pdf = pdf;
        _renderer = renderer;
        _ocr = ocr;
        _dialogs = dialogs;
        Title = "Ingest";
    }

    [ObservableProperty]
    private string? pdfPath;

    [ObservableProperty]
    private bool runOcrWhenScanned = true;

    [ObservableProperty]
    private bool isScanned;

    [ObservableProperty]
    private int pageCount;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? author;

    [ObservableProperty]
    private string? publisher;

    [ObservableProperty]
    private int? publicationYear;

    [ObservableProperty]
    private string? language;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private Guid? categoryId;

    [ObservableProperty]
    private string status = string.Empty;

    public ObservableCollection<Category> Categories { get; } = new();

    public string FileName => string.IsNullOrWhiteSpace(PdfPath) ? "(none selected)" : Path.GetFileName(PdfPath);

    public override Task OnNavigatedToAsync(object? parameter) => RunAsync(async () =>
    {
        if (Categories.Count == 0)
        {
            foreach (var category in await _categories.GetAllAsync())
            {
                Categories.Add(category);
            }
        }
    });

    [RelayCommand]
    private async Task BrowseAsync()
    {
        var file = _dialogs.OpenFile("PDF files (*.pdf)|*.pdf|All files (*.*)|*.*", "Select a PDF to ingest");
        if (file is null)
        {
            return;
        }

        PdfPath = file;
        OnPropertyChanged(nameof(FileName));
        await RunAsync(async () =>
        {
            try
            {
                var info = await _pdf.ReadInfoAsync(file);
                PageCount = info.PageCount;
                IsScanned = !info.HasText;
                if (!string.IsNullOrWhiteSpace(info.Title))
                {
                    Title = info.Title!;
                }
                if (!string.IsNullOrWhiteSpace(info.Author))
                {
                    Author = info.Author;
                }
                Status = IsScanned
                    ? "Scanned document detected — OCR will be required."
                    : "Digital text detected — no OCR needed.";
            }
            catch (Exception ex)
            {
                Status = "Unable to read the PDF.";
                _dialogs.ShowError(ex.Message);
            }
        });
    }

    [RelayCommand]
    private async Task IngestAsync()
    {
        if (string.IsNullOrWhiteSpace(PdfPath) || !File.Exists(PdfPath))
        {
            _dialogs.ShowError("Choose a PDF file first.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Title))
        {
            _dialogs.ShowError("Enter a title.");
            return;
        }

        await RunAsync(async () =>
        {
            var model = new BookEditModel
            {
                Title = Title.Trim(),
                Author = Author,
                Publisher = Publisher,
                PublicationYear = PublicationYear,
                Language = Language,
                Description = Description,
                CategoryId = CategoryId,
                Status = BookStatus.Published
            };

            Status = "Copying file and reading metadata…";
            var created = await _books.CreateAsync(model, PdfPath!);
            if (!created.Succeeded || created.Value is null)
            {
                Status = created.Error ?? "Ingest failed.";
                _dialogs.ShowError(Status);
                return;
            }

            var bookId = created.Value.Id;

            Status = "Generating cover…";
            try
            {
                var cover = await _renderer.GenerateCoverAsync(PdfPath!, 420);
                if (cover.Length > 0)
                {
                    await _books.SetCoverAsync(bookId, cover);
                }
            }
            catch
            {
                // Cover generation is best-effort.
            }

            if (IsScanned && RunOcrWhenScanned && _ocr.IsAvailable)
            {
                Status = "Running OCR (this may take a while)…";
                try
                {
                    var pages = await _ocr.RecognizePdfAsync(PdfPath!);
                    var text = string.Join(Environment.NewLine + Environment.NewLine, pages.Select(p => p.Text));
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        await _books.ApplyOcrTextAsync(bookId, text);
                    }
                }
                catch (Exception ex)
                {
                    Status = "OCR failed.";
                    _dialogs.ShowError(ex.Message);
                }
            }

            Status = "Ingest complete.";
            _dialogs.ShowInfo($"'{Title}' was added to the library.", "Ingest complete");
            Reset();
        });
    }

    private void Reset()
    {
        PdfPath = null;
        Title = string.Empty;
        Author = null;
        Publisher = null;
        PublicationYear = null;
        Language = null;
        Description = null;
        CategoryId = null;
        IsScanned = false;
        PageCount = 0;
        OnPropertyChanged(nameof(FileName));
    }
}
