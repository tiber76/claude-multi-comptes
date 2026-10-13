# Claude Multi Comptes pour Windows (aperçu)

Cette première version reprend les fonctions principales de l’app macOS : profils nommés, sessions Claude séparées, association à un dossier de données existant, état des sessions ouvertes et suivi de Claude Code.

## Prérequis

- Windows 10 ou ultérieur.
- SDK .NET 8.
- Claude Desktop installé depuis la [page officielle d’Anthropic](https://support.anthropic.com/fr/articles/10065433-installation-de-claude-pour-ordinateur-de-bureau).

L’application cherche Claude Desktop dans l’installation utilisateur classique et dans le package MSIX enregistré dans Windows. Elle lance chaque profil avec un dossier `--user-data-dir` séparé. Ce mécanisme multi-profils n’est pas officiellement documenté par Anthropic.

## Lancer depuis les sources

Depuis la racine du dépôt :

```powershell
dotnet run --project .\windows\ClaudeMultiComptes.Windows.csproj
```

## Créer une application autonome x64

```powershell
dotnet publish .\windows\ClaudeMultiComptes.Windows.csproj -c Release -r win-x64 --self-contained true -o .\windows\publish\win-x64
```

Le résultat se trouve dans `windows\publish\win-x64\Claude Multi Comptes.exe`. Pour ARM64, remplace `win-x64` par `win-arm64`.

## Données et validation

Les noms de comptes et associations sont enregistrés dans `%APPDATA%\Claude Multi Comptes\profiles.json`; les dossiers des nouvelles sessions sont dans `Profiles`. Une mise à jour du programme ne supprime pas ces données. Un dossier associé est réutilisé à son emplacement, sans copie.

Le projet compile depuis macOS avec le ciblage Windows activé, mais l’usage de Claude MSIX, l’ouverture simultanée de plusieurs comptes et la reprise d’un profil Windows doivent être vérifiés sur Windows avant publication. La build x64 n’est donc pas encore une release destinée aux utilisateurs.
