# Interface native Jerry

Implémentation dans `src/Jerry.App/UI/` : WPF en C#, sans navigateur. Palette marine, texte crème, accent menthe ; dix pages à navigation latérale, cartes de réglages, défilement vertical. Fenêtre initiale 1080 × 800, minimum 800 × 560 unités WPF. Les valeurs restent les choix de Jerry et ne sont pas présentées comme les défauts mesurés de MOUZZ.

## Intégration

- `MainWindow(AppController)` : navigation, réglages de toutes les familles du périmètre, préparation vidéo et speech, choix des périphériques et fichiers, raccourcis, réinitialisation et arrêt.
- `RecordingControlWindow(AppController)` : pillule topmost déplaçable, décompte `SetCountdown(int?)`, durée réelle depuis l’état actif et arrêt asynchrone. `SetCountdown(null)` doit être appelé après le décompte pour afficher le chronomètre. La fermeture est empêchée pendant la capture/finalisation, sauf arrêt applicatif.
- `AnnotationToolbar(AppController)` : huit outils, couleur/épaisseur, annuler/effacer/terminer. Les deux fenêtres flottantes utilisent `WindowTools.ExcludeFromCapture(this)` à `SourceInitialized`, `ShowActivated=false` et interception `WM_MOUSEACTIVATE=MA_NOACTIVATE` pour conserver le focus dans l’application présentée.
- `Theme.CreateResources()` : dictionnaire local réutilisable ; aucune ressource globale obligatoire. L’éditeur de captions peut conserver son propre thème.
- Contrat contrôleur fourni au départ, plus `UndoDrawing()` et `ResetSettings()`. Les changements de modèle appellent `SettingsChanged()`. Caméra et captions live appellent leurs méthodes asynchrones de démarrage/arrêt, pas seulement une case booléenne.
- Le contrôleur fournit les événements sur le thread WPF. État/statut mis à jour immédiatement ; les réglages sont reconstruits lors du retour de focus dans la fenêtre pour refléter les actions du menu/raccourcis. Les erreurs asynchrones sont affichées.
- Les raccourcis sont des champs texte appliqués à la sortie du champ ; les erreurs de conflit/d’enregistrement viennent du statut du contrôleur. Aucune combinaison initiale imposée.
- La fermeture de la fenêtre principale la masque si `MinimizeToTray && !IsShuttingDown`. Quitter appelle explicitement le contrôleur.

## Réglages couverts

- CUR-01–10 : spotlight trois formes, couleurs/opacités distinctes, rayon/barre, maintien/délai et extinction/distance mutuellement exclusifs, masquage pointeur ; anneau animé/néon/suivi ; sept traînées et densité ; sept clics/automatique/tap ; son intégré/fichier/volume ; secousse/sensibilité.
- ANN-01–04 : huit outils, maintien, texte, couleur/trait, largeur surligneur/alignement, taille numéro, expiration/effacement et actions directes.
- KEY-01–02 : seize actions globales, six apparences, taille/durée/filtres Espace/Supprimer/Entrée/accent.
- ZOOM-01–03 et FOCUS-01 : armement et commande de zoom, facteur/durée/suivi, zoom automatique distinct et délai, loupe, focus/intensité.
- REC-01–08 : résolution/cadrage/cadence/décompte, pistes système/micro, choix micro/dossier, lancement/arrêt, pillule et préparation outils ; les garanties de compositing et finalisation appartiennent aux services et nécessitent des captures de validation.
- CAM-01 : activation explicite, périphérique, cercle/carte, taille, bordure, miroir, aimantation et coordonnées.
- CAP-01–05 : activation live, langue/modèle/préparation locale, six styles, police/taille/couleurs/fond/découpage/lignes/position/maintien/fondu, ouverture de l’éditeur/import et dernière prise. L’édition de texte/minutages et les exports sont fournis par l’éditeur du module Captions.
- APP-01–03 : zone de notification configurable, persistance via contrôleur, lancement session, réduction au plateau, fréquence de rendu, réinitialisation.

## Validation restante

Compilation complète confiée à l’agent racine conformément à la coordination. Aucun succès de compilation ni de test Windows n’est revendiqué ici. À contrôler sur Windows : clavier et lecteur d’écran, DPI mixtes, petites résolutions et défilement, dialogues de périphériques, contrôles flottants sans vol de focus, capture excluant HUD/barre/décompte, changement de réglage pendant webcam/live captions actifs et finalisation des fichiers.

Les bornes proposées aux curseurs sont des limites de l’interface Jerry ; seules les bornes publiques de touches/captions ont une référence officielle. Aucune comparaison d’exécution macOS disponible.

## Corrections après revue

La fermeture avec réduction au plateau désactivée annule la destruction directe de la fenêtre puis appelle `controller.Quit()` pour libérer les services ; pendant l’arrêt applicatif, la fermeture est autorisée. Le constructeur de page curseur est renommé `CursorPage` pour éviter de masquer la propriété WPF. Le choix de modèle propose uniquement tiny/base, pris en charge par le service. Les nouveaux réglages couleur de surligneur, animation captions et ouverture de l’éditeur après prise sont exposés, ainsi que les noms français des raccourcis sons/touches/localisation.
