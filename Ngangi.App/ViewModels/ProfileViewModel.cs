using System.Windows.Input;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

/// <summary>Écran Profil et réglages : compte, thème, déconnexion, sauvegarde, confidentialité et conditions.</summary>
public class ProfileViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _initials = "?", _fullName = "", _phone = "", _role = "", _group = "";

    public ProfileViewModel(NjangiService service)
    {
        _s = service;
        BackCommand = new Command(async () => await Nav.Back());
        SetThemeCommand = new Command<string>(SetTheme);
        LogoutCommand = new Command(async () => await LogoutAsync());
        AboutCommand = new Command(async () => await Nav.Go("about"));
        // Sauvegarde branchée ; Confidentialité et Conditions : pages à venir.
        BackupCommand = new Command(async () => await Nav.Go("backup"));
        PrivacyCommand = new Command(async () => await ComingSoonAsync());
        TermsCommand = new Command(async () => await ComingSoonAsync());
    }

    public ICommand BackCommand { get; }
    public ICommand SetThemeCommand { get; }
    public ICommand LogoutCommand { get; }
    public ICommand AboutCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand PrivacyCommand { get; }
    public ICommand TermsCommand { get; }

    public string Initials { get => _initials; private set => Set(ref _initials, value); }
    public string FullName { get => _fullName; private set => Set(ref _fullName, value); }
    public string Phone { get => _phone; private set => Set(ref _phone, value); }
    public string Role { get => _role; private set => Set(ref _role, value); }
    public string Group { get => _group; private set => Set(ref _group, value); }

    public bool IsThemeAuto => ThemeService.Current == ThemeChoice.Auto;
    public bool IsThemeLight => ThemeService.Current == ThemeChoice.Light;
    public bool IsThemeDark => ThemeService.Current == ThemeChoice.Dark;

    public void Refresh()
    {
        var m = _s.CurrentMember;
        Initials = m?.Initials ?? "?";
        FullName = m?.FullName ?? "";
        Phone = m?.Phone ?? "";
        Role = m?.RoleLabel ?? "";
        Group = _s.Data.Group.Name;
        Raise(nameof(IsThemeAuto), nameof(IsThemeLight), nameof(IsThemeDark));
    }

    private void SetTheme(string? name)
    {
        var choice = name switch
        {
            "light" => ThemeChoice.Light,
            "dark" => ThemeChoice.Dark,
            _ => ThemeChoice.Auto,
        };
        ThemeService.Set(choice);
        Raise(nameof(IsThemeAuto), nameof(IsThemeLight), nameof(IsThemeDark));
    }

    private async Task LogoutAsync()
    {
        if (!await Nav.Confirm(AppTexts.LogoutConfirmTitle, AppTexts.LogoutConfirmBody,
                               AppTexts.LogoutConfirmAccept, AppTexts.Cancel)) return;

        // 1. Refermer le profil (et tout écran empilé) tant que la session est ouverte ;
        // 2. fermer la session ; 3. revenir à la connexion. La touche Retour n'a alors plus d'écran privé à rouvrir.
        await Nav.PopToRootAsync();
        _s.Logout();
        await Nav.GoToLoginAsync();
    }

    private static Task ComingSoonAsync() => Nav.Alert(AppTexts.ComingSoonTitle, AppTexts.ComingSoonBody);
}
