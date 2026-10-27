# Rendu des effets Jerry

Implémentation native WPF dans `src/Jerry.App/Effects/`. Pas de capture macOS de référence disponible : les animations sont une réalisation Windows des fonctions annoncées, sans prétention d’identité visuelle.

## Contrat d’intégration

- `EffectScene(AppSettings)` détient les états ; tous les appels doivent se faire sur le thread UI. `Move`, clic, locate, dessin, touches et captions enregistrent leurs instants avec **Environment.TickCount64**. Appeler `Tick(Environment.TickCount64)` avec la même horloge, puis `EffectWindow.Refresh()` sur chaque écran.
- Le contrôleur doit démarrer son timer après une mutation si `NeedsAnimation` est vrai et l’arrêter lorsque `Tick` retourne faux. Une fréquence `Settings.UpdateRate` est suffisante. Un anneau statique au repos ne maintient pas de timer ; `Ring.Animate` active volontairement la pulsation continue.
- `Drawing=false` termine le trait en cours puis applique `Annotation.ClearOnRelease`. `EndStroke()` seul ne quitte pas le mode dessin. Les coordonnées des appels de dessin, `Move` et `FocusBounds` sont en pixels physiques globaux, y compris négatifs.
- Les propriétés de réglage sont lues à chaque rendu, mais couleur/outil/largeur de dessin et style/couleur/taille/vitesse des clics sont copiés à leur création. `EffectScene` conserve la référence AppSettings passée au constructeur ; un reset remplaçant l’objet doit recréer la scène, ou muter l’objet existant.
- `EffectWindow` occupe le `PixelRect` du moniteur avec `WindowTools.Position`, utilise `MakeOverlay` (traversable aux clics, sans activation), et convertit les coordonnées physiques avec le facteur DPI de son propre visual WPF. Il faut recréer les fenêtres au changement de topologie écran.
- Caméra : instancier `CameraOverlayWindow(settings)`, appeler `MoveToMonitor` avec le moniteur choisi, puis livrer les vrais `WebcamFrame` sur `UpdateFrame`. Les frames sont PBGRA32, copiées dans un BitmapSource gelé. Les appels hors thread sont redispatchés. Le contrôleur reste propriétaire du service caméra et de son arrêt.
- `PositionChanged` de la caméra signale la fin du déplacement : sauvegarder alors les réglages. `Camera.X/Y` sont des fractions de l’espace disponible pour le **coin supérieur gauche** (dimensions du moniteur moins dimensions de la bulle). Une carte prend la largeur `Camera.Size`, hauteur 9/16 de cette largeur. L’aimantation fonctionne dans une zone de 100 pixels près d’un coin.
- Aucune fenêtre d’effet ni caméra n’utilise `ExcludeFromCapture`. Les fenêtres de contrôle restent exclues par le module UI.

## Rendus réalisés

- Focus : écran assombri hors rectangle de la fenêtre active ; absence de rectangle = assombrissement entier.
- Spotlight : trou cercle/barre horizontale/barre verticale, fond coloré et opacité indépendante, teinte/opacité de la zone éclairée. Le contrôleur fournit l’état effectif pour les règles maintien/extinction.
- Anneau : rayon, épaisseur, opacité, couleur, lissage de suivi et pulsation, couches de halo néon.
- Traînées : particules dispersées, lueur, ligne, points, pointillés, étoiles et cœurs Kawaii avec yeux ; intervalle d’apparition, longueur maximale, largeur, opacité et expiration.
- Clics : anneau simple, **trois** ondes différées, éclat d’anneau/rayons, flash, éclat de particules, pulsation lumineuse et rebond. La position de chaque clic est immuable ; l’expansion ne dérive pas avec le pointeur. Le rendu est indépendant du bouton de souris.
- Localisation : anneau expansif et repère en croix, durée une seconde.
- Annotations : dessin libre, flèche à pointe, rectangle, ellipse, ligne, surligneur translucide avec alignement horizontal selon l’angle, texte explicite et numérotation ; annuler, effacer, expiration et fondu. Les tailles du surligneur et du numéro sont distinctes. L’édition du texte est effectuée en amont par le contrôleur ; la scène reçoit `AddText`.
- Touches : cinq étiquettes au maximum, texte ordinaire groupé tant que les frappes restent espacées de moins d’une seconde, durée réglable et apparition/disparition ; Classic, Glass, Keycaps, Neon, MouzzGradient et Minimal. Filtres modificateurs et touches spéciales appliqués aussi dans la scène.
- Captions live : bandeau local, dernières paroles limitées au nombre de mots, police/couleur/fond/position/lignes, durée après parole et fondu. L’entrée `SetCaption(string)` ne fournit **aucun minutage par mot** : aucune synchronisation Word Fill/Pop/Slide Up n’est inventée pour ce bandeau. Les six styles synchronisés à des timestamps appartiennent au document de sous-titres et au module éditeur/export. Les styles live CleanBar/BoldOutline/Minimal distinguent déjà les présentations statiques.
- Caméra : vraie image couleur, cadrage remplissant le cercle ou la carte arrondie, miroir, bordure blanche/dégradée/aucune, glisser sans activation de fenêtre et aimantation ; position/forme/taille recalculées au fil des frames.

## Limites et vérifications

La capture, les sons, les permissions, les raccourcis, le masquage du curseur, le déclenchement des secousses et la magnification native restent sous la responsabilité du contrôleur et de Platform. Les overlays touches/captions se placent sur le moniteur contenant le curseur ; les annotations sont rendues dans leurs coordonnées globales sur tous les moniteurs concernés.

La compilation complète est coordonnée par l’agent racine. À vérifier réellement sur Windows : transparence aux clics, origine négative, passage entre DPI, rendu à haute fréquence, absence de dérive des clics, cycle complet dessin/texte, fermeture/clear/undo, touches internationales, capture contenant caméra/effets mais pas les contrôles, repositionnement de la webcam après redimensionnement et arrêt/reprise des services. Aucun test de rendu ou mesure de performances n’est revendiqué avant ces vérifications.

## Corrections après revue

`AddClick(int button = 1, bool force = false)` autorise le flash explicite même si les effets de clic automatiques sont désactivés, tout en respectant le maître. Le surligneur copie sa propre couleur `HighlighterColor` au début de chaque trait. `Captions.SmoothAnimation` contrôle apparition et disparition : faux = affichage immédiat et retrait au délai de silence ; vrai = apparition de 120 ms puis fondu de sortie configuré. Ces transitions ne prétendent pas disposer de minutages de parole.
