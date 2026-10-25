using Ngangi.Core.Models;

namespace Ngangi.Core.Services;

/// <summary>
/// Contrôle d'intégrité des données chargées depuis le disque : un fichier modifié à la main ou abîmé
/// ne doit jamais injecter de montant négatif, de taux absurde ou de lien cassé dans les calculs.
/// </summary>
public static class DataValidator
{
    public static List<string> Validate(NjangiData? d)
    {
        var e = new List<string>();
        if (d == null) { e.Add("Aucune donnée."); return e; }
        if (d.Group is null || d.Members is null || d.Sessions is null || d.Contributions is null ||
            d.Loans is null || d.Installments is null || d.Fines is null || d.Accounts is null)
        {
            e.Add("Structure de données incomplète.");
            return e;
        }

        var g = d.Group;
        CheckAmount(e, g.ContributionAmount, "cotisation du groupe");
        CheckAmount(e, g.LateContributionFine, "amende de cotisation");
        CheckAmount(e, g.LateRepaymentFine, "amende de remboursement");
        if (g.LoanFundShare < 0m || g.LoanFundShare > 1m) e.Add("La part de la caisse de prêts doit être entre 0 et 1.");
        if (Validation.RateError(g.MonthlyRate) is { } rateError) e.Add(rateError);

        CheckUnique(e, d.Members.Select(x => x.Id), "membres");
        CheckUnique(e, d.Sessions.Select(x => x.Id), "séances");
        CheckUnique(e, d.Contributions.Select(x => x.Id), "cotisations");
        CheckUnique(e, d.Loans.Select(x => x.Id), "prêts");
        CheckUnique(e, d.Installments.Select(x => x.Id), "échéances");
        CheckUnique(e, d.Fines.Select(x => x.Id), "amendes");

        var memberIds = d.Members.Select(x => x.Id).ToHashSet();
        var sessionIds = d.Sessions.Select(x => x.Id).ToHashSet();
        var loanIds = d.Loans.Select(x => x.Id).ToHashSet();

        foreach (var c in d.Contributions)
        {
            CheckAmount(e, c.Amount, $"cotisation n° {c.Id}");
            if (!memberIds.Contains(c.MemberId)) e.Add($"La cotisation n° {c.Id} cite un membre inconnu.");
            if (!sessionIds.Contains(c.SessionId)) e.Add($"La cotisation n° {c.Id} cite une séance inconnue.");
        }

        foreach (var l in d.Loans)
        {
            if (l.Principal <= 0 || l.Principal > Limits.MaxAmount) e.Add($"Le capital du prêt n° {l.Id} est invalide.");
            if (Validation.MonthsError(l.DurationMonths) != null) e.Add($"La durée du prêt n° {l.Id} est invalide.");
            if (Validation.RateError(l.MonthlyRate) != null) e.Add($"Le taux du prêt n° {l.Id} est invalide.");
            if (!memberIds.Contains(l.MemberId)) e.Add($"Le prêt n° {l.Id} cite un membre inconnu.");
        }

        foreach (var i in d.Installments)
        {
            CheckAmount(e, i.Amount, $"échéance n° {i.Id}");
            CheckAmount(e, i.InterestPart, $"intérêt de l'échéance n° {i.Id}");
            CheckAmount(e, i.PrincipalPart, $"capital de l'échéance n° {i.Id}");
            if (i.Number < 1) e.Add($"Le numéro de l'échéance n° {i.Id} est invalide.");
            if (!loanIds.Contains(i.LoanId)) e.Add($"L'échéance n° {i.Id} cite un prêt inconnu.");
        }

        foreach (var f in d.Fines)
        {
            CheckAmount(e, f.Amount, $"amende n° {f.Id}");
            if (!memberIds.Contains(f.MemberId)) e.Add($"L'amende n° {f.Id} cite un membre inconnu.");
        }

        var phones = new HashSet<string>();
        foreach (var a in d.Accounts)
        {
            if (!memberIds.Contains(a.MemberId)) e.Add("Un compte cite un membre inconnu.");
            if (string.IsNullOrEmpty(a.PasswordHash) || string.IsNullOrEmpty(a.Salt)) e.Add("Un compte n'a pas de mot de passe valide.");
            if (!phones.Add(a.Phone ?? "")) e.Add("Deux comptes ont le même numéro.");
        }
        return e;
    }

    private static void CheckAmount(List<string> errors, long value, string what)
    {
        if (value < 0 || value > Limits.MaxAmount) errors.Add($"Montant invalide : {what}.");
    }

    private static void CheckUnique(List<string> errors, IEnumerable<int> ids, string what)
    {
        var seen = new HashSet<int>();
        foreach (var id in ids)
            if (!seen.Add(id)) { errors.Add($"Identifiant en double parmi les {what}."); return; }
    }
}
