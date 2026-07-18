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
    [SettingsUIGroupOrder(GroupMode, GroupGrid, GroupCulDeSac, GroupOrganic, GroupKeybindings)]
    [SettingsUIShowGroupName(GroupMode, GroupGrid, GroupCulDeSac, GroupOrganic, GroupKeybindings)]
    public class GridRoadGeneratorSettings : ModSetting
    {
        public const string GroupMode = "Mode";
        public const string GroupGrid = "Grid";
        public const string GroupCulDeSac = "CulDeSac";
        /// <summary>Variation organique : jitter des lignes internes, courbure des collectrices, orientation par bloc.</summary>
        public const string GroupOrganic = "Organic";
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

        [SettingsUISlider(min = -90f, max = 90f, step = 1f, unit = "angle")]
        [SettingsUISection(GroupGrid)]
        public float AngleOffsetDegrees { get; set; }

        /// <summary>
        /// Vrai (défaut) : chaque point libre de la grille générée est reprojeté sur la
        /// hauteur réelle du terrain (comportement historique). Faux : ces points gardent
        /// la hauteur moyenne du périmètre sélectionné déjà calculée par GridGenerator —
        /// la grille reste plate. Les points raccordés au réseau existant (nœuds/arêtes
        /// du périmètre) gardent toujours leur hauteur réelle, dans les deux cas.
        /// </summary>
        [SettingsUISection(GroupGrid)]
        public bool FollowTerrain { get; set; }

        [SettingsUISection(GroupCulDeSac)]
        public bool CulDeSacMode { get; set; }

        /// <summary>Stocké en fraction (0.5–0.9) ; affiché en pourcentage (50–90 %) dans Options > Mods.</summary>
        [SettingsUISlider(min = 50f, max = 90f, step = 1f, unit = "percentage", scalarMultiplier = 100f)]
        [SettingsUISection(GroupCulDeSac)]
        public float CulDeSacDepth { get; set; }

        [SettingsUISection(GroupCulDeSac)]
        public bool Staggered { get; set; }

        [SettingsUISlider(min = 0f, max = 100f, step = 5f, unit = "percentage")]
        [SettingsUISection(GroupCulDeSac)]
        public float CulDeSacRatio { get; set; }

        /// <summary>Taille du cercle de retournement posé en bout d'impasse (Auto = déduite de la largeur du réseau).</summary>
        [SettingsUISection(GroupCulDeSac)]
        public CulDeSacCapSize CulDeSacCapSize { get; set; }

        /// <summary>Style du cercle de retournement posé en bout d'impasse, combiné à CulDeSacCapSize.</summary>
        [SettingsUISection(GroupCulDeSac)]
        public CulDeSacCapStyle CulDeSacCapStyle { get; set; }

        /// <summary>
        /// Amplitude (m, 0–15) du décalage pseudo-aléatoire déterministe des lignes
        /// internes de la grille (jamais les collectrices ni le périmètre). 0 = désactivé.
        /// </summary>
        [SettingsUISlider(min = 0f, max = 15f, step = 0.5f, unit = "length")]
        [SettingsUISection(GroupOrganic)]
        public float JitterAmount { get; set; }

        /// <summary>
        /// Graine du jitter : régénérée depuis le panneau ("Nova semente"), pas montrée
        /// dans les Options (un entier de graine n'y a pas grand sens en réglage manuel).
        /// </summary>
        [SettingsUIHidden]
        public int JitterSeed { get; set; }

        /// <summary>
        /// Courbure (0–100 %) appliquée aux collectrices (lignes traversantes complètes,
        /// jamais les impasses ni le périmètre) : purement une affaire de tracé de la
        /// NetCourse posée (GridRoadToolSystem), pas de la géométrie calculée par
        /// GridGenerator — la position des nœuds ne change pas, donc les rues
        /// perpendiculaires s'y raccordent normalement, sans logique particulière.
        /// </summary>
        [SettingsUISlider(min = 0f, max = 100f, step = 5f, unit = "percentage")]
        [SettingsUISection(GroupOrganic)]
        public float CurveAmount { get; set; }

        /// <summary>
        /// Réseau choisi explicitement dans le sélecteur du panneau, au format
        /// "TypePrefab:Nom" (ex. "RoadPrefab:Small Road"). Vide = mode auto
        /// (suivre le prefab de l'outil route natif). Persisté mais pas montré
        /// dans les Options : se règle depuis le panneau de l'outil.
        /// </summary>
        [SettingsUIHidden]
        public string RoadPrefabName { get; set; }

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
            AngleOffsetDegrees = d.AngleOffsetDegrees;
            FollowTerrain = true;
            CulDeSacMode = d.CulDeSacMode;
            CulDeSacDepth = d.CulDeSacDepth;
            Staggered = d.Staggered;
            CulDeSacRatio = d.CulDeSacRatio;
            CulDeSacCapSize = CulDeSacCapSize.Small;
            CulDeSacCapStyle = CulDeSacCapStyle.Asphalt;
            JitterAmount = d.JitterAmount;
            JitterSeed = d.JitterSeed;
            CurveAmount = 0f;
            RoadPrefabName = string.Empty;
        }

        public GridParameters ToGridParameters() => new GridParameters
        {
            Mode = Mode,
            Columns = Columns,
            Rows = Rows,
            SpacingMeters = SpacingMeters,
            AngleOffsetDegrees = AngleOffsetDegrees,
            CulDeSacMode = CulDeSacMode,
            CulDeSacDepth = CulDeSacDepth,
            Staggered = Staggered,
            CulDeSacRatio = CulDeSacRatio,
            JitterAmount = JitterAmount,
            JitterSeed = JitterSeed
        };
    }
}
