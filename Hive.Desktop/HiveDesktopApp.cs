using Avalonia.Controls.ApplicationLifetimes;
using Hive.Common;

namespace Hive.Desktop;

public class HiveDesktopApp : HiveApp
{
    protected override void InitialiseApplication()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            throw new NotSupportedException($"The current application lifetime is not '{nameof(IClassicDesktopStyleApplicationLifetime)}'.");

        // Desktop application - use Window
        desktop.MainWindow = new HiveHostWindow();
    }
}