// Pattern de bindings cohtml adapté de CS2-NetworkTools (c) Luca Rager,
// licence MIT — https://github.com/lucarager/CS2-NetworkTools
using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
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
        private ValueBinding<string> _roadPrefabNameBinding;
        private ValueBinding<string> _roadPrefabIconBinding;

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

            // Prefab de route utilisé par la grille (rangée en lecture seule du panneau).
            AddBinding(_roadPrefabNameBinding = new ValueBinding<string>(BindingGroup, "ROAD_PREFAB_NAME", string.Empty));
            AddBinding(_roadPrefabIconBinding = new ValueBinding<string>(BindingGroup, "ROAD_PREFAB_ICON", string.Empty));

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

            PrefabBase roadPrefab = _toolSystem.GetPrefab();
            _roadPrefabNameBinding.Update(roadPrefab != null ? roadPrefab.name : string.Empty);
            _roadPrefabIconBinding.Update(roadPrefab != null ? ImageSystem.GetThumbnail(roadPrefab) ?? string.Empty : string.Empty);
        }
    }
}
