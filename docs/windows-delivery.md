# Livraison Windows

## Installer Jerry 0.2.0

Les [versions GitHub](https://github.com/nRudzy/Jerry/releases) contiennent :

- `Jerry-Setup-0.2.0.exe` : assistant français, installation pour votre compte.
- `Jerry-Windows-x64-0.2.0.zip` : publication autonome portable complète.
- `SHA256SUMS.txt` : empreintes SHA-256 des téléchargements.

L’assistant installe par défaut dans `%LOCALAPPDATA%\Programs\Jerry`, crée une
entrée dans Applications installées et un raccourci dans le menu Démarrer. Le
raccourci Bureau et le démarrage avec Windows sont proposés en option. Aucun
droit administrateur, SDK .NET, Node.js, REA ou script PowerShell n’est nécessaire
à l’utilisateur final. Les DLL .NET et natives, ressources et notices font
partie de la publication.

Installation et signature sont distinctes : les exécutables ne sont pas signés.
L’installation peut donc encore afficher « éditeur inconnu » ou SmartScreen.
Ne changez aucune protection Windows pour utiliser les scripts de développement.

## Mise à jour et données

Une nouvelle installation conserve le choix de démarrage actuel dans Windows,
sauf choix explicite dans l’assistant. Les préférences de Jerry reflètent ce
choix. Si une copie portable est lancée, le démarrage automatique privilégie
l’exécutable local enregistré par l’installateur. Les chemins WSL, UNC et
lecteurs réseau sont refusés pour ce démarrage.

L’assistant demande une fermeture propre avant remplacement. Jerry refuse si une
capture, un décompte, une finalisation, un téléchargement, une caméra, des
sous-titres live ou un éditeur sont encore actifs. Terminez ce travail puis
réessayez. Une ancienne version sans canal de maintenance doit être quittée
manuellement. L’assistant n’arrête et ne tue aucun processus de force.

Les données restent dans `%LOCALAPPDATA%\Jerry` : réglages, outils vidéo,
modèles vocaux et travaux de sous-titrage. Les captures restent dans le dossier
choisi, `Vidéos\Jerry` par défaut, avec leurs `.Jerry-sessions` récupérables.
Il n’y a ni migration, ni suppression, ni téléchargement de ces ressources lors
de l’installation ou de la désinstallation.

Après confirmation, la désinstallation retire les fichiers installés, les
raccourcis, App Paths et le démarrage automatique. Elle conserve les réglages
avec `LaunchAtLogin` désactivé et toutes les autres données. Annuler la
confirmation ne ferme pas l’application et ne change pas le démarrage.

## Construction depuis les sources

Sous Windows x64 avec le SDK .NET 10 :

```powershell
./scripts/build-windows.ps1
./scripts/build-installer.ps1 -Version 0.2.0
```

Le second script exige Inno Setup et les DLL redistribuables VC++ x64 officielles
de Visual Studio. Il publie aussi `Jerry.Maintenance.exe`, un auxiliaire .NET
autonome utilisé avant la copie et avant la désinstallation. Il produit
l’installateur, le ZIP et les sommes dans `dist/`. Le paramètre
`-VcRedistDirectory` permet de préciser le dossier officiel des DLL CRT.
Les licences et notices sont copiées avec les applications. Si la politique locale
bloque les scripts PowerShell de développement, utilisez directement `dotnet test`
et `dotnet publish`, ou les livraisons construites en CI ; ne changez pas cette
politique. Le wrapper npm respecte également ce réglage Windows.

Les tests unitaires Windows ne modifient pas les inscriptions Jerry. Le script
`test-installer.ps1`, qui installe et désinstalle réellement, exige un runner
GitHub Windows hébergé et jetable et un profil sans installation ni données Jerry.
Ne contournez pas ses gardes sur un ordinateur personnel.

La CI exécute les tests sur une branche puis construit l’installateur. Pour un tag
`v0.2.0`, la publication GitHub intervient seulement après les vérifications.
Les sources sont sur `main` dans le dépôt public
[nRudzy/Jerry](https://github.com/nRudzy/Jerry). Les sorties binaires sont des
fichiers de release ; les réglages de machine, originaux et preuves brutes sont
toujours exclus du dépôt.

Voir le [rapport d’installation](windows-installation.md) et la
[validation fonctionnelle Windows](windows-verification.md) pour les essais
effectués et les limites distinctes de cette livraison.
