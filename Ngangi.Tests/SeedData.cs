using Ngangi.Core.Models;

namespace Ngangi.Core.Services;

/// <summary>
/// Données de démonstration : les mêmes que les maquettes Figma (au 06/10/2026).
/// Ce fichier vit dans Ngangi.Tests : il sert aux tests, et l'application ne l'embarque qu'en configuration Debug
/// (voir Ngangi.App.csproj). Il n'existe donc pas dans une version Release.
/// </summary>
public static class SeedData
{
    public const string DemoPassword = "njangi2026";
    public const string DemoPresidentPhone = "699041122";
    public const string DemoTreasurerPhone = "677123456";

    public static NjangiData Create()
    {
        var d = new NjangiData
        {
            Group = new Group
            {
                Name = "Njangi Les Amis de Bonamoussadi",
                CycleYear = 2026,
                CycleStart = new DateTime(2026, 1, 1),
                CycleEnd = new DateTime(2026, 12, 31),
                InvitationCode = "NJ-4821"
            }
        };

        (int id, string name, string phone, Role role)[] members =
        {
            (1, "Mireille Tchamba", "699041122", Role.President),
            (2, "Paul Nkeng", "677123456", Role.Tresorier),
            (3, "Esther Fotso", "655872003", Role.Secretaire),
            (4, "Rodrigue Mbarga", "671339048", Role.Membre),
            (5, "Aïcha Njoya", "690551234", Role.Membre),
            (6, "Blaise Kamga", "655409876", Role.Membre),
            (7, "Sandrine Etoa", "699778811", Role.Membre),
            (8, "Joël Biyong", "677204455", Role.Membre),
        };
        foreach (var m in members)
            d.Members.Add(new Member { Id = m.id, FullName = m.name, Phone = m.phone, Role = m.role });

        (int id, DateTime date, string host)[] sessions =
        {
            (1, new DateTime(2026, 1, 11), "Mireille Tchamba"),
            (2, new DateTime(2026, 2, 8), "Paul Nkeng"),
            (3, new DateTime(2026, 3, 8), "Aïcha Njoya"),
            (4, new DateTime(2026, 4, 12), "Blaise Kamga"),
            (5, new DateTime(2026, 5, 10), "Rodrigue Mbarga"),
            (6, new DateTime(2026, 6, 14), "Sandrine Etoa"),
            (7, new DateTime(2026, 7, 12), "Joël Biyong"),
            (8, new DateTime(2026, 8, 9), "Mireille Tchamba"),
            (9, new DateTime(2026, 9, 13), "Paul Nkeng"),
            (10, new DateTime(2026, 10, 11), "Esther Fotso"),
            (11, new DateTime(2026, 11, 8), "Aïcha Njoya"),
            (12, new DateTime(2026, 12, 13), "Blaise Kamga"),
        };
        foreach (var s in sessions)
            d.Sessions.Add(new MeetingSession { Id = s.id, Date = s.date, Host = s.host });

        // Cotisations : tout est payé jusqu'en août ; en septembre, Rodrigue (4) et Blaise (6) sont en retard ;
        // pour la séance d'octobre, 6 membres sur 8 ont déjà payé.
        int contribId = 1;
        var octoberPayers = new[] { 1, 2, 3, 5, 7, 8 };
        foreach (var s in d.Sessions)
        {
            foreach (var m in d.Members)
            {
                DateTime? paid = null;
                if (s.Id <= 8) paid = s.Date;
                else if (s.Id == 9 && m.Id != 4 && m.Id != 6) paid = s.Date;
                else if (s.Id == 10 && octoberPayers.Contains(m.Id)) paid = new DateTime(2026, 10, 4);
                d.Contributions.Add(new Contribution
                {
                    Id = contribId++, SessionId = s.Id, MemberId = m.Id,
                    Amount = d.Group.ContributionAmount, PaidOn = paid
                });
            }
        }

        // Amendes déjà encaissées (archives) : 12 x 1 000 FCFA.
        for (int i = 0; i < 12; i++)
        {
            var issued = d.Sessions[i % 8].Date.AddDays(1);
            d.Fines.Add(new Fine
            {
                Id = i + 1, MemberId = i % 2 == 0 ? 4 : 6, Reason = FineReason.Contribution, SourceId = 0,
                Amount = 1_000, IssuedOn = issued, PaidOn = issued.AddDays(6)
            });
        }

        // Prêts : un prêt soldé (Sandrine), un prêt en retard (Paul), un prêt à jour (Aïcha).
        AddLoan(d, 1, 7, 125_000, 4, new DateTime(2026, 2, 8), new DateTime(2026, 2, 15), LoanStatus.Closed, paidCount: 4);
        AddLoan(d, 2, 2, 100_000, 4, new DateTime(2026, 7, 1), new DateTime(2026, 7, 5), LoanStatus.Approved, paidCount: 2);
        AddLoan(d, 3, 5, 60_000, 3, new DateTime(2026, 8, 16), new DateTime(2026, 8, 20), LoanStatus.Approved, paidCount: 1);

        // Comptes de démonstration (mot de passe : njangi2026).
        // Un seul calcul PBKDF2 (600 000 itérations) partagé par les deux comptes de démonstration.
        var (hash, salt) = Security.HashPassword(DemoPassword);
        foreach (var memberId in new[] { 1, 2 })
        {
            var m = d.Members.First(x => x.Id == memberId);
            d.Accounts.Add(new UserAccount { Phone = m.Phone, FullName = m.FullName, PasswordHash = hash, Salt = salt, MemberId = m.Id });
        }
        return d;
    }

    private static void AddLoan(NjangiData d, int id, int memberId, long principal, int months,
        DateTime requested, DateTime approved, LoanStatus status, int paidCount)
    {
        var loan = new Loan
        {
            Id = id, MemberId = memberId, Principal = principal, DurationMonths = months,
            MonthlyRate = d.Group.MonthlyRate, RequestedOn = requested, ApprovedOn = approved,
            FirstDueDate = approved.AddMonths(1), Status = status
        };
        d.Loans.Add(loan);
        int nextId = d.Installments.Count == 0 ? 1 : d.Installments.Max(i => i.Id) + 1;
        foreach (var inst in LoanCalculator.BuildSchedule(loan, loan.FirstDueDate.Value))
        {
            inst.Id = nextId++;
            if (inst.Number <= paidCount) inst.PaidOn = inst.DueDate;
            d.Installments.Add(inst);
        }
    }
}
