using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Hive.Common.Views;

namespace Hive.Desktop;

public partial class HiveHostWindow : Window
{
    public HiveHostWindow()
    {
        InitializeComponent();

        var hiveView = new HiveGameView();
        Content = hiveView;
        
        Closing += (sender, e) =>
        {
            hiveView.SaveGameState();
        };
    }
}