// Pattern de bindings cohtml adapté de CS2-NetworkTools (c) Luca Rager,
// licence MIT — https://github.com/lucarager/CS2-NetworkTools
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

        private ValueBinding<bool> _toolActiveBinding;
        private ValueBinding<int> _nodeCountBinding;
        private ValueBinding<bool> _canApplyBinding;
        private ValueBinding<bool> _invalidBinding;
        private ValueBinding<int> _modeBinding;
        private ValueBinding<int> _columnsBinding;
        private ValueBinding<int> _rowsBinding;
        private ValueBinding<float> _spacingBinding;
        private ValueBinding<float> _angleOffsetBinding;
        private ValueBinding<bool> _followTerrainBinding;
        private ValueBinding<bool> _culDeSacModeBinding;
        private ValueBinding<int> _culDeSacAxisBinding;
        private ValueBinding<float> _culDeSacDepthBinding;
        private ValueBinding<bool> _staggeredBinding;
        private ValueBinding<float> _culDeSacRatioBinding;
        private ValueBinding<int> _culDeSacCapSizeBinding;
        private ValueBinding<int> _culDeSacCapStyleBinding;
        private ValueBinding<bool> _adaptiveModeBinding;
        private ValueBinding<int> _radialConnectionsBinding;
        private ValueBinding<bool> _adaptiveRoundedCornersBinding;
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
        private ValueBinding<string> _avenueRoadPrefabNameBinding;
        private ValueBinding<string> _avenueRoadPrefabIconBinding;
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

            // Réglages ↔ UI (synchronisés avec Options > Mods).
            AddBinding(_modeBinding = new ValueBinding<int>(BindingGroup, "MODE", (int)_settings.Mode));
            AddBinding(_columnsBinding = new ValueBinding<int>(BindingGroup, "COLUMNS", _settings.Columns));
            AddBinding(_rowsBinding = new ValueBinding<int>(BindingGroup, "ROWS", _settings.Rows));
            AddBinding(_spacingBinding = new ValueBinding<float>(BindingGroup, "SPACING", _settings.SpacingMeters));
            AddBinding(_angleOffsetBinding = new ValueBinding<float>(BindingGroup, "ANGLE_OFFSET", _settings.AngleOffsetDegrees));
            AddBinding(_followTerrainBinding = new ValueBinding<bool>(BindingGroup, "FOLLOW_TERRAIN", _settings.FollowTerrain));
            AddBinding(_culDeSacModeBinding = new ValueBinding<bool>(BindingGroup, "CULDESAC_MODE", _settings.CulDeSacMode));
            AddBinding(_culDeSacAxisBinding = new ValueBinding<int>(BindingGroup, "CULDESAC_AXIS", (int)_settings.CulDeSacAxis));
            // Exposée en pourcentage (50-90) côté UI, comme le slider Options > Mods ;
            // stockée en fraction (0.5-0.9) dans les settings pour matcher GridParameters.
            AddBinding(_culDeSacDepthBinding = new ValueBinding<float>(BindingGroup, "CULDESAC_DEPTH", _settings.CulDeSacDepth * 100f));
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
            AddBinding(_adaptiveModeBinding = new ValueBinding<bool>(BindingGroup, "ADAPTIVE_MODE", _settings.AdaptiveMode));
            AddBinding(_radialConnectionsBinding = new ValueBinding<int>(BindingGroup, "RADIAL_CONNECTIONS", _settings.RadialConnections));
            AddBinding(_adaptiveRoundedCornersBinding = new ValueBinding<bool>(BindingGroup, "ADAPTIVE_ROUNDED_CORNERS", _settings.AdaptiveRoundedCorners));

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
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_COLUMNS", value =>
            {
                _settings.Columns = math.clamp(value, 1, 12);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_ROWS", value =>
            {
                _settings.Rows = math.clamp(value, 1, 12);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_SPACING", value =>
            {
                _settings.SpacingMeters = math.clamp(value, 10f, 300f);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_ANGLE_OFFSET", value =>
            {
                _settings.AngleOffsetDegrees = math.clamp(value, -90f, 90f);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_FOLLOW_TERRAIN", value =>
            {
                _settings.FollowTerrain = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_CULDESAC_MODE", value =>
            {
                _settings.CulDeSacMode = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_CULDESAC_AXIS", value =>
            {
                _settings.CulDeSacAxis = (CulDeSacAxis)math.clamp(value, 0, 2);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CULDESAC_DEPTH", value =>
            {
                // value reçu en pourcentage (50-90) depuis le panneau, converti en fraction.
                _settings.CulDeSacDepth = math.clamp(value / 100f, 0.5f, 0.9f);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_STAGGERED", value =>
            {
                _settings.Staggered = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<float>(BindingGroup, "SET_CULDESAC_RATIO", value =>
            {
                _settings.CulDeSacRatio = math.clamp(value, 0f, 100f);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_CULDESAC_CAP_SIZE", value =>
            {
                _settings.CulDeSacCapSize = (CulDeSacCapSize)math.clamp(value, 0, 4);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_CULDESAC_CAP_STYLE", value =>
            {
                _settings.CulDeSacCapStyle = (CulDeSacCapStyle)math.clamp(value, 0, 2);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_COLUMN_ENABLED", value =>
            {
                _settings.AvenueColumnEnabled = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_AVENUE_COLUMN_INDEX", value =>
            {
                // Bornage large et permissif : un index hors de la plage réellement générée
                // (dépend de Columns/Spacing/mode) est déjà un no-op silencieux côté Core
                // (GridParameters.AvenueColumnIndex) — pas besoin de connaître ici le nombre
                // exact de lignes pour rester sûr.
                _settings.AvenueColumnIndex = math.clamp(value, 0, 63);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_AVENUE_ROW_ENABLED", value =>
            {
                _settings.AvenueRowEnabled = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_AVENUE_ROW_INDEX", value =>
            {
                _settings.AvenueRowIndex = math.clamp(value, 0, 63);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_ADAPTIVE_MODE", value =>
            {
                _settings.AdaptiveMode = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_RADIAL_CONNECTIONS", value =>
            {
                _settings.RadialConnections = math.clamp(value, 0, 24);
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_ADAPTIVE_ROUNDED_CORNERS", value =>
            {
                _settings.AdaptiveRoundedCorners = value;
                _settings.ApplyAndSave();
            }));
            AddBinding(new TriggerBinding<int>(BindingGroup, "SET_SELECTED_VIEWS", value =>
            {
                var views = (ViewOption)value & ViewOption.All;
                _toolSystem.SelectedViews = views;
                _toolSystem.RefreshViews();
                _settings.SelectedViews = views;
                _settings.ApplyAndSave();
            }));

            // Actions du panneau.
            AddBinding(new TriggerBinding(BindingGroup, "GENERATE", () => _toolSystem.RequestApply()));
            AddBinding(new TriggerBinding(BindingGroup, "TOGGLE_TOOL", () => _toolSystem.ToggleTool()));
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();

            _toolActiveBinding.Update(_gameToolSystem.activeTool == _toolSystem);
            _nodeCountBinding.Update(_toolSystem.NodeCount);
            _canApplyBinding.Update(_toolSystem.CanApply);
            _invalidBinding.Update(_toolSystem.PerimeterInvalid);
            _modeBinding.Update((int)_settings.Mode);
            _columnsBinding.Update(_settings.Columns);
            _rowsBinding.Update(_settings.Rows);
            _spacingBinding.Update(_settings.SpacingMeters);
            _angleOffsetBinding.Update(_settings.AngleOffsetDegrees);
            _followTerrainBinding.Update(_settings.FollowTerrain);
            _culDeSacModeBinding.Update(_settings.CulDeSacMode);
            _culDeSacAxisBinding.Update((int)_settings.CulDeSacAxis);
            _culDeSacDepthBinding.Update(_settings.CulDeSacDepth * 100f);
            _staggeredBinding.Update(_settings.Staggered);
            _culDeSacRatioBinding.Update(_settings.CulDeSacRatio);
            _culDeSacCapSizeBinding.Update((int)_settings.CulDeSacCapSize);
            _culDeSacCapStyleBinding.Update((int)_settings.CulDeSacCapStyle);
            _avenueColumnEnabledBinding.Update(_settings.AvenueColumnEnabled);
            _avenueColumnIndexBinding.Update(_settings.AvenueColumnIndex);
            _avenueRowEnabledBinding.Update(_settings.AvenueRowEnabled);
            _avenueRowIndexBinding.Update(_settings.AvenueRowIndex);
            _adaptiveModeBinding.Update(_settings.AdaptiveMode);
            _radialConnectionsBinding.Update(_settings.RadialConnections);
            _adaptiveRoundedCornersBinding.Update(_settings.AdaptiveRoundedCorners);
            _selectedViewsBinding.Update((int)_settings.SelectedViews);

            if (!_anarchyAvailable && IsAnarchyLoaded())
            {
                _anarchyAvailable = true;
                _anarchyAvailableBinding.Update(true);
            }

            PrefabBase roadPrefab = _toolSystem.GetPrefab();
            _roadPrefabNameBinding.Update(roadPrefab != null ? roadPrefab.name : string.Empty);
            _roadPrefabIconBinding.Update(roadPrefab != null ? ImageSystem.GetThumbnail(roadPrefab) ?? string.Empty : string.Empty);
            _roadPrefabAutoBinding.Update(_toolSystem.RoadPrefabIsAuto);

            PrefabBase secondaryRoadPrefab = _toolSystem.GetSecondaryPrefab();
            _secondaryRoadPrefabNameBinding.Update(secondaryRoadPrefab != null ? secondaryRoadPrefab.name : string.Empty);
            _secondaryRoadPrefabIconBinding.Update(secondaryRoadPrefab != null ? ImageSystem.GetThumbnail(secondaryRoadPrefab) ?? string.Empty : string.Empty);
            _secondaryRoadPrefabAutoBinding.Update(_toolSystem.SecondaryRoadPrefabIsAuto);

            PrefabBase avenueRoadPrefab = _toolSystem.GetAvenuePrefab();
            _avenueRoadPrefabNameBinding.Update(avenueRoadPrefab != null ? avenueRoadPrefab.name : string.Empty);
            _avenueRoadPrefabIconBinding.Update(avenueRoadPrefab != null ? ImageSystem.GetThumbnail(avenueRoadPrefab) ?? string.Empty : string.Empty);
            _avenueRoadPrefabAutoBinding.Update(_toolSystem.AvenueRoadPrefabIsAuto);

            // Reconstruit la liste du sélecteur quand l'onglet change (coûteux, donc jamais par frame).
            if (_lastPickerType != _pickerTypeBinding.value)
            {
                _lastPickerType = _pickerTypeBinding.value;
                RebuildPickerEntries((PickerType)_lastPickerType);
                _pickerDataBinding.Update();
            }
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
