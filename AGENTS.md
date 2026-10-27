# Jerry

## Intention du projet

Créer pour Gabriel une application Windows couvrant les fonctions actuelles de
MOUZZ. Usage personnel, dépôt GitHub privé. Les nouvelles fonctions
viendront après la couverture du périmètre existant. La cible originale est
une application macOS, pas iOS. L'utilisateur dispose uniquement de Windows.

Le périmètre de référence comprend les éditions gratuite et Pro. Consulter
`docs/mouzz-scope.md` et les preuves déjà conservées avant une nouvelle analyse.
Ne pas présenter cette liste initiale comme une preuve de parité exhaustive.

## REA

- Utiliser le checkout voisin `../rea`, version 6.8.0 lors de la préparation.
- `npm run rea -- <commande>` utilise le Node.js local au projet.
- `npm run rea:build` reconstruit REA après une modification de son checkout.
- La connexion Codex propre à cette machine est dans `.codex/config.toml`.
- Les consignes fournies avec REA sont dans
  `.agents/skills/reverse-engineer-anything/SKILL.md`.
- Aucun moteur de décompilation natif n'a encore été validé pour ce projet.

Distinguer les résultats REA, les observations d'autres outils, les déclarations
du site, les inférences et les inconnues. L'analyse statique ne prouve pas le
comportement à l'exécution. Conserver la version, la provenance et les empreintes
des fichiers analysés, ainsi que les identifiants des preuves REA.

## Git et données locales

Les applications originales dans `targets/`, les preuves brutes dans
`evidence/raw/`, les dépendances et les réglages propres à la machine sont
exclus de Git. Les sources, les synthèses et les ressources originales de Jerry
sont destinées au dépôt privé `https://github.com/nRudzy/Jerry`, branche `main`.
Conserver cette visibilité privée ; ne pas publier les réglages de machine,
les applications de référence ou les preuves brutes.

## Validation

Vérifier les fonctions Windows par rapport aux critères du périmètre, puis les
scénarios multi-écrans, les différents facteurs de mise à l'échelle, les
raccourcis, les captures, les entrées audio et les performances. Documenter
explicitement les comparaisons avec l'original qui restent impossibles faute
d'environnement macOS. Ne pas annoncer une fidélité à 100 % sans ces preuves.
