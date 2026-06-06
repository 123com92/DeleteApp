using System.Security.Principal;

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
}
