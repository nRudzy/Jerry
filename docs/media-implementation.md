# Médias Windows Jerry

Implémentation du 27 octobre 2026. Choix de réalisation Jerry ; aucune preuve
d’exécution de MOUZZ ni comparaison macOS n’est ajoutée par ce travail.

## Contrats pour l’interface

Dans `Jerry.App.Media` :

- `ToolchainService.EnsureFfmpegAsync(IProgress<double>?, CancellationToken)` et
  `EnsureFfprobeAsync(CancellationToken)` renvoient le chemin absolu de l’outil.
  `FindFfmpeg()` consulte uniquement l’installation privée de Jerry.
- `RecordingService(AppSettings, ToolchainService)` expose `IsRecording`,
  `StatusChanged`, `RecordingChanged`, `StartAsync(PixelRect, CancellationToken)`
  et `StopAsync(CancellationToken)`. L’arrêt renvoie le MP4 final ; un nouvel
  arrêt renvoie le même chemin. Un démarrage pendant une capture active est ignoré.
  Les transitions sont sérialisées, y compris la finalisation.
- `RecoveryDirectory` expose le dossier des originaux ; `OwnedProcessId` expose
  uniquement le processus de capture créé par cette instance.
- `DeviceEnumeration.GetMicrophones()` fournit les identifiants WASAPI et noms
  des microphones actifs. Un identifiant vide sélectionne le microphone Windows
  de communications par défaut. Un identifiant sélectionné indisponible échoue.
- `DeviceEnumeration.GetCamerasAsync()` fournit les groupes de sources caméra.
  `WebcamService.StartAsync(string, CancellationToken)` utilise exactement ce
  type d’identifiant. `StopAsync()` libère le lecteur et la caméra.
  Le chemin principal est MediaCapture/MediaFrameReader CPU ; lorsque le pilote
  refuse ce chemin avec une erreur COM autre qu’un refus d’autorisation, le mode
  compatible FFmpeg DirectShow fournit des trames BGRA 640×360 avec recadrage 16:9.
  L’identité PnP est comparée avant la catégorie d’interface pour retrouver le même
  périphérique malgré les GUID distincts de WinRT et DirectShow ; un nom ambigu
  provoque une erreur. `OwnedProcessId` identifie le processus compatible possédé.
  Les erreurs de ce chemin sont conservées dans `%LOCALAPPDATA%\Jerry\logs`.
  Un refus d’accès caméra ne déclenche pas ce repli.
- `WebcamService.FrameReady` livre des `WebcamFrame` BGRA 32 bits prémultipliés,
  `Stride = Width * 4`, pixels possédés par chaque trame. L’interface gère la
  forme, le miroir, la position et la répartition sur ses fenêtres.
- Les événements peuvent arriver hors du thread WPF : l’interface doit les
  transférer via son Dispatcher. `WebcamService.StatusChanged` indique les erreurs
  matérielles après le démarrage ; les erreurs de démarrage sont aussi levées.

Le décompte, la sélection d’écran, les fenêtres de contrôle exclues de la capture
et l’arrêt général de l’application appartiennent à l’interface. Le service
n’utilise pas `AppSettings.Enabled` : couper les effets ne coupe pas la capture.

## Capture et finalisation

FFmpeg `gdigrab` reçoit un rectangle en pixels physiques, origines négatives
comprises. Le bureau composé contient les effets/fenêtres rendus ; le code FFmpeg
utilise `BitBlt(..., SRCCOPY | CAPTUREBLT)` pour les fenêtres superposées. Les
fenêtres devant être exclues doivent recevoir l’affinité Windows appropriée dans
le module Platform. Le filtre de `RecordingGeometry` applique la résolution et
le cadrage choisis ; FPS validés entre 1 et 120, H.264 x264, yuv420p, mode de faible
latence. Les arguments sont transmis séparément, sans interprétation par un shell.

Le système et le microphone sont capturés par NAudio WASAPI dans deux WAV. Le
chronomètre démarre à la première image annoncée par FFmpeg ; les échantillons
antérieurs sont écartés. Les absences de paquets loopback pendant le silence sont
remplies de zéros et la fin est complétée jusqu’à la durée d’enregistrement.
Une tolérance de 100 ms absorbe la variation des callbacks WASAPI. Cette stratégie
est une synchronisation approximative sur horloge monotone ; sa précision réelle
reste à mesurer sur le matériel. Le microphone ne passe pas par le périphérique
de sortie, évitant son mélange dans la piste système par l’application.

L’arrêt envoie `q` au processus enfant possédé, attend au plus 15 secondes, puis
interrompt uniquement cet arbre de processus si nécessaire. Le MKV intermédiaire
reste récupérable lors d’une interruption, sous réserve de données déjà écrites.
La finalisation remuxe sa vidéo et encode chaque WAV en une piste AAC distincte
nommée « Son système » et « Microphone ». Le fichier MP4 n’est déplacé vers son
nom final qu’après une sortie FFmpeg réussie et un fichier non vide.

Les erreurs audio, l’arrêt inattendu de FFmpeg et les changements de configuration
d’écran provoquent une finalisation avec statut explicite. Un échec de finalisation
lève une erreur et conserve les originaux. Aucun processus étranger n’est recherché
ou interrompu par son nom.

Chaque session conserve dans `<sortie>/.Jerry-sessions/<identifiant>/` :

- `screen.mkv`, `system.wav` et/ou `microphone.wav` ;
- `capture.log`, `mux.log`, `session.json` et `owned-process.json` ;
- `result.json` après finalisation ; `finalizing.mp4` si interrompue à ce stade.

Les WAV sont vidés et leur en-tête actualisé à chaque paquet. Les originaux restent
également après une réussite : leur suppression est manuelle. Une restauration
après crash n’est pas automatique ; les fichiers et les journaux permettent un
remuxage avec FFmpeg. La limite WAV classique d’environ 4 Go par piste reste une
limite pour les enregistrements très longs ; une erreur arrête et conserve la capture.

## Outils et sources primaires

Installation privée dans `%LOCALAPPDATA%\Jerry\tools\ffmpeg`. Aucun droit
administrateur ni installation globale. Téléchargement HTTPS de l’archive ZIP
release essentials de Gyan, distribution explicitement liée par FFmpeg ; contrôle
SHA-256 contre le fichier publié par ce même distributeur avant extraction.
Le fichier `provenance.json` conserve URL, empreinte d’archive et date. Le contrôle
ne constitue pas une signature indépendante du distributeur. Les exécutables et
la licence fournie sont conservés ; aucun média n’est envoyé à un service distant.

Sources consultées :

- [Distributions FFmpeg](https://ffmpeg.org/download.html)
- [Builds Gyan et empreintes](https://www.gyan.dev/ffmpeg/builds/)
- [Code source gdigrab](https://github.com/FFmpeg/FFmpeg/blob/master/libavdevice/gdigrab.c)
- [NAudio 2.2.1 WasapiCapture](https://github.com/naudio/NAudio/blob/v2.2.1/NAudio.Wasapi/WasapiCapture.cs)
- [NAudio 2.2.1 loopback](https://github.com/naudio/NAudio/blob/v2.2.1/NAudio.Wasapi/WasapiLoopbackCapture.cs)
- [Microsoft MediaFrameReader](https://learn.microsoft.com/en-us/windows/apps/develop/camera/process-media-frames-with-mediaframereader)

## Validation réellement exécutée

`RecordingMediaTests` : sept cas réussis après cycles rouge/vert (six cas initiaux,
plus régression sur les noms MP4 et correspondance d’identité caméra). Vérifications
de coordonnées négatives, FPS, chemins contenant des espaces, refus des rectangles
invalides, pistes audio indépendantes, silence initial/intermédiaire/final et deux
caméras portant le même nom. Suite complète Jerry.Core : **36/36 réussites**, journal
`evidence/raw/media-core-suite.log`.

Une compilation Windows de l’ensemble des fichiers média a réussi sans erreur
ni avertissement (journal d’intégration `evidence/raw/app-build.log`). Un harnais
Windows indépendant compile également directement les dernières sources média,
sans modifier le projet de l’application : dernière compilation réussie avec
**0 avertissement, 0 erreur**, journal `evidence/raw/media-build.log`.
Essais réellement exécutés :

- acquisition privée de FFmpeg, contrôle SHA-256 et lancement de ffprobe ;
- énumération du microphone Realtek et de la caméra intégrée ;
- capture d’un bureau physique 2560×1600 en H.264 1280×720 ;
- choix de l’identifiant Realtek et deux pistes AAC distinctes nommées
  « Son système » et « Microphone », confirmées par ffprobe ;
- boucle système entièrement silencieuse : données WAV toutes nulles, durée
  conservée après finalisation ;
- Start/Stop répétés : un seul enregistrement et même chemin MP4 final ;
- caméra intégrée : échec natif MediaCapture `0x80070490` reproduit en modes
  SharedReadOnly et ExclusiveControl, tandis que le même périphérique fonctionne
  dans DirectShow. Le repli a ensuite fourni **86 vraies trames BGRA** ; arrêt
  répété et libération du processus en **482 ms** au dernier essai.

Le premier essai a révélé environ 0,96 s de différence de durée audio/vidéo dans
la compilation initiale. Après horloge liée à la première image et faible latence,
le dernier MP4 mesure 2,866 s pour la vidéo et 2,697667 s pour chacune des pistes
AAC. Cette incertitude résiduelle d’environ **0,17 s** doit être réduite/mesurée
avant toute revendication de synchronisation précise. L’essai ne mesure pas un
signal audiovisuel commun, donc l’écart de durées n’établit pas à lui seul le
retard exact des événements à la lecture. Les noms MP4 utilisent `handler_name` ;
`title` seul n’était pas conservé par le conteneur.

Journaux bruts exclus de Git : `evidence/raw/media-smoke.log`,
`evidence/raw/media-smoke-first.log`, `evidence/raw/media-camera-diagnostic.log`.
Les essais interactifs de l’application complète sont intégrés par l’agent principal.
Restent à documenter : autorisations et débranchement de caméra, précision A/V
sur signal commun, audio système passant de silencieux à actif, sélection
multi-écrans et DPI mixtes, contenu des effets et exclusion effective des contrôles,
charge CPU à 4K/60 FPS, interruption de capture et lecture interactive du MP4 avec
sélection de chaque piste. Aucune comparaison d’exécution macOS n’a été possible ;
aucune fidélité exhaustive ou parité à 100 % n’est revendiquée.
