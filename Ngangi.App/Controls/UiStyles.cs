namespace Ngangi.App.Controls;

/// <summary>Retrouve un style de Styles.xaml par sa clé (les couleurs restent dans Colors.xaml).</summary>
internal static class UiStyles
{
    public static Style? Get(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true ? value as Style : null;
}
