# Installation Windows de Jerry

Demande du 28 octobre 2026 : adapter le modèle Inno Setup de KOL à Jerry,
sans modifier ses fonctions, son interface WPF ou sa mascotte. Le dépôt reste
public. La livraison prévue est Jerry 0.2.0.

## Choix retenus

Inno Setup produit `Jerry-Setup-0.2.0.exe`, en français, sans élévation, pour
Windows x64. L'installation par défaut est `%LOCALAPPDATA%\Programs\Jerry`.
Le menu Démarrer, Applications installées, App Paths et le désinstallateur
utilisent exclusivement les noms et identifiants de Jerry. Une archive portable
autonome reste disponible. MSIX imposerait une autre identité et un autre modèle
de signature ; un simple ZIP ne fournit pas l'intégration demandée.

Toute la publication .NET 10 autonome est incluse : DLL, composants natifs,
ressources et notices. Le SDK .NET et PowerShell ne sont pas nécessaires à
l'utilisateur final. L'installation ne rend pas les exécutables signés.

Les données restent à leur emplacement actuel : `%LOCALAPPDATA%\Jerry`, le
dossier de captures choisi, les sessions récupérables, outils et modèles. Ni
l'installation ni la désinstallation ne les suppriment ou ne les retéléchargent.
Une désinstallation confirmée désactive seulement le démarrage automatique.

## Démarrage et maintenance

Le choix de démarrage est facultatif, désactivé pour une première installation,
restauré lors d'une mise à jour et synchronisé avec les préférences et Windows.
Les sélections explicites `/TASKS` et `/MERGETASKS` sont respectées. L'exécutable
enregistré dans App Paths est préféré à une copie portable. Aucun chemin UNC,
WSL ou lecteur réseau ne peut être enregistré pour le démarrage.

Un petit auxiliaire .NET autonome `Jerry.Maintenance.exe` est extrait dans un
dossier temporaire avant le remplacement ; il ne dépend pas de l'ancienne
publication. Ses commandes sont `--prepare-update`, `--startup-status` (20 :
activé, 0 : désactivé) et `--configure-startup=enable|disable` (0 : réussite).
Les erreurs et refus renvoient un code non nul. Il ne lance aucun service média.

L'instance conserve son mutex `Local\Jerry.CursorStudio`. Un canal nommé,
restreint au même utilisateur et à la même session, permet une demande d'arrêt
propre. Une capture, un décompte, une finalisation, une opération occupée ou un
éditeur ouvert entraîne un refus sans arrêter l'activité. Une ancienne version
sans ce canal demande une fermeture manuelle. Aucun processus n'est tué et le
Restart Manager ne ferme pas automatiquement les applications. Une barrière de
maintenance empêche un nouveau lancement pendant la copie ou la suppression.

L'auxiliaire doit constater la libération de l'instance avant de réussir.
La désinstallation demande cet arrêt et désactive le démarrage après la
confirmation, jamais dans `InitializeUninstall`. Annuler ne change rien.

## Validation et livraison

Les tests unitaires couvrent la sélection d'un exécutable local, les chemins
avec espaces et Unicode, la conservation des autres préférences et les refus.
Les tests Windows vérifient le refus pendant une capture/décompte et la sortie
propre. Les installations et désinstallations sont réservées aux runners
GitHub Windows hébergés et jetables, avec vérification préalable d'absence de
données et d'installation Jerry. Ils couvrent installation, raccourcis,
publication complète, mise à jour, choix de démarrage, annulation, refus occupé
et suppression sans perte de données. Aucun test destructif sur ce poste.

La CI construit, teste puis publie l'installateur, le ZIP et `SHA256SUMS.txt`
pour un tag de version. Les sources vérifiées sont intégrées à `main`. La copie
locale ne sera installée qu'en l'absence d'une session Jerry active ; sinon
l'installation sera différée pour préserver la préférence de l'utilisateur.
