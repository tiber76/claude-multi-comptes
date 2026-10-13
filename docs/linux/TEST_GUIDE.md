# Guide de test Linux

Ce guide s’applique à la version **0.1.0-preview**. Le build croisé depuis macOS vérifie la compilation, pas le fonctionnement de l’interface ou de Claude sur une vraie distribution Linux.

## Préparer Linux

- Ubuntu 22.04 LTS ou ultérieur, ou Debian 12 ou ultérieur.
- Architecture x64 ou ARM64.
- Claude Desktop installé depuis le [dépôt APT officiel d’Anthropic](https://support.anthropic.com/en/articles/10065433-install-claude-desktop).
- Pour l’interface, X11 ou XWayland ; sur un bureau minimal, `libx11-6`, `libice6`, `libsm6` et `libfontconfig1`.

Depuis un Mac Apple Silicon, UTM peut faire tourner Ubuntu ARM64 dans une VM. Pour un Mac Intel, utiliser une VM Linux x64. Il est aussi possible de tester sur un PC Linux existant. Le [guide UTM pour installer Ubuntu](https://docs.getutm.app/guides/ubuntu/) décrit la création de la machine virtuelle.

## Construire ou lancer

Depuis la racine du dépôt, avec .NET 8 :

```sh
dotnet run --project linux/ClaudeMultiComptes.Linux.csproj
```

Pour fabriquer les archives portables x64 et ARM64 :

```sh
bash linux/package-preview.sh
```

Extrais l’archive correspondant à l’architecture du système. Dans le dossier extrait :

```sh
./Claude\ Multi\ Comptes
```

## Vérifications à effectuer

1. Ajoute deux profils, connecte chacun à un compte Claude distinct et confirme qu’ils peuvent rester ouverts en même temps.
2. Vérifie que les fiches indiquent l’état ouvert, puis ferme une fenêtre Claude et confirme que son profil redevient disponible.
3. Quitte et relance le gestionnaire. Les noms de comptes doivent rester présents et les fenêtres Claude encore ouvertes doivent être reconnues.
4. Associe un profil à un dossier Claude existant. Vérifie que la session et l’historique du compte sont repris.
5. Détache le profil et vérifie que le dossier source et ses données n’ont pas été supprimés.
6. Vérifie les versions de Claude Code, puis son action de mise à jour si Claude Code est installé.
7. Répète les essais sur Wayland avec XWayland, puis sur X11 si possible.
8. Vérifie le résultat sur x64 et ARM64. Note la distribution, la version de Claude Desktop et l’architecture.

## Emplacement des données

Par défaut, la liste des comptes se trouve dans `~/.config/Claude Multi Comptes/profiles.json`. Si `XDG_CONFIG_HOME` est défini, ce dossier remplace `~/.config`. Les profils créés par l’application sont sous `Profiles`. Ne supprime pas ce dossier pour mettre à jour l’application.
