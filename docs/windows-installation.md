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

Les preuves locales détaillées sont dans `evidence/raw/` (ignoré par Git).

## Cycle d'installation sur runner jetable

Validation CI en cours de préparation. Ce paragraphe sera remplacé par le lien
du run et ses résultats avant la livraison.

## Limites

Les exécutables ne sont pas signés Authenticode. Aucun certificat, aucune
politique PowerShell et aucune protection Windows n'ont été modifiés. Un contrôle
SmartScreen ou l'indication « éditeur inconnu » reste possible.

Une version Jerry antérieure au canal de maintenance exige une fermeture
manuelle. Une activité protégée entraîne un refus, sans arrêt de capture ni
remplacement des fichiers. L'installation locale est différée si l'utilisateur
conserve une session ouverte.

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
