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
export const alignTerrain$ = bindValue<boolean>(mod.id, "ALIGN_TERRAIN", false);
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
export const radialMode$ = bindValue<boolean>(mod.id, "RADIAL_MODE", false);
export const treeMode$ = bindValue<boolean>(mod.id, "TREE_MODE", false);
export const organicMode$ = bindValue<boolean>(mod.id, "ORGANIC_MODE", false);
/// Motif Misto : Radial au centre, Orgânico autour.
export const mixedMode$ = bindValue<boolean>(mod.id, "MIXED_MODE", false);
export const mixedCoreRadius$ = bindValue<number>(mod.id, "MIXED_CORE_RADIUS", 220);
export const setMixedMode = (value: boolean) => trigger(mod.id, "SET_MIXED_MODE", value);
export const setMixedCoreRadius = (value: number) => trigger(mod.id, "SET_MIXED_CORE_RADIUS", value);
export const contourMode$ = bindValue<boolean>(mod.id, "CONTOUR_MODE", false);
export const contourSpacing$ = bindValue<number>(mod.id, "CONTOUR_SPACING", 90);
export const contourConnectorSpacing$ = bindValue<number>(mod.id, "CONTOUR_CONNECTOR_SPACING", 250);
export const contourFlat$ = bindValue<boolean>(mod.id, "CONTOUR_FLAT", false);
export const selectionMode$ = bindValue<number>(mod.id, "SELECTION_MODE", 0);
export const brushSize$ = bindValue<number>(mod.id, "BRUSH_SIZE", 150);
export const brushSquare$ = bindValue<boolean>(mod.id, "BRUSH_SQUARE", false);
export const brushAngle$ = bindValue<number>(mod.id, "BRUSH_ANGLE", 0);
export const freeAreaClosed$ = bindValue<boolean>(mod.id, "FREE_AREA_CLOSED", false);
export const freeAreaInvalid$ = bindValue<boolean>(mod.id, "FREE_AREA_INVALID", false);
export const organicStreetSpacing$ = bindValue<number>(mod.id, "ORGANIC_STREET_SPACING", 80);
export const organicCurviness$ = bindValue<number>(mod.id, "ORGANIC_CURVINESS", 60);
export const organicLoopShare$ = bindValue<number>(mod.id, "ORGANIC_LOOP_SHARE", 30);
export const organicSeed$ = bindValue<number>(mod.id, "ORGANIC_SEED", 1);
export const treeBranchSpacing$ = bindValue<number>(mod.id, "TREE_BRANCH_SPACING", 240);
export const treeCulDeSacSpacing$ = bindValue<number>(mod.id, "TREE_CULDESAC_SPACING", 80);
export const treeCulDeSacLength$ = bindValue<number>(mod.id, "TREE_CULDESAC_LENGTH", 90);
export const radialAvenues$ = bindValue<number>(mod.id, "RADIAL_AVENUES", 8);
export const radialRoundabout$ = bindValue<number>(mod.id, "RADIAL_ROUNDABOUT", 50);
export const radialLayers$ = bindValue<number>(mod.id, "RADIAL_LAYERS", 2);
/// Nombre maximal d'anneaux du Radial pour la sélection et les réglages (voir ConcentricGenerator.RadialMaxLayers).
export const radialMaxLayers$ = bindValue<number>(mod.id, "RADIAL_MAX_LAYERS", 10);
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
export const avenueSideGrassLeft$ = bindValue<boolean>(mod.id, "AVENUE_SIDE_GRASS_LEFT", false);
export const avenueSideGrassRight$ = bindValue<boolean>(mod.id, "AVENUE_SIDE_GRASS_RIGHT", false);
export const principalSideGrassLeft$ = bindValue<boolean>(mod.id, "PRINCIPAL_SIDE_GRASS_LEFT", false);
export const principalSideGrassRight$ = bindValue<boolean>(mod.id, "PRINCIPAL_SIDE_GRASS_RIGHT", false);
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
/// Melhoramentos que chaque réseau choisi sait afficher (masque, voir GridRoadUISystem.UpgradeSupport).
export const UpgradeSupport = { MiddleTrees: 1, MiddleGrass: 2, SideTrees: 4, SideGrass: 8, WideSidewalk: 16, BikeLane: 32 } as const;
export const primaryUpgradeSupport$ = bindValue<number>(mod.id, "PRIMARY_UPGRADE_SUPPORT", 63);
export const secondaryUpgradeSupport$ = bindValue<number>(mod.id, "SECONDARY_UPGRADE_SUPPORT", 63);
export const avenueUpgradeSupport$ = bindValue<number>(mod.id, "AVENUE_UPGRADE_SUPPORT", 63);
/// Rotonde centrale du motif Radial (RoadSegmentDef.IsRoundabout) : son propre réseau.
export const roundaboutRoadPrefabName$ = bindValue<string>(mod.id, "ROUNDABOUT_ROAD_PREFAB_NAME", "");
export const roundaboutRoadPrefabIcon$ = bindValue<string>(mod.id, "ROUNDABOUT_ROAD_PREFAB_ICON", "");
export const roundaboutRoadPrefabAuto$ = bindValue<boolean>(mod.id, "ROUNDABOUT_ROAD_PREFAB_AUTO", true);
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
    RadialAvenues: 14,
    RadialRoundabout: 15,
    RadialLayers: 16,
    // 17 = FishboneRibSpacing, motif retiré — valeur volontairement non réutilisée.
    TreeBranchSpacing: 18,
    TreeCulDeSacSpacing: 19,
    TreeCulDeSacLength: 20,
    OrganicStreetSpacing: 21,
    OrganicCurviness: 22,
    OrganicLoopShare: 23,
    OrganicSeed: 24,
    ContourSpacing: 25,
    ContourConnectorSpacing: 26,
    MixedCoreRadius: 27,
} as const;
/// Croquis léger pendant un drag (voir GridRoadOverlaySystem.DrawLiveSketch) : ne touche
/// jamais les settings ni ne régénère la vraie grille, juste des lignes dessinées. Un seul
/// mécanisme générique pour TOUS les sliders — voir SliderControl (onDragPreview/onDragEnd).
export const setLivePreview = (field: number, value: number) => trigger(mod.id, "SET_LIVE_PREVIEW", field, value);
export const clearLivePreview = () => trigger(mod.id, "CLEAR_LIVE_PREVIEW");
export const setAngleOffset = (value: number) => trigger(mod.id, "SET_ANGLE_OFFSET", value);
/** Remet les paramètres de forme de tous les motifs aux valeurs d'origine (voir GridRoadGeneratorSettings.ResetPanelParameters). */
export const resetDefaults = () => trigger(mod.id, "RESET_DEFAULTS");
/// Annuler / refaire (aussi Ctrl+Z / Ctrl+Y en jeu) — voir GridRoadToolSystem.History.
export const canUndo$ = bindValue<boolean>(mod.id, "CAN_UNDO", false);
export const canRedo$ = bindValue<boolean>(mod.id, "CAN_REDO", false);
export const undo = () => trigger(mod.id, "UNDO");
/// Zonage automatique : zones du jeu ("nom\ticône" par ligne) et zone choisie (vide = aucune).
export const zoneOptions$ = bindValue<string>(mod.id, "ZONE_OPTIONS", "");
export const zoningPrefab$ = bindValue<string>(mod.id, "ZONING_PREFAB", "");
export const setZoningPrefab = (name: string) => trigger(mod.id, "SET_ZONING_PREFAB", name);
/// Résumé de la grille prévisualisée (au-dessus de Générer) : tronçons, longueur (m), coût estimé.
export const summarySegments$ = bindValue<number>(mod.id, "SUMMARY_SEGMENTS", 0);
export const summaryLength$ = bindValue<number>(mod.id, "SUMMARY_LENGTH", 0);
export const summaryCost$ = bindValue<number>(mod.id, "SUMMARY_COST", 0);
export const redo = () => trigger(mod.id, "REDO");
export const setFollowTerrain = (value: boolean) => trigger(mod.id, "SET_FOLLOW_TERRAIN", value);
export const setAlignTerrain = (value: boolean) => trigger(mod.id, "SET_ALIGN_TERRAIN", value);
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
export const setConcentricConnections = (value: number) => trigger(mod.id, "SET_CONCENTRIC_CONNECTIONS", value);
export const setRadialMode = (value: boolean) => trigger(mod.id, "SET_RADIAL_MODE", value);
export const setTreeMode = (value: boolean) => trigger(mod.id, "SET_TREE_MODE", value);
export const setOrganicMode = (value: boolean) => trigger(mod.id, "SET_ORGANIC_MODE", value);
export const setContourMode = (value: boolean) => trigger(mod.id, "SET_CONTOUR_MODE", value);
export const setSelectionMode = (value: number) => trigger(mod.id, "SET_SELECTION_MODE", value);
export const setBrushSize = (value: number) => trigger(mod.id, "SET_BRUSH_SIZE", value);
export const setBrushSquare = (value: boolean) => trigger(mod.id, "SET_BRUSH_SQUARE", value);
export const setBrushAngle = (value: number) => trigger(mod.id, "SET_BRUSH_ANGLE", value);
export const setContourSpacing = (value: number) => trigger(mod.id, "SET_CONTOUR_SPACING", value);
export const setContourConnectorSpacing = (value: number) => trigger(mod.id, "SET_CONTOUR_CONNECTOR_SPACING", value);
export const setOrganicStreetSpacing = (value: number) => trigger(mod.id, "SET_ORGANIC_STREET_SPACING", value);
export const setOrganicCurviness = (value: number) => trigger(mod.id, "SET_ORGANIC_CURVINESS", value);
export const setOrganicLoopShare = (value: number) => trigger(mod.id, "SET_ORGANIC_LOOP_SHARE", value);
export const setOrganicSeed = (value: number) => trigger(mod.id, "SET_ORGANIC_SEED", value);
export const setTreeBranchSpacing = (value: number) => trigger(mod.id, "SET_TREE_BRANCH_SPACING", value);
export const setTreeCulDeSacSpacing = (value: number) => trigger(mod.id, "SET_TREE_CULDESAC_SPACING", value);
export const setTreeCulDeSacLength = (value: number) => trigger(mod.id, "SET_TREE_CULDESAC_LENGTH", value);
export const setRadialAvenues = (value: number) => trigger(mod.id, "SET_RADIAL_AVENUES", value);
export const setRadialRoundabout = (value: number) => trigger(mod.id, "SET_RADIAL_ROUNDABOUT", value);
export const setRadialLayers = (value: number) => trigger(mod.id, "SET_RADIAL_LAYERS", value);
export const setLoopCulDeSacRatio = (value: number) => trigger(mod.id, "SET_LOOP_CULDESAC_RATIO", value);
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
export const setAvenueSideGrassLeft = (value: boolean) => trigger(mod.id, "SET_AVENUE_SIDE_GRASS_LEFT", value);
export const setAvenueSideGrassRight = (value: boolean) => trigger(mod.id, "SET_AVENUE_SIDE_GRASS_RIGHT", value);
export const setPrincipalSideGrassLeft = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_SIDE_GRASS_LEFT", value);
export const setPrincipalSideGrassRight = (value: boolean) => trigger(mod.id, "SET_PRINCIPAL_SIDE_GRASS_RIGHT", value);
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
export const pickRoundaboutPrefab = (entity: Entity) => trigger(mod.id, "PICK_PREFAB_ROUNDABOUT", entity);
export const pickRoundaboutAuto = () => trigger(mod.id, "PICK_AUTO_ROUNDABOUT");
/// Liaisons piétonnes entre impasses et leur réseau (chemin piéton par défaut).
export const pedestrianLinks$ = bindValue<boolean>(mod.id, "PEDESTRIAN_LINKS", false);
export const setPedestrianLinks = (value: boolean) => trigger(mod.id, "SET_PEDESTRIAN_LINKS", value);
export const pathRoadPrefabName$ = bindValue<string>(mod.id, "PATH_ROAD_PREFAB_NAME", "");
export const pathRoadPrefabIcon$ = bindValue<string>(mod.id, "PATH_ROAD_PREFAB_ICON", "");
export const pathRoadPrefabAuto$ = bindValue<boolean>(mod.id, "PATH_ROAD_PREFAB_AUTO", true);
export const pickPathPrefab = (entity: Entity) => trigger(mod.id, "PICK_PREFAB_PATH", entity);
export const pickPathAuto = () => trigger(mod.id, "PICK_AUTO_PATH");
