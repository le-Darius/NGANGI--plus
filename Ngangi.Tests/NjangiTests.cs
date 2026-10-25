using Ngangi.Core.Models;
using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

public class LoanCalculatorTests
{
    [Fact]
    public void Quote_PretDeLaMaquette_80000_sur_4_mois()
    {
        var q = LoanCalculator.Quote(80_000, 4, 0.05m);
        Assert.Equal(16_000, q.Interest);
        Assert.Equal(96_000, q.Total);
        Assert.Equal(24_000, q.Monthly);
    }

    [Fact]
    public void Quote_PaulNkeng_100000_sur_4_mois()
    {
        var q = LoanCalculator.Quote(100_000, 4, 0.05m);
        Assert.Equal(20_000, q.Interest);
        Assert.Equal(120_000, q.Total);
        Assert.Equal(30_000, q.Monthly);
    }

    [Fact]
    public void Quote_AichaNjoya_60000_sur_3_mois()
    {
        var q = LoanCalculator.Quote(60_000, 3, 0.05m);
        Assert.Equal(9_000, q.Interest);
        Assert.Equal(69_000, q.Total);
        Assert.Equal(23_000, q.Monthly);
    }

    [Fact]
    public void Quote_MontantInvalide_Leve_Une_Exception()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(0, 4, 0.05m));
        Assert.Throws<ArgumentOutOfRangeException>(() => LoanCalculator.Quote(10_000, 0, 0.05m));
    }

    [Fact]
    public void Echeancier_La_Derniere_Mensualite_Absorbe_Larrondi()
    {
        var loan = new Loan { Id = 1, Principal = 10_000, DurationMonths = 3, MonthlyRate = 0.05m };
        var schedule = LoanCalculator.BuildSchedule(loan, new DateTime(2026, 11, 5));
        Assert.Equal(3, schedule.Count);
        Assert.Equal(3_833, schedule[0].Amount);
        Assert.Equal(3_834, schedule[2].Amount);
        Assert.Equal(11_500, schedule.Sum(i => i.Amount));
        Assert.Equal(1_500, schedule.Sum(i => i.InterestPart));
        Assert.Equal(10_000, schedule.Sum(i => i.PrincipalPart));
        Assert.Equal(new DateTime(2026, 12, 5), schedule[1].DueDate);
    }
}

public class ValidationTests
{
    [Fact]
    public void Telephone_Valide_Avec_Espaces_Et_Indicatif()
    {
        Assert.Equal("670123456", Validation.NormalizePhone("6 70 12 34 56"));
        Assert.Equal("677123456", Validation.NormalizePhone("+237 677 12 34 56"));
    }

    [Fact]
    public void Telephone_Trop_Court_Donne_Le_Message_Des_Maquettes()
    {
        Assert.Equal("Le numéro doit contenir 9 chiffres.", Validation.PhoneError("6 70 12 34"));
        Assert.Null(Validation.NormalizePhone("6 70 12 34"));
    }

    [Fact]
    public void Telephone_Doit_Commencer_Par_6()
    {
        Assert.Equal("Le numéro doit commencer par 6.", Validation.PhoneError("570123456"));
    }

    [Fact]
    public void MotDePasse_8_Caracteres_Minimum()
    {
        Assert.NotNull(Validation.PasswordError("abc"));
        Assert.Null(Validation.PasswordError("abcdefgh"));
    }

    [Fact]
    public void Formatage_Des_Montants_Et_Dates()
    {
        Assert.Equal("1 240 000 FCFA", Formatting.Fcfa(1_240_000));
        Assert.Equal("Dimanche 11 oct. 2026", Formatting.Long(new DateTime(2026, 10, 11)));
        Assert.Equal("05 oct. 2026", Formatting.DayMonthYear(new DateTime(2026, 10, 5)));
    }
}

public class NjangiServiceTests
{
    private static readonly DateTime DemoDay = new(2026, 10, 6);

    private static NjangiService Make(string? loginPhone = SeedData.DemoPresidentPhone)
    {
        var service = new NjangiService(SeedData.Create(), () => DemoDay);
        if (loginPhone != null) Assert.True(service.Login(loginPhone, SeedData.DemoPassword).Ok);
        return service;
    }

    [Fact]
    public void Tableau_De_Bord_Correspond_Aux_Maquettes()
    {
        var s = Make();
        s.RefreshFines();
        var d = s.GetDashboard();
        Assert.Equal(340_000, d.Cash);
        Assert.Equal(2, d.ActiveLoanCount);
        Assert.Equal(38_000, d.InterestReceived);
        Assert.Equal(3, d.PendingFineCount);
        Assert.Equal(3_000, d.PendingFineTotal);
        Assert.Equal("Cycle 2026 · mois 10 sur 12", d.CycleLabel);
        Assert.Equal(new DateTime(2026, 10, 11), d.NextSession!.Date);
        Assert.Equal("Esther Fotso", d.NextSession.Host);
        Assert.Equal(6, d.NextSessionPaid);
        Assert.Equal(8, d.MemberCount);
    }

    [Fact]
    public void Amendes_Automatiques_Sans_Doublon()
    {
        var s = Make();
        Assert.Equal(3, s.RefreshFines());
        Assert.Equal(0, s.RefreshFines());
        Assert.Equal(3_000, s.PendingFinesTotal());
        Assert.Equal(12_000, s.PaidFinesTotal());
    }

    [Fact]
    public void Statut_Des_Membres()
    {
        var s = Make();
        Assert.Equal(Standing.UpToDate, s.StandingOf(1));
        Assert.Equal(Standing.LoanLate, s.StandingOf(2));
        Assert.Equal(Standing.ContributionLate, s.StandingOf(4));
        Assert.Equal(Standing.ContributionLate, s.StandingOf(6));
        Assert.Equal(Standing.UpToDate, s.StandingOf(5));
    }

    [Fact]
    public void Echeance_En_Retard_De_Paul()
    {
        var s = Make();
        var inst = s.InstallmentsOf(2);
        Assert.Equal(InstallmentStatus.Paid, s.StatusOf(inst[0]));
        Assert.Equal(InstallmentStatus.Late, s.StatusOf(inst[2]));
        Assert.Equal(1, s.LateDays(inst[2]));
        Assert.Equal(InstallmentStatus.Upcoming, s.StatusOf(inst[3]));
        Assert.Equal(60_000, s.RepaidOf(2));
        Assert.Equal(120_000, s.TotalDueOf(2));
    }

    [Fact]
    public void Demande_De_Pret_Approuvee_Genere_Lecheancier()
    {
        var s = Make();
        var request = s.RequestLoan(3, 80_000, 4);
        Assert.True(request.Ok);
        var approve = s.ApproveLoan(request.Id!.Value);
        Assert.True(approve.Ok);
        var schedule = s.InstallmentsOf(request.Id.Value);
        Assert.Equal(4, schedule.Count);
        Assert.Equal(new DateTime(2026, 11, 6), schedule[0].DueDate);
        Assert.Equal(24_000, schedule[0].Amount);
        Assert.Equal(260_000, s.CashAvailable());
    }

    [Fact]
    public void Demande_De_Pret_Trop_Grande_Est_Refusee()
    {
        var s = Make();
        var r = s.RequestLoan(3, 400_000, 4);
        Assert.False(r.Ok);
        Assert.Contains("caisse", r.Message);
    }

    [Fact]
    public void Un_Membre_Avec_Un_Pret_Ou_Un_Retard_Ne_Peut_Pas_Emprunter()
    {
        var s = Make();
        Assert.Contains("déjà un prêt", s.RequestLoan(5, 50_000, 3).Message);
        Assert.Contains("retard", s.RequestLoan(4, 50_000, 3).Message);
    }

    [Fact]
    public void Un_Simple_Membre_Ne_Peut_Pas_Enregistrer_De_Paiement()
    {
        var s = Make(loginPhone: null);
        var reg = s.Register("Nadine Owona", "6 91 22 33 44", "motdepasse", "motdepasse", true);
        Assert.True(reg.Ok);
        var c = s.ContributionsOf(10).First(x => x.PaidOn == null);
        Assert.False(s.RecordContributionPayment(c.Id).Ok);
        Assert.False(s.RequestLoan(1, 50_000, 3).Ok);
    }

    [Fact]
    public void Paiement_Tardif_Dune_Mensualite_Garde_Lamende()
    {
        var s = Make();
        s.RefreshFines();
        var pay = s.RecordNextInstallmentPayment(2);
        Assert.True(pay.Ok);
        Assert.Equal(90_000, s.RepaidOf(2));
        Assert.Equal(3, s.PendingFines().Count);
    }

    [Fact]
    public void Dernier_Paiement_Solde_Le_Pret()
    {
        var s = Make();
        for (int i = 0; i < 2; i++) Assert.True(s.RecordNextInstallmentPayment(3).Ok);
        Assert.Equal(LoanStatus.Closed, s.Data.Loans.First(l => l.Id == 3).Status);
        Assert.False(s.RecordNextInstallmentPayment(3).Ok);
    }

    [Fact]
    public void Paiement_De_Cotisation_Retard_Cree_Pas_Un_Second_Doublon()
    {
        var s = Make();
        s.RefreshFines();
        var late = s.ContributionsOf(9).First(c => c.MemberId == 4);
        Assert.True(s.RecordContributionPayment(late.Id).Ok);
        Assert.Equal(3, s.PendingFines().Count);
        Assert.False(s.RecordContributionPayment(late.Id).Ok);
    }

    [Fact]
    public void Seule_La_Presidente_Annule_Une_Amende()
    {
        var treasurer = Make(SeedData.DemoTreasurerPhone);
        treasurer.RefreshFines();
        var fine = treasurer.PendingFines().First();
        Assert.False(treasurer.CancelFine(fine.Id).Ok);
        Assert.True(treasurer.PayFine(fine.Id).Ok);

        var president = Make();
        president.RefreshFines();
        var other = president.PendingFines().First();
        Assert.True(president.CancelFine(other.Id).Ok);
        Assert.Equal(2_000, president.PendingFinesTotal());
    }

    [Fact]
    public void Ajout_Dun_Membre_Cree_Ses_Cotisations_A_Venir()
    {
        var s = Make();
        var r = s.AddMember("Clarisse Ngo", "6 55 12 12 12");
        Assert.True(r.Ok);
        Assert.Equal(3, s.Data.Contributions.Count(c => c.MemberId == r.Id));
        Assert.False(s.AddMember("Clarisse Ngo", "655121212").Ok);
    }

    [Fact]
    public void Bilan_Du_Cycle_Somme_Des_Parts_Egale_Au_Total()
    {
        var s = Make();
        var r = s.BuildReport(includePlanned: false);
        Assert.Equal(380_000, r.Contributions);
        Assert.Equal(38_000, r.Interest);
        Assert.Equal(12_000, r.Fines);
        Assert.Equal(430_000, r.TotalToShare);
        Assert.Equal(r.TotalToShare, r.Shares.Sum(x => x.Total));
        Assert.Equal(6_250, r.Shares[0].Gains);
    }

    [Fact]
    public void Bilan_Projection_Et_Reste_De_Larrondi()
    {
        var s = Make();
        var p = s.BuildReport(includePlanned: true);
        Assert.True(p.IsProjection);
        Assert.Equal(480_000, p.Contributions);
        Assert.Equal(54_000, p.Interest);

        s.Data.Fines.Add(new Fine { Id = 99, MemberId = 1, Amount = 5, IssuedOn = DemoDay, PaidOn = DemoDay });
        var r = s.BuildReport(includePlanned: false);
        Assert.Equal(r.TotalToShare, r.Shares.Sum(x => x.Total));
    }

    [Fact]
    public void Inscription_Et_Connexion()
    {
        var s = Make(loginPhone: null);
        Assert.False(s.Register("Nadine Owona", "6 91 22 33", "motdepasse", "motdepasse", true).Ok);
        Assert.False(s.Register("Nadine Owona", "691223344", "motdepasse", "autrechose", true).Ok);
        Assert.False(s.Register("Nadine Owona", "691223344", "motdepasse", "motdepasse", false).Ok);
        Assert.True(s.Register("Nadine Owona", "691223344", "motdepasse", "motdepasse", true).Ok);
        Assert.False(s.Register("Nadine Owona", "691223344", "motdepasse", "motdepasse", true).Ok);
        s.Logout();
        Assert.False(s.Login("691223344", "mauvais").Ok);
        Assert.True(s.Login("691223344", "motdepasse").Ok);
        Assert.Equal("Nadine Owona", s.CurrentMember!.FullName);
    }

    [Fact]
    public void Reinitialisation_Restaure_La_Demo_Et_Deconnecte()
    {
        var s = Make();
        s.Data.Members.Clear();
        s.ReplaceData(SeedData.Create());
        Assert.Null(s.CurrentUser);
        Assert.Equal(8, s.Data.Members.Count);
        Assert.Equal(340_000, s.CashAvailable());
    }

    [Fact]
    public void Sauvegarde_Et_Rechargement_Du_Fichier_Json()
    {
        var path = Path.Combine(Path.GetTempPath(), "ngangi-test-" + Guid.NewGuid() + ".json");
        try
        {
            var store = new JsonDataStore(path);
            Assert.Null(store.Load());
            store.Save(SeedData.Create());
            var loaded = store.Load();
            Assert.NotNull(loaded);
            Assert.Equal(8, loaded!.Members.Count);
            Assert.Equal(Role.Tresorier, loaded.Members[1].Role);
            Assert.Equal(3, loaded.Loans.Count);
            var s = new NjangiService(loaded, () => DemoDay);
            Assert.Equal(340_000, s.CashAvailable());
        }
        finally { File.Delete(path); }
    }
}
