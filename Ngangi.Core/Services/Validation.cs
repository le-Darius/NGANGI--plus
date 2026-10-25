using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Ngangi.Core.Services;

public static class Validation
{
    // ------------------------------------------------------------ Téléphone

    private static bool IsAllowedPhoneChar(char c) =>
        char.IsAsciiDigit(c) || c == ' ' || c == '+' || c == '-' || c == '.' || c == '(' || c == ')' || c == '\u00A0';

    /// <summary>Chiffres ASCII uniquement : char.IsDigit accepterait aussi les chiffres arabes, hindi, etc.</summary>
    private static string AsciiDigits(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
            if (char.IsAsciiDigit(ch)) sb.Append(ch);
        return sb.ToString();
    }

    private static string? Check(string? input, out string digits)
    {
        digits = "";
        if (string.IsNullOrWhiteSpace(input)) return "Saisissez votre numéro de téléphone.";
        if (input.Length > Limits.MaxPhoneInputLength || !input.All(IsAllowedPhoneChar))
            return "Le numéro contient des caractères non valides.";

        digits = AsciiDigits(input);
        if (digits.Length == 12 && digits.StartsWith("237", StringComparison.Ordinal)) digits = digits[3..];
        if (digits.Length != 9) return "Le numéro doit contenir 9 chiffres.";
        if (digits[0] != '6') return "Le numéro doit commencer par 6.";
        return null;
    }

    /// <summary>Retourne les 9 chiffres du numéro (sans +237 ni espaces), ou null s'il est invalide.</summary>
    public static string? NormalizePhone(string? input) =>
        Check(input, out var digits) == null ? digits : null;

    public static string? PhoneError(string? input) => Check(input, out _);

    // ------------------------------------------------------------------ Nom

    public static string? NameError(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Trim().Length < 3) return "Saisissez votre nom complet.";
        var t = input.Trim();
        if (t.Length > Limits.MaxNameLength) return $"Le nom ne doit pas dépasser {Limits.MaxNameLength} caractères.";
        if (t.Any(char.IsControl)) return "Le nom contient des caractères non valides.";
        return null;
    }

    /// <summary>Nom nettoyé : espaces de début et de fin retirés, espaces multiples réduits à un seul.</summary>
    public static string CleanName(string input) =>
        string.Join(' ', input.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    // ---------------------------------------------------------- Mot de passe

    public static string? PasswordError(string? input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < Limits.MinPasswordLength) return "8 caractères minimum.";
        if (input.Length > Limits.MaxPasswordLength) return $"{Limits.MaxPasswordLength} caractères maximum.";
        if (input.Distinct().Count() == 1) return "Choisissez un mot de passe moins simple.";
        return null;
    }

    public static string? ConfirmError(string? password, string? confirm) =>
        password != confirm ? "Les deux mots de passe sont différents." : null;

    // ------------------------------------------------------ Montants, durées

    /// <summary>
    /// Lit un montant saisi en FCFA. Accepte les chiffres et les espaces (séparateurs de milliers).
    /// Tout le reste (signe moins, virgule, point, lettres) est refusé au lieu d'être ignoré en silence :
    /// « -80000 » ou « 80.5 » ne deviennent plus 80000 ou 805.
    /// Retourne null si le montant est valide, sinon le message d'erreur.
    /// </summary>
    public static string? ParseAmount(string? text, out long amount)
    {
        amount = 0;
        if (string.IsNullOrWhiteSpace(text)) return "Saisissez un montant.";

        var sb = new StringBuilder();
        foreach (var ch in text.Trim())
        {
            if (char.IsAsciiDigit(ch)) sb.Append(ch);
            else if (char.IsWhiteSpace(ch)) continue;
            else return "Le montant ne doit contenir que des chiffres.";
        }
        if (sb.Length == 0) return "Saisissez un montant.";
        if (sb.Length > 12) return $"Le montant maximum est de {Formatting.Fcfa(Limits.MaxAmount)}.";

        if (!long.TryParse(sb.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out amount))
        {
            amount = 0;
            return "Montant invalide.";
        }
        if (amount <= 0) { amount = 0; return "Le montant doit être supérieur à zéro."; }
        if (amount > Limits.MaxAmount)
        {
            amount = 0;
            return $"Le montant maximum est de {Formatting.Fcfa(Limits.MaxAmount)}.";
        }
        return null;
    }

    public static string? AmountError(long amount, long min = 1) =>
        amount < min ? $"Le montant minimum est de {Formatting.Fcfa(min)}."
        : amount > Limits.MaxAmount ? $"Le montant maximum est de {Formatting.Fcfa(Limits.MaxAmount)}."
        : null;

    public static string? MonthsError(int months) =>
        months < Limits.MinMonths || months > Limits.MaxMonths
            ? $"La durée doit être comprise entre {Limits.MinMonths} et {Limits.MaxMonths} mois."
            : null;

    public static string? RateError(decimal monthlyRate) =>
        monthlyRate < 0m || monthlyRate > Limits.MaxMonthlyRate
            ? $"Le taux mensuel doit être compris entre 0 % et {Limits.MaxMonthlyRate * 100:0.##} %."
            : null;
}

public static class Security
{
    /// <summary>Itérations PBKDF2-SHA256 pour les nouveaux mots de passe (recommandation OWASP : 600 000).</summary>
    public const int CurrentIterations = 600_000;

    private const int LegacyIterations = 50_000;   // anciens comptes : format sans préfixe
    private const int MinAcceptedIterations = 10_000;
    private const int MaxAcceptedIterations = 5_000_000;   // évite qu'un fichier trafiqué bloque le téléphone
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const string Prefix = "v2";

    private static readonly byte[] DummySalt = new byte[SaltBytes];

    /// <summary>Retourne le hash (format « v2$itérations$base64 ») et le sel (base64).</summary>
    public static (string Hash, string Salt) HashPassword(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Derive(password, salt, CurrentIterations);
        return ($"{Prefix}${CurrentIterations}${Convert.ToBase64String(hash)}", Convert.ToBase64String(salt));
    }

    /// <summary>Vérifie un mot de passe en temps constant. Ne lève jamais d'exception sur des données corrompues.</summary>
    public static bool Verify(string password, string storedHash, string salt)
    {
        if (password == null || storedHash == null || salt == null) return false;
        if (!TryParse(storedHash, out var iterations, out var expected)) return false;

        byte[] saltBytes;
        try { saltBytes = Convert.FromBase64String(salt); }
        catch (FormatException) { return false; }
        if (saltBytes.Length == 0) return false;

        var actual = Derive(password, saltBytes, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Fait le même travail qu'une vraie vérification quand le compte n'existe pas,
    /// pour qu'on ne puisse pas deviner quels numéros ont un compte en mesurant le temps de réponse.
    /// </summary>
    public static void BurnTime(string? password) =>
        Derive(password ?? "", DummySalt, CurrentIterations);

    /// <summary>Vrai si le hash a été fait avec moins d'itérations que le niveau actuel (à refaire après connexion).</summary>
    public static bool NeedsRehash(string storedHash) =>
        !TryParse(storedHash, out var iterations, out _) || iterations < CurrentIterations;

    private static bool TryParse(string stored, out int iterations, out byte[] hash)
    {
        iterations = LegacyIterations;
        hash = Array.Empty<byte>();
        try
        {
            if (stored.StartsWith(Prefix + "$", StringComparison.Ordinal))
            {
                var parts = stored.Split('$');
                if (parts.Length != 3) return false;
                if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out iterations)) return false;
                if (iterations < MinAcceptedIterations || iterations > MaxAcceptedIterations) return false;
                hash = Convert.FromBase64String(parts[2]);
            }
            else
            {
                hash = Convert.FromBase64String(stored);   // ancien format : base64 seul, 50 000 itérations
            }
            return hash.Length == HashBytes;
        }
        catch (FormatException) { return false; }
    }

    private static byte[] Derive(string password, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, HashBytes);
}
