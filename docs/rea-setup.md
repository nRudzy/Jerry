# Préparation de REA dans Jerry

Préparation effectuée le 27 octobre 2026, sous Ubuntu / WSL sur Windows.

## Installation utilisée

| Élément | Valeur |
| --- | --- |
| Source REA | Checkout voisin `../rea` |
| Dépôt source configuré | `git@github.com:nRudzy/rea.git` |
| Révision source | `baec375c347f129cb109823cff31831e906039f0` |
| Version REA | `6.8.0` |
| Node.js local | `24.22.1` |
| npm local | `11.16.0` |
| Configuration MCP | `.codex/config.toml`, exclue de Git |
| Instructions REA | `.agents/skills/reverse-engineer-anything/`, exclues de Git |
| Familles MCP | `native,artifact,session,application` |

Les outils Node.js et npm sont installés dans `Jerry/node_modules`. Les
versions système n'ont pas été remplacées. REA a été construit avec ses
dépendances verrouillées et la commande documentée `npm run build:cached`.
Le checkout source ne présente aucune modification suivie après construction.

La connexion est propre à Jerry. Les réglages globaux Codex n'ont pas été
modifiés. La configuration utilise directement le binaire Node.js local et
le lanceur du checkout REA. Les délais configurés sont 30 secondes au démarrage
et 450 secondes par appel ; les profils de schémas sont compacts.

La portée projet et ces paramètres sont décrits dans la
[documentation MCP officielle de Codex](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

## Vérifications exécutées

- Compilation REA : 3 tâches réussies, commande terminée avec le code 0.
- Version retournée par `npm run rea -- --version` : `6.8.0`.
- Diagnostic MCP sur la portée configurée : `healthy: true`, 96 outils attendus
  et observés, 3 prompts attendus et observés, aucun manquant ni doublon.
- Vérification indépendante du lanceur décrit par `.codex/config.toml` :
  initialisation, catalogue et appel réel de `binary_session` réussis.
- Lecture de la configuration par `codex mcp get rea --json` : connexion active
  reconnue dans ce projet, avec les chemins et délais prévus.
- Copie locale du fichier principal des instructions identique au fichier
  généré par REA, SHA-256
  `18d8cd8f241edff3fe4016b9b65d33e5f48e77ebf332df3f9f2b64313bf4870c`.
- Inspection statique du bundle officiel MOUZZ Pro 1.3.7 réussie ; les preuves
  sont conservées dans `evidence/raw/` et résumées dans `mouzz-scope.md`.

Le contrôle du lanceur a initialement confondu l'opération interne
`inventory_artifact` avec un outil MCP public. La vérification corrigée utilise
le contrat public `inspect_artifact` et réussit. Aucune modification de REA
n'a été nécessaire.

## Limites et suite

Le serveur a été lancé et interrogé indépendamment pour les contrôles. Le chat
déjà ouvert n'a pas été reconnecté automatiquement. Rouvrir le projet ou
redémarrer le client charge la nouvelle connexion et les instructions.

Aucun moteur natif de décompilation n'a été configuré ou validé dans ce
projet. Les utilitaires Apple et l'exécution macOS ne sont pas disponibles
dans l'environnement utilisé. L'inventaire des fichiers fonctionne ; la
décompilation et la vérification du comportement demandent des étapes
supplémentaires.

Lors de la préparation initiale, le dépôt était uniquement local sur `main`,
avec une préférence pour un futur dépôt privé. Les sources sont désormais
publiées sur [nRudzy/Jerry](https://github.com/nRudzy/Jerry). Le 28 octobre 2026,
l’utilisateur a confirmé vouloir conserver ce dépôt **public**.
