namespace Ngangi.Core.Services;

/// <summary>
/// Textes visibles produits par le Core, regroupés ici pour pouvoir être traduits plus tard.
/// Les textes plus anciens (messages d'erreur de Validation et de NjangiService) pourront être déplacés ici
/// ultérieurement ; les nouveaux textes du Core s'ajoutent toujours dans ce fichier.
/// </summary>
public static class CoreTexts
{
    /// <summary>Nom du groupe créé automatiquement à la première inscription (destiné à être remplacé par un assistant de création du njangi).</summary>
    public const string DefaultGroupName = "Mon njangi";

    /// <summary>Nom affiché quand aucun groupe n'existe encore.</summary>
    public const string NoGroupName = "Aucun njangi";

    /// <summary>Libellé du cycle quand le groupe n'a pas encore de cycle défini.</summary>
    public const string NoCycleLabel = "Cycle non défini";
}
