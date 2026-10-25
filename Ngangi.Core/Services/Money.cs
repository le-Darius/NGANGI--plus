namespace Ngangi.Core.Services;

/// <summary>
/// Arithmétique monétaire : tout passe par <c>decimal</c> (jamais float ni double),
/// avec un arrondi explicite au FCFA et un contrôle de dépassement.
/// </summary>
public static class Money
{
    /// <summary>Arrondit au FCFA le plus proche (0,5 vers le haut). Lève OverflowException si hors limites.</summary>
    public static long RoundToFcfa(decimal value) =>
        checked((long)Math.Round(value, 0, MidpointRounding.AwayFromZero));

    /// <summary>Part d'un montant : amount × share, arrondie au FCFA.</summary>
    public static long Share(long amount, decimal share) => RoundToFcfa(amount * share);

    /// <summary>Rapport part / total borné entre 0 et 1. Retourne 0 si le total est nul : jamais de division par zéro.</summary>
    public static decimal Ratio(long part, long total)
    {
        if (total <= 0 || part <= 0) return 0m;
        if (part >= total) return 1m;
        return (decimal)part / total;
    }
}
