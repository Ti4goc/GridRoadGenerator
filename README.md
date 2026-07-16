# GridRoadGenerator — squelette de mod CS2

## Idée
Sélectionner plusieurs nœuds d'une route existante (typiquement 4, formant un rectangle),
puis générer automatiquement une grille de routes internes (façon quartier pavillonnaire),
avec espacement, nombre de lignes et de colonnes réglables.

## Structure
- `Core/GridGenerator.cs` — la géométrie pure, qui gère des **périmètres quelconques** :
  polygones convexes, concaves (en L, en U...), tournés, irréguliers. Le polygone est
  construit dans l'ordre de clic des nœuds, la grille s'oriente sur l'arête la plus
  longue (elle suit la "rue principale", pas les axes du monde), et chaque ligne est
  découpée aux frontières par clipping pair-impair (les concaves produisent plusieurs
  sous-segments par ligne). Les segments < 8 m sont éliminés. Avec 2 nœuds seulement,
  ils sont traités comme coins opposés d'un rectangle aligné sur les axes.
  Ce fichier ne dépend d'aucune API du jeu : testable isolément (validé par un port
  Python 1:1 sur rectangle tourné 30°, polygone en L et pentagone irrégulier).
- `Settings/GridRoadGeneratorSettings.cs` — les réglages exposés dans Options > Mods
  (mode, colonnes, lignes, espacement).
- `Systems/GridRoadToolSystem.cs` — l'outil en jeu : sélection des nœuds au clic,
  validation, appel à GridGenerator, puis pose des segments.
- `GridRoadGeneratorMod.cs` — point d'entrée (`IMod`), enregistrement du système et des settings.

## Ce qui est fonctionnel tel quel
Toute la logique de `GridGenerator.cs` est complète et testable indépendamment du jeu.

## Ce qu'il reste à brancher (dépendant du SDK/version du jeu)
1. **Input Actions** : déclarer les actions `SelectNode`, `ConfirmGrid`, `Cancel` dans le
   système d'input du mod (fichier d'actions à créer, référencé par `InputManager.instance.FindAction`).
2. **Placement réel des routes** (`PlaceRoadSegment`) : c'est la seule partie "boîte noire"
   du squelette. Deux pistes :
   - Piloter `NetToolSystem` par code, comme si le joueur cliquait-glissait à la souris
     (le plus robuste dans le temps, recommandé).
   - Créer directement les entités réseau (Edge/Node/NetCourse) via l'`EntityManager`
     (plus rapide mais plus fragile aux mises à jour du jeu).
   Cherche `NetCourse`, `NetToolSystem.GetAvailableSnapMask` et les exemples de mods
   routiers existants (beaucoup de mods CS2 open-source manipulent déjà NetToolSystem)
   sur le Discord/wiki officiel de modding CS2 pour la syntaxe exacte de ta version du jeu.
3. **Choix du prefab de route** (`_roadPrefab`) : à remplir avec le prefab sélectionné par
   le joueur (ex. réutiliser le prefab actuellement choisi dans le NetTool natif du jeu).
4. **Snapping aux nœuds du périmètre** : pour que la grille se raccorde proprement aux
   4 routes déjà sélectionnées (et pas seulement "à côté"), il faut que `PlaceRoadSegment`
   utilise le système de snapping natif du jeu plutôt que des coordonnées brutes.

## Localisation
Le mod inclut 16 langues dans `Localization/Translations.cs` :
- **12 officielles** : en-US, fr-FR, de-DE, es-ES, it-IT, pl-PL, pt-BR, ru-RU, ja-JP, ko-KR, zh-HANS, zh-HANT
- **4 communautaires** : pt-PT (portugais du Portugal), uk-UA, th-TH, vi-VN

Les langues communautaires ne s'activent que si la locale existe côté jeu, c'est-à-dire si
le joueur a un mod de langue installé (ex. I18n EveryWhere ou un mod de localisation dédié
comme le tien pour le PT-PT). Le code vérifie `SupportsLocale` avant d'enregistrer la source,
donc aucune erreur si la locale est absente — le jeu retombe sur l'anglais.

Pour ajouter/modifier une langue : édite simplement le dictionnaire `Translations.All`.

## Compilation
Adapte `CSIIPath` dans le `.csproj` vers ton dossier d'installation CS2, et vérifie les
noms des DLL référencées (ils varient selon les mises à jour du jeu). Utilise de préférence
le template de mod officiel/communautaire à jour comme base de référence pour les chemins
et versions exactes des dépendances.
