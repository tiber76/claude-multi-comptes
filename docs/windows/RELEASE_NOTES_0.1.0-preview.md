# Claude Multi Comptes Windows — 0.1.0-preview

**Date : 13 octobre 2026**  
**Statut : aperçu technique, non validé sur un PC Windows**

Cette version apporte une première interface Windows pour gérer plusieurs profils Claude Desktop depuis une seule application. Elle est développée en C# et WPF avec .NET 8, dans le dossier [`windows/`](../../windows/).

## Nouveautés

- Créer, nommer, renommer et retirer des profils Claude.
- Ouvrir chaque profil dans un dossier de données séparé, sans limite artificielle sur le nombre de profils.
- Associer un profil à un dossier Claude existant et le détacher sans supprimer les données.
- Conserver les profils et leurs associations après fermeture ou remplacement de l’application.
- Détecter les instances ouvertes et afficher leur état.
- Vérifier les versions de Claude Code et lancer sa commande officielle de mise à jour.
- Repérer les installations utilisateur classiques et les packages MSIX connus de Claude Desktop.

## Construire et lancer

Depuis Windows 10 ou ultérieur, avec le SDK .NET 8 et Claude Desktop installé :

```powershell
dotnet run --project .\windows\ClaudeMultiComptes.Windows.csproj
```

Pour générer une application autonome x64 :

```powershell
dotnet publish .\windows\ClaudeMultiComptes.Windows.csproj -c Release -r win-x64 --self-contained true -o .\windows\publish\win-x64
```

Le guide de validation décrit comment vérifier les profils, les sessions et la conservation des données : [Guide de test Windows](TEST_GUIDE.md).

## Emplacement des données

La liste des comptes est enregistrée dans `%APPDATA%\Claude Multi Comptes\profiles.json`. Les profils créés par l’application résident dans le sous-dossier `Profiles`. Les dossiers associés restent à leur emplacement d’origine. Ces données ne sont pas transférées automatiquement depuis la version macOS.

## Limites connues

- Le projet a été compilé depuis macOS avec le ciblage Windows .NET, mais l’application n’a pas encore été exécutée dans Windows.
- Le lancement de plusieurs instances, la reprise d’un dossier existant et la détection des instances doivent être validés sur Windows.
- En particulier, le lancement direct du binaire Claude et l’accès aux dossiers de données de la version MSIX doivent être vérifiés dans une vraie installation Windows.
- Le mécanisme multi-profils repose sur `--user-data-dir`, usage qu’Anthropic ne documente pas officiellement ; une mise à jour de Claude Desktop peut le modifier.
- Cette version n’a ni installateur ni signature de distribution Windows. Ne la considère pas comme une release stable.

## Validation avant une release publique

Une release Windows publique devra être précédée d’un essai sur Windows 11 x64 et, si possible, Windows 11 ARM : ajout et suppression de profils, deux connexions distinctes ouvertes ensemble, redémarrage de l’application, association/détachement d’un profil existant et mise à jour de Claude Code.
