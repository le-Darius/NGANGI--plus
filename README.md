# Ngangi+ — gestion de la caisse de prêts d'un njangi

Application .NET MAUI (hors ligne) : groupe et membres, cotisations, prêts avec intérêt, amendes de retard,
échéancier de remboursement et bilan de fin de cycle. Les écrans suivent la maquette Figma.

## Structure

| Dossier | Rôle |
|---|---|
| `Ngangi.Core` | Modèles et règles métier (aucune interface). `NjangiService` contient toutes les règles. |
| `Ngangi.Tests` | Tests xunit : calculs d'argent, amendes, droits, bilan, sécurité, démarrage à vide. Contient aussi `SeedData` (démonstration). |
| `Ngangi.App` | Application MAUI : 10 écrans XAML, ViewModels (MVVM), thèmes clair et sombre. |

## Lancer l'application (VS Code)

1. Extensions : **C# Dev Kit** et **.NET MAUI**.
2. Premier lancement (connexion Internet nécessaire pour télécharger les paquets MAUI) :
   ```
   dotnet restore Ngangi.sln
   ```
3. Windows (déjà possible avec la charge `maui-windows`) :
   ```
   dotnet run --project Ngangi.App -f net10.0-windows10.0.19041.0 -c Release
   ```
   (sans `-c Release`, c'est la configuration Debug : date et panneau de démonstration.) Si l'application ne démarre pas faute de « Windows App SDK », installez-le, ou dans `Ngangi.App.csproj`
   supprimez les lignes `WindowsAppSDKSelfContained`, `SelfContained` et `RuntimeIdentifier`.
4. Android :
   ```
   dotnet workload install maui-android
   ```
   puis dans `Ngangi.App.csproj` mettez `<NgangiAndroid>true</NgangiAndroid>` et lancez :
   ```
   dotnet build Ngangi.App -f net10.0-android -t:Run -c Release
   ```
   (un émulateur ou un téléphone en mode développeur est nécessaire ; JDK et SDK Android requis).

## Tests

```
dotnet test Ngangi.Tests
```

## Premier lancement

L'application démarre **vide** : aucun groupe, aucun compte, aucune donnée de démonstration (version Release).
Sur l'écran de connexion, « Créer un compte » crée le compte du responsable. Pour l'instant, cette
inscription crée un groupe « Mon njangi » dont l'inscrit est président ; un assistant de création du njangi
est prévu.

La date utilisée est la vraie date du téléphone.

## Données de démonstration (Debug seulement)

Les données de démonstration (`Ngangi.Tests/SeedData.cs`) servent aux tests et aux builds **Debug** ; elles ne sont
pas compilées dans une version Release. En Debug :

- l'écran de connexion affiche un petit panneau avec les comptes de démonstration et le bouton
  « Réinitialiser les données de démonstration » ;
- l'application vit au 06/10/2026 (`Ngangi.App/AppSettings.cs`), la date des maquettes (caisse 340 000 FCFA, 3 amendes…).

Pour tester comme un utilisateur, lancez toujours en Release (`-c Release`).

## Règles métier (dans `NjangiService` et `LoanCalculator`)

- **Intérêt simple** : capital × taux mensuel × nombre de mois (5 % par mois par défaut). Mensualité = total ÷ durée,
  la dernière mensualité absorbe l'arrondi.
- **Caisse de prêts** = 50 % des cotisations payées (`LoanFundShare`) + amendes encaissées + mensualités payées − capital prêté.
- **Demande de prêt** : minimum 10 000 FCFA, au plus la caisse disponible, pas de prêt en cours ni de retard pour l'emprunteur.
  Le président ou le trésorier approuve ; la première échéance tombe un mois plus tard.
- **Amendes** (1 000 FCFA) : créées automatiquement pour chaque cotisation ou mensualité en retard, une seule fois par cause.
  Seule la présidente peut les annuler.
- **Bilan** : cotisations versées à la caisse + intérêts + amendes ; les gains (intérêts + amendes) sont répartis à parts égales.
  Le bouton « Projection » ajoute les cotisations prévues et tous les intérêts des prêts.
- **Droits** : le président et le trésorier enregistrent les paiements, approuvent les prêts et ajoutent des membres.

## Données

Un fichier JSON local (`ngangi-data.json`, dans le dossier de données de l'application). Pas de serveur.
Pour passer à SQLite plus tard, il suffit de remplacer `JsonDataStore` ; les règles métier ne changent pas.

## Configuration du groupe

Le Core expose `ConfigureGroup` et `GenerateMonthlySchedule` : les séances mensuelles respectent les mois courts et la fin du cycle. Les tests correspondants sont dans `Ngangi.Tests/GroupSetupTests.cs`. L’écran Membres propose désormais le partage du résumé du groupe au lieu d’un code d’invitation, qui ne pouvait pas synchroniser des téléphones.

## Identité visuelle

Palette issue du logo (clair et sombre, contrastes AA), icône adaptative avec couche monochrome, splash, logo vectoriel dans l'app (`Controls/BrandLogo`), typographie fixe (`Resources/Styles/Typography.xaml` : 24/18/16/13) et visuels du Play Store dans `PlayStore/`. Les couleurs ne s'écrivent que dans `Colors.xaml` et `Palette.cs` ; `python3 tools/verify_palette.py` le contrôle (avec les contrastes).

## Navigation, icônes, profil

19 icônes vectorielles (Lucide, voir `NOTICE.md`) dessinées et teintées par code (`Controls/Icon`), barre du bas à icônes
avec indicateur d'onglet choisi (`Controls/BottomBar`), raccourcis de l'accueil à icônes, écran Profil et réglages (ouvert par
l'avatar : compte, thème, déconnexion confirmée, emplacements Sauvegarde / Confidentialité / Conditions, À propos),
icône de retour de 48 dp, touche Retour d'Android et garde-fou des routes (`RouteAccess` dans le Core, testé).
Pour changer une icône : modifier le SVG dans `Resources/Icons/`, puis `python3 tools/generate_icons.py`.

## Sauvegarde chiffrée

Profil > Sauvegarde permet de créer une archive `.ngangi` (AES-256-GCM, clé dérivée d'un mot de passe de sauvegarde par
PBKDF2, indépendante de la clé du téléphone), de la partager, puis de la restaurer après aperçu et confirmation. Les
fichiers modifiés, les mauvais mots de passe, les données invalides et les versions futures sont refusés ; une copie
chiffrée des données actuelles est conservée avant tout remplacement. Le code est dans
`Ngangi.Core/Services/BackupArchive.cs`, testé par `Ngangi.Tests/BackupArchiveTests.cs`.

## Limites connues

- Application mono-groupe, sans synchronisation entre téléphones.
- Les mots de passe sont hachés (PBKDF2) mais stockés localement : à renforcer avec un serveur pour un usage réel.


## Sécurité

Voir `AUDIT-SECURITE.md` : données chiffrées sur le disque (AES-256-GCM), clé dans le coffre du système,
validation renforcée, calculs en `decimal` bornés, blocage des tentatives de connexion.
