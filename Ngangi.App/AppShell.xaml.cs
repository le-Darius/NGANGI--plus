using Microsoft.Extensions.DependencyInjection;
using Ngangi.App.ViewModels;
using Ngangi.App.Views;
using Ngangi.Core.Services;

namespace Ngangi.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Écrans ouverts par-dessus les pages principales (raccourcis, détails, profil)
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("addmember", typeof(AddMemberPage));
        Routing.RegisterRoute("loanrequest", typeof(LoanRequestPage));
        Routing.RegisterRoute("schedule", typeof(SchedulePage));
        Routing.RegisterRoute("fines", typeof(FinesPage));
        Routing.RegisterRoute("profile", typeof(ProfilePage));
        Routing.RegisterRoute("backup", typeof(BackupPage));
        Routing.RegisterRoute("about", typeof(AboutPage));

        Navigating += OnNavigating;
    }

    /// <summary>
    /// Garde-fou : sans session ouverte, aucun écran privé ne s'ouvre (règle testée : <see cref="RouteAccess"/>).
    /// Protège de tout écran resté dans une pile de navigation après une déconnexion. La navigation refusée est
    /// remplacée par un retour à la connexion.
    /// </summary>
    private void OnNavigating(object? sender, ShellNavigatingEventArgs e)
    {
        var service = IPlatformApplication.Current?.Services.GetService<NjangiService>();
        if (service == null) return;

        var signedIn = service.CurrentMember != null;
        var target = e.Target?.Location.OriginalString;
        if (RouteAccess.CanOpen(target, signedIn)) return;
        if (!e.CanCancel) return;

        e.Cancel();
        Dispatcher.Dispatch(async () => await Nav.GoToLoginAsync());
    }

    /// <summary>
    /// Touche Retour d'Android. Écrans empilés (profil, échéancier, amendes…) : Shell les referme un par un.
    /// Depuis Membres, Cotisations, Prêts ou Bilan : retour à l'Accueil. Depuis l'Accueil ou la Connexion : on quitte
    /// l'application (jamais de retour vers la connexion après une connexion réussie).
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        var onRootOfSection = Navigation.NavigationStack.Count <= 1 && Navigation.ModalStack.Count == 0;
        var onOtherTab = CurrentPage is MembersPage or ContributionsPage or LoansPage or ReportPage;

        if (onRootOfSection && onOtherTab)
        {
            Dispatcher.Dispatch(async () => await GoToAsync("//main/dashboard"));
            return true;
        }
        return base.OnBackButtonPressed();
    }
}
