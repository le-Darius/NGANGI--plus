using Microsoft.Maui.Storage;

namespace Ngangi.App;

public enum ThemeChoice { Auto, Light, Dark }

/// <summary>
/// Thème choisi par l'utilisateur : automatique (suit le téléphone), clair ou sombre.
/// Mémorisé sur l'appareil (Preferences, local) et appliqué dès le démarrage. Les couleurs viennent de Colors.xaml
/// par AppThemeBinding : changer le thème met l'écran à jour sans redémarrage.
/// </summary>
public static class ThemeService
{
    private const string Key = "ngangi.theme";

    public static ThemeChoice Current { get; private set; } = ThemeChoice.Auto;

    /// <summary>Lit le choix mémorisé et l'applique. À appeler une fois au démarrage.</summary>
    public static void Load()
    {
        var saved = "";
        try { saved = Preferences.Default.Get(Key, ""); }
        catch (Exception) { /* stockage indisponible : on reste en automatique */ }

        Current = saved switch
        {
            nameof(ThemeChoice.Light) => ThemeChoice.Light,
            nameof(ThemeChoice.Dark) => ThemeChoice.Dark,
            _ => ThemeChoice.Auto,
        };
        Apply();
    }

    public static void Set(ThemeChoice choice)
    {
        Current = choice;
        try { Preferences.Default.Set(Key, choice.ToString()); }
        catch (Exception) { /* le choix s'applique quand même pour cette session */ }
        Apply();
    }

    private static void Apply()
    {
        if (Application.Current == null) return;
        Application.Current.UserAppTheme = Current switch
        {
            ThemeChoice.Light => AppTheme.Light,
            ThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
    }
}
