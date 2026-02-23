using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Browser;
using Hive.Browser.Features.Storage;
using Hive.Common.Services;

[assembly: SupportedOSPlatform("browser")]

namespace Hive.Browser;

internal sealed class Program
{
    private static Task Main(string[] args)
    {
        GameStateSerializer.StorageProvider = new BrowserStorageProvider();

        return BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<HiveWebApp>();
    }
}