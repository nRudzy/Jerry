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
| `components` (par défaut) | Dix pages réelles, rendus d’effets distincts, clic forcé, affinité HUD, vidéo incluant annotation et excluant contrôle |
| `render-regressions` | Clic forcé malgré le réglage désactivé ; restauration du focus sur le même premier plan, pixels réels du rendu de notre fenêtre |
| `close-no-tray` | Fermer la fenêtre avec `MinimizeToTray=false` quitte l’application et ses services |
| `quit-countdown` | Quitter pendant le décompte attend la transition annulée |
| `quit-start` | Quitter pendant le démarrage du moteur attend sa transition et vérifie les PID enfants enregistrés |
| `session-countdown` | Appel direct à `PrepareForSessionEnd` pendant le décompte, sans fermer la session Windows |
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
