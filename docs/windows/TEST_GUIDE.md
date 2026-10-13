# Guide de test Windows

Ce guide s’applique à la version **0.1.0-preview**. Le test nécessite Windows : une compilation croisée depuis macOS ne suffit pas à valider le lancement de Claude Desktop.

## Préparer l’environnement

- Windows 10 ou ultérieur (Windows 11 conseillé).
- Claude Desktop installé depuis la [page officielle d’Anthropic](https://support.anthropic.com/fr/articles/10065433-installation-de-claude-pour-ordinateur-de-bureau).
- Pour compiler les sources : SDK .NET 8.

Sur un Mac Apple Silicon, une machine virtuelle Windows 11 ARM peut servir à ce test. UTM documente [l’installation d’une VM Windows 11](https://docs.getutm.app/guides/windows/). Windows demande une licence valide ; voir les [options Microsoft pour exécuter Windows sur Mac](https://support.microsoft.com/en-gb/windows/experience/platform-variants/options-for-using-windows-11-with-mac-computers-with-apple-m1-m2-and-m3-chips). Le paquet x64 peut être essayé sous Windows 11 ARM, qui prend en charge l’émulation des applications x64 ; une validation sur un PC x64 reste préférable.

## Démarrer

Depuis la racine du dépôt, lance les sources :

```powershell
dotnet run --project .\windows\ClaudeMultiComptes.Windows.csproj
```

Ou construis puis lance une publication x64 autonome :

```powershell
dotnet publish .\windows\ClaudeMultiComptes.Windows.csproj -c Release -r win-x64 --self-contained true -o .\windows\publish\win-x64
& ".\windows\publish\win-x64\Claude Multi Comptes.exe"
```

## Vérifications à effectuer

1. Ajoute deux profils avec des noms distincts et ouvre-les l’un après l’autre.
2. Connecte chaque fenêtre à un compte Claude différent et confirme que les comptes restent distincts.
3. Laisse les deux fenêtres ouvertes et vérifie que l’application indique les deux sessions comme ouvertes.
4. Ferme puis relance **Claude Multi Comptes** : les fiches et les états des fenêtres encore ouvertes doivent être retrouvés.
5. Associe un profil à un dossier Claude existant. Vérifie qu’il réutilise la session choisie, puis détache-le et vérifie que le dossier source est toujours présent.
6. Ferme une fenêtre Claude et vérifie que son profil redevient prêt à ouvrir.
7. Si Claude Code est installé, vérifie l’affichage des versions et l’action de mise à jour.
8. Répète le lancement avec Claude Desktop installé en MSIX. Note tout message d’accès refusé ou toute fenêtre qui ne s’ouvre pas.

## Données de test

Les données gérées par l’application sont dans `%APPDATA%\Claude Multi Comptes`. Pour éviter toute perte, ne supprime pas ce dossier pendant la validation. Le retrait d’une fiche doit retirer seulement la fiche ; il ne doit pas effacer son dossier de données.

Pour chaque essai, note la version de Windows, la version et le type d’installation de Claude Desktop (MSIX ou installation utilisateur), le résultat observé et le texte exact des erreurs éventuelles.
