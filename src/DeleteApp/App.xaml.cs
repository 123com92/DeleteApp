using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;

namespace DeleteApp;

public partial class App
{
    public static bool IsAdministrator { get; }

    static App()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        IsAdministrator = principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            args.Handled = true;
            MessageBox.Show(
                $"程序发生未处理异常：\n\n{args.Exception.Message}\n\n类型：{args.Exception.GetType().Name}\n\n程序将继续运行，但建议重新启动。",
                "异常",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        };
    }
}
