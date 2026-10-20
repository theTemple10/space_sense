using System.Windows;

namespace SpaceSense.Desktop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new Window { Title = "Space Sense", Width = 1100, Height = 740,
            Content = "Space Sense · Understand your storage before making a change." }.Show();
    }
}
