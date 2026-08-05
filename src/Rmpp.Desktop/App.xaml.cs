using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Rmpp.Desktop.Composition;
using Rmpp.Desktop.Services;

namespace Rmpp.Desktop;

public partial class App : System.Windows.Application
{
    private ServiceProvider? services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (TryRunOfflineSmoke(e.Args))
        {
            return;
        }
        if (TryRunStorageSmoke(e.Args))
        {
            return;
        }
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        try
        {
            services = await AppHost.BuildServicesAsync();
            services.GetRequiredService<MainWindow>().Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.ToString(), "RMPP 启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        services?.Dispose();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.Message, "RMPP", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private bool TryRunOfflineSmoke(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "--offline-smoke", StringComparison.Ordinal)) return false;
        int exitCode;
        try
        {
            string root = args.Length > 1 ? args[1] : Path.Combine(Path.GetTempPath(), "rmpp-offline-smoke-" + Guid.NewGuid().ToString("N"));
            Task.Run(() => OfflineSmokeWorkflow.RunAsync(root)).GetAwaiter().GetResult();
            exitCode = 0;
        }
        catch (Exception exception)
        {
            try
            {
                string root = args.Length > 1 ? args[1] : Path.GetTempPath();
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "offline-smoke-error.txt"), exception.ToString());
            }
            catch (Exception) { }
            exitCode = 2;
        }
        Shutdown(exitCode);
        return true;
    }

    private bool TryRunStorageSmoke(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "--storage-smoke", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            AppStoragePaths paths = AppStoragePaths.Detect();
            File.WriteAllText(
                Path.Combine(paths.Root, "storage-smoke.json"),
                JsonSerializer.Serialize(new { paths.Root, paths.IsPortable }));
            Shutdown(0);
        }
        catch (Exception exception)
        {
            try
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "rmpp-storage-smoke-error.txt"), exception.ToString());
            }
            catch (Exception) { }
            Shutdown(2);
        }

        return true;
    }
}
