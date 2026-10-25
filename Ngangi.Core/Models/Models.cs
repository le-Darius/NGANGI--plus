using System.Text.Json.Serialization;

namespace Ngangi.Core.Models;

public enum Role { Membre = 0, Secretaire = 1, Tresorier = 2, President = 3 }

public enum LoanStatus { Pending = 0, Approved = 1, Rejected = 2, Closed = 3 }

public enum FineReason { Contribution = 0, Installment = 1 }

public enum InstallmentStatus { Paid, Upcoming, Late }

public enum ContributionStatus { Paid, Upcoming, Late }

public enum Standing { UpToDate, ContributionLate, LoanLate }

/// <summary>Paramètres du njangi. Les montants sont en FCFA (nombres entiers).</summary>
public class Group
{
    public string Name { get; set; } = "";
    public int CycleYear { get; set; }
    public DateTime CycleStart { get; set; }
    public DateTime CycleEnd { get; set; }
    public string InvitationCode { get; set; } = "";

    /// <summary>Cotisation par membre et par séance.</summary>
    public long ContributionAmount { get; set; } = 10_000;

    /// <summary>Part de chaque cotisation versée à la caisse de prêts (0.5 = 50 %).</summary>
    public decimal LoanFundShare { get; set; } = 0.5m;

    /// <summary>Taux d'intérêt simple par mois (0.05 = 5 % par mois).</summary>
    public decimal MonthlyRate { get; set; } = 0.05m;

    public long LateContributionFine { get; set; } = 1_000;
    public long LateRepaymentFine { get; set; } = 1_000;
}

public class Member
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public Role Role { get; set; } = Role.Membre;

    [JsonIgnore]
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return (parts[0][..1] + parts[1][..1]).ToUpperInvariant();
        }
    }

    [JsonIgnore]
    public string RoleLabel => Role switch
    {
        Role.President => "Président(e)",
        Role.Tresorier => "Trésorier",
        Role.Secretaire => "Secrétaire",
        _ => "Membre"
    };
}

public class MeetingSession
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public string Host { get; set; } = "";
    public string TimeText { get; set; } = "15 h";
}

public class Contribution
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public int MemberId { get; set; }
    public long Amount { get; set; }
    public DateTime? PaidOn { get; set; }
}

public class Loan
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public long Principal { get; set; }
    public int DurationMonths { get; set; }
    public decimal MonthlyRate { get; set; }
    public DateTime RequestedOn { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public DateTime? FirstDueDate { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Pending;
}

public class Installment
{
    public int Id { get; set; }
    public int LoanId { get; set; }
    public int Number { get; set; }
    public DateTime DueDate { get; set; }
    public long Amount { get; set; }
    public long InterestPart { get; set; }
    public long PrincipalPart { get; set; }
    public DateTime? PaidOn { get; set; }
}

public class Fine
{
    public int Id { get; set; }
    public int MemberId { get; set; }
    public FineReason Reason { get; set; }
    /// <summary>Id de la cotisation ou de l'échéance en retard (0 = amende archivée).</summary>
    public int SourceId { get; set; }
    public long Amount { get; set; }
    public DateTime IssuedOn { get; set; }
    public DateTime? PaidOn { get; set; }
    public bool Cancelled { get; set; }
}

public class UserAccount
{
    public string Phone { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Salt { get; set; } = "";
    public int MemberId { get; set; }
    public bool MustChangePassword { get; set; }
}

/// <summary>Toutes les données de l'application (sauvegardées dans un seul fichier JSON).</summary>
public class NjangiData
{
    public Group Group { get; set; } = new();
    public List<Member> Members { get; set; } = new();
    public List<MeetingSession> Sessions { get; set; } = new();
    public List<Contribution> Contributions { get; set; } = new();
    public List<Loan> Loans { get; set; } = new();
    public List<Installment> Installments { get; set; } = new();
    public List<Fine> Fines { get; set; } = new();
    public List<UserAccount> Accounts { get; set; } = new();
}
