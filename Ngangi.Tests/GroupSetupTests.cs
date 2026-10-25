using Ngangi.Core.Models;
using Ngangi.Core.Services;
using Xunit;

namespace Ngangi.Tests;

public class GroupSetupTests
{
    private static NjangiService Founder(DateTime? date = null)
    {
        var service = new NjangiService(new NjangiData(), () => date ?? new DateTime(2026, 1, 1));
        Assert.True(service.Register("Mireille Tchamba", "670123456", "motdepasse", "motdepasse", true).Ok);
        return service;
    }

    private static OperationResult Configure(NjangiService s, DateTime start, DateTime end, int day = 31,
        long contribution = 10000, decimal rate = .05m, long fine = 1000) =>
        s.ConfigureGroup("Njangi du quartier", start, end, contribution, rate, .5m, fine, day, "15 h", "Mireille Tchamba");

    [Fact]
    public void Configure_Groupe_Genere_Une_Seance_Par_Mois_Et_Cotisations()
    {
        var s = Founder();
        var result = Configure(s, new DateTime(2026, 1, 1), new DateTime(2026, 4, 30));
        Assert.True(result.Ok, result.Message);
        Assert.Equal("Njangi du quartier", s.Data.Group.Name);
        Assert.Equal(new[] { 31, 28, 31, 30 }, s.Data.Sessions.Select(x => x.Date.Day));
        Assert.Equal(4, s.Data.Sessions.Count);
        Assert.Equal(4, s.Data.Contributions.Count);
        Assert.All(s.Data.Sessions, x => Assert.Equal("Mireille Tchamba", x.Host));
    }

    [Fact]
    public void Calendrier_Respecte_Fin_De_Cycle_Meme_Si_Mois_Suivant_Existerait()
    {
        var s = Founder();
        Assert.True(Configure(s, new DateTime(2026, 10, 15), new DateTime(2026, 12, 20), 31).Ok);
        Assert.Equal(new[] { new DateTime(2026, 10, 31), new DateTime(2026, 11, 30) }, s.Data.Sessions.Select(x => x.Date).ToArray());
        Assert.DoesNotContain(s.Data.Sessions, x => x.Date > s.Data.Group.CycleEnd);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100000001)]
    public void Configuration_Refuse_Cotisation_Hors_Bornes(long amount)
    {
        var s = Founder();
        Assert.False(Configure(s, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), contribution: amount).Ok);
        Assert.Empty(s.Data.Sessions);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.31)]
    public void Configuration_Refuse_Taux_Hors_Bornes(decimal rate)
    {
        var s = Founder();
        Assert.False(Configure(s, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), rate: rate).Ok);
        Assert.Empty(s.Data.Sessions);
    }

    [Fact]
    public void Calendrier_Refuse_Jour_Invalide()
    {
        var s = Founder();
        Assert.False(Configure(s, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), day: 32).Ok);
        Assert.Empty(s.Data.Sessions);
    }

    [Fact]
    public void Reglages_Ne_Modifient_Pas_Le_Taux_Des_Prets_Existants()
    {
        var s = Founder();
        var loan = new Loan { Id = 1, MemberId = s.CurrentMember!.Id, Principal = 10000, DurationMonths = 2, MonthlyRate = .05m };
        s.Data.Loans.Add(loan);
        Assert.True(Configure(s, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), rate: .1m).Ok);
        Assert.Equal(.05m, loan.MonthlyRate);
        Assert.Equal(.1m, s.Data.Group.MonthlyRate);
    }
}
