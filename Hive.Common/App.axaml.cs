using Avalonia;
using Avalonia.Markup.Xaml;

namespace Hive.Common;

public class HiveApp : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected virtual void InitialiseApplication()
    {
        throw new NotImplementedException();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        InitialiseApplication();

        base.OnFrameworkInitializationCompleted();
    }
}