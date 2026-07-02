using AuraCoursePlanner.Data;
using AuraCoursePlanner.ViewModels;
using System.Windows;

namespace AuraCoursePlanner.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainViewModel(() => new AuraDbContext());
        DataContext = vm;

        Loaded += async (_, _) => await vm.LoadCoursesCommand.ExecuteAsync(null);
    }
}
