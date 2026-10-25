namespace Ngangi.Core.Services;

/// <summary>
/// Règle d'accès aux écrans. Sans session ouverte, seuls la connexion et l'inscription sont permises ;
/// tout autre écran (accueil, membres, profil, échéancier…) est privé.
/// L'interface s'en sert pour refuser une navigation vers un écran privé après une déconnexion
/// (par exemple un écran resté dans la pile de navigation).
/// </summary>
public static class RouteAccess
{
    /// <summary>Vrai si l'adresse désigne un écran accessible sans être connecté. Adresse vide ou inconnue : privée.</summary>
    public static bool IsPublic(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return false;

        var path = location.Trim();
        var cut = path.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) path = path[..cut];

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        bool Is(string part, string name) => part.Equals(name, StringComparison.OrdinalIgnoreCase);

        if (Is(parts[0], "login"))
            return parts.Length == 1 || (parts.Length == 2 && Is(parts[1], "register"));

        return parts.Length == 1 && Is(parts[0], "register");
    }

    /// <summary>Vrai si l'écran peut s'ouvrir : session ouverte, ou écran public.</summary>
    public static bool CanOpen(string? location, bool signedIn) => signedIn || IsPublic(location);
}
