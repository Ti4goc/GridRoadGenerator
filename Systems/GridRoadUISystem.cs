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
        private ValueBinding<bool> _culDeSacModeBinding;
        private ValueBinding<float> _culDeSacDepthBinding;
        private ValueBinding<bool> _staggeredBinding;
        private ValueBinding<float> _culDeSacRatioBinding;
        private ValueBinding<string> _roadPrefabNameBinding;
        private ValueBinding<string> _roadPrefabIconBinding;
        private ValueBinding<bool> _roadPrefabAutoBinding;

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
            AddBinding(_culDeSacModeBinding = new ValueBinding<bool>(BindingGroup, "CULDESAC_MODE", _settings.CulDeSacMode));
            // Exposée en pourcentage (50-90) côté UI, comme le slider Options > Mods ;
            // stockée en fraction (0.5-0.9) dans les settings pour matcher GridParameters.
            AddBinding(_culDeSacDepthBinding = new ValueBinding<float>(BindingGroup, "CULDESAC_DEPTH", _settings.CulDeSacDepth * 100f));
            AddBinding(_staggeredBinding = new ValueBinding<bool>(BindingGroup, "STAGGERED", _settings.Staggered));
            AddBinding(_culDeSacRatioBinding = new ValueBinding<float>(BindingGroup, "CULDESAC_RATIO", _settings.CulDeSacRatio));

            // Prefab de réseau utilisé par la grille (rangée du panneau, ouvre le sélecteur).
            AddBinding(_roadPrefabNameBinding = new ValueBinding<string>(BindingGroup, "ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_roadPrefabIconBinding = new ValueBinding<string>(BindingGroup, "ROAD_PREFAB_ICON", string.Empty));
            AddBinding(_roadPrefabAutoBinding = new ValueBinding<bool>(BindingGroup, "ROAD_PREFAB_AUTO", true));

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

            // Mod Anarchy (tiers, optionnel) : présence détectée une fois, côté TS la
            // rangée lit/déclenche directement les bindings cohtml d'Anarchy lui-même.
            AddBinding(new ValueBinding<bool>(BindingGroup, "ANARCHY_AVAILABLE", IsAnarchyLoaded()));

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
            AddBinding(new TriggerBinding<bool>(BindingGroup, "SET_CULDESAC_MODE", value =>
            {
                _settings.CulDeSacMode = value;
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

            // Actions du panneau.
            AddBinding(new TriggerBinding(BindingGroup, "GENERATE", () => _toolSystem.RequestApply()));
            AddBinding(new TriggerBinding(BindingGroup, "CLEAR_SELECTION", () => _toolSystem.RequestClear()));
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
            _culDeSacModeBinding.Update(_settings.CulDeSacMode);
            _culDeSacDepthBinding.Update(_settings.CulDeSacDepth * 100f);
            _staggeredBinding.Update(_settings.Staggered);
            _culDeSacRatioBinding.Update(_settings.CulDeSacRatio);

            PrefabBase roadPrefab = _toolSystem.GetPrefab();
            _roadPrefabNameBinding.Update(roadPrefab != null ? roadPrefab.name : string.Empty);
            _roadPrefabIconBinding.Update(roadPrefab != null ? ImageSystem.GetThumbnail(roadPrefab) ?? string.Empty : string.Empty);
            _roadPrefabAutoBinding.Update(_toolSystem.RoadPrefabIsAuto);

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

            // Tête de liste des récents, sans doublon, plafonnée.
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
