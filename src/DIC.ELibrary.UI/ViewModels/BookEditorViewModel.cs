using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;
using DIC.ELibrary.UI.Services;

namespace DIC.ELibrary.UI.ViewModels;

public partial class BookEditorViewModel : ViewModelBase
{
    private readonly IBookService _books;
    private readonly ICategoryService _categories;
    private readonly ICurrentUser _currentUser;

    public BookEditorViewModel(
        IBookService books,
        ICategoryService categories,
        ICurrentUser currentUser)
    {
        _books = books;
        _categories = categories;
        _currentUser = currentUser;
        Categories = new ObservableCollection<Category>();
    }

    public ObservableCollection<Category> Categories { get; }

    [ObservableProperty]
    private Guid? bookId;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string? subtitle;

    [ObservableProperty]
    private string? author;

    [ObservableProperty]
    private string? publisher;

    [ObservableProperty]
    private int? publicationYear;

    [ObservableProperty]
    private string? isbn;

    [ObservableProperty]
    private string? language;

    [ObservableProperty]
    private string? description;

    [ObservableProperty]
    private Guid? categoryId;

    [ObservableProperty]
    private BookStatus status = BookStatus.Published;

    public async Task InitializeAsync(Guid? id)
    {
        BookId = id;
        Categories.Clear();
        foreach (var category in await _categories.GetAllAsync())
        {
            Categories.Add(category);
        }

        if (id is not null)
        {
            var detail = await _books.GetAsync(id.Value);
            if (detail is not null)
            {
                Title = detail.Title;
                Subtitle = detail.Subtitle;
                Author = detail.Author;
                Publisher = detail.Publisher;
                PublicationYear = detail.PublicationYear;
                Isbn = detail.Isbn;
                Language = detail.Language;
                Description = detail.Description;
                CategoryId = detail.CategoryId;
                Status = detail.Status;
            }
        }
    }

    public async Task<OperationResult> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return OperationResult.Fail("Title is required.");
        }

        var model = new BookEditModel
        {
            Id = BookId,
            Title = Title.Trim(),
            Subtitle = Subtitle,
            Author = Author,
            Publisher = Publisher,
            PublicationYear = PublicationYear,
            Isbn = Isbn,
            Language = Language,
            Description = Description,
            CategoryId = CategoryId,
            Status = Status
        };

        if (BookId is null)
        {
            var result = await _books.CreateAsync(model, null);
            if (result.Succeeded && result.Value is not null)
            {
                BookId = result.Value.Id;
                return OperationResult.Ok();
            }
            return OperationResult.Fail(result.Error ?? "Failed to create book.");
        }

        return await _books.UpdateAsync(model);
    }
}
