# Vérification Windows

## Mascotte néon et lisibilité de la zone de notification — 28 octobre 2026

La nouvelle mascotte ImageGen rend hommage à Jerry de Tom et Jerry : tête de
souris, grandes oreilles, contours cyan et magenta. Les sources et le prompt
sont dans `assets/branding/` ; les exports conservent la transparence et retirent
les marges vides, sans déformer le personnage.

Le test natif `tray-icon` a d’abord échoué sur la version précédente : le
NotifyIcon recevait un bitmap de 32 × 32 alors que Windows demandait 24 × 24.
Le premier plan mesuré occupait 22 × 22 dans ce bitmap de 32 pixels. Après
correction, le NotifyIcon reçoit **24 × 24** et le premier plan mesure
**24 × 20**. Le masque de mesure retient alpha ≥ 96 et max(R,V,B) ≥ 90 ; il
mesure la silhouette lumineuse, pas la case Windows elle-même.

- **4 contrôles natifs réussis**, dont la conservation d’une image de fenêtre
  indépendante de 32 pixels.
- **52 tests Core réussis**, **2 156 contrôles UI réussis**, puis fermeture sans
  réduction au plateau : **2 contrôles réussis**.
- Les dix résolutions ICO et la transparence du PNG ont été vérifiées.
- Le rendu en 16/20/24/32/48 pixels est montré sur fond clair et sombre dans
  `docs/assets/icon-sizes.png`.
- Une revue indépendante n’a trouvé aucun problème matériel dans le chargement,
  la propriété des icônes et leur libération après fermeture de l’interface.
- La publication autonome est préparée séparément dans `dist/neon-windows-x64/`
  et `%LOCALAPPDATA%\Jerry\app-neon`. Une fois l’ancienne instance terminée, la
  nouvelle version a été lancée en arrière-plan : processus réactif, sans journal
  d’exception applicative.

Preuves locales : `windows-tray-icon-red/`, `windows-tray-icon-green/`,
`windows-neon-ui/`, `windows-neon-close/` et `windows-neon-delivery.json`, sous
`evidence/raw/`. Les changements de DPI pendant l’exécution et les écrans à
échelles mixtes restent à vérifier. Les limites de capture décrites plus bas
restent applicables ; aucun nouveau résultat de composition vidéo n’est annoncé.

## Icône et préparation GitHub du 28 octobre 2026

- Icône originale fournie en SVG, PNG 1 024 × 1 024 et ICO à dix résolutions.
- Le même ICO est intégré à l’exécutable et chargé comme ressource WPF pour la
  fenêtre et la zone de notification.
- Nouvelle publication Release autonome réussie et **52 tests Core réussis**.
- Nouvelle exécution de `minimal-ui` avec la ressource intégrée : **2 156
  contrôles réussis**, sans échec. Les captures du README proviennent de cette
  exécution ; seules les fenêtres de test propres à Jerry y figurent.
- Les liens locaux du README et les dix images de l’ICO ont été vérifiés.
- Preuves locales : `evidence/raw/windows-branding-ui/`. Les limites de capture,
  de session verrouillée et de configurations matérielles ci-dessous restent
  applicables.

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
