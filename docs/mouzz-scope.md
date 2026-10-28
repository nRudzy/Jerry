# MOUZZ : périmètre de référence initial

État relevé le **27 octobre 2026**, fuseau Europe/Paris.

## Demande et contraintes

Recréer pour un usage personnel les fonctions existantes de MOUZZ dans une
application adaptée et optimisée pour Windows. Le choix initial était un dépôt
GitHub privé ; le 28 octobre 2026, l’utilisateur a confirmé vouloir le conserver
public. Les nouvelles fonctions viennent ensuite. La couverture des éditions
gratuite et Pro est l'interprétation retenue de la demande de périmètre complet.

MOUZZ est une application **macOS**. L'utilisateur dispose uniquement de
Windows. La fidélité complète est un objectif ; elle n'est pas démontrée par
les documents publics ni par l'inspection statique.

## Références officielles

- [Présentation](https://mouzz.dev/).
- [Comparatif gratuit / Pro](https://mouzz.dev/pricing).
- [Fiche Mac App Store](https://apps.apple.com/us/app/mouzz-cursor-highlighter/id6760307235?mt=12).
- [Enregistrement](https://mouzz.dev/features/screen-recorder).
- [Sous-titres](https://mouzz.dev/features/auto-captions).
- [Zoom](https://mouzz.dev/features/live-zoom).
- [Touches à l'écran](https://mouzz.dev/features/keystroke-visualizer).

Les lignes suivantes sont des **fonctions annoncées**, à confronter aux
fichiers et au comportement réel. La colonne de validation décrit les essais de Jerry réalisés pendant
l’implémentation, détaillés dans `windows-verification.md`.

## Matrice de mise en œuvre Windows

| ID | Fonction à couvrir | Référence | Validation Windows |
| --- | --- | --- | --- |
| CUR-01 | Spotlight circulaire et barres horizontale / verticale | Comparatif | Rendu Windows testé |
| CUR-02 | Réglages de taille, couleur et opacité du spotlight | App Store | Rendu et bornes testés |
| CUR-03 | Activation maintenue et extinction au mouvement | Historique App Store | Logique testée ; geste réel à vérifier |
| CUR-04 | Masquage du pointeur dans le spotlight | App Store | Implémenté ; visibilité à confirmer |
| CUR-05 | Anneau animé et réglages de rendu | App Store | Rendu Windows testé |
| CUR-06 | Sept styles de traînée et leurs réglages | App Store | Sept rendus testés |
| CUR-07 | Effets de clic, y compris anneau simple récent | Historique App Store | Sept rendus testés |
| CUR-08 | Déclenchement du feedback automatique ou à la demande | Historique App Store | Tap et flash testés |
| CUR-09 | Sons de clic intégrés / personnalisés et volume | App Store | Implémenté ; écoute à vérifier |
| CUR-10 | Localisation du pointeur par secousse | App Store | Logique et rendu implémentés |
| ANN-01 | Dessin libre, flèche, rectangle, cercle et ligne | App Store | Outils rendus et testés |
| ANN-02 | Activation temporaire, couleur et épaisseur | App Store | Implémenté ; rendu testé |
| ANN-03 | Effacement à la sortie ou selon temporisation | App Store | Durée et sortie traitées |
| ANN-04 | Surligneur, texte et repères numérotés | Comparatif Pro | Huit outils présents et testés |
| KEY-01 | Raccourcis globaux configurables | Comparatif | Enregistrement natif testé ; saisie réelle à vérifier |
| KEY-02 | Affichage des touches, six styles | Comparatif Pro | Six rendus testés |
| ZOOM-01 | Zoom live centré sur le curseur | Page zoom | Interpolation et transformation native testées |
| ZOOM-02 | Zoom automatique au clic | Comparatif Pro | Implémenté sur moteur natif testé |
| ZOOM-03 | Loupe suivant le pointeur | Comparatif Pro | Loupe native, grossissement et bords testés |
| FOCUS-01 | Assombrissement hors de la fenêtre active | Comparatif Pro | Découpe et réactivation testées |
| REC-01 | Capture vidéo avec effets composités en direct | Page enregistrement | Inclusion vérifiée dans la vidéo |
| REC-02 | Sélection de l'écran par position du curseur | Page enregistrement | Écran réel capturé |
| REC-03 | MP4 : natif, 720p, 1080p, 4K et 30 / 60 i/s | Page enregistrement | Presets testés ; MP4 720p réel vérifié |
| REC-04 | Cadrages ajusté, recadré et étiré | Page enregistrement | Filtres testés |
| REC-05 | Audio système et micro sur pistes séparées | Page enregistrement | Deux pistes et priorité micro vérifiées |
| REC-06 | Choix du micro et dossier de sortie | Page enregistrement | Choix matériel et dossier implémentés |
| REC-07 | Décompte, commandes flottantes et notification finale | Page enregistrement | HUD et exclusion vérifiés |
| REC-08 | Commandes exclues de la vidéo et finalisation après interruption | Page enregistrement | Régressions de cycle de vie réussies |
| CAM-01 | Webcam en cercle ou carte, incluse dans les captures | Comparatif Pro | Images webcam réelles vérifiées |
| CAP-01 | Transcription locale et sous-titres live | Page sous-titres | Whisper et micro réel vérifiés |
| CAP-02 | Import vidéo et édition du texte / minutage par mot | Page sous-titres | Édition par mot et regroupement testés |
| CAP-03 | Six styles de sous-titres, dont trois animés | Page sous-titres | Six exports distincts vérifiés |
| CAP-04 | Réglages de position, lignes et apparence | Page sous-titres | Contrôles et polices livrées vérifiés |
| CAP-05 | Aperçu, export MP4 incrusté et export SRT | Page sous-titres | MP4/SRT et chemins spéciaux vérifiés |
| APP-01 | Commandes de zone de notification et fenêtre de réglages | Présentation / App Store | Dix pages et contrôles testés |
| APP-02 | Conservation des paramètres et démarrage à l'ouverture de session | Historique App Store | Persistance et fermeture testées |
| APP-03 | Gestion de plusieurs écrans et faible consommation | Présentation / App Store | Repos mesuré ; écran réel testé ; DPI mixtes à vérifier |

Les équivalents Windows des touches Fn / Option / Command et des commandes
macOS seront définis lors de la conception. Le site indique que les
sous-titres utilisent un moteur Apple disponible sous macOS 26 : un moteur
local Windows devra être choisi et évalué pour cette fonction.

## Premières observations sur les fichiers

Le paquet officiel [MOUZZ Pro 1.3.7](https://mouzz.dev/downloads/MouzzPro-1.3.7.pkg)
a été téléchargé, puis décompressé sans exécuter son installateur ni l'app.

- SHA-256 du paquet : `454a942ac6907a22f6b7938d97bd1773f2f23a0489d76995bc37877562d5412c`.
- Métadonnées lues avec Python : bundle `com.marceliseli.MouzzPro`, version
  `1.3.7`, build `11`, minimum déclaré macOS `13.0`.
- Conteneur Mach-O universel : en-tête déclarant deux architectures ; leur
  détail reste à établir avec l'outil adapté.
- L'extraction CPIO contient 113 entrées. Il s'agit d'observations d'outils
  auxiliaires, distinctes des résultats REA.
- REA a inspecté le bundle après restauration de ses permissions : 85 fichiers
  et 24 dossiers, racine comprise, soit 109 occurrences et 56 contenus distincts
  dans son graphe. La couverture de l'inventaire des entrées est complète ;
  cela ne prouve pas la couverture des fonctions de l'application.
- L'identité de racine renvoyée par cet inventaire REA est
  `631d3c840635ec1be4e1a431cc39890be4937b410fc3c8901550defa560f2dbf`.
  Elle est distincte de l'empreinte du paquet téléchargé.

Preuves REA conservées localement :

- Inspection : `ev_d6fec44ccfcca50e59d6dc40870ff4c21134b5de6f2c2d7a61499b04724f3f89`.
- Inventaire : `ev_f5e59cea42659a93a2c6d0218083f95b5b7deb162a7688ef0c04faedca7ca69f`.

Un premier scan de répertoire, antérieur à la restauration des permissions,
est aussi conservé. Son identité et sa couverture sont propres à cet état.
Ces résultats restent statiques : aucune logique d'exécution n'a été vérifiée.

## Comparaison avec l’original encore ouverte

- Relever tous les écrans, valeurs par défaut, bornes et combinaisons de réglages.
- Confirmer la couverture de la version gratuite face au paquet Pro analysé.
- Examiner les ressources et le code natif avec un moteur adapté.
- Définir des critères vérifiables pour chaque ligne, puis tester sur Windows.
- Vérifier les animations et les cas limites face à l'original lorsque des
  preuves adaptées seront disponibles ; aucune exécution macOS n'a eu lieu.
- Compléter la matrice à chaque découverte. Ses 37 lignes ne prouvent pas
  qu'elle couvre déjà toutes les fonctions de l'application.

La version Windows est implémentée. Voir [windows-verification.md](windows-verification.md)
pour la portée des essais ; une fonction implémentée ou testée ne signifie pas
que sa fidélité à l’original macOS est démontrée.
