# Essais Windows réels

Ce programme construit les vrais composants WPF de Jerry et ouvre des fenêtres de
test. Il utilise un dossier de réglages temporaire unique. Aucun SendInput, UI
Automation ou changement des réglages Windows n’est employé.

Depuis la racine du dépôt, sous Windows :

```text
dotnet run --project tests/Jerry.Windows.Tests/Jerry.Windows.Tests.csproj -- <dossier-des-preuves> <mode>
```

| Mode | Vérification |
| --- | --- |
| `components` (par défaut) | Quinze pages réelles, rendus d’effets distincts, clic forcé, affinité HUD, vidéo incluant annotation et excluant contrôle |
| `minimal-menu` | Sous-ensemble rapide du menu réel, avec images de son état compact initial et de ses outils supplémentaires dépliés |
| `tray-icon` | Icône réellement fournie au NotifyIcon : taille native Windows, premier plan visible sur au moins 80 % des deux dimensions, image indépendante de la fenêtre ; enregistre le PNG natif et ses mesures |
| `minimal-ui` | Quinze pages à taille normale et 780×560, absence de débordement horizontal, menu rapide vivant, sélection/couleurs/largeur de la barre compacte et HUD exclu des captures |
| `render-regressions` | Clic forcé malgré le réglage désactivé ; restauration du focus sur le même premier plan, pixels réels du rendu de notre fenêtre |
| `close-no-tray` | Fermer la fenêtre avec `MinimizeToTray=false` quitte l’application et ses services |
| `quit-countdown` | Quitter pendant le décompte attend la transition annulée |
| `quit-start` | Quitter pendant le démarrage du moteur attend sa transition et vérifie les PID enfants enregistrés |
| `session-countdown` | Appel direct à `PrepareForSessionEnd` pendant le décompte, sans fermer la session Windows |
| `maintenance` | Refus de maintenance pendant décompte et capture réels, conservation du processus, arrêt explicite, protection caméra/live/éditeur, sortie inactive |
| `session-recording` | Même appel pendant une vraie capture courte ; MP4 finalisé, interface et processus libérés |
| `native-services` | Réservation/conflit/libération de Ctrl+Alt+F24 sans touche envoyée ; zoom 1,05 et restauration exacte ; états natifs du curseur ; position/échelle de la loupe |
| `idle` | Processus neuf avec contrôleur, anneau statique, aucun modèle chargé ; 1 s de chauffe puis CPU/mémoire sur 5 s |
| `quit-editor-operation` | Éditeur réel sur WAV silencieux local de 600 s et modèle déjà présent ; démarrage réel de transcription puis Quit, fermeture/cancellation et libération des processus médias possédés |

Chaque mode s’exécute dans son propre processus, car les scénarios de fermeture
arrêtent volontairement `Application.Run()`. Un échec rend un code non nul et
conserve sa trace dans `run.log` et `results.json`. Après fermeture du Dispatcher,
les PID enfants créés et identifiés par Jerry sont contrôlés sans agir sur les
processus étrangers.

Les modes de capture nécessitent FFmpeg déjà disponible ou son acquisition privée
via le service réel. Les contrôles de focus lisent seulement le rectangle du
premier plan Windows et rendent notre propre fenêtre d’effets ; ils ne lisent ni
n’enregistrent le contenu de la fenêtre au premier plan. Le test de focus exige
que ce premier plan reste stable et intersecte le moniteur testé. Les essais ont
été validés sur un seul écran réel ; ils ne prouvent pas les scénarios multi-écrans.

Les preuves brutes sont destinées à `evidence/raw/` et restent exclues de Git.

Le mode `native-services` conserve la transformation plein écran initiale et la
restaure dans un bloc `finally`. Il relève les capacités réelles : un retour de
zoom négatif ou un état de curseur non observable est écrit comme limitation,
sans prétendre à une validation visuelle. Les dimensions de la loupe et sa matrice
native ne prouvent pas à elles seules son contenu magnifié. Le mode `idle` publie
CPU brut, durée réelle, nombre de processeurs logiques, mémoire privée et working
set avant/après ; son pourcentage CPU est donné pour un cœur et pour l’ensemble
des cœurs. C’est un court échantillon contextualisé, pas un benchmark exhaustif.

Le scénario `quit-editor-operation` consigne exactement le statut et la progression
atteints avant Quit. Un statut « Transcription Whisper » avec une progression
restée à 0,2 prouve le passage dans cette phase annoncée, mais ne prouve pas une
progression native d’inférence au-delà de la préparation audio. Aucun texte reconnu
sur la fixture silencieuse n’est attendu ni simulé.

Le mode `minimal-ui` teste les contrôles WPF réels du nouveau thème charbon/bleu.
Les interrupteurs utilisent leur propre événement de clic dans notre processus,
sans entrée de bureau synthétique ; leurs effets sont vérifiés dans les réglages
publics du contrôleur. La présence des noms accessibles est lue directement dans
les propriétés WPF, sans automatisation externe. Un test supplémentaire appelle
le chemin privé existant `PointerPressed` par réflexion, sans envoyer d’entrée au
système, pour vérifier la fermeture du menu même lorsque le dessin intercepte la
souris et lorsque l’interrupteur général est éteint. Les instantanés couvrent chaque
page et son défilement aux deux tailles. La hauteur compacte de 54 est mesurée en
DIPs et rapprochée des dimensions natives selon le DPI réellement observé. Les
assertions géométriques complètent, sans la remplacer, l’inspection visuelle.

La passe minimale vérifie aussi les libellés vivants sur les mêmes contrôles,
sans changer de page : dessin puis arrêt, et démarrage/finalisation d’une véritable
courte capture. Le menu est fermé via sa propre méthode `Close`, puis rouvert pour
vérifier la réutilisation sûre de la fenêtre. Le popup réel de la liste de fréquence
est rendu séparément et sa sélection doit modifier puis restaurer le modèle.

La fixture vidéo exige une session Windows déverrouillée. Une image de verrouillage
ou l’absence des marqueurs attendus constitue un échec du test de capture ; les
rendus `RenderTargetBitmap` restent des preuves de composants WPF et ne sont pas
présentés comme une preuve que ces composants apparaissaient réellement à l’écran.
