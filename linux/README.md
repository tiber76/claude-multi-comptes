# Claude Multi Comptes pour Linux (aperçu)

Cette version .NET/Avalonia reprend les fonctions principales du gestionnaire : comptes nommés, dossiers de données isolés, association à un profil Claude existant, état des sessions et suivi de Claude Code.

## Systèmes pris en charge

Anthropic propose actuellement Claude Desktop pour Linux en bêta sur **Ubuntu 22.04 LTS ou ultérieur** et **Debian 12 ou ultérieur**, sur x64 et ARM64. Installe Claude Desktop depuis son [dépôt APT officiel](https://support.anthropic.com/en/articles/10065433-install-claude-desktop) avant d’utiliser cette application.

Sur Ubuntu ou Debian :

```sh
sudo curl -fsSLo /usr/share/keyrings/claude-desktop-archive-keyring.asc https://downloads.claude.ai/claude-desktop/key.asc
echo "deb [signed-by=/usr/share/keyrings/claude-desktop-archive-keyring.asc] https://downloads.claude.ai/claude-desktop/apt/stable stable main" | sudo tee /etc/apt/sources.list.d/claude-desktop.list
sudo apt update
sudo apt install claude-desktop
```

Les mises à jour de Claude Desktop restent gérées par APT. Le bouton de l’application ouvre la documentation officielle ; celui de Claude Code lance `claude update`.

## Lancer depuis les sources

Avec le SDK .NET 8 :

```sh
dotnet run --project linux/ClaudeMultiComptes.Linux.csproj
```

Avalonia utilise X11 par défaut ; sur un bureau Wayland, XWayland doit être disponible. Sur une installation minimale, les bibliothèques `libx11-6`, `libice6`, `libsm6` et `libfontconfig1` peuvent être requises.

## Créer les archives de prévisualisation

Depuis la racine du dépôt, exécute :

```sh
bash linux/package-preview.sh
```

Le script produit des archives autonomes x64 et ARM64 dans `build/` :

- `Claude-Multi-Comptes-0.1.0-preview-linux-x64.tar.gz`
- `Claude-Multi-Comptes-0.1.0-preview-linux-arm64.tar.gz`

Extrais l’archive de l’architecture de la machine, puis lance `Claude Multi Comptes` depuis le dossier extrait. Les instructions sont incluses dans `LISEZ-MOI.txt`.

## Données et limites

La liste des comptes est conservée dans `${XDG_CONFIG_HOME:-$HOME/.config}/Claude Multi Comptes/profiles.json`; les dossiers des nouvelles sessions sont dans `Profiles`. Les dossiers associés sont réutilisés à leur emplacement d’origine. La mise à jour de l’application ne réinitialise pas ces données. Il n’y a pas de migration automatique des profils entre macOS, Windows et Linux.

L’application lance Claude Desktop avec un dossier de données séparé via `--user-data-dir`. Claude Desktop ne documente pas officiellement cet usage multi-profils. Le projet compile depuis macOS, mais son fonctionnement doit encore être vérifié sur Ubuntu ou Debian avant toute release publique.

Lire la [note de version Linux 0.1.0-preview](../docs/linux/RELEASE_NOTES_0.1.0-preview.md) et le [guide de test](../docs/linux/TEST_GUIDE.md).
