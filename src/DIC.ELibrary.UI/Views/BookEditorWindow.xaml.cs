using System.Windows;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class BookEditorWindow : Window
{
    public BookEditorWindow(BookEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        StatusBox.ItemsSource = Enum.GetValues<BookStatus>();
    }

    public BookEditorViewModel ViewModel { get; }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var result = await ViewModel.SaveAsync();
        if (result.Succeeded)
        {
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(result.Error ?? "Unable to save the book.", "Book details",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
