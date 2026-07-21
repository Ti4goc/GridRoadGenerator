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

        /// <summary>Quel(s) axe(s) peuvent devenir des impasses : colonnes (historique), rangées, ou les deux.</summary>
        [SettingsUIHidden]
        public CulDeSacAxis CulDeSacAxis { get; set; }

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
        /// Avenue (grille classique uniquement) : la colonne d'index AvenueColumnIndex (parmi
        /// les lignes u effectivement générées, 0-based) devient un troisième réseau dédié,
        /// traversant tout le périmètre, jamais un cul-de-sac. Voir GridParameters.
        /// AvenueColumnEnabled/GridGenerator.EmitLine.
        /// </summary>
        [SettingsUIHidden]
        public bool AvenueColumnEnabled { get; set; }

        [SettingsUIHidden]
        public int AvenueColumnIndex { get; set; }

        /// <summary>Même principe qu'AvenueColumnEnabled/AvenueColumnIndex, pour une rangée (ligne v).</summary>
        [SettingsUIHidden]
        public bool AvenueRowEnabled { get; set; }

        [SettingsUIHidden]
        public int AvenueRowIndex { get; set; }

        /// <summary>
        /// Mode "Adaptativo" : au lieu de la grille de lignes droites (Mode/Rows/Columns/Angle/
        /// CulDeSac* ci-dessus, tous ignorés quand actif), génère des anneaux concentriques par
        /// offset successif du polygone du périmètre vers l'intérieur — voir
        /// GridGenerator.GenerateAdaptiveGrid. Réutilise SpacingMeters comme distance entre
        /// deux anneaux.
        /// </summary>
        [SettingsUIHidden]
        public bool AdaptiveMode { get; set; }

        /// <summary>
        /// Nombre de connexions radiales reliant les anneaux entre eux en mode Adaptativo
        /// (0 = aucune, anneaux isolés). Voir GridGenerator.GenerateAdaptiveGrid.
        /// </summary>
        [SettingsUIHidden]
        public int RadialConnections { get; set; }

        /// <summary>
        /// Mode Adaptativo : coins arrondis (un arc à chaque sommet net des anneaux) au lieu de
        /// la jonction en pointe par défaut. Voir GridGenerator.RoundCorners.
        /// </summary>
        [SettingsUIHidden]
        public bool AdaptiveRoundedCorners { get; set; }

        /// <summary>
        /// Réseau choisi explicitement dans le sélecteur du panneau, au format
        /// "TypePrefab:Nom" (ex. "RoadPrefab:Small Road"). Vide = mode auto
        /// (suivre le prefab de l'outil route natif).
        /// </summary>
        [SettingsUIHidden]
        public string RoadPrefabName { get; set; }

        /// <summary>
        /// Réseau utilisé pour les tronçons "locaux" (impasses en mode CulDeSacMode, rayons en
        /// mode Adaptativo) — voir RoadSegmentDef.IsCulDeSacEnd/IsRadial. Même format que
        /// RoadPrefabName. Vide = mode auto, qui suit ici RoadPrefabName (pas indépendamment
        /// l'outil route natif) : tant qu'aucun réseau secondaire n'est choisi explicitement,
        /// le comportement reste identique à avant l'existence de ce second réseau.
        /// </summary>
        [SettingsUIHidden]
        public string SecondaryRoadPrefabName { get; set; }

        /// <summary>
        /// Réseau utilisé pour les tronçons "avenue" (voir RoadSegmentDef.IsAvenue,
        /// AvenueColumnEnabled/AvenueRowEnabled). Même format et même logique de mode auto que
        /// SecondaryRoadPrefabName, indépendant de lui.
        /// </summary>
        [SettingsUIHidden]
        public string AvenueRoadPrefabName { get; set; }

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
            CulDeSacAxis = d.CulDeSacAxis;
            CulDeSacDepth = d.CulDeSacDepth;
            Staggered = d.Staggered;
            CulDeSacRatio = d.CulDeSacRatio;
            CulDeSacCapSize = CulDeSacCapSize.Small;
            CulDeSacCapStyle = CulDeSacCapStyle.Asphalt;
            AvenueColumnEnabled = d.AvenueColumnEnabled;
            AvenueColumnIndex = d.AvenueColumnIndex;
            AvenueRowEnabled = d.AvenueRowEnabled;
            AvenueRowIndex = d.AvenueRowIndex;
            AdaptiveMode = false;
            RadialConnections = 8;
            AdaptiveRoundedCorners = false;
            RoadPrefabName = string.Empty;
            SecondaryRoadPrefabName = string.Empty;
            AvenueRoadPrefabName = string.Empty;
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
            CulDeSacAxis = CulDeSacAxis,
            CulDeSacDepth = CulDeSacDepth,
            Staggered = Staggered,
            CulDeSacRatio = CulDeSacRatio,
            AvenueColumnEnabled = AvenueColumnEnabled,
            AvenueColumnIndex = AvenueColumnIndex,
            AvenueRowEnabled = AvenueRowEnabled,
            AvenueRowIndex = AvenueRowIndex,
            RadialConnections = RadialConnections,
            AdaptiveRoundedCorners = AdaptiveRoundedCorners
        };
    }
}
