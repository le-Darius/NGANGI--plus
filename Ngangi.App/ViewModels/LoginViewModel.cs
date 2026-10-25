using System.Windows.Input;
using Ngangi.Core.Services;

namespace Ngangi.App.ViewModels;

public class LoginViewModel : BaseViewModel
{
    private readonly NjangiService _s;
    private string _phone = "";
    private string _password = "";
    private string _error = "";

    public LoginViewModel(NjangiService service)
    {
        _s = service;
        LoginCommand = new Command(async () => await LoginAsync());
        RegisterCommand = new Command(async () => await Nav.Go("register"));
#if DEBUG
        ResetDemoCommand = new Command(async () => await ResetDemoAsync());
#endif
    }

    public string Phone { get => _phone; set => Set(ref _phone, value); }
    public string Password { get => _password; set => Set(ref _password, value); }

    public string Error
    {
        get => _error;
        private set { if (Set(ref _error, value)) OnPropertyChanged(nameof(HasError)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_error);

    /// <summary>Vrai tant qu'aucun njangi n'est enregistré sur ce téléphone : l'écran invite à créer le compte du responsable.</summary>
    public bool IsFirstUse => _s.IsEmpty;

    public ICommand LoginCommand { get; }
    public ICommand RegisterCommand { get; }

    public void Refresh()
    {
        _s.Logout();
        Password = "";
        Error = "";
        OnPropertyChanged(nameof(IsFirstUse));
    }

    private async Task LoginAsync()
    {
        var result = _s.Login(Phone, Password);
        if (!result.Ok)
        {
            Error = result.Message;
            return;
        }
        Error = "";
        Password = "";
        await Nav.GoToMainAsync();
    }

    // ------------------------------------------------------------------------------------------------
    // Outils de développement : tout ce qui suit n'existe qu'en configuration Debug.
    // En Release, le panneau reste invisible et ne contient aucun texte ni identifiant.
    // ------------------------------------------------------------------------------------------------
#if DEBUG
    public bool ShowDebugPanel => true;

    public string DebugInfo =>
        $"Debug · comptes : {SeedData.DemoPresidentPhone} (président), {SeedData.DemoTreasurerPhone} (trésorier)";

    public string DebugResetText => AppTexts.DebugResetButton;

    public ICommand? ResetDemoCommand { get; }

    private async Task ResetDemoAsync()
    {
        if (!await Nav.Confirm(AppTexts.DebugResetConfirmTitle, AppTexts.DebugResetConfirmBody)) return;
        _s.ReplaceData(SeedData.Create());
        Error = "";
        OnPropertyChanged(nameof(IsFirstUse));
        await Nav.Alert(AppTexts.DebugResetDoneTitle, AppTexts.DebugResetDoneBody);
    }
#else
    public bool ShowDebugPanel => false;
    public string DebugInfo => "";
    public string DebugResetText => "";
    public ICommand? ResetDemoCommand => null;
#endif
}
