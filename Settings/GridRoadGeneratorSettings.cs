using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using GridRoadGenerator.Core;
using GridRoadGenerator.Localization;
using Mod = GridRoadGenerator.Mod;

namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Réglages persistés du mod. La page Options > Mods du jeu ne montre plus que
    /// les raccourcis clavier (GroupKeybindings) et la section "Sobre" (GroupAbout) :
    /// tous les réglages de géométrie/organique (grille, culs-de-sac, variation
    /// organique...) ci-dessous sont [SettingsUIHidden] — ils restent persistés entre
    /// sessions (ModSetting) mais ne se pilotent plus que depuis le panneau en jeu
    /// (GridRoadUISystem lit/écrit directement ces propriétés). Les propriétés
    /// ProxyBinding sont détectées par ModSetting et enregistrées comme actions
    /// d'input du mod via RegisterKeyBindings() (appelé dans Mod.OnLoad).
    /// </summary>
    [FileLocation("ModsSettings/GridRoadGenerator/GridRoadGenerator")]
    [SettingsUIGroupOrder(GroupKeybindings, GroupAbout)]
    [SettingsUIShowGroupName(GroupKeybindings, GroupAbout)]
    public class GridRoadGeneratorSettings : ModSetting
    {
        public const string GroupKeybindings = "Keybindings";
        public const string GroupAbout = "About";

        /// <summary>Nom de l'action qui active/désactive l'outil (Ctrl+G par défaut).</summary>
        public const string ActionToggleTool = "ToggleTool";
        /// <summary>Nom de l'action qui valide la sélection et pose la grille (Entrée par défaut).</summary>
        public const string ActionConfirmGrid = "ConfirmGrid";

        public GridRoadGeneratorSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        [SettingsUIHidden]
        public SpacingMode Mode { get; set; }

        [SettingsUIHidden]
        public int Columns { get; set; }

        [SettingsUIHidden]
        public int Rows { get; set; }

        [SettingsUIHidden]
        public float SpacingMeters { get; set; }

        [SettingsUIHidden]
        public float AngleOffsetDegrees { get; set; }

        /// <summary>
        /// Vrai (défaut) : chaque point libre de la grille générée est reprojeté sur la
        /// hauteur réelle du terrain (comportement historique). Faux : ces points gardent
        /// la hauteur moyenne du périmètre sélectionné déjà calculée par GridGenerator —
        /// la grille reste plate. Les points raccordés au réseau existant (nœuds/arêtes
        /// du périmètre) gardent toujours leur hauteur réelle, dans les deux cas.
        /// </summary>
        [SettingsUIHidden]
        public bool FollowTerrain { get; set; }

        [SettingsUIHidden]
        public bool CulDeSacMode { get; set; }

        /// <summary>Stocké en fraction (0.5–0.9) ; affiché en pourcentage (50–90 %) dans le panneau.</summary>
        [SettingsUIHidden]
        public float CulDeSacDepth { get; set; }

        [SettingsUIHidden]
        public bool Staggered { get; set; }

        [SettingsUIHidden]
        public float CulDeSacRatio { get; set; }

        /// <summary>Taille du cercle de retournement posé en bout d'impasse (Auto = déduite de la largeur du réseau).</summary>
        [SettingsUIHidden]
        public CulDeSacCapSize CulDeSacCapSize { get; set; }

        /// <summary>Style du cercle de retournement posé en bout d'impasse, combiné à CulDeSacCapSize.</summary>
        [SettingsUIHidden]
        public CulDeSacCapStyle CulDeSacCapStyle { get; set; }

        /// <summary>
        /// Amplitude (m, 0–15) du décalage pseudo-aléatoire déterministe des lignes
        /// internes de la grille (jamais les collectrices ni le périmètre). 0 = désactivé.
        /// </summary>
        [SettingsUIHidden]
        public float JitterAmount { get; set; }

        /// <summary>
        /// Graine du jitter : régénérée depuis le panneau ("Nova semente").
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
        [SettingsUIHidden]
        public float CurveAmount { get; set; }

        /// <summary>
        /// Forme de la courbure des collectrices : Bulge (bombée d'un seul côté, comportement
        /// historique) ou SCurve (change de sens à mi-segment). Purement une affaire de tracé,
        /// comme CurveAmount ci-dessus.
        /// </summary>
        [SettingsUIHidden]
        public GridGenerator.CurveStyle CurveStyle { get; set; }

        /// <summary>
        /// Ângulo fixo (manuel, AngleOffsetDegrees) ou Seguir relevo (recalculé à chaque
        /// génération pour suivre le terrain — voir OrientationMode et
        /// GridRoadToolSystem.ComputeTerrainFollowAngle pour l'étendue réelle, volontairement
        /// simplifiée, de ce chantier exploratoire).
        /// </summary>
        [SettingsUIHidden]
        public OrientationMode OrientationMode { get; set; }

        /// <summary>
        /// Réseau choisi explicitement dans le sélecteur du panneau, au format
        /// "TypePrefab:Nom" (ex. "RoadPrefab:Small Road"). Vide = mode auto
        /// (suivre le prefab de l'outil route natif).
        /// </summary>
        [SettingsUIHidden]
        public string RoadPrefabName { get; set; }

        /// <summary>
        /// Vue active (Underground/ZoneGrid/InvisibleNetworks) pendant que l'outil est
        /// actif : pattern repris de CS2-NetworkTools. Persistée entre sessions, restaurée
        /// dans GridRoadToolSystem.OnStartRunning.
        /// </summary>
        [SettingsUIHidden]
        public ViewOption SelectedViews { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.G, ActionToggleTool, ctrl: true)]
        [SettingsUISection(GroupKeybindings)]
        public ProxyBinding ToggleToolBinding { get; set; }

        [SettingsUIKeyboardBinding(BindingKeyboard.Enter, ActionConfirmGrid)]
        [SettingsUISection(GroupKeybindings)]
        public ProxyBinding ConfirmGridBinding { get; set; }

        // ------------------------------------------------------------------
        // Section "Sobre" (About)
        // ------------------------------------------------------------------

        [SettingsUISection(GroupAbout)]
        public string ModDisplayName => Translations.ModName;

        /// <summary>
        /// Champ multiligne : c'est le LIBELLÉ de l'option (Options.OPTION[...], voir
        /// Translations.Build) qui porte le texte affiché, jamais la valeur retournée
        /// ici (MultilineTextSettingItemData ne lit jamais property.GetValue).
        /// </summary>
        [SettingsUISection(GroupAbout)]
        [SettingsUIMultilineText]
        public string Credits => string.Empty;

        [SettingsUISection(GroupAbout)]
        public string Version => Mod.Instance.Version;

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
            CurveStyle = GridGenerator.CurveStyle.Bulge;
            OrientationMode = OrientationMode.FixedAngle;
            RoadPrefabName = string.Empty;
            // Comme CS2-NetworkTools : tout coché par défaut à la première ouverture.
            SelectedViews = ViewOption.All;
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
