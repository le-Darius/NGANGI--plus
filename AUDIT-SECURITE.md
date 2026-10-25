# Sécurité et protection des données — Ngangi+

Ce document décrit les failles identifiées lors de l'audit du projet, les protections mises en place et leurs limites.
Les données (noms, téléphones, cotisations, prêts, comptes) restent sur l'appareil : il n'y a ni serveur ni réseau.

## 1. Failles identifiées et corrigées

| # | Gravité | Fichier | Problème |
|---|---------|---------|----------|
| 1 | Critique | `JsonDataStore.cs` | Données (noms, téléphones, prêts, hash) en JSON **lisible**. |
| 2 | Critique | `JsonDataStore.cs` + `MauiProgram.cs` | Un fichier illisible était **remplacé en silence par les données de démonstration** (`loaded ?? SeedData.Create()` puis `Save`) : perte de toutes les vraies données. |
| 3 | Élevée | `Validation.cs` (`Security.Verify`) | Comparaison du hash avec `==` (pas en temps constant) ; `FormatException` possible si le sel est abîmé ; 50 000 itérations PBKDF2 (OWASP : 600 000). |
| 4 | Élevée | `NjangiService.Login` | Aucune limite de tentatives (force brute) ; temps de réponse différent si le numéro n'existe pas (énumération des comptes). |
| 5 | Moyenne | `LoanRequestViewModel.ParseAmount` | Retirait en silence tout ce qui n'est pas un chiffre : `-80000` devenait `80000`, `80.5` devenait `805`. Règle métier placée dans l'interface. |
| 6 | Moyenne | `LoanCalculator.cs` | Pas de borne haute : `principal + interest` pouvait déborder **sans erreur** (addition non vérifiée). L'interface était protégée par le contrôle « dépasse la caisse », pas le Core. |
| 7 | Moyenne | `NjangiService.ApproveLoan` | Le prêt passait à « Approuvé » **avant** le calcul de l'échéancier : si le calcul échouait, état à moitié modifié. |
| 8 | Moyenne | `JsonDataStore.Load` | Aucun contrôle des données lues : montant négatif, taux 500 %, durée 0, liens cassés, dans un fichier modifié à la main. |
| 9 | Moyenne | `Validation.NormalizePhone` | `char.IsDigit` accepte les chiffres arabes/hindi ; `StartsWith` dépend de la culture ; lettres ignorées ; aucune limite de longueur. |
| 10 | Faible | `AndroidManifest.xml` | `allowBackup="true"` : le fichier de données peut être sauvegardé/exfiltré. |
| 11 | Info | `SeedData.cs` | Deux comptes de démonstration au mot de passe connu étaient créés au premier lancement. **Corrigé** : le fichier a quitté le Core, il n'est compilé qu'en Debug et dans les tests ; une version Release démarre vide. |

## 2. Montants et `decimal`

Le Core n'utilise **aucun `float` ni `double`**. Les seuls `double` du projet sont dans l'interface :
la `ProgressBar` impose un `double`, et l'affichage du taux en utilisait un. Le rapport est calculé en
`decimal` (`Money.Ratio`) puis converti en `double` **uniquement** à la frontière de la barre ; le taux est un `decimal`.

Les montants en FCFA sont des `long` : le franc CFA n'a pas de centimes, donc un entier est exact et plus
simple qu'un `decimal`. Seuls les calculs avec un taux passent par `decimal`, avec **un seul arrondi explicite**
(`Money.RoundToFcfa`) et un contrôle de dépassement.

## 3. Détail par fichier

### Ngangi.Core/Services/DataProtection.cs
`IKeyProvider`, `IDataCipher` et `AesGcmCipher` : chiffrement **AES-256-GCM** (confidentialité + détection de toute
modification). Nonce aléatoire neuf à chaque écriture ; l'en-tête `NGC1` est authentifié. Aucune dépendance MAUI :
la clé arrive par `IKeyProvider`.

### Ngangi.Core/Services/JsonDataStore.cs
- Chiffre avant d'écrire (si un `IDataCipher` est fourni) ; sans chiffreur, le comportement est celui d'un JSON simple.
- **Migration automatique** : un ancien JSON en clair est lu, puis chiffré à la première sauvegarde ; aucune copie en
  clair n'est conservée dans `.bak`.
- Écriture sûre : fichier temporaire, `Flush(true)` jusqu'au disque, copie de secours `.bak`, puis remplacement.
- Un fichier modifié, illisible, avec une mauvaise clé ou des données invalides est **mis de côté**
  (`.corrupt-…`), jamais écrasé ; la sauvegarde précédente est restaurée si elle est saine.
- Les erreurs d'accès (fichier verrouillé) ne sont pas confondues avec une corruption.
- `LastProblem` explique ce qui s'est passé.

### Ngangi.App/MauiKeyProvider.cs + MauiProgram.cs
La clé de 32 octets est tirée au hasard une fois et gardée dans le **coffre du système** (`SecureStorage` :
Android Keystore). Sous Windows non empaqueté (`dotnet run`), `SecureStorage` peut être indisponible : la clé est
alors protégée par **DPAPI** (liée au compte Windows). `MauiProgram` branche le tout et ne laisse plus une erreur
de disque faire planter l'écran. `SecureStorage` ne sert **pas** à stocker le fichier (il est fait pour de petits
secrets) : il garde la clé, le fichier est chiffré avec elle.

### Ngangi.Core/Services/Validation.cs
Téléphone : chiffres ASCII seulement, caractères autorisés listés, longueur bornée. Nom : borné, sans caractères
de contrôle, espaces normalisés. Mot de passe : 8 à 128 caractères, pas un seul caractère répété.
`ParseAmount` (déplacé du ViewModel) refuse `-`, `.`, `,`, lettres, zéro et tout ce qui dépasse 100 M FCFA, avec un
message clair. `MonthsError` et `RateError` bornent durée et taux.
`Security` : hash versionné `v2$600000$…`, comparaison en temps constant, jamais d'exception sur des données
corrompues, anciens comptes toujours valides (renforcés à la prochaine connexion).

### Ngangi.Core/Services/Limits.cs et Money.cs
Bornes métier en un seul endroit. `Money` : arrondi explicite au FCFA, `Share`, et `Ratio` sans division par zéro.

### Ngangi.Core/Services/LoanCalculator.cs
Bornes sur capital, durée et taux ; calcul en `decimal` avec un seul arrondi ; addition `checked` ; division par la
durée toujours sûre. `TryQuote` et `TryBuildSchedule` ne lèvent pas d'exception (pour l'interface).
Échéancier : date de départ bornée (2000–2100), échéances toujours calculées depuis la première date
(31 janv. → 28 févr. → 31 mars, sans dérive). `OverdueDays` ne retourne jamais de valeur négative.

### Ngangi.Core/Services/NjangiService.cs
`Login` : blocage 5 min après 5 échecs par numéro, temps de réponse identique si le compte n'existe pas, renforcement
automatique d'un ancien hash. `ApproveLoan` : l'échéancier est calculé **avant** de modifier le prêt.
`ValidateLoanRequest` utilise `Limits` et vérifie le taux du groupe. `StatusOf` ne plante plus sur une séance
absente. L'heure de blocage est injectable (`utcNow`) pour les tests.

### Ngangi.Core/Services/DataValidator.cs
Contrôle tout fichier chargé : montants dans les bornes, taux et durées valides, identifiants uniques, liens
membre/séance/prêt existants, comptes complets.

### ViewModels (Ngangi.App)
`LoanRequestViewModel` ne contient plus de règle métier : il appelle `Validation.ParseAmount` et
`LoanCalculator.TryQuote`. `ContributionsViewModel` et `ScheduleViewModel` utilisent `Money.Ratio`.

### Ngangi.Tests/SecurityHardeningTests.cs
Une quarantaine de cas : validation, bornes et débordement, échéancier sur 60 combinaisons, hash et
anciens hash, blocage de connexion, validateur, et le magasin chiffré (rien de lisible, nonce différent, migration,
modification d'un octet, mauvaise clé, restauration de la sauvegarde).

## 4. Limites à connaître

- **Blocage de connexion en mémoire** : il disparaît si l'application redémarre. Une vraie protection contre la
  force brute demande un serveur.
- **Fichier en clair d'avant la migration** : il est remplacé, mais l'ancien contenu peut rester récupérable sur un
  SSD (effacement non garanti).
- **Les données sont en clair en mémoire** pendant l'utilisation. Le chiffrement protège le fichier au repos, pas un
  téléphone « rooté » ni un attaquant qui lit la mémoire.
- **Android** : si l'application est désinstallée/réinstallée, la clé est perdue ; l'ancien fichier chiffré est alors
  mis de côté et l'application repart de zéro. C'est voulu (`allowBackup=false`) ; la sauvegarde chiffrée
  (Profil > Sauvegarde) permet de restaurer les données.
- **Windows** : si SecureStorage n'est pas disponible, la clé est protégée par DPAPI : sûr contre la copie du
  fichier, pas contre un programme lancé avec le même compte Windows.
- **Coût de PBKDF2 à 600 000 itérations** : estimé à environ 0,1–0,3 s sur PC et jusqu'à 1–2 s sur un téléphone
  d'entrée de gamme (à mesurer sur l'appareil). `Security.CurrentIterations` peut être abaissé (par exemple à 210 000)
  si c'est trop lent.
- **Comptes de démonstration** (point 11) : absents des versions Release.
- Le paquet NuGet `System.Security.Cryptography.ProtectedData` (Windows) demande un `dotnet restore` avec Internet.

## 5. Vérifier

```
dotnet restore Ngangi.sln
dotnet test Ngangi.Tests
dotnet run --project Ngangi.App -f net10.0-windows10.0.19041.0
```
Un fichier `ngangi-data.json` créé par une ancienne version est lu puis chiffré automatiquement.
