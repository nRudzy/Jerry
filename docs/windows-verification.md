# Vérification Windows

## Refonte minimaliste du 28 octobre 2026

- Publication Release autonome réussie ; **52 tests Core réussis**, zéro échec.
- Mode Windows `minimal-ui` : **2 156 contrôles réussis**, 55 captures de nos
  composants, quinze rubriques à 940 × 740 et 780 × 560 unités WPF.
- Interrupteurs à droite, palettes et menus sombres ; menu rapide gardé ouvert
  pendant les bascules, état modifié par le contrôleur reflété immédiatement.
- Régressions contrôlées : libellés dessin/capture mis à jour sur place, vraie
  transition d’enregistrement et fichier MP4 finalisé, fermeture/réouverture du
  menu, clic extérieur via le chemin d’entrée propre au contrôleur pendant le
  dessin et quand le maître est coupé.
- Barre de dessin 548 × 44, huit outils sur une ligne, sélection/couleur/épaisseur
  synchronisées ; panneau de capture 210 × 46. Les affinités natives des trois
  panneaux flottants valent `WDA_EXCLUDEFROMCAPTURE` (17).
- Fermeture sans réduction au plateau : mode `close-no-tray`, deux contrôles
  réussis, arrêt effectif du contrôleur et de ses fenêtres.
- Reprise du mode `components` : 93 contrôles réussis avant l’échec du témoin
  vidéo. Le fichier décodé montre l’écran de verrouillage Windows, pas la scène
  de test. Les pixels témoins ne peuvent donc pas être validés. Ce dernier
  essai de composition vidéo **n’a pas réussi** et doit être repris sur une
  session déverrouillée ; les essais encodés du 27 octobre restent historiques.
- L’outil Computer Use est indisponible dans ce contexte WSL
  (`sandboxCwd is not a local file URI`). Les essais construisent et commandent
  nos propres composants WPF, sans injection de clavier/souris ni UI Automation.
  Ils ne prouvent pas les interactions physiques ni les scénarios DPI mixtes.
- L’ancienne instance reste ouverte à la demande de Gabriel. La nouvelle copie
  se trouve dans `%LOCALAPPDATA%\Jerry\app-minimal`, prête après sa fermeture.

Preuves brutes hors Git : `windows-minimal-ui-final/`, `windows-minimal-components/`,
`windows-minimal-close-no-tray/`, `windows-minimal-summary.json` et
`windows-minimal-delivery.json`, sous `evidence/raw/`.

## Résultats de la version initiale du 27 octobre

Essai complémentaire de fermeture : un éditeur occupé a chargé Whisper puis
annoncé sa transcription ; Quitter a fermé l’éditeur, annulé son travail et
libéré trois processus média en 935 ms. La progression était encore à 0,2 :
ce scénario ne prouve pas une annulation en inférence avancée.

Implémentation du 27 octobre 2026. Ces essais ne constituent pas une comparaison
à une exécution de MOUZZ sur macOS.

## Construction

- Windows x64 `10.0.26300.0`, SDK .NET `10.0.301`.
- Un écran réel 2560 × 1600, microphone Realtek, caméra intégrée.
- .NET 10 / WPF, manifeste PerMonitorV2, sans élévation.
- Publication autonome x64 par `npm run build:windows`.
- Exécutable installé puis lancé : fenêtre « Jerry — Votre présentation, au
  premier plan », processus réactif et prêt aux entrées ; aucun journal
  d’exception non gérée créé au contrôle de démarrage.
- **51 tests Release réussis**, aucun échec ; build intégré sans avertissement
  ni erreur lors du contrôle précédant la publication.

## Essais réels

| Domaine | Résultat obtenu |
| --- | --- |
| Interface | Dix pages WPF et défilement, images de rendu vérifiées |
| Effets | Trois spotlights, sept traînées, sept clics, huit annotations et six styles de touches distincts |
| Capture | Écran réel en H.264 1280 × 720, MP4 inspecté par FFprobe |
| Effets inclus | Annotation cyan de notre fenêtre : 14 297 pixels dans la vidéo décodée |
| Commandes exclues | Marqueur exclu : 0 pixel magenta ; témoin inclus : 10 000 pixels verts |
| Audio | Deux pistes AAC nommées ; microphone déclaré par défaut |
| Silence | Durée de la piste système silencieuse conservée |
| Webcam | 86 images BGRA réelles, arrêt répété et événement de fin validés |
| Transcription | Audio JFK transcrit localement : 4 lignes, 22 mots ; modèles tiny/base chargés |
| Piste de voix | Transcription depuis le micro même avec système stéréo initialement par défaut |
| Sous-titres | Six MP4 distincts ; SRT, chemins spéciaux, édition par mot et regroupement vérifiés |
| Slide Up | Trois images successives vérifient l’entrée séparée des mots |
| Polices | Montserrat/Oswald chargées depuis les TTF livrés, rendus distincts, licences présentes |
| Fermeture | Fermer sans minimiser quitte proprement après correction d’une réentrance WPF |
| Interruption | Quitter pendant décompte/démarrage attend et libère FFmpeg |
| Fin de session | Appel de finalisation pendant décompte puis prise : fermeture et MP4 valide |
| Focus | Réactivation sur le même premier plan : découpe transparente restaurée |
| Clic manuel | Flash forcé visible avec clics automatiques coupés, puis expiration |
| Raccourcis natifs | Ctrl+Alt+F24 enregistré, conflit détecté et liaison libérée |
| Zoom natif | Windows confirme 1,05× ; retour exact à 1× et offsets 0,0 |
| Loupe native | Contrôle réel à 1,4×, maintenu dans le moniteur au bord inférieur droit |

Dernière passe complète : **96 assertions réussies**. Sept modes de régression
terminent avec `exitCode=0` et `appCode=0` ; processus enfants identifiés libérés.

MediaCapture WinRT renvoie `0x80070490` sur cette machine alors que la caméra
fonctionne en DirectShow : Jerry sélectionne automatiquement ce second chemin.
Les images citées sont réelles. L’incertitude résiduelle de synchronisation sur
les prises courtes est d’environ **0,17 s** ; la dérive d’une longue prise reste
à mesurer.

Le masquage utilise `MagShowSystemCursor` et un refus Windows est signalé dans
la barre d’état. Son effet visible reste **à confirmer** : `GetCursorInfo`
n’a pas modifié son indicateur pendant l’essai, malgré un retour natif `true` ; aucun succès de masquage
effectif n’est déduit de ce contrôle.

Au repos, sur processus neuf, fenêtre ouverte et anneau statique, après une
seconde de chauffe (harnais Debug) : **15,625 ms de CPU sur 5,005 s**, soit **0,312 % d’un cœur
logique** (0,0098 % réparti sur 32 cœurs). Mémoire privée : 268,58 à 266,56 Mio.
Aucun modèle de parole n’est chargé. Cette mesure ne couvre pas la capture,
la transcription ni une longue session.

## Preuves et reproduction

Les preuves brutes restent exclues de Git : `evidence/raw/windows-publish.log`,
`final-core-tests.log`, `windows-component-smoke-final/` et
`windows-regressions-summary.json`. L’échec initial de fermeture est conservé
dans `windows-close-no-tray/`, sa correction dans `windows-close-no-tray-fixed/`.
Les rapports de modules détaillent les essais de médias et de polices.

Voir `tests/Jerry.Windows.Tests/README.md`. Le harnais construit nos composants
et leurs rendus ; il n’injecte pas de frappes dans d’autres applications.

## Limites

- Pas de comparaison d’exécution avec MOUZZ macOS.
- Plusieurs moniteurs physiques, DPI mixtes et débranchement à vérifier ; les
  géométries négatives sont testées séparément.
- Livraison réelle des raccourcis/gestes au quotidien et claviers internationaux
  à vérifier avec un utilisateur.
- Parole française variée et longues prises à évaluer.
- Les appels de fin de session sont testés sans verrouiller ni arrêter Windows.

L’outil de contrôle du bureau a été essayé mais refuse le contexte WSL :
`sandboxCwd is not a local file URI: file:///home/gabriel/Projects/Jerry`.
Les essais WPF et captures réelles ont continué ; aucun test d’entrée physique
automatisée n’est annoncé.
