# MOUZZ Pro 1.3.7 : observations pour Jerry

Relevé du 27 octobre 2026. Complément de `mouzz-scope.md`, sans exécution macOS. Les 37 exigences initiales restent une grille de travail, pas une preuve de parité exhaustive.

## Sources et degré de preuve

- **REA conservé, réutilisé** : `evidence/raw/mouzz-pro-1.3.7-artifact.evidence.json`, inspection `ev_d6fec44ccfcca50e59d6dc40870ff4c21134b5de6f2c2d7a61499b04724f3f89`, inventaire `ev_f5e59cea42659a93a2c6d0218083f95b5b7deb162a7688ef0c04faedca7ca69f`. Identité de racine : `631d3c840635ec1be4e1a431cc39890be4937b410fc3c8901550defa560f2dbf`. Aucun résultat REA de décompilation ou d'exécution n'est disponible.
- **Observation auxiliaire A** : GNU `strings -a -n 4` sur `targets/mouzz-pro-1.3.7/Applications/Mouzz Pro.app/Contents/MacOS/Mouzz Pro`, conservé dans `evidence/raw/mouzz-pro-1.3.7-auxiliary-strings.txt`. Les mentions A renvoient à cette extraction ; rechercher les intitulés ou clés cités. Elle contient des doublons d'architectures et des fragments d'instructions : une chaîne présente n'établit ni le chemin de code actif, ni le comportement réel.
- Repères vérifiés dans A : aide raccourcis sans défaut ligne 27250 ; aide webcam ligne 27114 ; aperçu captions ligne 27147 ; redécoupage captions ligne 27149 ; arrêt système ligne 27453 ; taux de mise à jour ligne 27647.
- **Observation auxiliaire B** : Python `plistlib`, lecture du conteneur universel avec `struct` et SHA-256, conservés dans `evidence/raw/mouzz-pro-1.3.7-auxiliary-observations.json`. Exécutable : `0b369afd08cf643e9fd64145dd47020bc39af159be4b491ad83590d21f3dfc3b`. Plist : version 1.3.7, build 11, identifiant `com.marceliseli.MouzzPro`, minimum macOS 13.0. En-têtes : x86_64 et arm64. Les ressources comprennent un WAV et Oswald-Bold / Montserrat-ExtraBold. Ce sont des faits de fichiers, pas des preuves de leur utilisation.
- **Site officiel S** : pages consultées et copies HTML dans `evidence/raw/mouzz-site-*.html` ; URL, heure et empreintes dans `evidence/raw/mouzz-site-provenance.json`. Ce sont des déclarations de l'éditeur, distinctes des faits REA et de l'observation directe.
- **Limite d'outil** : REA 6.8.0 `inspect-plist` essayé une fois ; réponse `capability_unavailable`, hôte macOS exigé. La réponse est conservée dans `evidence/raw/mouzz-pro-1.3.7-plist.evidence.json` ; malgré son nom, ce fichier d'erreur ne constitue pas une Evidence REA réussie. Aucun moteur natif installé pour contourner cette limite.

## Valeurs et interactions suffisamment documentées

Les valeurs suivantes sont **annoncées** par le texte d'aide embarqué ou par le site, pas mesurées sur une session vierge :

| Élément | Valeur ou règle | Source |
| --- | --- | --- |
| Raccourcis globaux | Aucun par défaut ; réinitialiser efface les raccourcis personnalisés | A |
| Clic 1.3.7 | Single Ring devient le défaut ; Ripple Wave comporte trois anneaux ; épaisseur réglable | [Changelog 1.3.7](https://mouzz.dev/pro/changelog) |
| Outil d'annotation | Dernier outil mémorisé ; initialement stylo | [Changelog 1.3.6](https://mouzz.dev/pro/changelog) |
| Second déclencheur d'annotation | Right Option désactivé initialement ; Fn reste disponible | A |
| Webcam | Désactivée à chaque lancement ; miroir et aimantation aux coins activés par défaut ; position mémorisée | A |
| Touches | Modifiers Only activé par défaut ; caractères groupés, rupture après une seconde sans frappe ; au maximum cinq étiquettes ; taille 16–120 points et durée 1–5 secondes annoncées | [Touches](https://mouzz.dev/features/keystroke-visualizer) |
| Spotlight | Hold a Key to Show et Turn Off When Moving mutuellement exclusifs ; délai de maintien et distance d'extinction configurables | A |
| Déclenchement par tap | Un appui isolé produit l'effet ; combinaison avec touche, clic ou défilement ignorée ; un même modificateur peut faire tap=clic / maintien=spotlight | A |
| Zoom | Le bouton d'activation arme les raccourcis ; il faut ensuite Toggle Live Zoom pour grossir ; Escape ou bouton Exit pour sortir | A ; [Changelog 1.1.0](https://mouzz.dev/pro/changelog) |
| Capture | Dossier initial Movies/Mouzz ; 30 ou 60 i/s ; marche même si le bouton maître est coupé | A ; [Enregistrement](https://mouzz.dev/features/screen-recorder) |
| Sous-titres | Changer Words/caption redécoupe le texte et réinitialise les corrections ; transcription des prises depuis la piste micro | A |

Les autres valeurs initiales, bornes et pas de curseurs ne sont **pas établis**. Ne pas transformer une constante, une clé de préférence ou une valeur choisie pour Jerry en défaut observé de MOUZZ.

## Énumérations utilisables pour l'interface

| Groupe | Valeurs | État de preuve |
| --- | --- | --- |
| Spotlight | `circle`, `horizontalBar`, `verticalBar` | Identifiants A ; trois formes confirmées par [comparatif](https://mouzz.dev/pricing) |
| Traînées | `particles`, `glow`, `solid`, `dots`, `dashed`, `stars`, `kawaii` | Identifiants A ; sept styles confirmés par comparatif. Les fragments UI A suggèrent Particles, Glow Effect, Solid Line, Dots, Dashed Line, Stars, Kawaii : intitulés reconstruits, pas tous intégralement extraits |
| Clics | `singleRing`, `ripple`, `ringBurst`, `colorFlash`, `particleBurst`, `glowPulse`, `scaleBounce` | Identifiants A ; fragments UI A suggèrent Single Ring, Ripple Wave, Ring Burst, Color Flash, Particle Burst, Glow Pulse, Scale Bounce |
| Touches | Classic, Glass, Keycaps, Neon, Mouzz Gradient, Minimal | Noms exacts confirmés par [Changelog 1.2.0](https://mouzz.dev/pro/changelog) ; fragments UI A |
| Sous-titres | Clean Bar, Bold Outline, Minimal, Word Fill, Word Pop, Slide Up | A ; [Sous-titres](https://mouzz.dev/features/auto-captions) ; trois derniers animés |
| Polices sous-titres | system, montserrat, oswald, avenirCondensed, futura, impact, arialBlack | Identifiants A ; seules Oswald et Montserrat sont confirmées embarquées ; disponibilité des autres à vérifier |
| Webcam | Circle / carte 16:9 arrondie ; bordure None, White, Mouzz Gradient | A et 26956–26963 |
| Sortie vidéo | Native, 720p, 1080p, 4K ; Fit, Crop, Stretch | A ; [Enregistrement](https://mouzz.dev/features/screen-recorder) |

## Précisions par exigence initiale

Les cellules A sont des textes et clés de réglage présents dans le binaire. Les cellules S sont des annonces du site. Toutes restent à valider dans Jerry et, lorsque possible, face à l'original.

| ID | Contrôles et comportements documentés / lacunes |
| --- | --- |
| CUR-01 | Trois formes ci-dessus ; barres pour ligne ou colonne (A). |
| CUR-02 | Taille, teinte, opacité de zone et assombrissement du fond distincts ; épaisseur de barre (A clés `spotlightBarThickness`, `spotlightOpacity`, `spotlightBackgroundOpacity`). [Spotlight S](https://mouzz.dev/features/spotlight-effect). Bornes inconnues. |
| CUR-03 | Maintien avec choix de modificateur + délai ; extinction après distance ; modes exclusifs. Via menu, le déplacement pour revenir au contenu peut déjà éteindre le spotlight (A). |
| CUR-04 | Clé `spotlightHideCursor` présente (A). Restauration exacte du curseur inconnue. |
| CUR-05 | Ring Size, Thickness, Ring Color, Animation Speed, Animate Ring, Neon Glow suggérés par A ; neon confirmé [Changelog](https://mouzz.dev/pro/changelog). [Anneau S](https://mouzz.dev/features/ring-cursor) décrit un suivi plus ou moins lissé. |
| CUR-06 | Sept styles ; longueur, largeur, couleur, opacité, Fade Speed, Spawn Rate ; texte indiquant que taux plus bas = davantage d'icônes (A). [Traînée S](https://mouzz.dev/features/cursor-trail). Densité réelle inconnue. |
| CUR-07 | Sept identifiants d'effet, couleur/taille/vitesse/épaisseur ; Single Ring par défaut en 1.3.7. [Clic S](https://mouzz.dev/features/click-feedback). |
| CUR-08 | Show on Mouse Clicks, Show With a Key Tap et raccourci Show Click Effect ; tap isolé filtré (A). |
| CUR-09 | Default Sound / Custom Sound, choix d'un fichier et volume ; ressource `mouseclickadvanced.wav` (A, B). [Sons S](https://mouzz.dev/features/click-sounds) : capture des sons exige audio système activé. Formats personnalisés acceptés inconnus. |
| CUR-10 | Interrupteur et sensibilité ; agrandissement/flash bref annoncé (A). Seuil et animation inconnus. |
| ANN-01 | Freehand F, Arrow A, Rectangle R, Circle C, Line L ; barre en haut ; Escape quitte (A). |
| ANN-02 | Annotations activées puis Fn maintenu, Right Option facultatif, menu Start Drawing ou raccourci de bascule ; couleur et épaisseur dans barre et réglages (A). Undo et clear annoncés ; extraction ASCII ne conserve pas toutes les touches macOS. |
| ANN-03 | Clés `annotationAutoFadeEnabled`, `annotationAutoFadeDuration`, `annotationClearOnExit` ; distinction temporisation et sortie (A). |
| ANN-04 | Highlighter H avec bande translucide, hauteur/couleur propres et aimantation horizontale ; Text T cliquer/taper/Return, taille liée à l'épaisseur ; Callout N numérotage automatique et taille propre (A). |
| KEY-01 | Actions pour maître, chaque effet, flash seul, annotations, zoom bascule/+/-/loupe, focus, prise, captions, webcam (A). Aucun raccourci initial ; conflits signalés. Correspondances Windows à concevoir. |
| KEY-02 | Six styles ; couleur d'accent ; filtres Space/Delete/Return ; règles et bornes dans tableau précédent. Clavier actif respecté selon [Touches S](https://mouzz.dev/features/keystroke-visualizer). |
| ZOOM-01 | Armer puis déclencher ; Zoom Level, Animation Duration et Movement Speed (A). [Zoom S](https://mouzz.dev/features/live-zoom) : contenu vivant et clics utilisables. |
| ZOOM-02 | Auto-Zoom Level et Hold Duration ; maintien calculé après dernier clic, sortie automatique (A). |
| ZOOM-03 | Taille de lentille et grossissement (A) ; transparence aux clics et respect du maître selon [Loupe S](https://mouzz.dev/features/loupe). Éviter l'auto-capture récursive : correctif original décrit au changelog 1.3.1. |
| FOCUS-01 | Dim Amount, suivi du changement de fenêtre active ; assombrissement traversable aux clics et capturé (A). Définition précise de fenêtre active, fenêtres enfants et écrans inconnue. |
| REC-01 | Tous les effets visibles composités ; activation/désactivation pendant prise ; maître ne stoppe pas capture (A). [Enregistrement S](https://mouzz.dev/features/screen-recorder). |
| REC-02 | Écran sous curseur à la prise ; instant exact du choix pendant décompte non établi. |
| REC-03 | MP4 ; Native/720p/1080p/4K ; 30/60 i/s ; mise à l'échelle pendant capture. Bitrate exact inconnu ; symbole H264 présent, insuffisant pour prouver tous les exports. |
| REC-04 | Fit contient toute l'image avec bandes ; Crop remplit sans déformation en rognant ; Stretch déforme pour remplir (A). |
| REC-05 | Interrupteurs système/micro indépendants, pistes séparées ; certains lecteurs n'écoutent qu'une piste, aide annonçant voix prioritaire dans ce cas (A). |
| REC-06 | Input Device et dossier configurables, défaut Movies/Mouzz. Sauvegarder choix d'entrée et réagir à sa déconnexion à vérifier. |
| REC-07 | Décompte configurable ; HUD déplaçable/temps écoulé ; arrêt HUD/menu/raccourci ; notification ouvre dossier ou Add Captions (A). |
| REC-08 | HUD/décompte exclus ; finalisation partielle au changement d'écran/verrouillage/arrêt (A ; S). Garantie en crash brutal non démontrée. |
| CAM-01 | Voir défauts et formes ; choix caméra, taille, bordure, miroir, position mémorisée et aimantation ; resize attaché au coin, sinon au centre (A). |
| CAP-01 | Micro en direct, modèle local avec téléchargement préalable par langue ; Caption Size, Distance from Bottom, Clear After Silence, Fade-Out Duration (0 = instantané), Smooth Animation (A). Hors ligne après installation, selon [Sous-titres S](https://mouzz.dev/features/auto-captions). |
| CAP-02 | Import vidéo, Add Captions/Caption Last Recording, auto après prise ; éditer texte, minutages début/fin, clic timestamp lit cet instant ; changer découpage détruit corrections (A). |
| CAP-03 | Six styles cités ; Word Fill remplit progressivement, Word Pop rebondit/colore, Slide Up entre mot à mot (A). |
| CAP-04 | Words/caption, 1–3 lignes équilibrées, taille, police, fond, placement horizontal/vertical [Sous-titres S](https://mouzz.dev/features/auto-captions). |
| CAP-05 | MP4 nouveau fichier, SRT séparé ; choix fichier et nom suggéré `(Captioned).mp4` (A). Fidélité de prévisualisation animée contradictoire, ci-dessous. |
| APP-01 | Réglages Cursor Effects, Annotations, Keystrokes, Zoom & Loupe, Focus Mode, Recorder, Captions, Hotkeys, General, Advanced, Help, Support ; cases par entrée de menu ; au moins une présence menu/Dock pour rester accessible (A). Adapter au plateau Windows. |
| APP-02 | Persistance maître et états des effets confirmée [Changelog 1.2.0/1.3.6](https://mouzz.dev/pro/changelog), exception webcam off au lancement ; démarrage session présent A. |
| APP-03 | Plusieurs écrans annoncés [Spotlight S](https://mouzz.dev/features/spotlight-effect), performance annoncée [Changelog](https://mouzz.dev/pro/changelog). Aide mentionne un taux de mise à jour réglable (A). Aucun chiffre de consommation mesuré ici. |

## Écarts et inconnues à garder visibles

1. **Prévisualisation captions** : le site annonce un aperçu correspondant exactement à l'export ; l'aide de 1.3.7 distingue aperçu de texte/style/disposition et mouvement mot à mot dans l'export (A). Impossible de trancher sans exécuter l'original. Jerry doit documenter le niveau réellement implémenté.
2. **Zoom** : la page publique résume l'interrupteur comme déclencheur. Le changelog 1.1.0 et l'aide binaire décrivent explicitement une étape d'armement distincte. Cette seconde description est plus précise et liée à une version.
3. **Microphone** : Info.plist dit micro seulement pendant capture, alors que l'aide et les pages annoncent des captions live autonomes. La déclaration de permission paraît incomplète ; ne pas limiter l'usage Windows sur cette seule phrase.
4. Les pages généralistes présentent parfois un sous-ensemble ancien des réglages : spotlight circulaire seul, anneau sans neon, traînée seulement particules. Le comparatif, le changelog et les identifiants de 1.3.7 attestent des options supplémentaires. Pas de contradiction de code déduite de ces omissions.
5. La couverture Free/Pro vient du comparatif officiel ; seul le paquet Pro 1.3.7 a été inspecté. Aucune comparaison binaire avec Free.
6. Aucune valeur numérique initiale n'est déduite des chaînes : tailles, épaisseurs, opacités, délais, vitesse, zoom, seuil de secousse, débit vidéo et cadence d'overlay restent à établir. Les seules bornes publiques reprises ici concernent touches et lignes de captions.
7. Aucune preuve de rendu/animation, latence, qualité speech, débit réel, transparence des clics, comportement multi-DPI/multi-écran ou résistance aux interruptions sous Windows. Les exclusions du décompte/HUD exigent un contrôle du fichier capturé, pas seulement du code.
8. À ajouter aux critères des 37 lignes : undo/clear et raccourcis locaux d'annotation, aimantation highlighter, réinitialisation des réglages, menu configurable, conservation des états avec exception caméra, activation automatique du maître en allumant un effet, sélection des touches spéciales, choix des polices captions, téléchargement du modèle, et taux de mise à jour. Ce sont des sous-critères découverts, sans prétention d'exhaustivité.
