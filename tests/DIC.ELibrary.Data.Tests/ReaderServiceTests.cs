using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;

namespace DIC.ELibrary.Data.Tests;

public class ReaderServiceTests
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

    [Fact]
    public async Task GetFavorites_WithCategory_ReturnsBookAndDoesNotThrow()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var categories = host.GetService<ICategoryService>();
        var reader = host.GetService<IReaderService>();
        var userId = await CreateStudentAsync(host);

        var category = await categories.CreateAsync("Astronomy", null, null);
        var book = await books.CreateAsync(
            new BookEditModel { Title = "Cosmos", Status = BookStatus.Published, CategoryId = category.Value!.Id },
            null);
        Assert.True(book.Succeeded);

        await reader.ToggleFavoriteAsync(userId, book.Value!.Id);

        var favorites = await reader.GetFavoritesAsync(userId);

        var summary = Assert.Single(favorites);
        Assert.Equal("Cosmos", summary.Title);
        Assert.Equal("Astronomy", summary.CategoryName);
    }

    [Fact]
    public async Task GetHistory_WithCategory_ReturnsBookAndDoesNotThrow()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var books = host.GetService<IBookService>();
        var categories = host.GetService<ICategoryService>();
        var reader = host.GetService<IReaderService>();
        var userId = await CreateStudentAsync(host);

        var category = await categories.CreateAsync("Fiction", null, null);
        var book = await books.CreateAsync(
            new BookEditModel { Title = "Dune", Status = BookStatus.Published, CategoryId = category.Value!.Id },
            null);
        Assert.True(book.Succeeded);

        await reader.SaveProgressAsync(userId, book.Value!.Id, 12, 30);

        var history = await reader.GetHistoryAsync(userId, 10);

        var summary = Assert.Single(history);
        Assert.Equal("Dune", summary.Title);
        Assert.Equal("Fiction", summary.CategoryName);
    }
}
