# Sous-titres locaux Jerry — implémentation et vérifications

État du 27 octobre 2026. Ce travail implémente CAP-01 à CAP-05 du périmètre
Windows ; il ne prouve pas une parité exhaustive avec MOUZZ/macOS.

## Fonctionnement livré

`CaptionService` utilise réellement Whisper.net et Whisper.net.Runtime **1.9.0**,
en CPU x64, sans service de transcription distant. Les modèles multilingues
`base` (défaut, environ 142 Mio) et `tiny` (environ 75 Mio) sont téléchargés depuis
la distribution whisper.cpp officielle dans `%LOCALAPPDATA%\Jerry\models`.
Chaque fichier est vérifié contre le SHA-1 publié par whisper.cpp ; une fiche
locale conserve aussi son URL, sa date et son SHA-256. Le chargement, le progrès,
l’annulation et les erreurs sont exposés à l’interface.

Les médias importés sont convertis localement en WAV mono 16 kHz. FFprobe choisit
explicitement la piste portant une étiquette microphone/voix ; à défaut, la
piste audio déclarée par défaut, puis la première piste audio. Les fichiers
Jerry avec pistes `Son système` et `Microphone` privilégient donc le microphone,
y compris les anciennes captures où le système était la piste par défaut.
Le choix est conservé dans `audio-selection.json` avec les fichiers de travail.

Les horodatages des tokens Whisper, exprimés en unités de 10 ms, sont assemblés
en mots en conservant les sous-mots, les apostrophes et la ponctuation. Si
Whisper ne fournit pas un alignement exploitable pour un segment, Jerry
répartit son texte sur l’intervalle et marque ces temps comme **estimés**.
Il n’annonce pas ces valeurs comme un alignement acoustique certain.

Le live utilise le microphone WASAPI choisi dans les réglages audio, avec
rééchantillonnage mono 16 kHz, blocs de quatre secondes et file bornée à deux
blocs. Les blocs anciens sont abandonnés si la reconnaissance ne suit pas.
Les blocs silencieux sont ignorés. L’arrêt annule le traitement courant et
libère le microphone ; aucun fichier audio live n’est envoyé ou conservé.

L’éditeur WPF permet l’import, la lecture/recherche dans le média, la correction
des lignes et de leurs débuts/fins, et l’édition de chaque mot et de son
minutage. Les mots manuels doivent rester ordonnés, sans chevauchement, dans
l’intervalle de leur ligne. Changer seulement le minutage d’une ligne conserve
et redimensionne ses temps de mots ; changer son texte recalcule des temps
estimés. Les cellules et exports refusent les intervalles invalides.

Les projets `.jerrycaptions.json` conservent le média, les corrections et les
segments Whisper d’origine. Changer le nombre de mots par sous-titre regroupe
immédiatement le document et **réinitialise les corrections** à cette référence
d’origine. Pour les anciens projets sans référence enregistrée, le document
courant sert de première référence ; le texte original perdu ne peut pas être
reconstitué. Position, police, taille, couleurs, fond et une à trois lignes
sont réglables. Langue et modèle s’appliquent à la prochaine transcription.

Les six styles sont rendus dans le MP4 par ASS/libass : `CleanBar` (fond par
ligne), `BoldOutline`, `Minimal`, `WordFill` (balayage mot par mot respectant
les silences), `WordPop` (accent et mise à l’échelle du mot actif), `SlideUp`
(entrée et fondu indépendants de chaque mot à son horodatage). L’éditeur
prévisualise leurs principaux effets ; le balayage WordFill y est discret,
et les métriques WPF peuvent différer de libass. Le MP4 constitue le rendu
final. Les déclarations publiques et l’aide originale divergent sur la
présence des animations dans l’aperçu ; Jerry propose cet aperçu utile sans
prétendre reproduire exactement l’original.

Le SRT est UTF-8 avec temps en millisecondes et texte protégé contre le balisage.
Dans ASS, les accolades et antislashs saisis sont remplacés par leurs formes
pleine chasse pour empêcher l’interprétation de commandes de rendu.
L’export MP4 conserve les pistes audio, encode en H.264/AAC et utilise un
fichier intermédiaire. Les arguments de processus sont séparés ; le filtre ASS
emploie un nom relatif dans son dossier de travail, ce qui évite les problèmes
de chemins avec espaces et apostrophes. Le média original est protégé contre
un export au même emplacement. En cas d’échec, les journaux, ASS/SRT et fichiers
intermédiaires restent récupérables dans `%LOCALAPPDATA%\Jerry\work\captions`.

## Vérifications exécutées

- Tests Core écrits avant l’implémentation des formats et du recalage : 13
  échecs attendus observés, puis passage au vert. Tests supplémentaires
  observés rouges puis verts pour l’édition des mots, le regroupement qui
  rétablit l’original, le choix de la piste micro et le SlideUp par mot.
- Suite complète `Jerry.Core.Tests` : **51 tests réussis** lors du dernier
  passage avant l’intégration finale de l’application. Couverture des six
  styles, du passage d’heure SRT, de l’échappement, des silences karaoke, du
  recalage et des refus atomiques de chevauchement.
- Harnais Windows x64 séparé compilant les sources réelles des sous-titres,
  l’éditeur, Core et `ToolchainService`. Compilation des sources de ce module
  réussie. Le build complet de Jerry est vérifié par l’agent d’intégration.
- Reconnaissance réelle de l’échantillon public JFK : `tiny` a produit quatre
  lignes et 22 mots, sans recours au recalage estimé dans cet essai. `base`
  a aussi reconnu les 22 mots ; certaines lignes nécessitaient des temps
  estimés, signalés comme tels.
- Export réel des six styles, tous avec des contenus SHA-256 distincts ;
  images extraites et examinées pour le fond CleanBar, Minimal et WordFill.
  Un premier essai a révélé un fond CleanBar invisible avec une bordure ASS
  nulle ; sa correction a été vérifiée par rendu réel et test de régression.
- Fixture MP4 à deux pistes : système sinusoïdal déclaré par défaut et JFK
  étiqueté `Microphone` en index 2. Jerry a sélectionné explicitement l’index 2
  et reconnu la parole, sans transcrire la piste système.
- Regroupement réel après correction, puis sérialisation/rechargement du
  projet : texte original et horodatages d’origine rétablis.
- SlideUp rendu sur une phrase de deux mots : à 0,7 s seul « Un » est présent ;
  à 1,3 s « deux » entre plus bas et partiellement transparent ; à 1,7 s les
  deux mots sont alignés. Les trois images ont été examinées.
- Démarrage réel WASAPI, acquisition pendant 5,2 s et arrêt sans erreur.
  Ce test ne mesure pas la précision de reconnaissance de phrases live.
- Annulation pendant l’inférence native, puis nouvelle transcription réussie ;
  rejet explicite d’une vidéo sans piste audio ; libération du modèle vérifiée.
- Éditeur ouvert sur Windows avec les ressources de thème réelles de Jerry :
  média ouvert à 640 px de largeur naturelle, quatre lignes et six mots pour
  la sélection, modification programmée du texte et du début d’un mot
  effectivement appliquée au document. Lisibilité examinée sur une image WPF.

Les essais ont notamment utilisé `C:\Users\DE ALMEIDA\AppData\Local\Temp\Jerry
caption's smoke` et des noms de fichiers contenant une apostrophe. Le harnais
et les clips de test restent hors des sources applicatives. L’échantillon
JFK provient de whisper.cpp ; SHA-256 :
`59dfb9a4acb36fe2a2affc14bacbee2920ff435cb13cc314a08c13f66ba7860e`.

## Intégration et limites restantes

Créer une instance partagée de `CaptionService(settings, tools)`. Les contrats
`EnsureModelAsync`, `TranscribeAsync`, `StartLiveAsync`, `StopLiveAsync` et
`CaptionEditorWindow(settings, captions, tools, initialPath)` sont disponibles.
`LiveText`, `StatusChanged` et `LiveError` peuvent être émis depuis un thread de
travail : l’interface doit les transmettre à son Dispatcher. À l’arrêt de
l’application, attendre `DisposeAsync()` pour arrêter le live et libérer le
modèle natif. Les services média restent responsables de l’enregistrement et
de la politique d’ouverture automatique de l’éditeur.

Le live comporte au moins la latence d’un bloc, n’est pas une reconnaissance
en continu par échantillon et peut perdre du contexte aux frontières. Les
blocs incomplets ne sont pas transcrits après l’arrêt. La transcription d’un
long fichier dépend de la mémoire utilisée par Whisper.net pour son WAV ; les
performances sur de longues vidéos restent à mesurer. L’aperçu dépend des
codecs Windows, alors que l’export repose sur FFmpeg. La prévisualisation WPF
et le rendu ASS ne sont pas équivalents au pixel près. Les dialogues système,
les clics manuels complets dans l’éditeur, le multi-écran à DPI mixtes et la
précision française live restent à valider sur des scénarios utilisateur.
Aucune comparaison d’exécution avec MOUZZ n’a été possible sans macOS.

## Sources primaires utilisées

- [Whisper.net, commit du paquet 1.9.0 — tokens et segments](https://github.com/sandrohanea/whisper.net/blob/094615b303e4db907ea2218c18e3965f5c9acbb2/Whisper.net/WhisperProcessorEvents.cs)
- [Whisper.net — conversion des horodatages natifs](https://github.com/sandrohanea/whisper.net/blob/094615b303e4db907ea2218c18e3965f5c9acbb2/Whisper.net/WhisperProcessor.cs)
- [Distribution et empreintes des modèles whisper.cpp](https://github.com/ggml-org/whisper.cpp/blob/master/models/README.md)
- [Balises ASS, karaoke, mouvement et transformations](https://aegisub.org/docs/latest/ass_tags/)
- [Échantillon JFK de whisper.cpp](https://github.com/ggml-org/whisper.cpp/blob/master/samples/jfk.wav)

## Polices intégrées — complément CAP-04

Deux faces statiques non modifiées sont désormais livrées dans
`src/Jerry.App/Assets/Fonts`, avec leurs licences SIL OFL 1.1 et une fiche
`provenance.json`. Elles proviennent de commits identifiés du dépôt officiel
Google Fonts ; aucun fichier de MOUZZ n’a été repris et aucune police système
n’est installée.

| Face | Version | Taille | SHA-256 |
| --- | --- | ---: | --- |
| Montserrat ExtraBold | 7.200 | 262 108 octets | `257af9a05de6371e1f7b345d02a93af5c2e0ab9b9224418a45189b8cc86049cd` |
| Oswald Bold | 4.100 | 94 272 octets | `0cde70ba3b3398ab0bc19be74c7517442ef711846d33f286d7932fef9784e2d8` |

Le sélecteur de l’éditeur propose Montserrat, Oswald et les familles installées
possédant une face grasse. Les familles intégrées sont résolues depuis leur
répertoire local par WPF, avec les poids respectifs 800 et 700. ASS utilise
les noms de faces `Montserrat ExtraBold` et `Oswald Bold` ; le filtre FFmpeg
inclut explicitement `fontsdir` vers le dossier livré `Assets/Fonts`. Le chemin
est échappé aux deux niveaux de lecture du filtre, sans installation globale.
Le paquet doit copier `Assets/Fonts/**/*` vers la sortie et la publication en
conservant ce chemin relatif, avec les licences et la provenance.

Vérifications supplémentaires : trois tests de noms ASS et d’échappement ont
été observés rouges puis verts. Sur Windows, les URI des `GlyphTypeface`
pointent bien vers les TTF livrés, aux poids 800/700, et l’aperçu WPF s’ouvre.
Les MP4 et images de Segoe UI, Montserrat et Oswald ont été produits et examinés :
leurs formes et empreintes diffèrent. Un essai natif avec un dossier de polices
contenant espaces, apostrophe, crochets et virgule réussit ; le journal FFmpeg
confirme le chargement des TTF de ce dossier et la sélection de
`Montserrat-ExtraBold`. Une première résolution WPF sans séparateur final
retournait zéro famille ; le séparateur a été ajouté et les deux URI de fichiers
ont ensuite été vérifiées.

Sources exactes :
[Montserrat ExtraBold et licence](https://github.com/google/fonts/tree/160c7fe82ecb74b108d886ed8d27762f6e346163/ofl/montserrat),
[Oswald Bold et licence](https://github.com/google/fonts/tree/23a95a792463c0670ef0f6d8cb0ba5fc4e7fd7ad/ofl/oswald).
