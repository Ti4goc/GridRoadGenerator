using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using GridRoadGenerator.Core;

namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Réglages exposés dans le menu Options > Mods du jeu, plus les raccourcis clavier.
    /// Les propriétés ProxyBinding sont détectées par ModSetting et enregistrées comme
    /// actions d'input du mod via RegisterKeyBindings() (appelé dans Mod.OnLoad).
    /// </summary>
    [FileLocation("ModsSettings/GridRoadGenerator/GridRoadGenerator")]
    [SettingsUIGroupOrder(GroupMode, GroupGrid, GroupKeybindings)]
    [SettingsUIShowGroupName(GroupMode, GroupGrid, GroupKeybindings)]
    public class GridRoadGeneratorSettings : ModSetting
    {
        public const string GroupMode = "Mode";
        public const string GroupGrid = "Grid";
        public const string GroupKeybindings = "Keybindings";

        /// <summary>Nom de l'action qui active/désactive l'outil (Ctrl+G par défaut).</summary>
        public const string ActionToggleTool = "ToggleTool";
        /// <summary>Nom de l'action qui valide la sélection et pose la grille (Entrée par défaut).</summary>
        public const string ActionConfirmGrid = "ConfirmGrid";

        public GridRoadGeneratorSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUISection(GroupMode)]
        public SpacingMode Mode { get; set; }

        [SettingsUISlider(min = 1, max = 12, step = 1)]
        [SettingsUISection(GroupGrid)]
        public int Columns { get; set; }

        [SettingsUISlider(min = 1, max = 12, step = 1)]
        [SettingsUISection(GroupGrid)]
        public int Rows { get; set; }

        [SettingsUISlider(min = 10f, max = 300f, step = 5f, unit = "length")]
        [SettingsUISection(GroupGrid)]
        public float SpacingMeters { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.G, ActionToggleTool, ctrl: true)]
        [SettingsUISection(GroupKeybindings)]
        public ProxyBinding ToggleToolBinding { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.Enter, ActionConfirmGrid)]
        [SettingsUISection(GroupKeybindings)]
        public ProxyBinding ConfirmGridBinding { get; set; }

        public override void SetDefaults()
        {
            var d = GridParameters.Default;
            Mode = d.Mode;
            Columns = d.Columns;
            Rows = d.Rows;
            SpacingMeters = d.SpacingMeters;
        }

        public GridParameters ToGridParameters() => new GridParameters
        {
            Mode = Mode,
            Columns = Columns,
            Rows = Rows,
            SpacingMeters = SpacingMeters
        };
    }
}
