# Vérification des composants Windows

Essais réels du 27 octobre 2026, avec .NET 10 sur Windows x64. Ce document décrit
les observations de Jerry ; aucune comparaison d’exécution avec MOUZZ sur macOS
n’a été possible.

## Harnais reproductible

`tests/Jerry.Windows.Tests/Jerry.Windows.Tests.csproj` référence le projet réel
`Jerry.App`. Il crée une `Application` WPF et son Dispatcher sur un thread STA,
construit `AppController` avec un dossier de réglages `%TEMP%` unique, puis montre
sa vraie `MainWindow` avec `background: false`. Il utilise les propres contrôles
WPF de l’application et leurs propriétés ; aucun SendInput, UI Automation,
contrôle de bureau externe ou simulation d’entrée n’est employé.

Depuis la racine du dépôt, sous Windows :

```text
dotnet run --project tests/Jerry.Windows.Tests/Jerry.Windows.Tests.csproj
```

Un premier argument facultatif choisit le dossier des résultats ; par défaut :
`evidence/raw/windows-component-smoke`. Un échec produit un code de sortie non nul.
Le harnais est un programme d’intégration matériel, pas un projet de tests unitaires
qui s’exécute implicitement sur toute machine.

La séquence vérifie la construction réelle du contrôleur et l’installation de ses
hooks natifs sans exception. Elle sélectionne chaque page par le `ListBox` réel,
parcourt son `ScrollViewer` et rend le véritable arbre WPF avec `RenderTargetBitmap`.
Les fenêtres d’effets sont ensuite créées indépendamment avec des échantillons de
curseur exprimés en pixels physiques et les modèles de scène publics. Les fenêtres
créées pour ces tests sont fermées ; `AppController.Quit()` exécute sa libération
normale sur le thread UI. Le retour de `Application.Run()` et la disparition des
processus enfants possédés sont contrôlés.

## Résultats observés

Dernier lancement complet : **code de sortie 0**. Résultats structurés dans
`evidence/raw/windows-component-smoke/results.json`, journal `run.log` dans le même
dossier. Le journal de lancement est `evidence/raw/windows-component-smoke-launch.log`.
Le bureau testé a un écran physique **2560×1600**, avec mise à l’échelle **150 %**.

| Vérification | Résultat réel |
| --- | --- |
| Contrôleur, hooks, fenêtre principale visible | Construction et affichage réussis |
| Dix pages de réglages | Toutes construites, texte rendu et images non vides |
| Captures de pages avec défilement | 25 images `ui-XX-YY.png` |
| Spotlight | 3 formes, images distinctes et non vides |
| Traînées | 7 styles, images distinctes et non vides |
| Clics | 7 styles, images distinctes et non vides |
| Annotations | 8 outils, images distinctes et non vides |
| Affichage des touches | 6 styles, images distinctes et non vides |
| HUD d’enregistrement | Affinité native effective `WDA_EXCLUDEFROMCAPTURE = 0x11` |
| Sauvegarde des réglages isolés | Fichier JSON effectivement créé |
| Arrêt normal | Quit exécuté, processus possédés libérés, sortie 0 |

Les six styles de sous-titres live ont tous produit une image non vide, mais
seulement **trois images distinctes**. CleanBar, WordFill, WordPop et SlideUp
partagent actuellement une présentation live statique ; la partie export minuté
traite les animations de mots. Cette observation est enregistrée explicitement
et n’est pas présentée comme six animations live distinctes.

Le lancement a également signalé l’avertissement de compilation préexistant
`CS0108` : `MainWindow.Cursor()` masque la propriété `FrameworkElement.Cursor`.
Il ne s’agit pas d’un échec d’exécution ; le propriétaire de l’interface a été informé.
Les images sont destinées à une inspection visuelle complémentaire : les assertions
sur les pixels ne prouvent pas à elles seules l’absence de texte coupé ou le confort
d’utilisation de chaque page.

## Vidéo réelle : contenu des effets et exclusion des contrôles

Le harnais montre un fond opaque de test, un marqueur vert visible, un contrôle
magenta exclu par `WindowTools.ExcludeFromCapture`, puis une **vraie `EffectWindow`**
contenant une annotation rectangle cyan. Les rectangles sont placés en coordonnées
physiques connues dans une zone de bureau de 1200×700. Le fond de test couvre cette
zone pour éviter d’attribuer les couleurs d’applications étrangères à ces marqueurs.

`RecordingService` encode environ deux secondes de cette zone en H.264/MP4 natif,
sans audio pour cet essai précis. FFmpeg extrait ensuite une image vers
`encoded-fixture-frame.png`. Les couleurs sont comptées avec des seuils tolérant
la compression, uniquement dans les régions attendues :

| Région de la vidéo encodée | Pixels détectés | Assertion |
| --- | ---: | --- |
| Intérieur du marqueur vert, 100×100 | **10 000** verts | Fenêtre visible réellement capturée |
| Intérieur du contrôle magenta exclu, 100×100 | **0** magenta | Contrôle effectivement absent |
| Périmètre de l’annotation cyan | **14 297** cyan | Rendu de `EffectWindow` effectivement capturé |

L’affinité native du contrôle magenta est aussi interrogée et vaut `0x11`.
L’image extraite et `fixture-probe.json` proviennent du fichier réellement encodé,
pas d’un rendu WPF utilisé à sa place. `fixture-effect-before-capture.png` et
`fixture-control-before-capture.png` conservent les rendus des composants avant
capture. Le chemin du MP4 local figure dans `results.json` ; les médias restent
sous `%LOCALAPPDATA%\Jerry\qa\<identifiant>` avec leurs originaux récupérables.

Un premier lancement avait masqué l’effet cyan : le harnais remettait son fond
opaque au sommet **après** avoir montré l’effet. Remettre l’effet au sommet en
dernier a corrigé cette erreur de fixture. Aucune modification de production du
rendu ou du moteur de capture n’a été nécessaire pour obtenir le résultat ci-dessus.
Le premier journal et l’image sont conservés sous
`evidence/raw/windows-component-smoke-first.log` et
`encoded-fixture-first-frame.png` pour ne pas confondre ce résultat initial avec
le lancement valide.

## Audio, caméra et événements de service

Le harnais matériel média séparé a aussi été relancé après les corrections
d’intégration. Journal : `evidence/raw/media-smoke.log`.

- Microphone Realtek sélectionné par identifiant ; pistes AAC « Son système » et
  « Microphone » séparées. `ffprobe` confirme **default=0** pour le système et
  **default=1** pour le microphone. Le test de régression du choix par défaut a
  d’abord échoué, puis réussi après ajout des dispositions explicites.
- `WebcamService.RunningChanged` a émis exactement **true, false**, malgré un arrêt
  répété. La caméra intégrée a livré **86 trames BGRA réelles**, puis s’est arrêtée
  en **477 ms** au dernier essai. Le mode DirectShow compatible est utilisé car
  l’initialisation WinRT du pilote échoue sur cette machine.
- `RecordingChanged(false)` est émis après la finalisation ; lors de cet événement,
  `LastOutputPath` contenait déjà le même chemin que le résultat de `StopAsync`.
  Start/Stop répétés ont conservé une seule transition dans chaque sens.
- Le dernier fichier mesurait 2,900 s de vidéo et 2,682292 s pour chacune des deux
  pistes AAC. La précision de synchronisation doit encore être mesurée avec un
  signal audiovisuel commun ; cet écart de durées ne mesure pas directement le
  retard exact des événements à la lecture.

Suite unitaire Core complète lors de ce passage : **39/39 réussites**, journal
`evidence/raw/media-core-suite.log`. Ces tests et essais ne remplacent pas une
validation ergonomique complète de l’application.

## Limites conservées

Non vérifiés dans ce passage : plusieurs moniteurs réels, origines négatives sur
le bureau réel, mélange de facteurs DPI, changement de configuration d’écran,
raccourcis déclenchés au clavier réel, session Windows verrouillée, déconnexion
matérielle caméra/microphone, performances prolongées à 4K/60 FPS, zoom de bureau,
comportement d’autres fenêtres topmost et comparaison visuelle macOS. Les tests
purs couvrant certaines coordonnées ne sont pas présentés comme des essais
multi-écrans matériels.

Aucune autorisation Windows, préférence de démarrage ou connexion cloud n’est
modifiée par ce harnais. Aucun raccourci global n’est activé dans ses réglages
isolés. Les fichiers de preuve et images brutes restent exclus de Git.
