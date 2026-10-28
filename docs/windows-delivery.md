# Version Windows

## Exécutable

Version portable autonome x64 avec la mascotte néon, issue de la construction Release.

- Copie Windows : `%LOCALAPPDATA%\Jerry\app-neon\Jerry.exe`.
- Copie du projet : `dist/neon-windows-x64/Jerry.exe`.
- Archive : `dist/Jerry-Neon-Windows-x64.zip`.

Pour voir la nouvelle icône, choisir **Quitter Jerry** dans l’icône près de
l’horloge, puis ouvrir cette nouvelle copie. La session en cours reste ouverte
pendant la préparation. Jerry n’exécute qu’une seule instance ; les copies
partagent le même dossier de réglages.

Les livraisons précédentes restent dans `app`, `app-minimal` et
`dist/windows-x64`. Les commandes de construction standard du README produisent
la version courante des sources dans `dist/windows-x64`, après fermeture de la
copie qui y serait encore exécutée.

Décompresser **tout** le dossier de l’archive. Garder les DLL et `Assets/Fonts`
à côté de l’exécutable. Aucune installation de .NET, Node.js ou REA n’est
nécessaire pour lancer cette version.

Les modèles de parole et les outils vidéo sont conservés séparément dans les
dossiers locaux décrits dans README.md. Ils ont déjà été préparés sur cette
machine pendant les essais ; sur une nouvelle machine, le premier usage les
télécharge depuis leur distribution officielle.

L’archive et les exécutables restent exclus de Git. Les empreintes et le
résultat du lancement sont conservés dans `evidence/raw/windows-delivery.json`
et `windows-launch.json` pour la première livraison ; `windows-minimal-delivery.json`
pour la refonte ; `windows-neon-delivery.json` pour la mascotte néon. La nouvelle
copie n’est pas lancée pendant que l’ancienne reste
ouverte, conformément au choix de Gabriel.

Les sources sont disponibles dans le dépôt public
[nRudzy/Jerry](https://github.com/nRudzy/Jerry), sur la branche `main`. Les sorties
de compilation restent locales. Le README fournit les commandes pour construire
la version autonome depuis un clone propre.

## Premier usage

1. Ouvrir Jerry et choisir directement un effet dans le panneau de gauche.
   **Général** regroupe activation, démarrage Windows et fréquence d’animation.
2. Pour les raccourcis, aller dans **Raccourcis** : aucun n’est imposé.
3. Pour une vidéo, choisir audio/dossier/résolution dans **Enregistrement**.
4. Pour les sous-titres, choisir le modèle et ouvrir l’éditeur, ou activer le live.
5. Un clic sur l’icône Jerry près de l’horloge ouvre le menu rapide. Les
   interrupteurs restent accessibles pendant l’utilisation ; **Autres outils**
   regroupe les commandes supplémentaires. **Menu rapide** personnalise la liste.
6. Pour quitter complètement, utiliser **Quitter Jerry** dans le panneau ou la
   zone de notification ; fermer la fenêtre la masque par défaut.

Voir windows-verification.md pour les essais réussis et leurs limites, en
particulier l’absence de comparaison macOS et de tests sur plusieurs moniteurs.
