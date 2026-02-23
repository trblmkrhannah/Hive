using Avalonia.Controls.ApplicationLifetimes;
using Hive.Common;
using Hive.Common.Views;

namespace Hive.Browser;

public class HiveWebApp : HiveApp
{
    protected override void InitialiseApplication()
    {
        if (ApplicationLifetime is not ISingleViewApplicationLifetime singleViewPlatform)
            throw new NotSupportedException($"The current application lifetime is not '{nameof(ISingleViewApplicationLifetime)}'.");
        
        // Browser/mobile application - use UserControl
        singleViewPlatform.MainView = new HiveGameView();
    }
}