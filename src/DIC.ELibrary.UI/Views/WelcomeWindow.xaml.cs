using System.Windows;
using DIC.ELibrary.Core.Domain;
using DIC.ELibrary.UI.ViewModels;

namespace DIC.ELibrary.UI.Views;

public partial class WelcomeWindow : Window
{
    public WelcomeWindow(WelcomeViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public WelcomeViewModel ViewModel { get; }

    private void OnStudent(object sender, RoutedEventArgs e) => Choose(UserRole.Student);

    private void OnTeacher(object sender, RoutedEventArgs e) => Choose(UserRole.Teacher);

    private void OnLibrarian(object sender, RoutedEventArgs e) => Choose(UserRole.Librarian);

    private void OnAdministrator(object sender, RoutedEventArgs e) => Choose(UserRole.Administrator);

    private void Choose(UserRole role)
    {
        ViewModel.Choose(role);
        DialogResult = true;
    }
}
