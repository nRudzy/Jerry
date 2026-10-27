# Application Windows Jerry

L'utilisateur a demandé d'implémenter tout le périmètre décrit dans
`docs/mouzz-scope.md`. Cette spécification traduit ses 37 points en composants.
L'objectif est un outil personnel local ; la parité visuelle avec macOS reste
à vérifier faute de Mac. Aucun envoi GitHub n'est prévu pendant la construction.

## Choix de réalisation

Décision d'implémentation : C#/.NET 10, WPF et API Windows natives. Le SDK 10.0.301
est déjà installé. Le rendu des effets sera piloté par les événements d'entrée,
avec une horloge d'animation active seulement lorsque nécessaire. Les fenêtres
d'effets ne prennent pas le focus et laissent passer les clics.

Un frontend web aurait demandé une couche native supplémentaire pour les hooks
et les captures ; C++ aurait accru le coût de développement de l'interface et
des médias. WPF conserve un accès direct aux API Windows et une application
installable sans navigateur. Ce choix est fait par l'agent pour exécuter la
demande, sans l'attribuer à une préférence technique exprimée par l'utilisateur.

## Composants et périmètre

- `Jerry.Core` : réglages sérialisables, raccourcis, secousses, géométrie vidéo
  et documents de sous-titres, indépendants de l'interface.
- `Jerry.App/Platform` : hooks souris/clavier, fenêtres transparentes, DPI,
  raccourcis globaux, fenêtre active, masquage du curseur et zoom.
- `Jerry.App/Effects` : spotlight, anneau, sept traînées, effets et sons de clic,
  localisation, annotations, affichage des touches, focus, loupe et sous-titres.
- `Jerry.App/Media` : FFmpeg local, écran choisi par position du pointeur,
  fichiers MP4, résolutions/cadrages, audio système et micro séparés, webcam,
  finalisation et conservation des captures récupérables en cas d'échec.
- `Jerry.App/Captions` : Whisper local, sous-titres live et transcription vidéo,
  édition du texte/minutage, six styles, export SRT et MP4.
- `Jerry.App/UI` : réglages en français, zone de notification, raccourcis,
  contrôles d'enregistrement exclus de la capture et persistance.

## Contraintes

- Windows x64, application sans privilèges administrateur.
- Aucun cloud pour l'audio, les vidéos et la transcription.
- Modèles et outils téléchargés localement depuis leurs distributions officielles.
- Un échec audio, caméra ou de transcription doit être affiché ; aucun succès
  simulé. Les fichiers intermédiaires doivent rester récupérables.
- Les effets sont indépendants : couper les effets n'arrête pas l'enregistrement.
- Aucun raccourci global activé sans choix explicite de l'utilisateur.
- Coordonnées physiques des moniteurs, y compris origines négatives et DPI mixtes.
- Arrêt propre : restauration du curseur/zoom et libération des hooks et médias.

## Validation

Tests unitaires des réglages, raccourcis, géométrie, secousses et formats de
sous-titres. Compilation Windows puis essais réels de l'interface, des effets,
de l'enregistrement et des exports. Les fonctions nécessitant un périphérique
absent sont marquées comme non vérifiées matériellement. Le suivi des 37 points
sera actualisé d'après les preuves obtenues.
