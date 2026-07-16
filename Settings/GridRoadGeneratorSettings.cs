using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using GridRoadGenerator.Core;

namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Réglages exposés dans le menu Options > Mods du jeu.
    /// NB: les attributs [SettingsUI*] correspondent à l'API Colossal Mod Settings.
    /// Vérifie les noms exacts dans le SDK actuel (ils ont légèrement changé selon les versions).
    /// </summary>
    [FileLocation(nameof(GridRoadGenerator))]
    [SettingsUIGroupOrder(GroupMode, GroupGrid)]
    [SettingsUIShowGroupName(GroupMode, GroupGrid)]
    public class GridRoadGeneratorSettings : ModSetting
    {
        public const string GroupMode = "Mode";
        public const string GroupGrid = "Grid";

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

        [SettingsUISlider(min = 10f, max = 300f, step = 5f)]
        [SettingsUISection(GroupGrid)]
        public float SpacingMeters { get; set; }

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
