using AuraCoursePlanner.ViewModels;
using System.Windows;

namespace AuraCoursePlanner.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;

        Loaded += async (_, _) => await viewModel.LoadCoursesCommand.ExecuteAsync(null);
    }
}