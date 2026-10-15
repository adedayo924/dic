using CommunityToolkit.Mvvm.ComponentModel;
using DIC.ELibrary.Core.Abstractions;
using DIC.ELibrary.Core.Models;
using DIC.ELibrary.UI.Mvvm;

namespace DIC.ELibrary.UI.ViewModels;

public partial class CategoryEditorViewModel : ViewModelBase
{
    private readonly ICategoryService _categories;

    public CategoryEditorViewModel(ICategoryService categories)
    {
        _categories = categories;
        Title = "Category";
    }

    [ObservableProperty]
    private Guid? categoryId;

    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string? description;

    public bool IsNew => CategoryId is null;

    public string Header => IsNew ? "New category" : "Edit category";

    public async Task InitializeAsync(Guid? id)
    {
        CategoryId = id;
        OnPropertyChanged(nameof(IsNew));
        OnPropertyChanged(nameof(Header));

        if (id is null)
        {
            Name = string.Empty;
            Description = null;
            return;
        }

        var category = (await _categories.GetAllAsync()).FirstOrDefault(c => c.Id == id);
        if (category is not null)
        {
            Name = category.Name;
            Description = category.Description;
        }
    }

    public async Task<OperationResult> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return OperationResult.Fail("Category name is required.");
        }

        if (CategoryId is null)
        {
            var created = await _categories.CreateAsync(Name.Trim(), Description, null);
            if (!created.Succeeded)
            {
                return OperationResult.Fail(created.Error ?? "Unable to create the category.");
            }
            CategoryId = created.Value!.Id;
            return OperationResult.Ok();
        }

        return await _categories.UpdateAsync(CategoryId.Value, Name.Trim(), Description, null);
    }
}
