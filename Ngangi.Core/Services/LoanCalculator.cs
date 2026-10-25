using Ngangi.Core.Models;

namespace Ngangi.Core.Services;

public sealed record LoanQuote(long Principal, int Months, decimal MonthlyRate, long Interest, long Total, long Monthly);

/// <summary>
/// Intérêt simple : capital x taux mensuel x nombre de mois. Tout est arrondi au FCFA.
/// Calculs en <c>decimal</c> uniquement ; montants entiers en FCFA ; dépassements détectés.
/// </summary>
public static class LoanCalculator
{
    /// <summary>Calcule le devis. Lève ArgumentOutOfRangeException si une valeur est hors des bornes de <see cref="Limits"/>.</summary>
    public static LoanQuote Quote(long principal, int months, decimal monthlyRate)
    {
        if (principal <= 0 || principal > Limits.MaxAmount)
            throw new ArgumentOutOfRangeException(nameof(principal), principal, "Capital hors limites.");
        if (months < Limits.MinMonths || months > Limits.MaxMonths)
            throw new ArgumentOutOfRangeException(nameof(months), months, "Durée hors limites.");
        if (monthlyRate < 0m || monthlyRate > Limits.MaxMonthlyRate)
            throw new ArgumentOutOfRangeException(nameof(monthlyRate), monthlyRate, "Taux hors limites.");

        // decimal partout : (decimal)principal × taux × mois, puis un seul arrondi.
        long interest = Money.RoundToFcfa((decimal)principal * monthlyRate * months);
        long total = checked(principal + interest);
        long monthly = total / months;                  // months >= 1 : pas de division par zéro
        return new LoanQuote(principal, months, monthlyRate, interest, total, monthly);
    }

    /// <summary>Comme <see cref="Quote"/>, mais retourne false au lieu de lever une exception (pour l'interface).</summary>
    public static bool TryQuote(long principal, int months, decimal monthlyRate, out LoanQuote? quote)
    {
        try { quote = Quote(principal, months, monthlyRate); return true; }
        catch (Exception ex) when (ex is ArgumentOutOfRangeException or OverflowException)
        {
            quote = null;
            return false;
        }
    }

    /// <summary>
    /// Construit l'échéancier : une mensualité par mois à partir de la première échéance.
    /// La dernière mensualité absorbe le reste de l'arrondi pour que le total soit exact.
    /// </summary>
    public static List<Installment> BuildSchedule(Loan loan, DateTime firstDueDate)
    {
        ArgumentNullException.ThrowIfNull(loan);
        var first = firstDueDate.Date;
        if (first.Year < 2000 || first.Year > 2100)
            throw new ArgumentOutOfRangeException(nameof(firstDueDate), firstDueDate, "Date d'échéance hors limites.");

        var q = Quote(loan.Principal, loan.DurationMonths, loan.MonthlyRate);
        var list = new List<Installment>();
        long interestLeft = q.Interest;
        long totalLeft = q.Total;
        long interestEach = q.Interest / q.Months;

        for (int i = 1; i <= q.Months; i++)
        {
            bool last = i == q.Months;
            long amount = last ? totalLeft : q.Monthly;
            long interestPart = last ? interestLeft : interestEach;
            list.Add(new Installment
            {
                LoanId = loan.Id,
                Number = i,
                DueDate = first.AddMonths(i - 1),   // toujours calculé depuis la première date : pas de dérive (31 janv. → 28 févr. → 31 mars)
                Amount = amount,
                InterestPart = interestPart,
                PrincipalPart = amount - interestPart
            });
            totalLeft -= amount;
            interestLeft -= interestPart;
        }
        return list;
    }

    /// <summary>Comme <see cref="BuildSchedule"/>, sans exception : retourne false et un message si le prêt est invalide.</summary>
    public static bool TryBuildSchedule(Loan loan, DateTime firstDueDate, out List<Installment> schedule, out string? error)
    {
        try
        {
            schedule = BuildSchedule(loan, firstDueDate);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or OverflowException)
        {
            schedule = new List<Installment>();
            error = "Les paramètres de ce prêt sont invalides (montant, durée, taux ou date).";
            return false;
        }
    }

    /// <summary>Nombre de jours de retard. 0 si l'échéance n'est pas dépassée : jamais de valeur négative.</summary>
    public static int OverdueDays(DateTime dueDate, DateTime today)
    {
        int days = (today.Date - dueDate.Date).Days;
        return Math.Max(days, 0);
    }
}
