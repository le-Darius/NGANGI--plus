namespace Ngangi.App;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeService.Load();   // thème choisi dans Profil (automatique, clair ou sombre)
    }

    protected override Window CreateWindow(IActivationState? activationState) => new Window(new AppShell());
}
