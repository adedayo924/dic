using DIC.ELibrary.Core.Abstractions;

namespace DIC.ELibrary.Data.Tests;

public class CategoryServiceTests
{
    [Fact]
    public async Task Create_PersistsAndIsReturned()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();

        var created = await categories.CreateAsync("Astronomy", "Books about the stars", null);

        Assert.True(created.Succeeded);
        Assert.Equal("Astronomy", created.Value!.Name);

        var all = await categories.GetAllAsync();
        Assert.Contains(all, c => c.Id == created.Value.Id && c.Name == "Astronomy");
    }

    [Fact]
    public async Task Create_BlankName_Fails()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();

        var result = await categories.CreateAsync("   ", null, null);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Create_DuplicateName_IsRejected()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();

        Assert.True((await categories.CreateAsync("Fiction", null, null)).Succeeded);
        var duplicate = await categories.CreateAsync("Fiction", null, null);

        Assert.False(duplicate.Succeeded);
    }

    [Fact]
    public async Task Create_NamesDifferingOnlyByCase_AreAllowed()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();

        Assert.True((await categories.CreateAsync("Fiction", null, null)).Succeeded);
        var second = await categories.CreateAsync("fiction", null, null);

        Assert.True(second.Succeeded);
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();
        var created = await categories.CreateAsync("Fiction", "Old description", null);

        var updated = await categories.UpdateAsync(created.Value!.Id, "Fiction & Fantasy", "New description", null);

        Assert.True(updated.Succeeded);
        var all = await categories.GetAllAsync();
        var category = all.Single(c => c.Id == created.Value.Id);
        Assert.Equal("Fiction & Fantasy", category.Name);
        Assert.Equal("New description", category.Description);
    }

    [Fact]
    public async Task Delete_RemovesCategory()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();
        var created = await categories.CreateAsync("Temporary", null, null);

        var deleted = await categories.DeleteAsync(created.Value!.Id);

        Assert.True(deleted.Succeeded);
        var all = await categories.GetAllAsync();
        Assert.DoesNotContain(all, c => c.Id == created.Value.Id);
    }

    [Fact]
    public async Task Delete_WithChildren_IsRejected()
    {
        await using var host = await TestLibraryHost.CreateAsync();
        var categories = host.GetService<ICategoryService>();
        var parent = await categories.CreateAsync("Parent", null, null);
        Assert.True((await categories.CreateAsync("Child", null, parent.Value!.Id)).Succeeded);

        var deleted = await categories.DeleteAsync(parent.Value.Id);

        Assert.False(deleted.Succeeded);
    }
}
