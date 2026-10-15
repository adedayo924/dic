using System.Windows;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class CategoryEditorWindow : Window
{
    public CategoryEditorWindow(CategoryEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public CategoryEditorViewModel ViewModel { get; }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var result = await ViewModel.SaveAsync();
        if (result.Succeeded)
        {
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(result.Error ?? "Unable to save the category.", "Book category",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
