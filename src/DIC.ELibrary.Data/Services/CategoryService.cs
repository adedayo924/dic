using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.Data.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DIC.ELibrary.Data.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IDbContextFactory<LibraryDbContext> _factory;
    private readonly IAuditService _audit;

    public CategoryService(IDbContextFactory<LibraryDbContext> factory, IAuditService audit)
    {
        _factory = factory;
        _audit = audit;
    }

    public async Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<Category>> CreateAsync(string name, string? description, Guid? parentId, CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult<Category>.Fail("Category name is required.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        if (await db.Categories.AnyAsync(c => c.Name == name, cancellationToken))
        {
            return OperationResult<Category>.Fail("A category with that name already exists.");
        }

        var category = new Category { Name = name, Description = description, ParentId = parentId };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<Category>.Ok(category);
    }

    public async Task<OperationResult> UpdateAsync(Guid id, string name, string? description, Guid? parentId, CancellationToken cancellationToken = default)
    {
        name = name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return OperationResult.Fail("Category name is required.");
        }
        if (parentId == id)
        {
            return OperationResult.Fail("A category cannot be its own parent.");
        }

        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return OperationResult.Fail("Category not found.");
        }
        if (await db.Categories.AnyAsync(c => c.Name == name && c.Id != id, cancellationToken))
        {
            return OperationResult.Fail("A category with that name already exists.");
        }

        category.Name = name;
        category.Description = description;
        category.ParentId = parentId;
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return OperationResult.Fail("Category not found.");
        }
        if (await db.Categories.AnyAsync(c => c.ParentId == id, cancellationToken))
        {
            return OperationResult.Fail("Remove or reassign child categories first.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        await _audit.LogAsync(AuditAction.SettingsChanged, nameof(Category), id.ToString(), $"Deleted category {category.Name}", cancellationToken);
        return OperationResult.Ok();
    }
}
