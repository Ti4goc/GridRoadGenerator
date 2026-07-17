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

## État : fonctionnel
Tout est branché sur l'API du jeu (vérifiée contre les sources décompilées de Game.dll,
dossier `decompiled/`, non versionné) :

1. **Input actions** — via le système de keybindings de `ModSetting` (propriétés
   `ProxyBinding` + `[SettingsUIKeyboardBinding]`, réassignables dans Options) :
   - **Ctrl+G** : activer/désactiver l'outil (action `ToggleTool`) ;
   - **Clic gauche** : sélectionner/désélectionner un nœud (action native `Apply` de l'outil) ;
   - **Clic droit** : retirer le dernier nœud (action native `Secondary Apply`) ;
   - **Entrée** : valider et construire la grille (action `ConfirmGrid`) ;
   - **Échap** : annuler la sélection (action native `Cancel`).
2. **Placement des routes** — l'outil pilote le pipeline natif de construction réseau,
   comme `NetToolSystem` : à chaque changement de sélection il crée des entités de
   définition (`CreationDefinition` + `NetCourse` + `Updated`) via le `ToolOutputBarrier`.
   Le jeu en dérive des entités Temp qui servent d'**aperçu fantôme** (validation de
   collision native, croisements gérés par `CourseSplitSystem`, intersections réelles).
   À la validation, `ApplyMode.Apply` concrétise l'aperçu — même mécanique qu'un
   clic-glisser du joueur. Les hauteurs sont reprojetées sur le terrain (`TerrainSystem`).
3. **Prefab de route** — celui sélectionné dans l'outil route natif du joueur s'il s'agit
   d'une route, sinon la petite route deux voies ("Small Road").
4. **Snapping au périmètre** — chaque extrémité de segment est raccordée au nœud
   sélectionné le plus proche, ou à la route existante entre deux nœuds consécutifs
   (split d'arête via `CoursePos.m_SplitPosition`, comme un tracé manuel terminé au
   milieu d'une route). Les extrémités libres (mode 2 nœuds) suivent le terrain.
5. **Feedback visuel** — nœuds sélectionnés surlignés (`Highlighted`), grille en aperçu
   fantôme natif avant validation.

Pas de bouton dans la barre d'outils pour l'instant : cela demanderait un module UI
cohtml (React) complet ; le raccourci clavier configurable couvre l'activation.

## Interface en jeu
- **Survol** d'un nœud de route = surbrillance ; **clic gauche** = ajout au périmètre
  (re-clic = désélection) ; **clic droit** = retire le dernier ; **Échap** = tout annuler.
- **Tooltips contextuels** près du curseur selon l'étape (sélectionner, valider, périmètre invalide).
- **Panneau latéral** (React/cohtml, `UI/`) affiché quand l'outil est actif : mode
  d'espacement, colonnes, lignes, espacement, "Générer la grille", "Tout annuler".
  Les réglages sont synchronisés dans les deux sens avec Options > Mods.
- Le module UI est buildé par webpack (`UI/`, cible MSBuild `BuildUI` après déploiement)
  en `GridRoadGenerator.mjs` + `.css` dans le dossier du mod.

## Dépendances optionnelles
- **[Anarchy](https://mods.paradoxplaza.com/mods/74604/Windows)** de **yenyang** :
  si le mod est installé, le panneau affiche une rangée « Anarchy » qui active ou
  désactive l'anarchie directement (état synchronisé avec le bouton et le raccourci
  d'Anarchy, via ses propres bindings UI). Sans Anarchy, la rangée n'apparaît pas —
  aucune dépendance dure, détection par assembly au chargement.

## Crédits
Les patterns de sélection de nœuds (raycast + survol + surbrillance), de tooltips
contextuels, d'architecture du module UI (webpack/cohtml/bindings) et le sélecteur
de réseau (recherche/catégories/récents) sont adaptés de
[CS2-NetworkTools](https://github.com/lucarager/CS2-NetworkTools) de **Luca Rager
(lucarager)**, sous licence MIT. Merci !
L'icône de la rangée Anarchy provient d'**Unified Icon Library** (chargée par
Anarchy lui-même, `coui://uil/`).

## Test in-game
Lance le jeu avec `-developerMode`. Logs du mod :
`%AppData%\..\LocalLow\Colossal Order\Cities Skylines II\Logs\GridRoadGenerator.log`
(et `Player.log` pour les erreurs moteur). Vérifie au chargement : « GridRoadGenerator
chargé. » et les lignes « Localisation enregistrée ». En partie : Ctrl+G, clique des
nœuds de route existants dans l'ordre du périmètre, aperçu fantôme, Entrée pour
construire. Scénarios : rectangle 4 nœuds, 2 nœuds opposés (rectangle aligné aux axes,
extrémités libres), L concave à 6 nœuds, périmètre tourné, nœuds cliqués en zigzag
(le polygone suit l'ordre de clic), les deux modes d'espacement dans Options > Mods.

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
Le `.csproj` suit le template officiel du toolchain de modding : il importe `Mod.props`
et `Mod.targets` depuis `%CSII_TOOLPATH%` (variable définie par l'installation du
toolchain in-game). La résolution des DLL, le post-processing Burst et le déploiement
automatique dans `%CSII_LOCALMODSPATH%\GridRoadGenerator` sont gérés par ces imports :

```
dotnet build GridRoadGenerator.csproj
```

Le dossier `decompiled/` (sources de Game.dll décompilées via `ilspycmd`, non versionné)
sert de référence d'API ; régénère-le après une mise à jour du jeu :

```
ilspycmd -p -o decompiled --nested-directories "%CSII_MANAGEDPATH%\Game.dll"
```
