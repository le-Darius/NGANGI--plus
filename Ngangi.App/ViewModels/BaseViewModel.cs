using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Ngangi.App.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    protected void Raise(params string[] names)
    {
        foreach (var n in names) OnPropertyChanged(n);
    }
}

/// <summary>Helpers de navigation et de messages.</summary>
public static class Nav
{
    public static Task Go(string route) => Shell.Current.GoToAsync(route);
    public static Task Back() => Shell.Current.GoToAsync("..");
    public static Task Alert(string title, string message) => Shell.Current.DisplayAlert(title, message, "OK");
    public static Task<bool> Confirm(string title, string message) => Shell.Current.DisplayAlert(title, message, "Oui", "Non");

    /// <summary>Confirmation avec des boutons explicites (ex. « Se déconnecter » / « Annuler »).</summary>
    public static Task<bool> Confirm(string title, string message, string accept, string cancel) =>
        Shell.Current.DisplayAlert(title, message, accept, cancel);

    /// <summary>Referme tous les écrans empilés au-dessus de l'écran de départ de la section courante (sans animation).</summary>
    public static async Task PopToRootAsync()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
            await Shell.Current.Navigation.PopToRootAsync(false);
    }

    /// <summary>
    /// Entre dans l'application (après connexion ou inscription) par l'Accueil. Navigation absolue : l'écran de connexion
    /// n'est plus dans l'historique, la touche Retour ne peut pas y ramener.
    /// </summary>
    public static Task GoToMainAsync() => Shell.Current.GoToAsync("//main/dashboard");

    /// <summary>
    /// Retourne à la connexion. Une page empilée dans l'ancienne section (ex. l'inscription) y reste mémorisée par Shell :
    /// on referme donc la pile pour que la connexion s'affiche, et seulement elle.
    /// </summary>
    public static async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("//login");
        await PopToRootAsync();
    }
}

/// <summary>Une ligne de liste générique (membre, cotisation, prêt, échéance, amende).</summary>
public class Row
{
    public string Initials { get; set; } = "";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Trailing { get; set; } = "";
    public string BadgeText { get; set; } = "";
    public Color BadgeBg { get; set; } = Palette.None;
    public Color BadgeFg { get; set; } = Palette.Tone(Palette.Next).Fg;

    public ICommand? Tap { get; set; }
    public ICommand? PrimaryCommand { get; set; }
    public string PrimaryText { get; set; } = "";
    public ICommand? SecondaryCommand { get; set; }
    public string SecondaryText { get; set; } = "";

    public bool HasInitials => Initials != "";
    public bool HasDetail => Detail != "";
    public bool HasTrailing => Trailing != "";
    public bool HasBadge => BadgeText != "";
    public bool HasPrimary => PrimaryCommand != null;
    public bool HasSecondary => SecondaryCommand != null;
    public bool HasActions => HasPrimary || HasSecondary;

    public Row WithBadge(string text, string tone)
    {
        var (bg, fg) = Palette.Tone(tone);
        BadgeText = text;
        BadgeBg = bg;
        BadgeFg = fg;
        return this;
    }
}

public class ChipRow
{
    public string Label { get; set; } = "";
    public Color Bg { get; set; } = Palette.None;
    public Color Fg { get; set; } = Palette.TextColor;
    public ICommand? Command { get; set; }
}
