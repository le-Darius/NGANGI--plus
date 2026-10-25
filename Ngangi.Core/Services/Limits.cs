namespace Ngangi.Core.Services;

/// <summary>
/// Bornes métier uniques, partagées par la validation, le calcul et le contrôle des données chargées.
/// Les montants sont en FCFA (entiers : le franc CFA n'a pas de centimes).
/// </summary>
public static class Limits
{
    public const long MinLoan = 10_000;
    public const long MaxAmount = 100_000_000;      // 100 millions de FCFA : très au-delà d'une caisse de njangi
    public const int MinMonths = 1;
    public const int MaxMonths = 12;
    public const decimal MaxMonthlyRate = 0.30m;    // 30 % par mois au maximum

    public const int MaxNameLength = 80;
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;
    public const int MaxPhoneInputLength = 32;
}
