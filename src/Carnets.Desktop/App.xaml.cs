using System.IO;
using System.Windows;

namespace Carnets.Desktop;

public partial class App : Application
{
    private async void OnStartupEvent(object sender, StartupEventArgs e)
    {
        if (e.Args.Length > 0 && e.Args[0] == "--self-check")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            string output = Path.GetFullPath(e.Args.Length > 1 ? e.Args[1] : Path.Combine(Path.GetTempPath(), "ChenchoNet-self-check-" + Guid.NewGuid().ToString("N")));
            try { await PackageCheck.RunAsync(output); Shutdown(0); }
            catch (Exception ex)
            {
                Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "self-check-failed.txt"), ex.ToString()); Shutdown(1);
            }
            return;
        }
        var window = new MainWindow(); MainWindow = window;
        if (e.Args.Contains("--demo")) window.Loaded += (_, _) => window.LoadDemoSession();
        window.Show();
    }
}
