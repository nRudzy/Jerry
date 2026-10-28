# Vérification de l'installation Windows

Évolution du 28 octobre 2026, Jerry 0.2.0. Le périmètre est l'installation,
la mise à jour et la désinstallation ; les fonctions et la mascotte sont
conservées. Le [plan](superpowers/plans/2026-10-28-windows-installer.md) et la
[conception](superpowers/specs/2026-10-28-installer-design.md) décrivent les contrats.

## Essais locaux sans installation

- 60 tests Core passent, dont conservation des réglages inconnus, modification
  atomique du seul choix de démarrage et refus d'un JSON illisible.
- 14 tests Windows passent : préférence pour la copie installée, repli portable
  local, rejet UNC/WSL/relatif, chemins avec espaces et Unicode et guillemets.
- Le composant WPF `maintenance` vérifie un décompte réel, une capture MP4 réelle,
  le refus d'arrêt qui laisse son processus tourner, l'arrêt explicite avec MP4,
  la protection de la caméra/live/éditeur et la fermeture normale à l'état inactif.
- L'auxiliaire a été publié en exécutable autonome unique ; sa lecture du choix de
  démarrage fonctionne sans charger les services média.

Les 2 156 contrôles WPF `minimal-ui` passent également après intégration, sans
refonte de l’interface. Le raccourci `npm test` est bloqué par la politique
PowerShell locale ; les suites ci-dessus ont été exécutées directement avec
`dotnet`. Aucun contournement de cette politique n’a été ajouté.

Les preuves locales détaillées sont dans `evidence/raw/` (ignoré par Git).

## Cycle d'installation sur runner jetable

Le [run Windows 40686017879](https://github.com/nRudzy/Jerry/actions/runs/40686017879)
est réussi : deux machines GitHub hébergées distinctes pour les composants et
le cycle d'installation. L'assistant Inno compile en 61 secondes ; le cycle
d'installation compte **132 contrôles réussis**.

- Première installation par utilisateur dans un chemin avec espaces et Unicode,
  Applications installées, App Paths et raccourci Démarrer vérifiés.
- **539 fichiers** de publication vérifiés par taille et empreinte, puis comparés
  au ZIP. Runtime .NET, DLL natives, polices et licences sont inclus.
- Chargement réel de Whisper 1.8.2 et des quatre DLL CRT/OpenMP depuis le dossier
  Jerry, avec recherche limitée aux dossiers explicites et à Windows.
- Activations et désactivations du démarrage conservées lors de mises à jour,
  choix `/TASKS` et `/MERGETASKS`, ajout du raccourci Bureau et désactivation
  Windows StartupApproved pris en compte.
- Lancement bloqué pendant la réservation d'instance ; installation et
  désinstallation refusées si une ancienne instance conserve son mutex, avant
  suppression ou modification des préférences.
- Configuration du démarrage refusée pendant une instance active ; annulation
  réelle de la confirmation du désinstallateur sans fermer l'application ni
  changer ses préférences ou le registre.
- Mise à jour et désinstallation confirmée après sortie propre d'une instance
  inactive ; fichiers, raccourcis et inscriptions supprimés, démarrage désactivé.
- Réglages connus et inconnus conservés ; empreintes des captures, sessions,
  modèles et outils factices inchangées après le cycle. Aucun original ni
  réglage utilisateur local n'est employé sur ces runners.

Le premier essai CI avait révélé une attente du serveur Roslyn après la fin de
`dotnet publish`. Le script attend maintenant le seul processus de commande et
vérifie son code de sortie ; la construction complète prend 98 secondes sur le
run réussi. Aucun serveur de compilation n'est tué par le script.

## Livraison publiée et installation locale

La [version 0.2.0](https://github.com/nRudzy/Jerry/releases/tag/v0.2.0) est publiée
avec installateur, ZIP autonome et `SHA256SUMS.txt`. Le
[run du tag](https://github.com/nRudzy/Jerry/actions/runs/40687366572) a de nouveau
réussi les 60 tests Core, 14 tests Windows, 14 contrôles natifs et 132 contrôles
d'installation avant publication. Le
[run main](https://github.com/nRudzy/Jerry/actions/runs/40687366426) est également réussi.

Les téléchargements publiés ont été comparés à leurs sommes SHA-256 et aux
empreintes des fichiers GitHub. Sur le poste utilisateur, l'installation native
a ensuite été effectuée en l'absence d'instance Jerry active, sans arrêter de
session. Les 539 fichiers du manifeste installé correspondent à la release.
Le menu Démarrer, Applications installées, la version 0.2.0 et le chemin local du
démarrage automatique ont été contrôlés. Les empreintes des données existantes
et les autres préférences sont inchangées ; le choix de démarrage Windows
préexistant a été conservé et synchronisé.

Jerry a été lancé depuis `%LOCALAPPDATA%\Programs\Jerry`, répond normalement et
n'a créé aucun journal d'erreur. Aucune étape locale n'est différée. Aucun essai
de désinstallation n'a été exécuté sur le poste utilisateur ; ces essais restent
réservés aux runners jetables. Les réglages et preuves locales restent exclus
de Git.

## Limites

Les exécutables ne sont pas signés Authenticode. Aucun certificat, aucune
politique PowerShell et aucune protection Windows n'ont été modifiés. Un contrôle
SmartScreen ou l'indication « éditeur inconnu » reste possible.

Une version Jerry antérieure au canal de maintenance exige une fermeture
manuelle. Une activité protégée entraîne un refus, sans arrêt de capture ni
remplacement des fichiers. L'installation locale est différée si l'utilisateur
conserve une session ouverte.

Les chemins UNC/WSL et relatifs sont testés ; le rejet d'un lecteur réseau mappé
est implémenté par le type du lecteur, mais aucun lecteur réseau réel n'a été
monté pour ces essais.

Cette livraison ne certifie aucune parité supplémentaire avec MOUZZ. Les limites
macOS, multi-écrans et DPI documentées dans la validation fonctionnelle restent
applicables.

## Références techniques

- [Inno Setup : fermeture automatique et Restart Manager](https://jrsoftware.org/ishelp/topic_setup_closeapplications.htm).
  Jerry utilise `CloseApplications=no` et son propre accord de maintenance.
- [Inno Setup : événements d'installation et désinstallation](https://jrsoftware.org/ishelp/topic_scriptevents.htm).
  Les opérations de désinstallation ont lieu après confirmation.
- [Microsoft : canaux nommés limités à l'utilisateur](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.pipeoptions?view=net-10.0).
  Le canal Jerry vérifie aussi l'utilisateur et le niveau d'élévation.
