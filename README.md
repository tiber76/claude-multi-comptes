<p align="center">
  <img src="Assets/AppIcon.png" alt="Icône de Claude Multi Comptes" width="128" />
</p>

<h1 align="center">Claude Multi Comptes</h1>

<p align="center">Un profil Claude Desktop par compte, autant de sessions ouvertes que nécessaire.</p>

![Capture de Claude Multi Comptes avec les noms de comptes anonymisés](Screenshots/claude-multi-comptes.png)

*Aperçu de l’application. Cette capture date d’une version antérieure : le texte « trois sessions à la fois » et le compteur visible ne reflètent plus la version actuelle, qui n’impose aucune limite au nombre de sessions ouvertes.*

<p align="center"><strong>macOS 14 ou ultérieur · version Windows en aperçu</strong></p>

---

> Projet indépendant, non affilié à Anthropic. Claude Desktop ne propose pas actuellement de gestionnaire multi-profils officiel.

## Fonctionnalités

- Créer, renommer et retirer des profils Claude.
- Associer un profil à un dossier Claude existant pour reprendre sa session, son historique et ses réglages.
- Garder une session, un historique, des réglages et des connecteurs séparés par profil.
- Ouvrir plusieurs instances simultanément et voir lesquelles sont ouvertes ; l’application n’impose pas de limite.
- Consulter la version installée et la dernière version publique de **Claude Code**, puis savoir si une mise à jour est disponible.
- Lancer `claude update` pour mettre à jour Claude Code.
- Ouvrir le téléchargement officiel de Claude Desktop pour une réinstallation manuelle. En usage normal, Claude Desktop gère ses mises à jour automatiquement.

## Prérequis

- macOS 14 ou ultérieur.
- Claude Desktop installé dans `/Applications/Claude.app`.
- Xcode Command Line Tools pour construire l’application.
- Claude Code est facultatif ; le suivi et la mise à jour de Claude Code ne sont actifs que si une installation est détectée.

## Version Windows (aperçu)

Une première version Windows en .NET/WPF est disponible dans [`windows/`](windows/). Elle reprend la gestion des comptes, la création de sessions isolées, l’association d’un dossier de données existant et le suivi de Claude Code. La version macOS et son installation DMG restent indépendantes.

Elle demande Windows 10 ou ultérieur, le SDK .NET 8, et Claude Desktop installé depuis la [page officielle](https://support.anthropic.com/fr/articles/10065433-installation-de-claude-pour-ordinateur-de-bureau). Pour construire et publier une application autonome x64 depuis Windows :

```powershell
dotnet publish .\windows\ClaudeMultiComptes.Windows.csproj -c Release -r win-x64 --self-contained true -o .\windows\publish\win-x64
```

L’exécutable sera `windows\publish\win-x64\Claude Multi Comptes.exe`. Les comptes et leurs données sont enregistrés sous `%APPDATA%\Claude Multi Comptes`; remplacer l’application ne supprime pas ces données.

Cette version est un aperçu : le lancement avec données isolées et la découverte des installations MSIX doivent encore être validés sur un vrai PC Windows avant une release publique. Claude Desktop ne documente pas officiellement l’usage multi-profils.

## Construire et ouvrir

Dans un terminal, depuis le dossier du projet :

```sh
./build-app.sh
open "build/Claude Multi Comptes.app"
```

Le bundle est construit dans `build/Claude Multi Comptes.app` et signé localement avec une signature ad hoc. Il n’est pas notarié. Pour une distribution publique à d’autres utilisateurs, il faudra prévoir une signature Developer ID et la notarisation Apple.

## Installer depuis une release

Télécharge le fichier DMG depuis la [dernière release](https://github.com/tiber76/claude-multi-comptes/releases/latest), ouvre-le puis glisse **Claude Multi Comptes** dans **Applications**. Lors d’une mise à jour, confirme le remplacement de l’app existante. L’application demande macOS 14 ou ultérieur.

La mise à jour remplace uniquement **Claude Multi Comptes.app**. Elle ne réinitialise ni la liste des profils, ni les données de leurs instances Claude. Tu peux quitter Claude Multi Comptes pendant l’installation ; les fenêtres Claude déjà ouvertes peuvent rester ouvertes. Ne supprime pas le dossier `~/Library/Application Support/Claude Multi Comptes` : il contient la liste des profils, leurs sessions, leur historique et leurs réglages.

Le DMG actuel n’est pas notarié par Apple. Si Gatekeeper bloque l’ouverture, et seulement si tu as téléchargé l’application depuis la release officielle du projet :

1. Clique sur **Terminé** dans l’alerte, puis essaie une première fois d’ouvrir l’app depuis **Applications**.
2. Ouvre ** > Réglages Système > Confidentialité et sécurité**, puis descends jusqu’à la section **Sécurité**.
3. Clique sur **Ouvrir quand même** à côté de Claude Multi Comptes et confirme avec ton mot de passe ou Touch ID.
4. Relance l’application depuis **Applications**.

Le bouton **Ouvrir quand même** apparaît après une tentative de lancement et reste disponible environ une heure. [Voir la procédure Apple](https://support.apple.com/en-gb/guide/mac-help/mh40617/27/mac/27). Une distribution sans cet avertissement nécessite une signature Developer ID et une notarisation Apple.

## Première utilisation

1. Ouvre **Claude Multi Comptes** et clique sur **Ajouter un compte**.
2. Donne un nom explicite au profil, par exemple « Personnel », « Travail » ou le nom d’un client.
3. Clique sur **Ouvrir**. Lors du premier lancement, connecte-toi au compte voulu dans cette fenêtre Claude.
4. Crée les autres profils et connecte chacun au compte associé.
5. Garde ouvertes autant de fenêtres que nécessaire. Ferme une fenêtre Claude pour libérer les ressources utilisées.

Les comptes ne sont pas détectés automatiquement : une fiche correspond à un profil séparé que tu crées et connectes toi-même.

## Profils et données locales

Les profils et leurs données sont conservés sur ce Mac :

```text
~/Library/Application Support/Claude Multi Comptes/
├── profiles.json
└── Profiles/<identifiant-du-profil>/
```

Chaque dossier de profil contient les données Claude de cette instance, notamment la session et les réglages. Le fichier `profiles.json` contient les noms, couleurs, identifiants et éventuelles associations vers un dossier existant. Cet emplacement est indépendant de l’application installée dans `/Applications` : remplacer ou réinstaller l’app ne supprime donc pas les profils. Au redémarrage, l’application recharge la liste depuis ce fichier et détecte les fenêtres Claude toujours ouvertes.

Dans le menu d’un compte, **Associer un profil existant…** affiche les dossiers Claude détectés sur ce Mac, dont le profil classique et les autres dossiers `Claude-*`. Choisis le dossier qui contient la session du compte concerné. Le chemin choisi est enregistré dans `profiles.json` : l’association reste donc en place après avoir quitté l’app ou installé une mise à jour. L’association réutilise les données sur place, sans les copier ni les fusionner ; garde le dossier source au même emplacement. Si tu le déplaces, associe de nouveau le compte au nouveau dossier. Les données précédentes de la fiche restent conservées si tu la détaches. Un même dossier ne peut être associé qu’à une fiche à la fois. Retirer une fiche de l’interface ne supprime aucun dossier Claude.

L’application ne lit, ne copie ni ne stocke de mots de passe ou de jetons de connexion. Les vérifications de version de Claude Code contactent l’API publique des releases GitHub du dépôt officiel Anthropic ; aucun compte Anthropic n’est utilisé pour cette vérification.

## Versions et mises à jour

Le panneau **Mises à jour** sépare clairement les deux produits :

- **Claude Desktop** : la mise à jour normale est gérée automatiquement par Claude Desktop. Le bouton ouvre le téléchargement officiel pour une réinstallation manuelle si nécessaire.
- **Claude Code** : l’application lit la version installée avec `claude --version` et compare avec la dernière release publiée par [anthropics/claude-code](https://github.com/anthropics/claude-code/releases). La flèche relance la vérification ; le bouton de mise à jour exécute `claude update`.

Les installations de Claude Code sont recherchées dans `~/.local/bin/claude`, `/opt/homebrew/bin/claude` et `/usr/local/bin/claude`.

## Fonctionnement technique

Claude Multi Comptes est une application SwiftUI. Elle lance Claude Desktop avec un dossier de données distinct via l’argument Electron `--user-data-dir`. L’application n’impose pas de limite au nombre de profils ouverts simultanément ; la mémoire disponible et Claude Desktop lui-même restent les limites pratiques.

Claude Desktop ne documente pas officiellement cet usage multi-profils. Une mise à jour de Claude peut modifier son comportement. Le retour de connexion depuis le navigateur dépend aussi du routage `claude://` configuré par macOS ; vérifie le compte affiché lors de la première connexion de chaque profil.

## Dépannage

### Claude n’est pas détecté

Vérifie que l’app s’appelle `Claude.app` et se trouve dans `/Applications`.

### Le bouton Claude Code est désactivé

Installe Claude Code, ou vérifie qu’il se trouve dans l’un des chemins indiqués dans « Versions et mises à jour ». Ferme puis rouvre Claude Multi Comptes après l’installation.

### Le clic sur « Ouvrir » ne crée pas une nouvelle session

Vérifie qu’il s’agit bien d’un profil différent et que Claude Desktop accepte toujours l’argument `--user-data-dir`. Les mises à jour de Claude Desktop peuvent affecter ce mécanisme.

### Claude Code signale une erreur pendant sa mise à jour

Le message de la commande est affiché dans l’application. Certaines installations gérées par Homebrew ou npm peuvent demander une mise à jour par leur gestionnaire de paquets.

## Périmètre

La version macOS est publiée sous forme de DMG. La version Windows est fournie pour compilation et validation ; elle n’est pas encore distribuée sous forme d’installateur.
