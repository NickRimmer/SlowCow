using Avalonia;
using Avalonia.Markup.Xaml;
namespace SlowCow.Setup.UI;

internal class Dummy : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);
}

