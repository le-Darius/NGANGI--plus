using Ngangi.Core.Models;
using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

/// <summary>
/// Démarrage « comme une vraie application » : aucune donnée, aucun groupe, aucun compte.
/// Ces tests n'utilisent pas SeedData (sauf un contrôle de non-régression en fin de fichier).
/// </summary>
public class EmptyStartTests : IDisposable
{
    private static readonly DateTime Day = new(2026, 10, 25);
    private const string Phone = "6 70 12 34 56";
    private const string Password = "motdepasse";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ngangi-empty-" + Guid.NewGuid());
    private string FilePath => Path.Combine(_dir, "ngangi-data.json");

    public EmptyStartTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }

    private static NjangiService Empty() => new(new NjangiData(), () => Day);

    /// <summary>Installation vide, puis première inscription (le responsable devient président).</summary>
    private static NjangiService Founded()
    {
        var s = Empty();
        Assert.True(s.Register("Mireille Tchamba", Phone, Password, Password, true).Ok);
        return s;
    }

    // ------------------------------------------------------------ Base vide

    [Fact]
    public void Une_Base_Vide_Ne_Contient_Aucune_Donnee()
    {
        var d = new NjangiData();
        Assert.Empty(d.Members);
        Assert.Empty(d.Sessions);
        Assert.Empty(d.Contributions);
        Assert.Empty(d.Loans);
        Assert.Empty(d.Installments);
        Assert.Empty(d.Fines);
        Assert.Empty(d.Accounts);
        Assert.Empty(DataValidator.Validate(d));

        var s = new NjangiService(d, () => Day);
        Assert.True(s.IsEmpty);
        Assert.Null(s.CurrentUser);
        Assert.Null(s.CurrentMember);
        Assert.False(s.CanManage);
    }

    [Fact]
    public void Sans_Horloge_Fournie_Le_Service_Utilise_La_Vraie_Date()
    {
        var s = new NjangiService(new NjangiData());
        Assert.Equal(DateTime.Today, s.Today);
    }

    [Fact]
    public void Le_Tableau_De_Bord_Fonctionne_Sans_Aucune_Donnee()
    {
        var s = Empty();
        var d = s.GetDashboard();
        Assert.Equal(CoreTexts.NoGroupName, d.GroupName);
        Assert.Equal(CoreTexts.NoCycleLabel, d.CycleLabel);
        Assert.Equal(0, d.Cash);
        Assert.Equal(0, d.ActiveLoanCount);
        Assert.Equal(0, d.InterestReceived);
        Assert.Equal(0, d.PendingFineCount);
        Assert.Equal(0, d.PendingFineTotal);
        Assert.Null(d.NextSession);
        Assert.Equal(0, d.NextSessionPaid);
        Assert.Equal(0, d.MemberCount);
    }

    [Fact]
    public void Le_Tableau_De_Bord_Apres_Inscription_Est_Vide_Mais_Coherent()
    {
        var d = Founded().GetDashboard();
        Assert.Equal(CoreTexts.DefaultGroupName, d.GroupName);
        Assert.Equal("Cycle 2026 · mois 10 sur 12", d.CycleLabel);
        Assert.Equal(0, d.Cash);
        Assert.Equal(0, d.ActiveLoanCount);
        Assert.Null(d.NextSession);
        Assert.Equal(1, d.MemberCount);
    }

    [Fact]
    public void Toutes_Les_Lectures_Sont_Sures_Sans_Donnees()
    {
        var s = Empty();
        Assert.Null(s.NextSession());
        Assert.Empty(s.ContributionsOf(1));
        Assert.Empty(s.ActiveLoans());
        Assert.Empty(s.InstallmentsOf(1));
        Assert.Empty(s.PendingFines());
        Assert.Equal(0, s.CashAvailable());
        Assert.Equal(0, s.InterestReceived());
        Assert.Equal(0, s.PendingFinesTotal());
        Assert.Equal(0, s.PaidFinesTotal());
        Assert.Equal(0, s.RepaidOf(1));
        Assert.Equal(0, s.TotalDueOf(1));
        Assert.Equal(0, s.RefreshFines());
        Assert.Equal(Standing.UpToDate, s.StandingOf(1));
        Assert.Null(s.Member(1));
        Assert.False(s.HasLoanableCash());
        Assert.NotEmpty(s.ValidateLoanRequest(1, 50_000, 3));
    }

    [Fact]
    public void Le_Bilan_Est_Vide_Sans_Membre_Et_Ne_Divise_Pas_Par_Zero()
    {
        var s = Empty();
        foreach (var projection in new[] { false, true })
        {
            var r = s.BuildReport(projection);
            Assert.Empty(r.Shares);
            Assert.Equal(0, r.Contributions);
            Assert.Equal(0, r.Interest);
            Assert.Equal(0, r.Fines);
            Assert.Equal(0, r.TotalToShare);
            Assert.False(string.IsNullOrWhiteSpace(s.BuildReportText(projection)));
        }
    }

    [Fact]
    public void Le_Bilan_Avec_Un_Membre_Et_Sans_Seance_Vaut_Zero()
    {
        var r = Founded().BuildReport(includePlanned: true);
        Assert.Single(r.Shares);
        Assert.Equal(0, r.TotalToShare);
        Assert.Equal(0, r.Shares[0].Total);
    }

    // ------------------------------------------------------------ Inscription

    [Fact]
    public void Inscription_Sur_Base_Vide_Cree_Le_Groupe_Et_Le_President()
    {
        var s = Empty();
        var changes = 0;
        s.Changed += () => changes++;

        var r = s.Register("  Mireille   Tchamba ", Phone, Password, Password, true);

        Assert.True(r.Ok);
        Assert.Equal(1, changes);                       // une seule sauvegarde
        Assert.False(s.IsEmpty);

        var g = s.Data.Group;
        Assert.Equal(CoreTexts.DefaultGroupName, g.Name);
        Assert.Equal(2026, g.CycleYear);
        Assert.Equal(new DateTime(2026, 1, 1), g.CycleStart);
        Assert.Equal(new DateTime(2026, 12, 31), g.CycleEnd);

        var m = Assert.Single(s.Data.Members);
        Assert.Equal("Mireille Tchamba", m.FullName);
        Assert.Equal("670123456", m.Phone);
        Assert.Equal(Role.President, m.Role);
        Assert.Equal(m.Id, r.Id!.Value);

        var a = Assert.Single(s.Data.Accounts);
        Assert.Equal(m.Id, a.MemberId);
        Assert.Equal(m.Id, s.CurrentMember!.Id);
        Assert.True(s.IsPresident);
        Assert.True(s.CanManage);
        Assert.Empty(s.Data.Sessions);
        Assert.Empty(s.Data.Contributions);
    }

    [Fact]
    public void Les_Reglages_Du_Groupe_Cree_Respectent_Les_Bornes()
    {
        var g = Founded().Data.Group;
        Assert.Null(Validation.AmountError(g.ContributionAmount));
        Assert.Null(Validation.AmountError(g.LateContributionFine));
        Assert.Null(Validation.AmountError(g.LateRepaymentFine));
        Assert.Null(Validation.RateError(g.MonthlyRate));
        Assert.InRange(g.LoanFundShare, 0m, 1m);
        Assert.Equal(string.Empty, g.InvitationCode);   // plus de code d'invitation inventé
    }

    [Fact]
    public void Inscription_Invalide_Sur_Base_Vide_Ne_Cree_Rien()
    {
        var s = Empty();
        Assert.False(s.Register("Mi", Phone, Password, Password, true).Ok);                  // nom trop court
        Assert.False(s.Register("Mireille Tchamba", "6 70 12", Password, Password, true).Ok); // téléphone invalide
        Assert.False(s.Register("Mireille Tchamba", Phone, "court", "court", true).Ok);       // mot de passe trop court
        Assert.False(s.Register("Mireille Tchamba", Phone, Password, "autre-chose", true).Ok);
        Assert.False(s.Register("Mireille Tchamba", Phone, Password, Password, false).Ok);   // conditions refusées

        Assert.True(s.IsEmpty);
        Assert.Empty(s.Data.Members);
        Assert.Empty(s.Data.Accounts);
        Assert.Equal(string.Empty, s.Data.Group.Name);
        Assert.Null(s.CurrentUser);
    }

    [Fact]
    public void Une_Seconde_Inscription_Ne_Cree_Pas_Un_Second_Groupe()
    {
        var s = Founded();
        s.Logout();
        Assert.True(s.Register("Nadine Owona", "6 91 22 33 44", Password, Password, true).Ok);

        Assert.Equal(CoreTexts.DefaultGroupName, s.Data.Group.Name);
        Assert.Equal(2, s.Data.Members.Count);
        Assert.Equal(Role.Membre, s.CurrentMember!.Role);
        Assert.False(s.CanManage);
    }

    // ------------------------------------------------------------ Connexion

    [Fact]
    public void Connexion_Sur_Base_Vide_Est_Refusee_Sans_Exception()
    {
        var s = Empty();
        var r = s.Login(Phone, Password);
        Assert.False(r.Ok);
        Assert.Equal("Numéro ou mot de passe incorrect.", r.Message);
        Assert.Null(s.CurrentUser);
        Assert.False(s.Login("pas un numéro", "").Ok);
        Assert.False(s.Login("", "").Ok);
    }

    [Fact]
    public void Connexion_Apres_Inscription()
    {
        var s = Founded();
        s.Logout();
        Assert.Null(s.CurrentUser);

        Assert.False(s.Login(Phone, "mauvais-mot-de-passe").Ok);
        Assert.True(s.Login("+237 670 12 34 56", Password).Ok);
        Assert.Equal("Mireille Tchamba", s.CurrentMember!.FullName);
        Assert.True(s.IsPresident);
    }

    // ------------------------------------------------------------ Autres actions sur base neuve

    [Fact]
    public void Le_President_Peut_Ajouter_Un_Membre_Meme_Sans_Seance()
    {
        var s = Founded();
        var r = s.AddMember("Paul Nkeng", "6 77 12 34 56");
        Assert.True(r.Ok);
        Assert.Equal(2, s.Data.Members.Count);
        Assert.Empty(s.Data.Contributions);             // aucune séance : aucune cotisation à créer
        Assert.Equal(Standing.UpToDate, s.StandingOf(r.Id!.Value));
    }

    [Fact]
    public void Ajout_Membre_Respecte_Le_Role_Demande_Et_Les_Droits()
    {
        var s = Founded();
        var added = s.AddMember("Paul Nkeng", "6 77 12 34 56", Role.Tresorier);
        Assert.True(added.Ok);
        Assert.Equal(Role.Tresorier, s.Member(added.Id!.Value)!.Role);

        var second = s.AddMember("Clarisse Ngo", "6 55 12 12 12", Role.President);
        Assert.False(second.Ok);
        Assert.Equal(2, s.Data.Members.Count);
    }

    [Fact]
    public void Aucun_Pret_Possible_Tant_Que_La_Caisse_Est_Vide()
    {
        var s = Founded();
        Assert.False(s.HasLoanableCash());
        var r = s.RequestLoan(s.CurrentMember!.Id, Limits.MinLoan, 3);
        Assert.False(r.Ok);
        Assert.Contains("caisse", r.Message);
        Assert.Empty(s.Data.Loans);
    }

    [Fact]
    public void Aucune_Amende_Ni_Retard_Sur_Base_Neuve()
    {
        var s = Founded();
        Assert.Equal(0, s.RefreshFines());
        Assert.Empty(s.PendingFines());
        Assert.Equal(Standing.UpToDate, s.StandingOf(s.CurrentMember!.Id));
    }

    // ------------------------------------------------------------ Sauvegarde

    [Fact]
    public void Une_Base_Neuve_Se_Sauvegarde_Et_Se_Recharge()
    {
        var store = new JsonDataStore(FilePath);
        Assert.Null(store.Load());                       // aucun fichier : le premier lancement ne crée rien

        store.Save(Founded().Data);
        var loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(CoreTexts.DefaultGroupName, loaded!.Group.Name);
        Assert.Single(loaded.Members);
        Assert.Single(loaded.Accounts);

        var s = new NjangiService(loaded, () => Day);
        Assert.True(s.Login(Phone, Password).Ok);
        Assert.True(s.IsPresident);
    }

    [Fact]
    public void Une_Base_Neuve_Se_Sauvegarde_Et_Se_Recharge_Chiffree()
    {
        AesGcmCipher Cipher() => new(new FixedKeyProvider(5));
        var store = new JsonDataStore(FilePath, Cipher());
        store.Save(Founded().Data);

        var loaded = new JsonDataStore(FilePath, Cipher()).Load();
        Assert.NotNull(loaded);
        Assert.Single(loaded!.Members);
        Assert.Null(store.LastProblem);
    }

    [Fact]
    public void Une_Base_Entierement_Vide_Est_Valide_Pour_Le_Fichier()
    {
        var store = new JsonDataStore(FilePath);
        store.Save(new NjangiData());
        Assert.NotNull(store.Load());
        Assert.Null(store.LastProblem);
    }

    // ------------------------------------------------------------ Non-régression

    [Fact]
    public void Les_Donnees_De_Demonstration_Restent_Utilisables_Dans_Les_Tests()
    {
        var s = new NjangiService(SeedData.Create(), () => new DateTime(2026, 10, 6));
        Assert.False(s.IsEmpty);
        Assert.True(s.HasLoanableCash());
        Assert.Equal(340_000, s.CashAvailable());
        Assert.Equal(8, s.GetDashboard().MemberCount);
    }
}
