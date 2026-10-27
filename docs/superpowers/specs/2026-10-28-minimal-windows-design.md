# Interface Windows minimaliste

La demande du 28 octobre remplace l'habillage précédent par celui des deux
captures MOUZZ fournies : réglages anthracite compacts et commandes discrètes
pendant l'utilisation. Les captures de l'utilisateur font autorité.

## Direction retenue

Conserver WPF et tous les réglages existants. L'adaptation native permet de
garder les effets, la capture et les raccourcis déjà opérationnels. Une interface
web ou un changement de moteur ajouterait du travail sans servir cette demande.

- Fond neutre `#1D1D1D`, navigation `#222222`, groupes `#242424`, traits
  `#333333`, texte `#EAEAEA`, secondaire `#999999`, accent bleu `#3B82F6`.
- Segoe UI 13–14, titres de section 14, rayon 6–9, lignes de réglages 42–48.
- Fenêtre 940 × 740 environ, navigation 208, commandes Windows discrètes.
  Quinze rubriques donnent un accès direct à chaque effet, sans écran promotionnel.
- Interrupteurs à droite, valeurs et menus alignés, groupes plats séparés par de
  fins traits. Les descriptions restent proches des réglages concernés.
- Un clic sur l'icône de notification ouvre un menu rapide sombre, qui reste
  ouvert pendant les bascules et suit les changements par raccourci. Réglages,
  capture et sortie restent accessibles. L'extérieur et Échap le ferment.
- Barre d'annotation sur une ligne : poignée, huit outils, palette, épaisseur,
  annuler, effacer, fermer. L'outil choisi au clavier reste visible dans la barre.
- Capture : petit panneau avec point rouge, durée et arrêt. Les panneaux
  flottants restent exclus de la vidéo. Placement dans l'espace disponible.

## Contraintes et validation

Préserver les réglages de l'utilisateur, les services et leurs arrêts propres.
Ne pas réinterpréter les captures en ajoutant des panneaux décoratifs ou des
fonctions. Comparer les rendus WPF réels aux deux références. Vérifier les quinze
rubriques, les petits formats, les bascules du menu, leur synchronisation, le
dessin et l'exclusion des commandes de la capture. Une validation sur un écran
Windows ne prouve pas une parité visuelle sur macOS ni tous les scénarios DPI.
