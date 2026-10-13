# Claude Multi Comptes Linux — 0.1.0-preview

**Date : 13 octobre 2026** · **Statut : aperçu technique, compilé mais pas encore exécuté sur Linux**

Cette version ajoute une première application Linux basée sur C# et Avalonia, dans [`linux/`](../../linux/). Anthropic propose Claude Desktop sous Linux en bêta pour Ubuntu 22.04 LTS ou ultérieur et Debian 12 ou ultérieur, en x64 et ARM64.

## Nouveautés

- Créer, nommer, renommer et retirer des profils Claude.
- Ouvrir chaque compte avec un dossier de données séparé, sans limite artificielle au nombre de profils.
- Associer un profil à un dossier Claude existant, puis le détacher sans supprimer les données source.
- Conserver la liste et les associations après fermeture ou remplacement de l’application.
- Afficher les sessions ouvertes.
- Vérifier la version installée et la dernière version publiée de Claude Code, puis lancer `claude update`.
- Fournir une archive autonome `linux-x64` et une archive `linux-arm64`.

Claude Desktop doit être installé depuis le dépôt APT officiel. Ses mises à jour restent gérées par APT ; l’application ouvre le guide officiel d’installation et de mise à jour.

## Construction

Depuis la racine du dépôt, avec le SDK .NET 8 :

```sh
bash linux/package-preview.sh
```

Les archives apparaissent dans `build/`. Le [guide Linux](../../linux/README.md) détaille l’installation de Claude Desktop et les dépendances de l’interface.

## Données locales

Les noms et associations sont enregistrés sous `${XDG_CONFIG_HOME:-~/.config}/Claude Multi Comptes/profiles.json`. Les données de chaque nouveau profil restent dans le sous-dossier `Profiles`. Un dossier Claude associé est utilisé sur place, sans copie. Les profils ne sont pas automatiquement partagés avec les versions macOS ou Windows.

## Limites connues

- Compilation croisée depuis macOS réussie ; l’interface et les lancements n’ont pas encore été essayés dans une vraie session Linux.
- La compatibilité des chemins de données et des instances multiples doit être vérifiée sur Ubuntu et Debian, x64 et ARM64.
- Le multi-profil repose sur `--user-data-dir`, mécanisme non documenté par Anthropic.
- L’application Avalonia utilise X11 par défaut ; une session Wayland doit disposer de XWayland.
- Pas d’installateur `.deb`, signature ou dépôt APT pour Claude Multi Comptes à cette étape.

## Validation avant une release publique

Suivre le [guide de test Linux](TEST_GUIDE.md) : vérifier les connexions séparées en parallèle, les données qui persistent au redémarrage, l’association et le détachement d’un profil existant, l’état des processus et la mise à jour Claude Code. Valider les variantes x64 et ARM64 avant de les distribuer.
