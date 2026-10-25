namespace Ngangi.App;

/// <summary>
/// Textes visibles de l'interface, regroupés ici pour pouvoir être traduits plus tard.
/// Les écrans plus anciens contiennent encore des textes écrits directement dans le XAML ou les ViewModels :
/// ils pourront être déplacés ici ultérieurement. Les nouveaux textes s'ajoutent toujours dans ce fichier.
/// Les constantes servent dans le XAML : {x:Static t:AppTexts.Nom}.
/// </summary>
public static class AppTexts
{
    // ----------------------------------------------------------- Marque
    public const string AppName = "Ngangi+";
    public const string LogoDescription = "Logo Ngangi+";

    // ----------------------------------------------------------- Connexion
    public const string FirstUseTitle = "Première utilisation";
    public const string FirstUseBody = "Aucun njangi n'est encore enregistré sur ce téléphone. Créez le compte du responsable pour commencer.";

    // --------------------------------------------------------------- Séances
    public const string NoSessionTitle = "Aucune séance programmée";
    public const string NoSessionBody = "Les cotisations apparaîtront dès que des séances seront programmées.";

    // --------------------------------------------------------------- Cotisations
    public const string NoContributionTitle = "Aucune cotisation pour cette séance";
    public const string NoContributionBody = "Les cotisations de chaque membre apparaîtront ici.";

    // --------------------------------------------------------------- Membres
    public const string NoMemberTitle = "Aucun membre";
    public const string NoMemberBody = "Les membres du groupe apparaîtront ici.";
    public const string NoMemberFoundTitle = "Aucun résultat";
    public const string NoMemberFoundBody = "Aucun membre ne correspond à votre recherche.";

    public static string MemberCount(int count) => count == 1 ? "1 membre" : $"{count} membres";

    // --------------------------------------------------------------- Prêts
    public const string NoLoanTitle = "Aucun prêt";
    public const string NoLoanBody = "Les demandes et les prêts du groupe apparaîtront ici.";

    public const string LoanNoMemberNotice = "Aucun membre dans le groupe : un prêt ne peut pas être demandé pour le moment.";

    public static string LoanNoFundsNotice(string minimum) =>
        $"La caisse de prêts ne permet pas encore de prêt (minimum {minimum}). Elle se remplit avec les cotisations payées.";

    // --------------------------------------------------------------- Échéancier
    public const string LoanNotFoundTitle = "Prêt introuvable";
    public const string LoanNotFoundBody = "Ce prêt n'existe plus ou n'est plus disponible.";
    public const string NoInstallmentTitle = "Pas encore d'échéancier";
    public const string NoInstallmentBody = "L'échéancier est créé quand la demande de prêt est approuvée.";

    // --------------------------------------------------------------- Bilan
    public static string ReportTitle(int cycleYear) =>
        cycleYear > 0 ? $"Bilan du cycle {cycleYear}" : "Bilan du cycle";

    public const string ReportNoMemberTitle = "Aucun membre";
    public const string ReportNoMemberBody = "Le bilan apparaîtra dès que le groupe aura des membres.";
    public const string ReportEmptyTitle = "Rien à partager pour le moment";
    public const string ReportEmptyBody = "Le bilan se remplira avec les cotisations payées, les intérêts et les amendes encaissées.";

    // --------------------------------------------------------------- Navigation
    public const string TabHome = "Accueil";
    public const string TabMembers = "Membres";
    public const string TabContributions = "Cotisations";
    public const string TabLoans = "Prêts";
    public const string TabReport = "Bilan";

    /// <summary>Lu par le lecteur d'écran : nom, rôle, position, état. Ex. « Membres, onglet 2 sur 5, sélectionné ».</summary>
    public static string TabDescription(string name, int position, int count, bool selected) =>
        $"{name}, onglet {position} sur {count}" + (selected ? ", sélectionné" : "");

    public const string Back = "Retour";
    public const string Cancel = "Annuler";

    // --------------------------------------------------------------- Accueil : raccourcis et avatar
    public const string ShortcutContributions = "Cotisations";
    public const string ShortcutLoanRequest = "Demander un prêt";
    public const string ShortcutFines = "Amendes";
    public const string ProfileShort = "Profil";

    public static string ProfileOpenDescription(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Ouvrir le profil et les réglages" : $"Ouvrir le profil et les réglages de {name}";

    // --------------------------------------------------------------- Profil et réglages
    public const string ProfileTitle = "Profil et réglages";
    public const string ProfileAccountSection = "Compte";
    public const string ProfilePhoneLabel = "Téléphone";
    public const string ProfileRoleLabel = "Rôle";
    public const string ProfileGroupLabel = "Njangi";

    public const string ThemeSection = "Apparence";
    public const string ThemeTitle = "Thème";
    public const string ThemeAuto = "Automatique";
    public const string ThemeLight = "Clair";
    public const string ThemeDark = "Sombre";
    public const string ThemeAutoHint = "Suit le réglage du téléphone.";

    public static string ThemeOptionDescription(string name, bool selected) =>
        selected ? $"Thème {name}, sélectionné" : $"Thème {name}";

    public const string DataSection = "Données";
    public const string BackupTitle = "Sauvegarde";
    public const string BackupCaption = "Copier les données du njangi en lieu sûr.";
    public const string InfoSection = "Informations";
    public const string AboutTitle = "À propos";
    public const string AboutCaption = "Version et licences";
    public const string PrivacyTitle = "Confidentialité";
    public const string PrivacyCaption = "Ce que l'application garde, et où.";
    public const string TermsTitle = "Conditions d'utilisation";
    public const string TermsCaption = "Règles d'usage de l'application.";
    public const string ComingSoonBadge = "Bientôt";
    public const string ComingSoonTitle = "Pas encore disponible";
    public const string ComingSoonBody = "Cette fonction arrive dans une prochaine version de l'application.";

    public const string LogoutTitle = "Se déconnecter";
    public const string LogoutCaption = "Fermer la session sur ce téléphone.";
    public const string LogoutConfirmTitle = "Se déconnecter ?";
    public const string LogoutConfirmBody = "Vous devrez saisir à nouveau votre numéro et votre mot de passe. Vos données restent sur ce téléphone.";
    public const string LogoutConfirmAccept = "Se déconnecter";

    // --------------------------------------------------------------- À propos
    public const string AboutPageTitle = "À propos de Ngangi+";
    public const string AboutTagline = "Votre njangi, simple et hors ligne";
    public const string AboutOffline = "Ngangi+ fonctionne sans Internet : aucune donnée ne quitte votre téléphone.";
    public const string AboutLicensesSection = "Licences";
    public const string AboutLicensesIntro = "Ngangi+ utilise les éléments libres suivants. Les textes complets figurent aussi dans le fichier NOTICE.md du projet.";
    public const string AboutIconsName = "Icônes : Lucide (licence ISC) et Feather (licence MIT)";
    public const string AboutMauiName = ".NET MAUI et .NET (licence MIT)";
    public const string AboutLicenseTextTitle = "Texte des licences des icônes";

    public static string AboutVersion(string version, string build) => $"Version {version} (build {build})";

    // --------------------------------------------------------------- Debug seulement
#if DEBUG
    public const string DebugResetButton = "Réinitialiser les données de démonstration";
    public const string DebugResetConfirmTitle = "Réinitialiser";
    public const string DebugResetConfirmBody = "Remplacer toutes les données par celles de la démonstration ?";
    public const string DebugResetDoneTitle = "Terminé";
    public const string DebugResetDoneBody = "Les données de démonstration sont restaurées.";
#endif
}
