# Version Windows

## Exécutable

Version portable autonome x64, issue de la construction Release.

- Copie locale Windows : `%LOCALAPPDATA%\Jerry\app\Jerry.exe`.
- Copie du projet : `dist/windows-x64/Jerry.exe`.
- Archive : `dist/Jerry-Windows-x64.zip`.

Décompresser **tout** le dossier de l’archive. Garder les DLL et `Assets/Fonts`
à côté de l’exécutable. Aucune installation de .NET, Node.js ou REA n’est
nécessaire pour lancer cette version.

Les modèles de parole et les outils vidéo sont conservés séparément dans les
dossiers locaux décrits dans README.md. Ils ont déjà été préparés sur cette
machine pendant les essais ; sur une nouvelle machine, le premier usage les
télécharge depuis leur distribution officielle.

L’archive et les exécutables restent exclus de Git. Les empreintes et le
résultat du lancement sont conservés dans `evidence/raw/windows-delivery.json`
et `windows-launch.json`. Les sources restent dans le dépôt local, sans distant.

## Premier usage

1. Ouvrir Jerry et choisir les effets dans le panneau de gauche.
2. Pour les raccourcis, aller dans **Raccourcis** : aucun n’est imposé.
3. Pour une vidéo, choisir audio/dossier/résolution dans **Enregistrement**.
4. Pour les sous-titres, choisir le modèle et ouvrir l’éditeur, ou activer le live.
5. Pour quitter complètement, utiliser **Quitter Jerry** dans le panneau ou la
   zone de notification ; fermer la fenêtre la masque par défaut.

Voir windows-verification.md pour les essais réussis et leurs limites, en
particulier l’absence de comparaison macOS et de tests sur plusieurs moniteurs.
