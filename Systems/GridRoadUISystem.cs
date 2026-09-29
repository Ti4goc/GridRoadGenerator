// Pattern de bindings cohtml adapté de CS2-NetworkTools (c) Luca Rager,
// licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System;
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Pont C# ↔ panneau React (module UI GridRoadGenerator.mjs).
    /// Les valeurs (mode, colonnes, lignes, espacement) sont lues depuis les settings du mod
    /// et poussées vers l'UI à chaque frame (ValueBinding.Update ne notifie que les
    /// changements) : le panneau reste donc synchronisé avec Options > Mods dans les deux
    /// sens. Les setters écrivent dans les settings et sauvegardent.
    /// </summary>
    public partial class GridRoadUISystem : UISystemBase
    {
        /// <summary>Groupe des bindings, côté TS : bindValue(GROUP, clé) / trigger(GROUP, clé).</summary>
        public const string BindingGroup = "GridRoadGenerator";

        private GridRoadGeneratorSettings _settings;
        private GridRoadToolSystem _toolSystem;
        private ToolSystem _gameToolSystem;

        /// <summary>
        /// Délai (s) sans nouveau changement avant d'écrire les settings sur disque — retour
        /// utilisateur en jeu, "o menu está lento a qualquer interação" : chaque tick d'un
        /// slider (Espaçamento, Ângulo, Profondeur…) appelait auparavant _settings.ApplyAndSave()
        /// DIRECTEMENT, qui écrit sur disque (Setting.ApplyAndSave -> AssetDatabase.global.
        /// SaveSpecificSetting) à CHAQUE pixel parcouru pendant un drag de slider — voir
        /// MarkSettingsDirty/OnUpdate. La valeur reste appliquée en mémoire immédiatement (donc
        /// aucune perte de réactivité UI, ToGridParameters lit toujours _settings directement) ;
        /// seule l'écriture disque, coûteuse et inutile à chaque pixel, est différée.
        /// </summary>
        private const float SettingsSaveDebounceSeconds = 0.4f;
        private bool _settingsDirty;
        private float _settingsDirtyTimestamp;

        private ValueBinding<bool> _toolActiveBinding;
        private ValueBinding<int> _nodeCountBinding;
        private ValueBinding<bool> _canApplyBinding;
        private ValueBinding<bool> _invalidBinding;
        private ValueBinding<bool> _collisionBinding;
        private ValueBinding<int> _modeBinding;
        private ValueBinding<int> _columnsBinding;
        private ValueBinding<int> _rowsBinding;
        private ValueBinding<float> _spacingBinding;
        private ValueBinding<float> _angleOffsetBinding;
        private ValueBinding<bool> _followTerrainBinding;
        private ValueBinding<bool> _alignTerrainBinding;
        private ValueBinding<bool> _culDeSacModeBinding;
        private ValueBinding<int> _culDeSacAxisBinding;
        private ValueBinding<float> _culDeSacDepthBinding;
        private ValueBinding<bool> _staggeredBinding;
        private ValueBinding<float> _culDeSacRatioBinding;
        private ValueBinding<int> _culDeSacCapSizeBinding;
        private ValueBinding<int> _culDeSacCapStyleBinding;
        private ValueBinding<int> _availableViewsBinding;
        private ValueBinding<int> _selectedViewsBinding;
        private ValueBinding<string> _roadPrefabNameBinding;
        private ValueBinding<string> _roadPrefabIconBinding;
        private ValueBinding<bool> _roadPrefabAutoBinding;
        private ValueBinding<string> _secondaryRoadPrefabNameBinding;
        private ValueBinding<string> _secondaryRoadPrefabIconBinding;
        private ValueBinding<bool> _secondaryRoadPrefabAutoBinding;
        private ValueBinding<bool> _avenueColumnEnabledBinding;
        private ValueBinding<int> _avenueColumnIndexBinding;
        private ValueBinding<bool> _avenueRowEnabledBinding;
        private ValueBinding<int> _avenueRowIndexBinding;
        private ValueBinding<bool> _loopModeBinding;
        private ValueBinding<bool> _superblockModeBinding;
        private ValueBinding<float> _collectorSpacingBinding;
        private ValueBinding<float> _superblockZoneBinding;
        private ValueBinding<bool> _concentricModeBinding;
        private ValueBinding<int> _concentricLayersBinding;
        private ValueBinding<int> _concentricConnectionsBinding;
        private ValueBinding<bool> _radialModeBinding;
        private ValueBinding<int> _radialAvenuesBinding;
        private ValueBinding<float> _radialRoundaboutBinding;
        private ValueBinding<int> _radialLayersBinding;
        private ValueBinding<bool> _treeModeBinding;
        private ValueBinding<float> _treeBranchSpacingBinding;
        private ValueBinding<float> _treeCulDeSacSpacingBinding;
        private ValueBinding<float> _treeCulDeSacLengthBinding;
        private ValueBinding<bool> _organicModeBinding;
        private ValueBinding<bool> _mixedModeBinding;
        private ValueBinding<float> _mixedCoreRadiusBinding;
        private ValueBinding<float> _organicStreetSpacingBinding;
        private ValueBinding<float> _organicCurvinessBinding;
        private ValueBinding<float> _organicLoopShareBinding;
        private ValueBinding<int> _organicSeedBinding;
        private ValueBinding<bool> _contourModeBinding;
        private ValueBinding<float> _contourSpacingBinding;
        private ValueBinding<float> _contourConnectorSpacingBinding;
        private ValueBinding<bool> _contourFlatBinding;
        private ValueBinding<int> _selectionModeBinding;
        private ValueBinding<float> _brushSizeBinding;
        private ValueBinding<bool> _brushSquareBinding;
        private ValueBinding<bool> _canUndoBinding;
        private ValueBinding<string> _zoneOptionsBinding;
        private ValueBinding<string> _zoningPrefabBinding;
        private ValueBinding<int> _summarySegmentsBinding;
        private ValueBinding<float> _summaryLengthBinding;
        private ValueBinding<double> _summaryCostBinding;
        private ValueBinding<bool> _canRedoBinding;
        private ValueBinding<float> _brushAngleBinding;
        private ValueBinding<bool> _freeAreaClosedBinding;
        private ValueBinding<bool> _freeAreaInvalidBinding;
        private ValueBinding<int> _radialMaxLayersBinding;
        private ValueBinding<int> _concentricMaxLayersBinding;
        private ValueBinding<float> _loopCulDeSacRatioBinding;
        private ValueBinding<bool> _avenueMiddleTreesBinding;
        private ValueBinding<bool> _avenueMiddleGrassBinding;
        private ValueBinding<bool> _avenueSideTreesLeftBinding;
        private ValueBinding<bool> _avenueSideTreesRightBinding;
        private ValueBinding<bool> _avenueBikeLaneLeftBinding;
        private ValueBinding<bool> _avenueBikeLaneRightBinding;
        private ValueBinding<bool> _principalSideTreesLeftBinding;
        private ValueBinding<bool> _principalSideTreesRightBinding;
        private ValueBinding<bool> _principalWideSidewalkLeftBinding;
        private ValueBinding<bool> _principalWideSidewalkRightBinding;
        private ValueBinding<bool> _principalBikeLaneLeftBinding;
        private ValueBinding<bool> _principalBikeLaneRightBinding;
        private ValueBinding<bool> _avenueSideGrassLeftBinding;
        private ValueBinding<bool> _avenueSideGrassRightBinding;
        private ValueBinding<bool> _principalSideGrassLeftBinding;
        private ValueBinding<bool> _principalSideGrassRightBinding;
        private ValueBinding<string> _avenueRoadPrefabNameBinding;
        private ValueBinding<string> _avenueRoadPrefabIconBinding;
        private ValueBinding<int> _primarySupportBinding;
        private ValueBinding<int> _secondarySupportBinding;
        private ValueBinding<int> _avenueSupportBinding;
        private ValueBinding<string> _pathPrefabNameBinding;
        private ValueBinding<string> _pathPrefabIconBinding;
        private ValueBinding<bool> _pathPrefabAutoBinding;
        private ValueBinding<bool> _pedestrianLinksBinding;
        private ValueBinding<string> _roundaboutRoadPrefabNameBinding;
        private ValueBinding<string> _roundaboutRoadPrefabIconBinding;
        private ValueBinding<bool> _roundaboutRoadPrefabAutoBinding;
        private ValueBinding<bool> _avenueRoadPrefabAutoBinding;
        private ValueBinding<bool> _anarchyAvailableBinding;
        private bool _anarchyAvailable;

        // Sélecteur de réseau (pattern PrefabSelectionUISystem de CS2-NetworkTools, MIT).
        private ValueBinding<int> _pickerTypeBinding;
        private RawValueBinding _pickerDataBinding;
        private RawValueBinding _recentPrefabsBinding;
        private PrefabSystem _prefabSystem;
        private int _lastPickerType = -1;
        private readonly List<(Entity entity, string name, string icon)> _pickerEntries
            = new List<(Entity, string, string)>();
        /// <summary>Réseaux choisis récemment (session en cours), du plus récent au plus ancien.</summary>
        private readonly List<Entity> _recentPrefabs = new List<Entity>();
        private const int MaxRecentPrefabs = 5;

        /// <summary>Onglets du sélecteur ; les valeurs doivent correspondre au TS (prefabPicker.tsx).</summary>
        private enum PickerType
        {
            Road = 0,
            Path = 1,
            Rail = 2,
            Waterway = 3
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            _settings = Mod.Instance.Settings;
            _toolSystem = World.GetOrCreateSystemManaged<GridRoadToolSystem>();
            _gameToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            // État de l'outil → UI.
            AddBinding(_toolActiveBinding = new ValueBinding<bool>(BindingGroup, "TOOL_ACTIVE", false));
            AddBinding(_nodeCountBinding = new ValueBinding<int>(BindingGroup, "NODE_COUNT", 0));
            AddBinding(_canApplyBinding = new ValueBinding<bool>(BindingGroup, "CAN_APPLY", false));
            AddBinding(_invalidBinding = new ValueBinding<bool>(BindingGroup, "PERIMETER_INVALID", false));
            AddBinding(_collisionBinding = new ValueBinding<bool>(BindingGroup, "PERIMETER_COLLISION", false));

            // Réglages ↔ UI (synchronisés avec Options > Mods).
            AddBinding(_modeBinding = new ValueBinding<int>(BindingGroup, "MODE", (int)_settings.Mode));
            AddBinding(_columnsBinding = new ValueBinding<int>(BindingGroup, "COLUMNS", _settings.Columns));
            AddBinding(_rowsBinding = new ValueBinding<int>(BindingGroup, "ROWS", _settings.Rows));
            AddBinding(_spacingBinding = new ValueBinding<float>(BindingGroup, "SPACING", _settings.SpacingMeters));
            AddBinding(_angleOffsetBinding = new ValueBinding<float>(BindingGroup, "ANGLE_OFFSET", _settings.AngleOffsetDegrees));
            AddBinding(_followTerrainBinding = new ValueBinding<bool>(BindingGroup, "FOLLOW_TERRAIN", _settings.FollowTerrain));
            AddBinding(_alignTerrainBinding = new ValueBinding<bool>(BindingGroup, "ALIGN_TERRAIN", _settings.AlignToTerrain));
            AddBinding(_culDeSacModeBinding = new ValueBinding<bool>(BindingGroup, "CULDESAC_MODE", _settings.CulDeSacMode));
            AddBinding(_culDeSacAxisBinding = new ValueBinding<int>(BindingGroup, "CULDESAC_AXIS", (int)_settings.CulDeSacAxis));
            // Exposée en pourcentage (50-90) côté UI, comme le slider Options > Mods ;
            // stockée en fraction (0.5-0.9) dans les settings pour matcher GridParameters.
            AddBinding(_culDeSacDepthBinding = new ValueBinding<float>(BindingGroup, "CULDESAC_DEPTH", GridRoadGeneratorSettings.CulDeSacDepthRealToUi(_settings.CulDeSacDepth)));
            AddBinding(_staggeredBinding = new ValueBinding<bool>(BindingGroup, "STAGGERED", _settings.Staggered));
            AddBinding(_culDeSacRatioBinding = new ValueBinding<float>(BindingGroup, "CULDESAC_RATIO", _settings.CulDeSacRatio));
            AddBinding(_culDeSacCapSizeBinding = new ValueBinding<int>(BindingGroup, "CULDESAC_CAP_SIZE", (int)_settings.CulDeSacCapSize));
            AddBinding(_culDeSacCapStyleBinding = new ValueBinding<int>(BindingGroup, "CULDESAC_CAP_STYLE", (int)_settings.CulDeSacCapStyle));
            // Avenue (troisième réseau, grille classique uniquement) : colonne/rangée choisie
            // librement par index, jamais un cul-de-sac. Voir GridParameters.AvenueColumnEnabled.
            AddBinding(_avenueColumnEnabledBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_COLUMN_ENABLED", _settings.AvenueColumnEnabled));
            AddBinding(_avenueColumnIndexBinding = new ValueBinding<int>(BindingGroup, "AVENUE_COLUMN_INDEX", _settings.AvenueColumnIndex));
            AddBinding(_avenueRowEnabledBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_ROW_ENABLED", _settings.AvenueRowEnabled));
            AddBinding(_avenueRowIndexBinding = new ValueBinding<int>(BindingGroup, "AVENUE_ROW_INDEX", _settings.AvenueRowIndex));

            // Loop (collectrices éparses + laço interne par super-îlot) — voir
            // GridGenerator.GenerateLoopGrid/GridRoadGeneratorSettings.LoopMode.
            AddBinding(_loopModeBinding = new ValueBinding<bool>(BindingGroup, "LOOP_MODE", _settings.LoopMode));
            AddBinding(_superblockModeBinding = new ValueBinding<bool>(BindingGroup, "SUPERBLOCK_MODE", _settings.SuperblockMode));
            AddBinding(_collectorSpacingBinding = new ValueBinding<float>(BindingGroup, "COLLECTOR_SPACING", _settings.CollectorSpacingMeters));
            AddBinding(_superblockZoneBinding = new ValueBinding<float>(BindingGroup, "SUPERBLOCK_ZONE", _settings.EffectiveSuperblockZoneMeters));
            GridParameters initialParameters = _settings.ToGridParameters();
            AddBinding(_concentricModeBinding = new ValueBinding<bool>(BindingGroup, "CONCENTRIC_MODE", _settings.ConcentricMode));
            AddBinding(_concentricLayersBinding = new ValueBinding<int>(BindingGroup, "CONCENTRIC_LAYERS", initialParameters.ConcentricLayers));
            AddBinding(_concentricConnectionsBinding = new ValueBinding<int>(BindingGroup, "CONCENTRIC_CONNECTIONS", initialParameters.ConcentricConnections));
            AddBinding(_radialModeBinding = new ValueBinding<bool>(BindingGroup, "RADIAL_MODE", _settings.RadialMode));
            AddBinding(_radialAvenuesBinding = new ValueBinding<int>(BindingGroup, "RADIAL_AVENUES", initialParameters.RadialAvenues));
            AddBinding(_radialRoundaboutBinding = new ValueBinding<float>(BindingGroup, "RADIAL_ROUNDABOUT", initialParameters.RadialRoundaboutRadius));
            AddBinding(_radialLayersBinding = new ValueBinding<int>(BindingGroup, "RADIAL_LAYERS", initialParameters.RadialLayers));
            AddBinding(_treeModeBinding = new ValueBinding<bool>(BindingGroup, "TREE_MODE", _settings.TreeMode));
            AddBinding(_treeBranchSpacingBinding = new ValueBinding<float>(BindingGroup, "TREE_BRANCH_SPACING", TreeValue(_settings.TreeBranchSpacing, GridGenerator.TreeBranchSpacingDefault)));
            AddBinding(_treeCulDeSacSpacingBinding = new ValueBinding<float>(BindingGroup, "TREE_CULDESAC_SPACING", TreeValue(_settings.TreeCulDeSacSpacing, GridGenerator.TreeCulDeSacSpacingDefault)));
            AddBinding(_treeCulDeSacLengthBinding = new ValueBinding<float>(BindingGroup, "TREE_CULDESAC_LENGTH", TreeValue(_settings.TreeCulDeSacLength, GridGenerator.TreeCulDeSacLengthDefault)));
            AddBinding(_organicModeBinding = new ValueBinding<bool>(BindingGroup, "ORGANIC_MODE", _settings.OrganicMode));
            AddBinding(_mixedModeBinding = new ValueBinding<bool>(BindingGroup, "MIXED_MODE", _settings.MixedMode));
            AddBinding(_mixedCoreRadiusBinding = new ValueBinding<float>(BindingGroup, "MIXED_CORE_RADIUS", TreeValue(_settings.MixedCoreRadius, GridGenerator.MixedCoreRadiusDefault)));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_MIXED_MODE", value =>
            {
                _settings.MixedMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_MIXED_CORE_RADIUS", value =>
            {
                _settings.MixedCoreRadius = math.clamp(value, GridGenerator.MinMixedCoreRadius, GridGenerator.MaxMixedCoreRadius);
                MarkSettingsDirty();
            }));
            AddBinding(_organicStreetSpacingBinding = new ValueBinding<float>(BindingGroup, "ORGANIC_STREET_SPACING", TreeValue(_settings.OrganicStreetSpacing, GridGenerator.OrganicStreetSpacingDefault)));
            AddBinding(_organicCurvinessBinding = new ValueBinding<float>(BindingGroup, "ORGANIC_CURVINESS", _settings.OrganicCurviness));
            AddBinding(_organicLoopShareBinding = new ValueBinding<float>(BindingGroup, "ORGANIC_LOOP_SHARE", _settings.OrganicLoopShare));
            AddBinding(_organicSeedBinding = new ValueBinding<int>(BindingGroup, "ORGANIC_SEED", math.max(_settings.OrganicSeed, GridGenerator.MinOrganicSeed)));
            AddBinding(_contourModeBinding = new ValueBinding<bool>(BindingGroup, "CONTOUR_MODE", _settings.ContourMode));
            AddBinding(_contourSpacingBinding = new ValueBinding<float>(BindingGroup, "CONTOUR_SPACING", TreeValue(_settings.ContourSpacing, GridGenerator.ContourSpacingDefault)));
            AddBinding(_contourConnectorSpacingBinding = new ValueBinding<float>(BindingGroup, "CONTOUR_CONNECTOR_SPACING", TreeValue(_settings.ContourConnectorSpacing, GridGenerator.ContourConnectorSpacingDefault)));
            AddBinding(_contourFlatBinding = new ValueBinding<bool>(BindingGroup, "CONTOUR_FLAT", false));
            AddBinding(_selectionModeBinding = new ValueBinding<int>(BindingGroup, "SELECTION_MODE", SelectionMode()));
            AddBinding(_brushSizeBinding = new ValueBinding<float>(BindingGroup, "BRUSH_SIZE", BrushSize()));
            AddBinding(_brushSquareBinding = new ValueBinding<bool>(BindingGroup, "BRUSH_SQUARE", _settings.BrushSquare));
            AddBinding(_brushAngleBinding = new ValueBinding<float>(BindingGroup, "BRUSH_ANGLE", _settings.BrushAngle));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_BRUSH_ANGLE", value =>
            {
                _settings.BrushAngle = math.clamp(value, 0f, 90f) % 90f;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_BRUSH_SQUARE", value =>
            {
                _settings.BrushSquare = value;
                MarkSettingsDirty();
            }));
            AddBinding(_freeAreaClosedBinding = new ValueBinding<bool>(BindingGroup, "FREE_AREA_CLOSED", false));
            AddBinding(_freeAreaInvalidBinding = new ValueBinding<bool>(BindingGroup, "FREE_AREA_INVALID", false));
            AddBinding(_radialMaxLayersBinding = new ValueBinding<int>(BindingGroup, "RADIAL_MAX_LAYERS", ConcentricGenerator.MaxLayersLimit));
            AddBinding(_concentricMaxLayersBinding = new ValueBinding<int>(BindingGroup, "CONCENTRIC_MAX_LAYERS", ConcentricGenerator.MaxLayersLimit));            AddBinding(_loopCulDeSacRatioBinding = new ValueBinding<float>(BindingGroup, "LOOP_CULDESAC_RATIO", _settings.LoopCulDeSacRatio));

            // Melhoramentos automáticos (mode Loop, voir GridRoadToolSystem.BuildAvenueUpgradeFlags/
            // BuildPrincipalUpgradeFlags) : Coletor/Avenida (Geral + Esquerda/Direita) et Principal/
            // Laço (Esquerda/Direita uniquement, pas de séparateur).
            AddBinding(_avenueMiddleTreesBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_MIDDLE_TREES", _settings.AvenueMiddleTrees));
            AddBinding(_avenueMiddleGrassBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_MIDDLE_GRASS", _settings.AvenueMiddleGrass));
            AddBinding(_avenueSideTreesLeftBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_SIDE_TREES_LEFT", _settings.AvenueSideTreesLeft));
            AddBinding(_avenueSideTreesRightBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_SIDE_TREES_RIGHT", _settings.AvenueSideTreesRight));
            AddBinding(_avenueBikeLaneLeftBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_BIKE_LANE_LEFT", _settings.AvenueBikeLaneLeft));
            AddBinding(_avenueBikeLaneRightBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_BIKE_LANE_RIGHT", _settings.AvenueBikeLaneRight));
            AddBinding(_principalSideTreesLeftBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_SIDE_TREES_LEFT", _settings.PrincipalSideTreesLeft));
            AddBinding(_principalSideTreesRightBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_SIDE_TREES_RIGHT", _settings.PrincipalSideTreesRight));
            AddBinding(_principalWideSidewalkLeftBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_WIDE_SIDEWALK_LEFT", _settings.PrincipalWideSidewalkLeft));
            AddBinding(_principalWideSidewalkRightBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_WIDE_SIDEWALK_RIGHT", _settings.PrincipalWideSidewalkRight));
            AddBinding(_principalBikeLaneLeftBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_BIKE_LANE_LEFT", _settings.PrincipalBikeLaneLeft));
            AddBinding(_principalBikeLaneRightBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_BIKE_LANE_RIGHT", _settings.PrincipalBikeLaneRight));
            AddBinding(_avenueSideGrassLeftBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_SIDE_GRASS_LEFT", _settings.AvenueSideGrassLeft));
            AddBinding(_avenueSideGrassRightBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_SIDE_GRASS_RIGHT", _settings.AvenueSideGrassRight));
            AddBinding(_principalSideGrassLeftBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_SIDE_GRASS_LEFT", _settings.PrincipalSideGrassLeft));
            AddBinding(_principalSideGrassRightBinding = new ValueBinding<bool>(BindingGroup, "PRINCIPAL_SIDE_GRASS_RIGHT", _settings.PrincipalSideGrassRight));

            // Vue (Underground/ZoneGrid/InvisibleNetworks), pattern repris de CS2-NetworkTools.
            // AVAILABLE_VIEWS est fixe (un seul outil, qui les supporte toutes) — exposé quand
            // même comme binding séparé pour rester extensible sans changer le contrat côté UI.
            AddBinding(_availableViewsBinding = new ValueBinding<int>(BindingGroup, "AVAILABLE_VIEWS", (int)ViewOption.All));
            AddBinding(_selectedViewsBinding = new ValueBinding<int>(BindingGroup, "SELECTED_VIEWS", (int)_settings.SelectedViews));

            // Prefab de réseau utilisé par la grille (barre permanente, ouvre le sélecteur).
            AddBinding(_roadPrefabNameBinding = new ValueBinding<string>(BindingGroup, "ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_roadPrefabIconBinding = new ValueBinding<string>(BindingGroup, "ROAD_PREFAB_ICON", string.Empty));
            AddBinding(_roadPrefabAutoBinding = new ValueBinding<bool>(BindingGroup, "ROAD_PREFAB_AUTO", true));
            // Réseau secondaire (impasses/rayons) : même trio de bindings, préfixé SECONDARY_.
            AddBinding(_secondaryRoadPrefabNameBinding = new ValueBinding<string>(BindingGroup, "SECONDARY_ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_secondaryRoadPrefabIconBinding = new ValueBinding<string>(BindingGroup, "SECONDARY_ROAD_PREFAB_ICON", string.Empty));
            AddBinding(_secondaryRoadPrefabAutoBinding = new ValueBinding<bool>(BindingGroup, "SECONDARY_ROAD_PREFAB_AUTO", true));
            // Réseau avenue : même trio de bindings, préfixé AVENUE_.
            AddBinding(_avenueRoadPrefabNameBinding = new ValueBinding<string>(BindingGroup, "AVENUE_ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_avenueRoadPrefabIconBinding = new ValueBinding<string>(BindingGroup, "AVENUE_ROAD_PREFAB_ICON", string.Empty));
            AddBinding(_avenueRoadPrefabAutoBinding = new ValueBinding<bool>(BindingGroup, "AVENUE_ROAD_PREFAB_AUTO", true));
            AddBinding(_primarySupportBinding = new ValueBinding<int>(BindingGroup, "PRIMARY_UPGRADE_SUPPORT", UpgradeAll));
            AddBinding(_secondarySupportBinding = new ValueBinding<int>(BindingGroup, "SECONDARY_UPGRADE_SUPPORT", UpgradeAll));
            AddBinding(_avenueSupportBinding = new ValueBinding<int>(BindingGroup, "AVENUE_UPGRADE_SUPPORT", UpgradeAll));
            AddBinding(_roundaboutRoadPrefabNameBinding = new ValueBinding<string>(BindingGroup, "ROUNDABOUT_ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_roundaboutRoadPrefabIconBinding = new ValueBinding<string>(BindingGroup, "ROUNDABOUT_ROAD_PREFAB_ICON", string.Empty));
            AddBinding(_roundaboutRoadPrefabAutoBinding = new ValueBinding<bool>(BindingGroup, "ROUNDABOUT_ROAD_PREFAB_AUTO", true));

            // Sélecteur de réseau : onglet actif, liste des prefabs, récents, choix.
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            AddBinding(_pickerTypeBinding = new ValueBinding<int>(BindingGroup, "PICKER_TYPE", (int)PickerType.Road));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_PICKER_TYPE", value =>
            {
                _pickerTypeBinding.Update(math.clamp(value, 0, 3));
            }));
            AddBinding(_pickerDataBinding = new RawValueBinding(BindingGroup, "PICKER_DATA", WritePickerEntries));
            AddBinding(_recentPrefabsBinding = new RawValueBinding(BindingGroup, "RECENT_PREFABS", WriteRecentPrefabs));
            AddBinding(new TriggerBinding<Entity>(BindingGroup, "PICK_PREFAB", HandlePickPrefab));
            AddBinding(new TriggerBinding(BindingGroup, "PICK_AUTO", () => _toolSystem.SetRoadPrefab(null)));
            AddBinding(new TriggerBinding<Entity>(BindingGroup, "PICK_PREFAB_SECONDARY", HandlePickSecondaryPrefab));
            AddBinding(new TriggerBinding(BindingGroup, "PICK_AUTO_SECONDARY", () => _toolSystem.SetSecondaryRoadPrefab(null)));
            AddBinding(new TriggerBinding<Entity>(BindingGroup, "PICK_PREFAB_AVENUE", HandlePickAvenuePrefab));
            AddBinding(new TriggerBinding(BindingGroup, "PICK_AUTO_AVENUE", () => _toolSystem.SetAvenueRoadPrefab(null)));
            AddBinding(new TriggerBinding<Entity>(BindingGroup, "PICK_PREFAB_ROUNDABOUT", HandlePickRoundaboutPrefab));
            AddBinding(_pathPrefabNameBinding = new ValueBinding<string>(BindingGroup, "PATH_ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_pathPrefabIconBinding = new ValueBinding<string>(BindingGroup, "PATH_ROAD_PREFAB_ICON", string.Empty));
            AddBinding(_pathPrefabAutoBinding = new ValueBinding<bool>(BindingGroup, "PATH_ROAD_PREFAB_AUTO", true));
            AddBinding(new TriggerBinding<Entity>(BindingGroup, "PICK_PREFAB_PATH", entity =>
            {
                if (_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) && prefab != null)
                {
                    _toolSystem.SetPathRoadPrefab(prefab);
                    RememberRecentPrefab(entity);
                }
            }));
            AddBinding(new TriggerBinding(BindingGroup, "PICK_AUTO_PATH", () => _toolSystem.SetPathRoadPrefab(null)));
            AddBinding(_pedestrianLinksBinding = new ValueBinding<bool>(BindingGroup, "PEDESTRIAN_LINKS", _settings.PedestrianLinks));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PEDESTRIAN_LINKS", value =>
            {
                _settings.PedestrianLinks = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding(BindingGroup, "PICK_AUTO_ROUNDABOUT", () => _toolSystem.SetRoundaboutRoadPrefab(null)));

            // Mod Anarchy (tiers, optionnel) : côté TS la rangée lit/déclenche
            // directement les bindings cohtml d'Anarchy lui-même. La détection est
            // réévaluée chaque frame TANT QU'elle est négative (jamais figée à
            // OnCreate() : rien ne garantit que l'assembly Anarchy soit déjà chargée
            // dans l'AppDomain à cet instant précis selon l'ordre de chargement des
            // mods — un simple appel unique aurait pu manquer un Anarchy chargé après
            // nous). Une fois vraie, elle le reste (une assembly ne se décharge pas),
            // donc on arrête de vérifier.
            AddBinding(_anarchyAvailableBinding = new ValueBinding<bool>(BindingGroup, "ANARCHY_AVAILABLE", false));

            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_MODE", value =>
            {
                _settings.Mode = (SpacingMode)math.clamp(value, 0, 1);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_COLUMNS", value =>
            {
                _settings.Columns = math.clamp(value, 1, 12);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_ROWS", value =>
            {
                _settings.Rows = math.clamp(value, 1, 12);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_SPACING", value =>
            {
                _settings.SpacingMeters = math.clamp(value, 100f, 300f);
                MarkSettingsDirty();
            }));
            // Esquisse pendant un drag de N'IMPORTE QUEL slider (voir SliderControl côté React,
            // prop onDragPreview/onDragEnd, et GridRoadToolSystem.LivePreviewOverride/
            // LivePreviewField) : léger, ne touche jamais _settings ni ne régénère la vraie
            // grille — seul GridRoadOverlaySystem le lit pour dessiner un croquis. fieldId DOIT
            // rester synchronisé avec LiveField côté bindings.ts.
            AddBinding(new TriggerBinding<int, float>(BindingGroup, "SET_LIVE_PREVIEW", (fieldId, value) =>
            {
                var field = (GridRoadToolSystem.LivePreviewField)fieldId;
                _toolSystem.BeginOrContinueDrag(field); // log le début UNE fois, voir la méthode.
                _toolSystem.LivePreviewOverride = (field, value);
            }));
            AddBinding(new TriggerBinding(BindingGroup, "CLEAR_LIVE_PREVIEW", () =>
            {
                _toolSystem.EndDrag(); // log le résumé (frames/moyenne/max) UNE fois, voir la méthode.
                _toolSystem.LivePreviewOverride = null;
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_ANGLE_OFFSET", value =>
            {
                _settings.AngleOffsetDegrees = math.clamp(value, -90f, 90f);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_ALIGN_TERRAIN", value =>
            {
                _settings.AlignToTerrain = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_FOLLOW_TERRAIN", value =>
            {
                _settings.FollowTerrain = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_CULDESAC_MODE", value =>
            {
                _settings.CulDeSacMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_CULDESAC_AXIS", value =>
            {
                _settings.CulDeSacAxis = (CulDeSacAxis)math.clamp(value, 0, 2);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CULDESAC_DEPTH", value =>
            {
                // value reçu en pourcentage affiché (50-100), converti en fraction réelle
                // (0.5-0.8, voir GridRoadGeneratorSettings.CulDeSacDepthUiToReal).
                _settings.CulDeSacDepth = GridRoadGeneratorSettings.CulDeSacDepthUiToReal(value);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_STAGGERED", value =>
            {
                _settings.Staggered = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CULDESAC_RATIO", value =>
            {
                _settings.CulDeSacRatio = math.clamp(value, 0f, 100f);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_CULDESAC_CAP_SIZE", value =>
            {
                _settings.CulDeSacCapSize = (CulDeSacCapSize)math.clamp(value, 0, 4);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_CULDESAC_CAP_STYLE", value =>
            {
                _settings.CulDeSacCapStyle = (CulDeSacCapStyle)math.clamp(value, 0, 2);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_COLUMN_ENABLED", value =>
            {
                _settings.AvenueColumnEnabled = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_AVENUE_COLUMN_INDEX", value =>
            {
                // Bornage large et permissif : un index hors de la plage réellement générée
                // (dépend de Columns/Spacing/mode) est déjà un no-op silencieux côté Core
                // (GridParameters.AvenueColumnIndex) — pas besoin de connaître ici le nombre
                // exact de lignes pour rester sûr.
                _settings.AvenueColumnIndex = math.clamp(value, 0, 63);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_ROW_ENABLED", value =>
            {
                _settings.AvenueRowEnabled = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_AVENUE_ROW_INDEX", value =>
            {
                _settings.AvenueRowIndex = math.clamp(value, 0, 63);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_LOOP_MODE", value =>
            {
                _settings.LoopMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_SUPERBLOCK_MODE", value =>
            {
                _settings.SuperblockMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_COLLECTOR_SPACING", value =>
            {
                _settings.CollectorSpacingMeters = math.clamp(value, GridRoadGeneratorSettings.CollectorSpacingMetersMin, 1000f);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_SUPERBLOCK_ZONE", value =>
            {
                _settings.SuperblockZoneMeters = math.clamp(value, GridGenerator.SuperblockZoneMetersMin, GridRoadGeneratorSettings.SuperblockZoneMetersMax);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_CONCENTRIC_MODE", value =>
            {
                _settings.ConcentricMode = value;
                MarkSettingsDirty();
            }));
            // Motif Radial : famille du Concêntrico (ConcentricMode reste vrai, voir le panneau),
            // mais sans anneaux : rotonde + avenues (voir ConcentricGenerator.GenerateRadial).
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_RADIAL_MODE", value =>
            {
                _settings.RadialMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_RADIAL_AVENUES", value =>
            {
                _settings.RadialAvenues = math.clamp((int)math.round(value), ConcentricGenerator.MinRadialAvenues, ConcentricGenerator.MaxRadialAvenues);
                MarkSettingsDirty();
            }));
            // Motif Cul-de-sac em árvore : famille de la Grelha (LoopMode faux), voir GridGenerator.GenerateTree.
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_TREE_MODE", value =>
            {
                _settings.TreeMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_TREE_BRANCH_SPACING", value =>
            {
                _settings.TreeBranchSpacing = math.clamp(value, GridGenerator.MinTreeBranchSpacing, GridGenerator.MaxTreeBranchSpacing);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_TREE_CULDESAC_SPACING", value =>
            {
                _settings.TreeCulDeSacSpacing = math.clamp(value, GridGenerator.MinTreeCulDeSacSpacing, GridGenerator.MaxTreeCulDeSacSpacing);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_TREE_CULDESAC_LENGTH", value =>
            {
                _settings.TreeCulDeSacLength = math.clamp(value, GridGenerator.MinTreeCulDeSacLength, GridGenerator.MaxTreeCulDeSacLength);
                MarkSettingsDirty();
            }));
            // Motif Orgânico : famille de la Grelha (LoopMode faux), voir GridGenerator.GenerateOrganic.
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_ORGANIC_MODE", value =>
            {
                _settings.OrganicMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_ORGANIC_STREET_SPACING", value =>
            {
                _settings.OrganicStreetSpacing = math.clamp(value, GridGenerator.MinOrganicStreetSpacing, GridGenerator.MaxOrganicStreetSpacing);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_ORGANIC_CURVINESS", value =>
            {
                _settings.OrganicCurviness = math.clamp(value, 0f, 100f);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_ORGANIC_LOOP_SHARE", value =>
            {
                _settings.OrganicLoopShare = math.clamp(value, 0f, 100f);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_ORGANIC_SEED", value =>
            {
                _settings.OrganicSeed = math.clamp((int)math.round(value), GridGenerator.MinOrganicSeed, GridGenerator.MaxOrganicSeed);
                MarkSettingsDirty();
            }));
            // Motif Relevo : famille de la Grelha (LoopMode faux), voir GridGenerator.GenerateContour.
            // Mode de sélection : 0 = routes existantes, 1 = zone libre (points cliqués), 2 = pinceau.
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_SELECTION_MODE", value =>
            {
                if (value == SelectionMode())
                {
                    return;
                }
                _settings.FreeAreaMode = value != 0;
                _settings.FreeAreaBrush = value == 2;
                _toolSystem.OnSelectionModeChanged();
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_BRUSH_SIZE", value =>
            {
                _settings.BrushDiameter = math.clamp(value, BrushMask.MinDiameter, BrushMask.MaxDiameter);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_CONTOUR_MODE", value =>
            {
                _settings.ContourMode = value;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CONTOUR_SPACING", value =>
            {
                _settings.ContourSpacing = math.clamp(value, GridGenerator.MinContourSpacing, GridGenerator.MaxContourSpacing);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CONTOUR_CONNECTOR_SPACING", value =>
            {
                _settings.ContourConnectorSpacing = math.clamp(value, GridGenerator.MinContourConnectorSpacing, GridGenerator.MaxContourConnectorSpacing);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_RADIAL_LAYERS", value =>
            {
                _settings.RadialLayers = math.clamp((int)math.round(value), 0, ConcentricGenerator.MaxLayersLimit);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_RADIAL_ROUNDABOUT", value =>
            {
                _settings.RadialRoundaboutRadius = math.clamp(value, ConcentricGenerator.MinRoundaboutRadius, ConcentricGenerator.MaxRoundaboutRadius);
                MarkSettingsDirty();
            }));
            // Bouton "Repor valores" du panneau (voir GridRoadGeneratorSettings.ResetPanelParameters) :
            // les bindings relisent les settings à chaque frame, le panneau suit tout seul.
            AddBinding(_summarySegmentsBinding = new ValueBinding<int>(BindingGroup, "SUMMARY_SEGMENTS", 0));
            AddBinding(_summaryLengthBinding = new ValueBinding<float>(BindingGroup, "SUMMARY_LENGTH", 0f));
            AddBinding(_summaryCostBinding = new ValueBinding<double>(BindingGroup, "SUMMARY_COST", 0));
            AddBinding(_zoneOptionsBinding = new ValueBinding<string>(BindingGroup, "ZONE_OPTIONS", string.Empty));
            AddBinding(_zoningPrefabBinding = new ValueBinding<string>(BindingGroup, "ZONING_PREFAB", string.Empty));
            AddBinding(new TriggerBinding<string>(BindingGroup, "SET_ZONING_PREFAB", name =>
            {
                _settings.ZoningPrefabName = name ?? string.Empty;
                MarkSettingsDirty();
            }));
            AddBinding(_canUndoBinding = new ValueBinding<bool>(BindingGroup, "CAN_UNDO", false));
            AddBinding(_canRedoBinding = new ValueBinding<bool>(BindingGroup, "CAN_REDO", false));
            AddBinding(new TriggerBinding(BindingGroup, "UNDO", () => _toolSystem.RequestUndo()));
            AddBinding(new TriggerBinding(BindingGroup, "REDO", () => _toolSystem.RequestRedo()));
            AddBinding(new TriggerBinding(BindingGroup, "RESET_DEFAULTS", () =>
            {
                _settings.ResetPanelParameters();
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CONCENTRIC_LAYERS", value =>
            {
                _settings.ConcentricLayers = math.clamp((int)math.round(value), ConcentricGenerator.MinLayers, ConcentricGenerator.MaxLayersLimit);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CONCENTRIC_CONNECTIONS", value =>
            {
                _settings.ConcentricConnections = math.clamp((int)math.round(value), ConcentricGenerator.MinConnections, ConcentricGenerator.MaxConnections);
                MarkSettingsDirty();
            }));            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_LOOP_CULDESAC_RATIO", value =>
            {
                _settings.LoopCulDeSacRatio = math.clamp(value, 0f, 100f);
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_MIDDLE_TREES", value => { _settings.AvenueMiddleTrees = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_MIDDLE_GRASS", value => { _settings.AvenueMiddleGrass = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_SIDE_TREES_LEFT", value => { _settings.AvenueSideTreesLeft = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_SIDE_TREES_RIGHT", value => { _settings.AvenueSideTreesRight = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_BIKE_LANE_LEFT", value => { _settings.AvenueBikeLaneLeft = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_BIKE_LANE_RIGHT", value => { _settings.AvenueBikeLaneRight = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_SIDE_TREES_LEFT", value => { _settings.PrincipalSideTreesLeft = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_SIDE_TREES_RIGHT", value => { _settings.PrincipalSideTreesRight = value; MarkSettingsDirty(); }));
            // Passeio largo et relva na berma occupent la même bande dans le jeu (retour utilisateur :
            // "o grass não é compatível com o widesidewalk") : activer l'un désactive l'autre, du
            // même côté.
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_WIDE_SIDEWALK_LEFT", value =>
            {
                _settings.PrincipalWideSidewalkLeft = value;
                if (value) _settings.PrincipalSideGrassLeft = false;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_WIDE_SIDEWALK_RIGHT", value =>
            {
                _settings.PrincipalWideSidewalkRight = value;
                if (value) _settings.PrincipalSideGrassRight = false;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_BIKE_LANE_LEFT", value => { _settings.PrincipalBikeLaneLeft = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_BIKE_LANE_RIGHT", value => { _settings.PrincipalBikeLaneRight = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_SIDE_GRASS_LEFT", value => { _settings.AvenueSideGrassLeft = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_SIDE_GRASS_RIGHT", value => { _settings.AvenueSideGrassRight = value; MarkSettingsDirty(); }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_SIDE_GRASS_LEFT", value =>
            {
                _settings.PrincipalSideGrassLeft = value;
                if (value) _settings.PrincipalWideSidewalkLeft = false;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_PRINCIPAL_SIDE_GRASS_RIGHT", value =>
            {
                _settings.PrincipalSideGrassRight = value;
                if (value) _settings.PrincipalWideSidewalkRight = false;
                MarkSettingsDirty();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_SELECTED_VIEWS", value =>
            {
                var views = (ViewOption)value & ViewOption.All;
                _toolSystem.SelectedViews = views;
                _toolSystem.RefreshViews();
                _settings.SelectedViews = views;
                MarkSettingsDirty();
            }));
            // Actions du panneau.
            AddBinding(new TriggerBinding(BindingGroup, "GENERATE", () => _toolSystem.RequestApply()));
            AddBinding(new TriggerBinding(BindingGroup, "TOGGLE_TOOL", () => _toolSystem.ToggleTool()));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            // Aucune protection ici auparavant : une exception sur N'IMPORTE LEQUEL des ~50
            // bindings ci-dessous (ex. accès settings, ImageSystem.GetThumbnail) empêche TOUS
            // les bindings de se synchroniser pour cette frame, y compris TOOL_ACTIVE tout en
            // haut — silencieusement de la vue du joueur : Unity/le jeu n'écrit rien dans les
            // logs accessibles pour une exception de système avalée par le scheduler ECS. Log
            // explicite (Mod.Log a SetShowsErrorsInUI(true), donc visible en jeu sans console
            // de dev) pour ne plus jamais chercher à l'aveugle si "le panneau n'ouvre pas".
            try
            {
                OnUpdateBindings();
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Erreur dans GridRoadUISystem.OnUpdate, synchronisation UI interrompue pour cette frame.");
            }

            // Écriture disque différée (voir SettingsSaveDebounceSeconds) : flush dès que le
            // joueur s'arrête d'interagir pendant le délai, pas seulement à la fermeture du
            // panneau — évite de perdre le tout dernier réglage si le jeu se ferme entretemps.
            if (_settingsDirty && UnityEngine.Time.unscaledTime - _settingsDirtyTimestamp >= SettingsSaveDebounceSeconds)
            {
                _settingsDirty = false;
                try
                {
                    _settings.ApplyAndSave();
                }
                catch (Exception e)
                {
                    Mod.Log.Error(e, "Erreur lors de l'écriture différée des settings (ApplyAndSave).");
                }
            }
        }

        /// <summary>
        /// Remplace un _settings.ApplyAndSave() direct dans TOUS les déclencheurs SET_* du
        /// panneau (voir SettingsSaveDebounceSeconds pour le pourquoi) : applique la valeur en
        /// mémoire IMMÉDIATEMENT (Apply(), sans coût disque — juste l'événement
        /// onSettingsApplied), et marque seulement l'écriture disque comme à faire, différée
        /// jusqu'à SettingsSaveDebounceSeconds sans nouveau changement (voir OnUpdate). Le
        /// dernier appel avant la fin d'un drag réarme simplement le délai (comportement voulu :
        /// tant que le joueur bouge le slider, aucune écriture disque n'a lieu).
        /// </summary>
        /// <summary>Réglage du motif árvore affiché par le panneau : 0 (config antérieure au motif) = défaut.</summary>
        private static float TreeValue(float value, float fallback) => value > 0f ? value : fallback;

        private void MarkSettingsDirty()
        {
            _settings.Apply();
            _settingsDirty = true;
            _settingsDirtyTimestamp = UnityEngine.Time.unscaledTime;
            // Un réglage a changé : l'aperçu (GridRoadToolSystem, désormais régénéré seulement
            // sur changement réel — voir sa doc) doit être reconstruit à la prochaine frame,
            // même si la sélection de nœuds elle-même n'a pas bougé.
            _toolSystem.MarkPreviewDirty();
        }

        private int SelectionMode() => !_settings.FreeAreaMode ? 0 : _settings.FreeAreaBrush ? 2 : 1;

        private float BrushSize() => _settings.BrushDiameter > 0f
            ? math.clamp(_settings.BrushDiameter, BrushMask.MinDiameter, BrushMask.MaxDiameter)
            : BrushMask.DefaultDiameter;

        private void OnUpdateBindings()
        {
            _toolActiveBinding.Update(_gameToolSystem.activeTool == _toolSystem);
            _nodeCountBinding.Update(_toolSystem.SelectionCount);
            _selectionModeBinding.Update(SelectionMode());
            _brushSizeBinding.Update(BrushSize());
            _brushSquareBinding.Update(_settings.BrushSquare);
            _canUndoBinding.Update(_toolSystem.CanUndo);
            _zoningPrefabBinding.Update(_settings.ZoningPrefabName ?? string.Empty);
            if (_toolActiveBinding.value)
            {
                var zones = new List<string>();
                foreach ((string name, string icon, string color) in _toolSystem.ZoneOptions()) zones.Add(name + "\t" + icon + "\t" + color);
                _zoneOptionsBinding.Update(string.Join("\n", zones));
            }
            _summarySegmentsBinding.Update(_toolSystem.HasPreview ? _toolSystem.SummarySegments : 0);
            _summaryLengthBinding.Update(math.round(_toolSystem.SummaryLength / 10f) * 10f);
            _summaryCostBinding.Update(_toolSystem.SummaryCost);
            _canRedoBinding.Update(_toolSystem.CanRedo);
            _brushAngleBinding.Update(math.round(_settings.BrushAngle));
            _freeAreaClosedBinding.Update(_toolSystem.FreeAreaClosed);
            _freeAreaInvalidBinding.Update(_toolSystem.FreeAreaInvalid);
            // Limite de camadas selon la forme sélectionnée (voir GridRoadToolSystem.ConcentricMaxLayers) —
            // calculée seulement en mode Concêntrico, et seulement quand la sélection change.
            _concentricMaxLayersBinding.Update(_settings.LoopMode && _settings.ConcentricMode
                ? _toolSystem.ConcentricMaxLayers
                : ConcentricGenerator.MaxLayersLimit);
            _canApplyBinding.Update(_toolSystem.CanApply);
            _invalidBinding.Update(_toolSystem.PerimeterInvalid);
            _collisionBinding.Update(_toolSystem.PerimeterCollision);
            _modeBinding.Update((int)_settings.Mode);
            _columnsBinding.Update(_settings.Columns);
            _rowsBinding.Update(_settings.Rows);
            _spacingBinding.Update(_settings.SpacingMeters);
            _angleOffsetBinding.Update(_settings.AngleOffsetDegrees);
            _followTerrainBinding.Update(_settings.FollowTerrain);
            _alignTerrainBinding.Update(_settings.AlignToTerrain);
            _culDeSacModeBinding.Update(_settings.CulDeSacMode);
            _culDeSacAxisBinding.Update((int)_settings.CulDeSacAxis);
            _culDeSacDepthBinding.Update(GridRoadGeneratorSettings.CulDeSacDepthRealToUi(_settings.CulDeSacDepth));
            _staggeredBinding.Update(_settings.Staggered);
            _culDeSacRatioBinding.Update(_settings.CulDeSacRatio);
            _culDeSacCapSizeBinding.Update((int)_settings.CulDeSacCapSize);
            _culDeSacCapStyleBinding.Update((int)_settings.CulDeSacCapStyle);
            _avenueColumnEnabledBinding.Update(_settings.AvenueColumnEnabled);
            _avenueColumnIndexBinding.Update(_settings.AvenueColumnIndex);
            _avenueRowEnabledBinding.Update(_settings.AvenueRowEnabled);
            _avenueRowIndexBinding.Update(_settings.AvenueRowIndex);
            _loopModeBinding.Update(_settings.LoopMode);
            _superblockModeBinding.Update(_settings.SuperblockMode);
            _collectorSpacingBinding.Update(_settings.CollectorSpacingMeters);
            _superblockZoneBinding.Update(_settings.EffectiveSuperblockZoneMeters);
            GridParameters currentParameters = _settings.ToGridParameters();
            _concentricModeBinding.Update(_settings.ConcentricMode);
            _concentricLayersBinding.Update(currentParameters.ConcentricLayers);
            _concentricConnectionsBinding.Update(currentParameters.ConcentricConnections);
            _radialModeBinding.Update(_settings.RadialMode);
            _radialAvenuesBinding.Update(currentParameters.RadialAvenues);
            _radialRoundaboutBinding.Update(currentParameters.RadialRoundaboutRadius);
            _radialLayersBinding.Update(currentParameters.RadialLayers);
            _treeModeBinding.Update(_settings.TreeMode);
            _treeBranchSpacingBinding.Update(TreeValue(_settings.TreeBranchSpacing, GridGenerator.TreeBranchSpacingDefault));
            _treeCulDeSacSpacingBinding.Update(TreeValue(_settings.TreeCulDeSacSpacing, GridGenerator.TreeCulDeSacSpacingDefault));
            _treeCulDeSacLengthBinding.Update(TreeValue(_settings.TreeCulDeSacLength, GridGenerator.TreeCulDeSacLengthDefault));
            _organicModeBinding.Update(_settings.OrganicMode);
            _mixedModeBinding.Update(_settings.MixedMode);
            _mixedCoreRadiusBinding.Update(TreeValue(_settings.MixedCoreRadius, GridGenerator.MixedCoreRadiusDefault));
            _organicStreetSpacingBinding.Update(TreeValue(_settings.OrganicStreetSpacing, GridGenerator.OrganicStreetSpacingDefault));
            _organicCurvinessBinding.Update(_settings.OrganicCurviness);
            _organicLoopShareBinding.Update(_settings.OrganicLoopShare);
            _organicSeedBinding.Update(math.max(_settings.OrganicSeed, GridGenerator.MinOrganicSeed));
            _contourModeBinding.Update(_settings.ContourMode);
            _contourSpacingBinding.Update(TreeValue(_settings.ContourSpacing, GridGenerator.ContourSpacingDefault));
            _contourConnectorSpacingBinding.Update(TreeValue(_settings.ContourConnectorSpacing, GridGenerator.ContourConnectorSpacingDefault));
            _contourFlatBinding.Update(_toolSystem.ContourTerrainFlat);
            // Limite d'anneaux du Radial : calculée seulement en Radial (voir GridRoadToolSystem.RadialMaxLayers).
            _radialMaxLayersBinding.Update(_settings.LoopMode && _settings.ConcentricMode && _settings.RadialMode
                ? _toolSystem.RadialMaxLayers
                : ConcentricGenerator.MaxLayersLimit);            _loopCulDeSacRatioBinding.Update(_settings.LoopCulDeSacRatio);
            _avenueMiddleTreesBinding.Update(_settings.AvenueMiddleTrees);
            _avenueMiddleGrassBinding.Update(_settings.AvenueMiddleGrass);
            _avenueSideTreesLeftBinding.Update(_settings.AvenueSideTreesLeft);
            _avenueSideTreesRightBinding.Update(_settings.AvenueSideTreesRight);
            _avenueBikeLaneLeftBinding.Update(_settings.AvenueBikeLaneLeft);
            _avenueBikeLaneRightBinding.Update(_settings.AvenueBikeLaneRight);
            _principalSideTreesLeftBinding.Update(_settings.PrincipalSideTreesLeft);
            _principalSideTreesRightBinding.Update(_settings.PrincipalSideTreesRight);
            _principalWideSidewalkLeftBinding.Update(_settings.PrincipalWideSidewalkLeft);
            _principalWideSidewalkRightBinding.Update(_settings.PrincipalWideSidewalkRight);
            _principalBikeLaneLeftBinding.Update(_settings.PrincipalBikeLaneLeft);
            _principalBikeLaneRightBinding.Update(_settings.PrincipalBikeLaneRight);
            _avenueSideGrassLeftBinding.Update(_settings.AvenueSideGrassLeft);
            _avenueSideGrassRightBinding.Update(_settings.AvenueSideGrassRight);
            _principalSideGrassLeftBinding.Update(_settings.PrincipalSideGrassLeft);
            _principalSideGrassRightBinding.Update(_settings.PrincipalSideGrassRight);
            _selectedViewsBinding.Update((int)_settings.SelectedViews);

            if (!_anarchyAvailable && IsAnarchyLoaded())
            {
                _anarchyAvailable = true;
                _anarchyAvailableBinding.Update(true);
            }

            PrefabBase roadPrefab = _toolSystem.GetMainPrefab();
            _roadPrefabNameBinding.Update(roadPrefab != null ? roadPrefab.name : string.Empty);
            _roadPrefabIconBinding.Update(roadPrefab != null ? ImageSystem.GetThumbnail(roadPrefab) ?? string.Empty : string.Empty);
            _roadPrefabAutoBinding.Update(_toolSystem.RoadPrefabIsAuto);

            PrefabBase secondaryRoadPrefab = _toolSystem.GetSecondaryPrefab();
            _secondaryRoadPrefabNameBinding.Update(secondaryRoadPrefab != null ? secondaryRoadPrefab.name : string.Empty);
            _secondaryRoadPrefabIconBinding.Update(secondaryRoadPrefab != null ? ImageSystem.GetThumbnail(secondaryRoadPrefab) ?? string.Empty : string.Empty);
            _secondaryRoadPrefabAutoBinding.Update(_toolSystem.SecondaryRoadPrefabIsAuto);

            // Melhoramentos que chaque réseau choisi sait afficher (voir UpgradeSupport) : le panneau
            // cache les autres (ex. Travessa, estrada de cascalho : aucun).
            _primarySupportBinding.Update(UpgradeSupport(_toolSystem.GetMainPrefab()));
            _secondarySupportBinding.Update(UpgradeSupport(_toolSystem.GetSecondaryPrefab()));
            _avenueSupportBinding.Update(UpgradeSupport(_toolSystem.GetAvenuePrefab()));

            PrefabBase avenueRoadPrefab = _toolSystem.GetAvenuePrefab();
            _avenueRoadPrefabNameBinding.Update(avenueRoadPrefab != null ? avenueRoadPrefab.name : string.Empty);
            _avenueRoadPrefabIconBinding.Update(avenueRoadPrefab != null ? ImageSystem.GetThumbnail(avenueRoadPrefab) ?? string.Empty : string.Empty);
            _avenueRoadPrefabAutoBinding.Update(_toolSystem.AvenueRoadPrefabIsAuto);

            PrefabBase pathPrefab = _toolSystem.GetPathPrefab();
            _pathPrefabNameBinding.Update(pathPrefab != null ? pathPrefab.name : string.Empty);
            _pathPrefabIconBinding.Update(pathPrefab != null ? ImageSystem.GetThumbnail(pathPrefab) ?? string.Empty : string.Empty);
            _pathPrefabAutoBinding.Update(_toolSystem.PathRoadPrefabIsAuto);
            _pedestrianLinksBinding.Update(_settings.PedestrianLinks);

            PrefabBase roundaboutRoadPrefab = _toolSystem.GetRoundaboutPrefab();
            _roundaboutRoadPrefabNameBinding.Update(roundaboutRoadPrefab != null ? roundaboutRoadPrefab.name : string.Empty);
            _roundaboutRoadPrefabIconBinding.Update(roundaboutRoadPrefab != null ? ImageSystem.GetThumbnail(roundaboutRoadPrefab) ?? string.Empty : string.Empty);
            _roundaboutRoadPrefabAutoBinding.Update(_toolSystem.RoundaboutRoadPrefabIsAuto);

            // Reconstruit la liste du sélecteur quand l'onglet change (coûteux, donc jamais par frame).
            if (_lastPickerType != _pickerTypeBinding.value)
            {
                _lastPickerType = _pickerTypeBinding.value;
                RebuildPickerEntries((PickerType)_lastPickerType);
                _pickerDataBinding.Update();
            }
        }

        // Melhoramentos (masque de bits, même ordre que UpgradeSupport côté bindings.ts).
        private const int UpgradeMiddleTrees = 1, UpgradeMiddleGrass = 2, UpgradeSideTrees = 4, UpgradeSideGrass = 8,
            UpgradeWideSidewalk = 16, UpgradeBikeLane = 32, UpgradeAll = 63;
        private readonly Dictionary<PrefabBase, int> _upgradeSupport = new Dictionary<PrefabBase, int>();

        /// <summary>
        /// Melhoramentos qu'un réseau sait afficher : un melhoramento n'existe que si une section,
        /// sous-section ou pièce du prefab réagit à l'exigence correspondante (SideTrees, MiddleGrass…).
        /// Retour utilisateur : sur la Travessa ou l'estrada de cascalho, les boutons ne servaient à rien.
        /// Mémorisé par prefab ; prefab non géométrique (inconnu) : tout est proposé.
        /// </summary>
        private int UpgradeSupport(PrefabBase prefab)
        {
            if (prefab == null) return UpgradeAll;
            if (_upgradeSupport.TryGetValue(prefab, out int known)) return known;
            int mask = UpgradeAll;
            if (prefab is NetGeometryPrefab geometry && geometry.m_Sections != null)
            {
                var found = new HashSet<NetPieceRequirements>();
                var visited = new HashSet<NetSectionPrefab>();
                void Add(NetPieceRequirements[] requirements)
                {
                    if (requirements == null) return;
                    foreach (NetPieceRequirements r in requirements) found.Add(r);
                }
                void Visit(NetSectionPrefab section)
                {
                    if (section == null || !visited.Add(section)) return;
                    if (section.m_SubSections != null)
                    {
                        foreach (NetSubSectionInfo sub in section.m_SubSections)
                        {
                            Add(sub.m_RequireAll);
                            Add(sub.m_RequireAny);
                            Visit(sub.m_Section);
                        }
                    }
                    if (section.m_Pieces != null)
                    {
                        foreach (NetPieceInfo piece in section.m_Pieces)
                        {
                            Add(piece.m_RequireAll);
                            Add(piece.m_RequireAny);
                        }
                    }
                }
                foreach (NetSectionInfo info in geometry.m_Sections)
                {
                    Add(info.m_RequireAll);
                    Add(info.m_RequireAny);
                    Visit(info.m_Section);
                }
                bool Has(params NetPieceRequirements[] any)
                {
                    foreach (NetPieceRequirements r in any) if (found.Contains(r)) return true;
                    return false;
                }
                mask = (Has(NetPieceRequirements.MiddleTrees) ? UpgradeMiddleTrees : 0)
                    | (Has(NetPieceRequirements.MiddleGrass) ? UpgradeMiddleGrass : 0)
                    | (Has(NetPieceRequirements.SideTrees, NetPieceRequirements.OppositeTrees) ? UpgradeSideTrees : 0)
                    | (Has(NetPieceRequirements.SideGrass, NetPieceRequirements.OppositeGrass) ? UpgradeSideGrass : 0)
                    | (Has(NetPieceRequirements.WideSidewalk, NetPieceRequirements.OppositeWideSidewalk) ? UpgradeWideSidewalk : 0)
                    | (Has(NetPieceRequirements.BicycleLane, NetPieceRequirements.OppositeBicycleLane) ? UpgradeBikeLane : 0);
            }
            _upgradeSupport[prefab] = mask;
            return mask;
        }

        /// <summary>
        /// Vrai si l'assembly du mod Anarchy est chargée. Détection par nom, sans
        /// référence dure : aucun crash ni warning si le mod est absent.
        /// </summary>
        private static bool IsAnarchyLoaded()
        {
            foreach (System.Reflection.Assembly assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "Anarchy")
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Sélecteur de réseau
        // ------------------------------------------------------------------

        private void HandlePickPrefab(Entity entity)
        {
            if (!_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) || prefab == null)
            {
                return;
            }
            _toolSystem.SetRoadPrefab(prefab);
            RememberRecentPrefab(entity);
        }

        /// <summary>Identique à HandlePickPrefab, pour le réseau secondaire (impasses/rayons).</summary>
        private void HandlePickSecondaryPrefab(Entity entity)
        {
            if (!_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) || prefab == null)
            {
                return;
            }
            _toolSystem.SetSecondaryRoadPrefab(prefab);
            RememberRecentPrefab(entity);
        }

        /// <summary>Identique à HandlePickPrefab, pour le réseau avenue.</summary>
        private void HandlePickAvenuePrefab(Entity entity)
        {
            if (!_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) || prefab == null)
            {
                return;
            }
            _toolSystem.SetAvenueRoadPrefab(prefab);
            RememberRecentPrefab(entity);
        }

        /// <summary>Identique à HandlePickPrefab, pour la rotonde du motif Radial.</summary>
        private void HandlePickRoundaboutPrefab(Entity entity)
        {
            if (!_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) || prefab == null)
            {
                return;
            }
            _toolSystem.SetRoundaboutRoadPrefab(prefab);
            RememberRecentPrefab(entity);
        }

        /// <summary>Tête de liste des récents (partagée entre les trois sélecteurs), sans doublon, plafonnée.</summary>
        private void RememberRecentPrefab(Entity entity)
        {
            _recentPrefabs.Remove(entity);
            _recentPrefabs.Insert(0, entity);
            if (_recentPrefabs.Count > MaxRecentPrefabs)
            {
                _recentPrefabs.RemoveAt(_recentPrefabs.Count - 1);
            }
            _recentPrefabsBinding.Update();
        }

        /// <summary>
        /// Liste les prefabs de réseau de l'onglet demandé, triés comme le menu du jeu
        /// (priorité du groupe UI puis de l'élément — même heuristique que NetworkTools).
        /// </summary>
        private void RebuildPickerEntries(PickerType type)
        {
            _pickerEntries.Clear();

            EntityQuery query;
            switch (type)
            {
                case PickerType.Path:
                    query = GetEntityQuery(ComponentType.ReadOnly<PathwayData>());
                    break;
                case PickerType.Rail:
                    query = GetEntityQuery(ComponentType.ReadOnly<TrackData>());
                    break;
                case PickerType.Waterway:
                    query = GetEntityQuery(ComponentType.ReadOnly<WaterwayData>());
                    break;
                default:
                    query = GetEntityQuery(ComponentType.ReadOnly<RoadData>());
                    break;
            }

            var sortable = new List<(int groupPriority, int itemPriority, Entity entity, string name, string icon)>();
            using (NativeArray<Entity> entities = query.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in entities)
                {
                    if (!_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) || prefab == null)
                    {
                        continue;
                    }
                    if (IsBridgeOrDam(entity, prefab))
                    {
                        continue; // ouvrages à pièces fixes : sans objet pour une grille de rues
                    }
                    (int groupPriority, int itemPriority) = GetUIPriority(entity);
                    sortable.Add((groupPriority, itemPriority, entity, prefab.name, ImageSystem.GetThumbnail(prefab) ?? string.Empty));
                }
            }
            sortable.Sort((a, b) =>
            {
                int byGroup = a.groupPriority.CompareTo(b.groupPriority);
                return byGroup != 0 ? byGroup : a.itemPriority.CompareTo(b.itemPriority);
            });
            foreach (var item in sortable)
            {
                _pickerEntries.Add((item.entity, item.name, item.icon));
            }
        }

        /// <summary>
        /// Ponts et barrages (retour utilisateur : "retira todas as pontes e barragens das opções") :
        /// réseaux à pièces fixes (FixedNetElement), ponts (BridgeData), ou, pour les réseaux de mods
        /// sans ces marqueurs, un nom contenant "Bridge" ou "Dam" (mot entier).
        /// </summary>
        private bool IsBridgeOrDam(Entity entity, PrefabBase prefab)
        {
            if (EntityManager.HasComponent<BridgeData>(entity) || EntityManager.HasBuffer<FixedNetElement>(entity))
            {
                return true;
            }
            string name = prefab.name ?? string.Empty;
            return name.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0
                || System.Text.RegularExpressions.Regex.IsMatch(name, @"(^|[^A-Za-z])Dam([^a-z]|$)");
        }

        private (int groupPriority, int itemPriority) GetUIPriority(Entity entity)
        {
            if (!EntityManager.TryGetComponent(entity, out UIObjectData uiData))
            {
                return (int.MaxValue, int.MaxValue);
            }
            if (uiData.m_Group != Entity.Null
                && EntityManager.TryGetComponent(uiData.m_Group, out UIObjectData groupData))
            {
                return (groupData.m_Priority, uiData.m_Priority);
            }
            return (int.MaxValue, uiData.m_Priority);
        }

        private void WritePickerEntries(IJsonWriter writer)
        {
            writer.ArrayBegin(_pickerEntries.Count);
            foreach (var entry in _pickerEntries)
            {
                WritePrefabEntry(writer, entry.entity, entry.name, entry.icon);
            }
            writer.ArrayEnd();
        }

        private void WriteRecentPrefabs(IJsonWriter writer)
        {
            var valid = new List<(Entity entity, string name, string icon)>();
            foreach (Entity entity in _recentPrefabs)
            {
                if (_prefabSystem.TryGetPrefab(entity, out PrefabBase prefab) && prefab != null)
                {
                    valid.Add((entity, prefab.name, ImageSystem.GetThumbnail(prefab) ?? string.Empty));
                }
            }
            writer.ArrayBegin(valid.Count);
            foreach (var entry in valid)
            {
                WritePrefabEntry(writer, entry.entity, entry.name, entry.icon);
            }
            writer.ArrayEnd();
        }

        private static void WritePrefabEntry(IJsonWriter writer, Entity entity, string name, string icon)
        {
            writer.TypeBegin("GridRoadGenerator.PrefabEntry");
            writer.PropertyName("Entity");
            writer.Write(entity);
            writer.PropertyName("Name");
            writer.Write(name);
            writer.PropertyName("Icon");
            writer.Write(icon);
            writer.TypeEnd();
        }
    }
}
