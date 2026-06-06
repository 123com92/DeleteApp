namespace DeleteApp;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new UI.ViewModels.MainViewModel();
        Title = App.IsAdministrator
            ? "Windows Rogue Software Cleaner [管理员]"
            : "Windows Rogue Software Cleaner";
    }
}
