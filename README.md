# Jerry

Application Windows personnelle couvrant le périmètre étudié de [MOUZZ](https://mouzz.dev/), Free et Pro. Interface française, traitements locaux. Le futur dépôt GitHub est prévu **privé**.

## Démarrer

Ouvrir **`dist/windows-x64/Jerry.exe`** sous Windows. Conserver tout le dossier avec l’exécutable : il contient les bibliothèques et le runtime. Aucun SDK .NET, Node.js ou REA n’est nécessaire pour utiliser la version portable.

Depuis le projet, Windows ou WSL :

```sh
npm start
```

Jerry reste accessible dans la zone de notification. Un clic ouvre le menu rapide ;
**Autres outils** déploie les commandes supplémentaires et **Menu rapide** règle
leur visibilité. L’interface utilise quinze rubriques compactes et des interrupteurs
bleus sur fond anthracite. Les raccourcis sont vides au départ : les choisir dans **Raccourcis**. Le bouton maître coupe les effets sans arrêter une capture.

## Fonctions

- Spotlight à trois formes, anneau avec néon, sept traînées, sept effets de clic, sons personnalisés et localisation du pointeur.
- Huit outils d’annotation : crayon, flèche, rectangle, cercle, ligne, surligneur, texte, repères numérotés ; annulation et effacement.
- Six apparences pour les touches, raccourcis globaux, zoom animé/automatique, loupe et focus de fenêtre.
- MP4 avec effets, écran choisi par le curseur, 30/60 i/s, natif/720p/1080p/4K, Fit/Crop/Stretch, son système et microphone séparés.
- Webcam réelle en cercle ou carte, miroir, bordures, position et aimantation.
- Whisper local : live et vidéos importées, édition par ligne/mot, six styles, export MP4 incrusté et SRT.

**Dessiner à l’écran** ouvre la barre. `F`, `A`, `R`, `C`, `L`, `H`, `T`, `N` changent d’outil ; `Ctrl+Z` annule, `Suppr` efface et `Échap` quitte. Un modificateur maintenu peut aussi être configuré.

## Stockage local

FFmpeg est acquis et vérifié dans `%LOCALAPPDATA%\Jerry\tools`. Les modèles Whisper `tiny` et `base` restent dans `%LOCALAPPDATA%\Jerry\models`. Le premier téléchargement utilise Internet ; les médias ne sont pas envoyés pour être traités. Montserrat et Oswald sont livrées avec leurs licences OFL, sans installation système.

Les prises vont dans le dossier choisi, initialement `Vidéos\Jerry`. Les intermédiaires récupérables restent dans `.Jerry-sessions`, y compris après une prise réussie : les retirer après vérification de la vidéo pour récupérer cet espace.

Les réglages sont dans `%LOCALAPPDATA%\Jerry\settings.json`, avec sauvegarde avant remplacement et préservation d’un fichier illisible. La caméra est désactivée au lancement.

## Construire et tester

SDK .NET **10.0.301** ou version compatible de la même branche majeure :

```sh
npm test
npm run build:windows
npm start
```

Le build produit `dist/windows-x64/`, autonome en x64. Les essais WPF/vidéo sont documentés dans [tests/Jerry.Windows.Tests/README.md](tests/Jerry.Windows.Tests/README.md). Résultats : [docs/windows-verification.md](docs/windows-verification.md).

## Références et limites

Le [périmètre suivi](docs/mouzz-scope.md) et les [observations de l’original](docs/mouzz-observations.md) distinguent les faits, les annonces et les choix de Jerry. L’original n’a pas été exécuté sur Mac : aucune fidélité à 100 % n’est certifiée. Les essais matériels utilisent un seul écran ; plusieurs écrans, DPI mixtes et interactions physiques au clavier/souris restent à vérifier.

Les animations par mot sont synchronisées dans l’éditeur/export. Le live reçoit des segments par blocs, sans prétendre à un alignement audio instantané. Les minutages estimés sont signalés.

## REA et dépôt

REA 6.8.0 reste disponible depuis le checkout voisin `../rea` :

```sh
npm run rea -- --help
npm run rea:build
```

La [préparation de REA](docs/rea-setup.md) décrit sa connexion à Jerry. Les applications originales, preuves brutes, réglages de machine et builds sont exclus de Git. Aucun dépôt distant n’est associé ; aucun envoi GitHub n’a été effectué.
