# Interface native Jerry

Implémentation dans `src/Jerry.App/UI/` : WPF en C#, sans navigateur. La refonte
du 28 octobre suit les deux captures fournies par Gabriel : anthracite neutre,
accent bleu, lignes compactes avec interrupteurs à droite, navigation avec icônes
vectorielles. Fenêtre initiale 940 × 740, minimum 780 × 560 unités WPF, bornée par
l’espace disponible. Quinze rubriques donnent un accès direct à chaque effet.
Les couleurs des effets et les réglages personnalisés restent ceux du modèle.

Les références brutes et la maquette préparatoire Image Gen sont conservées dans
`evidence/raw/minimal-reference/` (hors Git). Le rendu livré est composé de vrais
contrôles WPF ; la maquette ne constitue pas une preuve de comportement de MOUZZ.

## Intégration

- `MainWindow(AppController)` et son partiel `MainWindow.Pages.cs` : navigation, réglages de toutes les familles du périmètre, préparation vidéo et speech, choix des périphériques et fichiers, raccourcis, réinitialisation et arrêt.
- `RecordingControlWindow(AppController)` : panneau discret 210 × 46, point rouge, durée et arrêt, déplaçable et topmost, décompte `SetCountdown(int?)`, durée réelle depuis l’état actif et arrêt asynchrone. `SetCountdown(null)` doit être appelé après le décompte pour afficher le chronomètre. La fermeture est empêchée pendant la capture/finalisation, sauf arrêt applicatif.
- `AnnotationToolbar(AppController)` : barre 548 × 44 sur une ligne, huit outils vectoriels, six couleurs et couleur personnalisée, épaisseur, annuler/effacer/terminer. Les sélections et les valeurs suivent aussi les changements au clavier. Les deux fenêtres flottantes utilisent `WindowTools.ExcludeFromCapture(this)` à `SourceInitialized`, `ShowActivated=false` et interception `WM_MOUSEACTIVATE=MA_NOACTIVATE` pour conserver le focus dans l’application présentée.
- `Theme.CreateResources()` : dictionnaire local réutilisable pour les interrupteurs, menus sombres, valeurs alignées, focus clavier bleu et défilement fin ; aucune ressource globale obligatoire. `Icons` partage les vecteurs et `AppIcon` fournit l’icône de notification. L’éditeur de captions peut conserver son propre thème.
- Contrat contrôleur fourni au départ, plus `UndoDrawing()` et `ResetSettings()`. Les changements de modèle appellent `SettingsChanged()`. Caméra et captions live appellent leurs méthodes asynchrones de démarrage/arrêt, pas seulement une case booléenne.
- `QuickMenuWindow(AppController)` : un clic sur l’icône Windows ouvre un menu compact, avec sept interrupteurs principaux ; **Autres outils** donne accès aux outils supplémentaires, à la capture, aux sous-titres et au dossier des prises. Les changements ne ferment pas le menu. Les préférences de visibilité antérieures restent prises en compte ; sons, localisation et touches s’ajoutent aux options disponibles. Le menu est exclu de la capture et placé en coordonnées physiques dans l’espace de travail du moniteur du pointeur. Échap, un clic extérieur et la fermeture du panneau le masquent sans détruire sa fenêtre.
- Le contrôleur fournit les événements sur le thread WPF. État/statut et libellés démarrer/arrêter mis à jour immédiatement sans reconstruire les contrôles actifs ; les réglages sont reconstruits lors du retour de focus dans la fenêtre pour refléter les actions du menu/raccourcis. Les erreurs asynchrones sont affichées.
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

## Validation

Le programme `Jerry.Windows.Tests` vérifie les quinze rubriques aux formats normal et 780 × 560, les commandes réelles, leur synchronisation et les panneaux flottants. Les résultats datés et les limites sont dans `windows-verification.md`. Restent à contrôler largement sur Windows : clavier et lecteur d’écran, DPI mixtes, petites résolutions et défilement, dialogues de périphériques, contrôles flottants sans vol de focus, capture excluant HUD/barre/décompte, changement de réglage pendant webcam/live captions actifs et finalisation des fichiers.

Les bornes proposées aux curseurs sont des limites de l’interface Jerry ; seules les bornes publiques de touches/captions ont une référence officielle. Aucune comparaison d’exécution macOS disponible.

## Corrections après revue

La fermeture avec réduction au plateau désactivée annule la destruction directe de la fenêtre puis appelle `controller.Quit()` pour libérer les services ; pendant l’arrêt applicatif, la fermeture est autorisée. Le choix de modèle propose uniquement tiny/base, pris en charge par le service. Les nouveaux réglages couleur de surligneur, animation captions et ouverture de l’éditeur après prise sont exposés, ainsi que les noms français des raccourcis sons/touches/localisation.
