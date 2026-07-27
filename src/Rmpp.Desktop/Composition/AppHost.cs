using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Rmpp.Desktop.ViewModels;
using Rmpp.Application.Abstractions;
using Rmpp.Application.Data;
using Rmpp.Application.Printing;
using Rmpp.Desktop.Services;
using Rmpp.Infrastructure.Data;
using Rmpp.Infrastructure.Pdf;
using Rmpp.Infrastructure.Persistence;
using Rmpp.Infrastructure.Templates;
using Rmpp.Printing.Windows.Jobs;
using Rmpp.Printing.Windows.Printers;

namespace Rmpp.Desktop.Composition;

/// <summary>桌面应用的本地组合根；只注册文件、数据库、渲染和打印服务，不注册网络客户端。</summary>
public static class AppHost
{
    public static ServiceProvider BuildServices()
    {
        ServiceCollection services = new();
        services.AddSingleton(AppStoragePaths.Detect());
        services.AddSingleton(static provider =>
        {
            AppStoragePaths paths = provider.GetRequiredService<AppStoragePaths>();
            string installedParent = Directory.GetParent(paths.Root)?.FullName ?? paths.Root;
            return new SqliteAppDatabase(new DatabaseOptions
            {
                StorageMode = paths.IsPortable ? DatabaseStorageMode.Portable : DatabaseStorageMode.Installed,
                ExecutableDirectory = AppContext.BaseDirectory,
                InstalledDataRoot = installedParent,
                ApplicationDirectoryName = Path.GetFileName(paths.Root),
                PortableDataDirectoryName = "data",
            });
        });
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<TemplateCatalogRepository>();
        services.AddSingleton<RecoverySessionRepository>();
        services.AddSingleton<CalibrationProfileRepository>();
        services.AddSingleton<RecentFileRepository>();
        services.AddSingleton<RmppPackageReader>();
        services.AddSingleton<RmppPackageWriter>();
        services.AddSingleton<RmppPackageValidator>();
        services.AddSingleton<AtomicTemplateFileWriter>();
        services.AddSingleton<TemplateCatalogScanner>();
        services.AddSingleton<RecoveryCoordinator>();
        services.AddSingleton<LocalHelpService>();
        services.AddSingleton<IDataSourceReader, CsvDataSourceReader>();
        services.AddSingleton<IDataSourceReader, ExcelDataSourceReader>();
        services.AddSingleton<DataPreviewService>();
        services.AddSingleton<PrintJobPlanner>();
        services.AddSingleton<PrintJobValidator>();
        services.AddSingleton<PrintPreviewService>();
        services.AddSingleton<IRenderExporter, LocalRenderExporter>();
        services.AddSingleton<IWindowsPrintSystemAdapter, SystemPrintingAdapter>();
        services.AddSingleton<WindowsPrinterCatalog>();
        services.AddSingleton<IWindowsSpoolAdapter, WpfXpsSpoolAdapter>();
        services.AddSingleton<ICalibrationProfileProvider, CalibrationProfileProviderAdapter>();
        services.AddSingleton<IPrinterService, WindowsPrintService>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<TemplateCatalogViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<SqliteAppDatabase>().InitializeAsync().GetAwaiter().GetResult();
        return provider;
    }
}

/// <summary>统一安装/便携数据目录；portable.marker 存在时不写入 LocalAppData。</summary>
public sealed record AppStoragePaths(string Root, bool IsPortable)
{
    public string DatabasePath => Path.Combine(Root, "rmpp.db");
    public string RecoveryDirectory => Path.Combine(Root, "recovery");

    public static AppStoragePaths Detect()
    {
        string executableDirectory = AppContext.BaseDirectory;
        bool portable = File.Exists(Path.Combine(executableDirectory, "portable.marker"))
            || File.Exists(Path.Combine(executableDirectory, "portable.flag"));
        string root = portable
            ? Path.Combine(executableDirectory, "data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RMPP");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "recovery"));
        return new AppStoragePaths(root, portable);
    }
}
