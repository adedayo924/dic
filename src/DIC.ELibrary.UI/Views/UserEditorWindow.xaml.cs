using System.Windows;
using System.Windows.Controls;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class UserEditorWindow : Window
{
    public UserEditorWindow(UserEditorViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        RoleBox.ItemsSource = new[] { UserRole.Student, UserRole.Teacher, UserRole.Librarian, UserRole.Administrator };
    }

    public UserEditorViewModel ViewModel { get; }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        ViewModel.Password = ((PasswordBox)sender).Password;
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var result = await ViewModel.SaveAsync();
        if (result.Succeeded)
        {
            DialogResult = true;
        }
        else
        {
            MessageBox.Show(result.Error ?? "Unable to save the user.", "User account",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
