using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Data.Tests;

public class CatalogServiceTests
{
    private static async Task<Guid> CreateStudentAsync(TestLibraryHost host, string username = "reader")
    {
        var users = host.GetService<IUserService>();
        var created = await users.CreateAsync(
            new UserEditModel { Username = username, DisplayName = username, Role = UserRole.Student },
            "Reader@123");
        Assert.True(created.Succeeded);
        return created.Value!.Id;
    }

    private static string WriteTempPdf()
    {
        var path = Path.Combine(Path.GetTempPath(), $"dic-test-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(path, "%PDF-1.4 fake content");
        return path;
    }

    [Fact]
    public async Task CreateBook_WithPdf_PopulatesMetadataAndIsQueryable()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var pdf = WriteTempPdf();
        try
        {
            var result = await books.CreateAsync(
                new BookEditModel { Title = "Computer Networks", Author = "Ada Lovelace", Status = BookStatus.Published },
                pdf);

            Assert.True(result.Succeeded);
            Assert.Equal("Computer Networks", result.Value!.Title);
            Assert.Equal(3, result.Value.PageCount);
            Assert.False(string.IsNullOrEmpty(result.Value.FilePath));

            var page = await books.QueryAsync(new BookQuery { SearchText = "Computer" });
            Assert.Equal(1, page.TotalCount);
            Assert.Equal("Computer Networks", page.Items.Single().Title);
        }
        finally
        {
            File.Delete(pdf);
        }
    }

    [Fact]
    public async Task CreateBook_WithoutTitle_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();

        var result = await books.CreateAsync(new BookEditModel { Title = "   " }, null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Borrow_DraftBook_IsRejected()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var loans = host.GetService<ILoanService>();
        var userId = await CreateStudentAsync(host);

        var draft = await books.CreateAsync(new BookEditModel { Title = "Draft Book", Status = BookStatus.Draft }, null);
        Assert.True(draft.Succeeded);

        var borrow = await loans.BorrowAsync(userId, draft.Value!.Id);

        Assert.False(borrow.Succeeded);
    }

    [Fact]
    public async Task Borrow_PublishedBook_ThenReturn()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var loans = host.GetService<ILoanService>();
        var userId = await CreateStudentAsync(host);

        var book = await books.CreateAsync(new BookEditModel { Title = "Borrowable", Status = BookStatus.Published }, null);
        Assert.True(book.Succeeded);

        var borrow = await loans.BorrowAsync(userId, book.Value!.Id);
        Assert.True(borrow.Succeeded);
        Assert.Equal(LoanStatus.Active, borrow.Value!.Status);

        var forUser = await loans.GetForUserAsync(userId);
        Assert.Single(forUser);

        Assert.True((await loans.ReturnAsync(borrow.Value.Id)).Succeeded);

        var afterReturn = await loans.GetForUserAsync(userId);
        Assert.Equal(LoanStatus.Returned, afterReturn.Single().Status);
    }

    [Fact]
    public async Task Borrow_SameBookTwice_IsRejected()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var loans = host.GetService<ILoanService>();
        var userId = await CreateStudentAsync(host);
        var book = await books.CreateAsync(new BookEditModel { Title = "Twice", Status = BookStatus.Published }, null);

        Assert.True((await loans.BorrowAsync(userId, book.Value!.Id)).Succeeded);
        var second = await loans.BorrowAsync(userId, book.Value!.Id);

        Assert.False(second.Succeeded);
    }

    [Fact]
    public async Task SetStatus_ToPublished_AllowsBorrow()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var loans = host.GetService<ILoanService>();
        var userId = await CreateStudentAsync(host);
        var book = await books.CreateAsync(new BookEditModel { Title = "Promote Me", Status = BookStatus.Draft }, null);

        Assert.True((await books.SetStatusAsync(book.Value!.Id, BookStatus.Published)).Succeeded);
        Assert.True((await loans.BorrowAsync(userId, book.Value!.Id)).Succeeded);
    }

    [Fact]
    public async Task ApplyOcrText_PersistsAndMarksCompleted()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var book = await books.CreateAsync(new BookEditModel { Title = "Scanned", Status = BookStatus.Published }, null);

        var result = await books.ApplyOcrTextAsync(book.Value!.Id, "Recognized text from a scanned page.");

        Assert.True(result.Succeeded);
        var detail = await books.GetAsync(book.Value.Id);
        Assert.Equal(OcrStatus.Completed, detail!.OcrStatus);
    }
}
