// Pattern de bindings adapté de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import { bindValue, trigger } from "cs2/api";
import { Entity } from "cs2/utils";
import mod from "mod.json";

/// Entrée du sélecteur de réseau (miroir de GridRoadUISystem.WritePrefabEntry).
export type PrefabEntry = {
    Entity: Entity;
    Name: string;
    Icon: string;
};

// Valeurs poussées par GridRoadUISystem (C#). Les noms doivent correspondre.
export const toolActive$ = bindValue<boolean>(mod.id, "TOOL_ACTIVE", false);
export const nodeCount$ = bindValue<number>(mod.id, "NODE_COUNT", 0);
export const canApply$ = bindValue<boolean>(mod.id, "CAN_APPLY", false);
export const perimeterInvalid$ = bindValue<boolean>(mod.id, "PERIMETER_INVALID", false);
export const perimeterCollision$ = bindValue<boolean>(mod.id, "PERIMETER_COLLISION", false);
export const mode$ = bindValue<number>(mod.id, "MODE", 0);
export const columns$ = bindValue<number>(mod.id, "COLUMNS", 3);
export const rows$ = bindValue<number>(mod.id, "ROWS", 3);
export const spacing$ = bindValue<number>(mod.id, "SPACING", 60);
export const angleOffset$ = bindValue<number>(mod.id, "ANGLE_OFFSET", 0);
export const followTerrain$ = bindValue<boolean>(mod.id, "FOLLOW_TERRAIN", true);
export const culDeSacMode$ = bindValue<boolean>(mod.id, "CULDESAC_MODE", false);
/// Axe des becos sans saída : 0=Colunas, 1=Linhas, 2=Ambos (miroir de GridGenerator.CulDeSacAxis en C#).
export const culDeSacAxis$ = bindValue<number>(mod.id, "CULDESAC_AXIS", 0);
export const culDeSacDepth$ = bindValue<number>(mod.id, "CULDESAC_DEPTH", 75);
export const staggered$ = bindValue<boolean>(mod.id, "STAGGERED", true);
export const culDeSacRatio$ = bindValue<number>(mod.id, "CULDESAC_RATIO", 100);
/// Taille du cercle de retournement : 0=Auto, 1=Small, 2=Medium, 3=Large, 4=XL (miroir de CulDeSacCapSize en C#).
export const culDeSacCapSize$ = bindValue<number>(mod.id, "CULDESAC_CAP_SIZE", 1);
/// Style du cercle de retournement : 0=Asphalte, 1=Engazonné, 2=Arbres (miroir de CulDeSacCapStyle en C#).
export const culDeSacCapStyle$ = bindValue<number>(mod.id, "CULDESAC_CAP_STYLE", 0);
/// Avenue (troisième réseau, grille classique uniquement) : colonne/rangée choisie librement
/// par index (parmi les lignes réellement générées, 0-based), jamais un cul-de-sac.
export const avenueColumnEnabled$ = bindValue<boolean>(mod.id, "AVENUE_COLUMN_ENABLED", false);
export const avenueColumnIndex$ = bindValue<number>(mod.id, "AVENUE_COLUMN_INDEX", 0);
export const avenueRowEnabled$ = bindValue<boolean>(mod.id, "AVENUE_ROW_ENABLED", false);
export const avenueRowIndex$ = bindValue<number>(mod.id, "AVENUE_ROW_INDEX", 0);
/// Mode "Loop" (collectrices éparses + laço interne par super-îlot, voir GenerateLoopGrid) :
/// remplace la grille de lignes droites quand actif.
export const loopMode$ = bindValue<boolean>(mod.id, "LOOP_MODE", false);
export const superblockMode$ = bindValue<boolean>(mod.id, "SUPERBLOCK_MODE", false);
export const collectorSpacing$ = bindValue<number>(mod.id, "COLLECTOR_SPACING", 150);
export const superblockZone$ = bindValue<number>(mod.id, "SUPERBLOCK_ZONE", 150);
export const concentricMode$ = bindValue<boolean>(mod.id, "CONCENTRIC_MODE", false);
export const concentricLayers$ = bindValue<number>(mod.id, "CONCENTRIC_LAYERS", 3);
export const concentricConnections$ = bindValue<number>(mod.id, "CONCENTRIC_CONNECTIONS", 4);
/// Nombre maximal d'anneaux pour la forme sélectionnée (voir ConcentricGenerator.MaxLayers) :
/// borne haute du slider Camadas, 0 = forme trop petite pour un seul anneau.
export const concentricMaxLayers$ = bindValue<number>(mod.id, "CONCENTRIC_MAX_LAYERS", 10);export const loopCulDeSacRatio$ = bindValue<number>(mod.id, "LOOP_CULDESAC_RATIO", 50);
/// Melhoramentos automáticos (mode Loop) : Coletor/Avenida (Geral + Esquerda/Direita) e
/// Principal/Laço (Esquerda/Direita apenas, sem separador central).
export const avenueMiddleTrees$ = bindValue<boolean>(mod.id, "AVENUE_MIDDLE_TREES", false);
export const avenueMiddleGrass$ = bindValue<boolean>(mod.id, "AVENUE_MIDDLE_GRASS", false);
export const avenueSideTreesLeft$ = bindValue<boolean>(mod.id, "AVENUE_SIDE_TREES_LEFT", false);
export const avenueSideTreesRight$ = bindValue<boolean>(mod.id, "AVENUE_SIDE_TREES_RIGHT", false);
export const avenueBikeLaneLeft$ = bindValue<boolean>(mod.id, "AVENUE_BIKE_LANE_LEFT", false);
export const avenueBikeLaneRight$ = bindValue<boolean>(mod.id, "AVENUE_BIKE_LANE_RIGHT", false);
export const principalSideTreesLeft$ = bindValue<boolean>(mod.id, "PRINCIPAL_SIDE_TREES_LEFT", false);
export const principalSideTreesRight$ = bindValue<boolean>(mod.id, "PRINCIPAL_SIDE_TREES_RIGHT", false);
export const principalWideSidewalkLeft$ = bindValue<boolean>(mod.id, "PRINCIPAL_WIDE_SIDEWALK_LEFT", false);
export const principalWideSidewalkRight$ = bindValue<boolean>(mod.id, "PRINCIPAL_WIDE_SIDEWALK_RIGHT", false);
export const principalBikeLaneLeft$ = bindValue<boolean>(mod.id, "PRINCIPAL_BIKE_LANE_LEFT", false);
export const principalBikeLaneRight$ = bindValue<boolean>(mod.id, "PRINCIPAL_BIKE_LANE_RIGHT", false);
/// Vue disponible/sélectionnée (bitmask, miroir de ViewOption en C#) : 1=Underground, 2=ZoneGrid, 4=InvisibleNetworks.
export const availableViews$ = bindValue<number>(mod.id, "AVAILABLE_VIEWS", 7);
export const selectedViews$ = bindValue<number>(mod.id, "SELECTED_VIEWS", 0);
export const roadPrefabName$ = bindValue<string>(mod.id, "ROAD_PREFAB_NAME", "");
export const roadPrefabIcon$ = bindValue<string>(mod.id, "ROAD_PREFAB_ICON", "");
export const roadPrefabAuto$ = bindValue<boolean>(mod.id, "ROAD_PREFAB_AUTO", true);
/// Réseau secondaire (impasses/rayons) : mêmes trois bindings, préfixés SECONDARY_.
export const secondaryRoadPrefabName$ = bindValue<string>(mod.id, "SECONDARY_ROAD_PREFAB_NAME", "");
export const secondaryRoadPrefabIcon$ = bindValue<string>(mod.id, "SECONDARY_ROAD_PREFAB_ICON", "");
export const secondaryRoadPrefabAuto$ = bindValue<boolean>(mod.id, "SECONDARY_ROAD_PREFAB_AUTO", true);
/// Réseau avenue : mêmes trois bindings, préfixés AVENUE_.
export const avenueRoadPrefabName$ = bindValue<string>(mod.id, "AVENUE_ROAD_PREFAB_NAME", "");
export const avenueRoadPrefabIcon$ = bindValue<string>(mod.id, "AVENUE_ROAD_PREFAB_ICON", "");
export const avenueRoadPrefabAuto$ = bindValue<boolean>(mod.id, "AVENUE_ROAD_PREFAB_AUTO", true);
export const anarchyAvailable$ = bindValue<boolean>(mod.id, "ANARCHY_AVAILABLE", false);
export const pickerType$ = bindValue<number>(mod.id, "PICKER_TYPE", 0);
export const pickerData$ = bindValue<PrefabEntry[]>(mod.id, "PICKER_DATA", []);
export const recentPrefabs$ = bindValue<PrefabEntry[]>(mod.id, "RECENT_PREFABS", []);

// Déclencheurs vers le C# (synchronisés avec Options > Mods côté C#).
export const setMode = (value: number) => trigger(mod.id, "SET_MODE", value);
export const setColumns = (value: number) => trigger(mod.id, "SET_COLUMNS", value);
export const setRows = (value: number) => trigger(mod.id, "SET_ROWS", value);
export const setSpacing = (value: number) => trigger(mod.id, "SET_SPACING", value);

/// Un champ de GridParameters à la fois, voir GridRoadToolSystem.LivePreviewField — l'ORDRE/
/// les valeurs DOIVENT rester synchronisés avec cet enum C#, un entier brut transite sur le
/// binding (pas de partage de type possible entre C# et TS).
export const LiveField = {
    Spacing: 0,
    Columns: 1,
    Rows: 2,
    Angle: 3,
    // 4 = ArterialSpacing, supprimé avec le niveau Arterial côté Core — valeur volontairement
    // non réutilisée pour rester synchronisée avec LivePreviewField côté C#.
    CollectorSpacing: 5,
    LoopCulDeSacRatio: 6,
    CulDeSacDepth: 7,
    CulDeSacRatio: 8,
    AvenueColumnIndex: 9,
    AvenueRowIndex: 10,
    SuperblockZone: 11,
    ConcentricLayers: 12,
    ConcentricConnections: 13,
} as const;
/// Croquis léger pendant un drag (voir GridRoadOverlaySystem.DrawLiveSketch) : ne touche
/// jamais les settings ni ne régénère la vraie grille, juste des lignes dessinées. Un seul
/// mécanisme générique pour TOUS les sliders — voir SliderControl (onDragPreview/onDragEnd).
export const setLivePreview = (field: number, value: number) => trigger(mod.id, "SET_LIVE_PREVIEW", field, value);
export const clearLivePreview = () => trigger(mod.id, "CLEAR_LIVE_PREVIEW");
export const setAngleOffset = (value: number) => trigger(mod.id, "SET_ANGLE_OFFSET", value);
/** Remet les paramètres de forme de tous les motifs aux valeurs d'origine (voir GridRoadGeneratorSettings.ResetPanelParameters). */
export const resetDefaults = () => trigger(mod.id, "RESET_DEFAULTS");
export const setFollowTerrain = (value: boolean) => trigger(mod.id, "SET_FOLLOW_TERRAIN", value);
export const setCulDeSacMode = (value: boolean) => trigger(mod.id, "SET_CULDESAC_MODE", value);
export const setCulDeSacAxis = (value: number) => trigger(mod.id, "SET_CULDESAC_AXIS", value);
export const setCulDeSacDepth = (value: number) => trigger(mod.id, "SET_CULDESAC_DEPTH", value);
export const setStaggered = (value: boolean) => trigger(mod.id, "SET_STAGGERED", value);
export const setCulDeSacRatio = (value: number) => trigger(mod.id, "SET_CULDESAC_RATIO", value);
export const setCulDeSacCapSize = (value: number) => trigger(mod.id, "SET_CULDESAC_CAP_SIZE", value);
export const setCulDeSacCapStyle = (value: number) => trigger(mod.id, "SET_CULDESAC_CAP_STYLE", value);
export const setAvenueColumnEnabled = (value: boolean) => trigger(mod.id, "SET_AVENUE_COLUMN_ENABLED", value);
export const setAvenueColumnIndex = (value: number) => trigger(mod.id, "SET_AVENUE_COLUMN_INDEX", value);
export const setAvenueRowEnabled = (value: boolean) => trigger(mod.id, "SET_AVENUE_ROW_ENABLED", value);
export const setAvenueRowIndex = (value: number) => trigger(mod.id, "SET_AVENUE_ROW_INDEX", value);
export const setLoopMode = (value: boolean) => trigger(mod.id, "SET_LOOP_MODE", value);
export const setSuperblockMode = (value: boolean) => trigger(mod.id, "SET_SUPERBLOCK_MODE", value);
export const setCollectorSpacing = (value: number) => trigger(mod.id, "SET_COLLECTOR_SPACING", value);
export const setSuperblockZone = (value: number) => trigger(mod.id, "SET_SUPERBLOCK_ZONE", value);
export const setConcentricMode = (value: boolean) => trigger(mod.id, "SET_CONCENTRIC_MODE", value);
export const setConcentricLayers = (value: number) => trigger(mod.id, "SET_CONCENTRIC_LAYERS", value);
export const setConcentricConnections = (value: number) => trigger(mod.id, "SET_CONCENTRIC_CONNECTIONS", value);export const setLoopCulDeSacRatio = (value: number) => trigger(mod.id, "SET_LOOP_CULDESAC_RATIO", value);
export const setAvenueMiddleTrees = (value: boolean) => trigger(mod.id, "SET_AVENUE_MIDDLE_TREES", value);
export const setAvenueMiddleGrass = (value: boolean) => trigger(mod.id, "SET_AVENUE_MIDDLE_GRASS", value);
export const setAvenueSideTreesLeft = (value: boolean) => trigger(mod.id, "SET_AVENUE_SIDE_TREES_LEFT", value);
export const setAvenueSideTreesRight = (value: boolean) => trigger(mod.id, "SET_AVENUE_SIDE_TREES_RIGHT", value);
export const setAvenueBikeLaneLeft = (value: boolean) => trigger(mod.id, "SET_AVENUE_BIKE_LANE_LEFT", value);
export const setAvenueBikeLaneRight = (value: boolean) => trigger(mod.id, "SET_AVENUE_BIKE_LANE_RIGHT", value);
export const setPrincipalSideTreesLeft = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_SIDE_TREES_LEFT", value);
export const setPrincipalSideTreesRight = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_SIDE_TREES_RIGHT", value);
export const setPrincipalWideSidewalkLeft = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_WIDE_SIDEWALK_LEFT", value);
export const setPrincipalWideSidewalkRight = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_WIDE_SIDEWALK_RIGHT", value);
export const setPrincipalBikeLaneLeft = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_BIKE_LANE_LEFT", value);
export const setPrincipalBikeLaneRight = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_BIKE_LANE_RIGHT", value);
export const setSelectedViews = (value: number) => trigger(mod.id, "SET_SELECTED_VIEWS", value);
export const generateGrid = () => trigger(mod.id, "GENERATE");
export const toggleTool = () => trigger(mod.id, "TOGGLE_TOOL");
export const setPickerType = (value: number) => trigger(mod.id, "SET_PICKER_TYPE", value);

// Bindings du mod Anarchy lui-même (groupe "Anarchy", noms relevés dans son
// bundle UI). Lus/déclenchés uniquement quand ANARCHY_AVAILABLE est vrai :
// sur un binding absent, bindValue reste simplement à sa valeur de repli.
export const anarchyEnabled$ = bindValue<boolean>("Anarchy", "AnarchyEnabled", false);
export const toggleAnarchy = () => trigger("Anarchy", "AnarchyToggled");
export const pickPrefab = (entity: Entity) => trigger(mod.id, "PICK_PREFAB", entity);
export const pickAuto = () => trigger(mod.id, "PICK_AUTO");
export const pickSecondaryPrefab = (entity: Entity) => trigger(mod.id, "PICK_PREFAB_SECONDARY", entity);
export const pickSecondaryAuto = () => trigger(mod.id, "PICK_AUTO_SECONDARY");
export const pickAvenuePrefab = (entity: Entity) => trigger(mod.id, "PICK_PREFAB_AVENUE", entity);
export const pickAvenueAuto = () => trigger(mod.id, "PICK_AUTO_AVENUE");
