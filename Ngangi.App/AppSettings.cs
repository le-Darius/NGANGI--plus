namespace Ngangi.App;

/// <summary>
/// Date du jour de l'application.
/// Release : toujours la vraie date du téléphone.
/// Debug : l'application « vit » au 06/10/2026, la date des maquettes, pour retrouver les chiffres du Figma
/// avec les données de démonstration (bouton réservé à Debug sur l'écran de connexion).
/// </summary>
public static class AppSettings
{
#if DEBUG
    public const bool UseDemoDate = true;
    public static readonly DateTime DemoDate = new(2026, 10, 6);

    public static DateTime Today => UseDemoDate ? DemoDate : DateTime.Today;
#else
    public static DateTime Today => DateTime.Today;
#endif
}
