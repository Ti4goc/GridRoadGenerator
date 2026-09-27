using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;
using GridRoadGenerator.Core;
using GridRoadGenerator.Localization;
using Unity.Mathematics;
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
    [SettingsUIGroupOrder(GroupGeneral, GroupKeybindings, GroupAbout)]
    [SettingsUIShowGroupName(GroupGeneral, GroupKeybindings, GroupAbout)]
    public class GridRoadGeneratorSettings : ModSetting
    {
        public const string GroupGeneral = "General";
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

        /// <summary>
        /// Stocké en fraction (0.5–0.8, plafonné ici et non 0.9 comme avant — retour utilisateur :
        /// au-delà, la ramification cul-de-sac devient trop longue/instable visuellement). Le
        /// panneau affiche cette plage comme 50–100 % (jamais 50-80 %, qui donnerait l'impression
        /// fausse d'un plafond artificiel non atteignable) — voir CulDeSacDepthUiToReal/
        /// CulDeSacDepthRealToUi pour la conversion, utilisée par GridRoadUISystem (SET_CULDESAC_
        /// DEPTH/binding) ET GridRoadOverlaySystem (esquisse en direct pendant le glissement du
        /// curseur), qui doivent impérativement rester synchronisées.
        /// </summary>
        [SettingsUIHidden]
        public float CulDeSacDepth { get; set; }

        /// <summary>Borne réelle basse de CulDeSacDepth (fraction) — voir sa doc.</summary>
        public const float CulDeSacDepthRealMin = 0.5f;

        /// <summary>Borne réelle haute de CulDeSacDepth (fraction) — voir sa doc.</summary>
        public const float CulDeSacDepthRealMax = 0.8f;

        /// <summary>Borne basse affichée dans le panneau (%) — correspond à CulDeSacDepthRealMin.</summary>
        public const float CulDeSacDepthUiMin = 50f;

        /// <summary>Borne haute affichée dans le panneau (%) — correspond à CulDeSacDepthRealMax, jamais 100 % réel.</summary>
        public const float CulDeSacDepthUiMax = 100f;

        /// <summary>Convertit une valeur de curseur (50–100 %) en fraction réelle (0.5–0.8) stockée dans CulDeSacDepth.</summary>
        public static float CulDeSacDepthUiToReal(float uiPercent) => math.clamp(
            CulDeSacDepthRealMin + (uiPercent - CulDeSacDepthUiMin) / (CulDeSacDepthUiMax - CulDeSacDepthUiMin) * (CulDeSacDepthRealMax - CulDeSacDepthRealMin),
            CulDeSacDepthRealMin, CulDeSacDepthRealMax);

        /// <summary>Convertit la fraction réelle stockée (0.5–0.8) en valeur affichée au curseur (50–100 %).</summary>
        public static float CulDeSacDepthRealToUi(float real) => CulDeSacDepthUiMin
            + (real - CulDeSacDepthRealMin) / (CulDeSacDepthRealMax - CulDeSacDepthRealMin) * (CulDeSacDepthUiMax - CulDeSacDepthUiMin);

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
        /// Mode "Loop" (voir GridGenerator.GenerateLoopGrid) : au lieu de la grille de lignes
        /// droites (Mode/Rows/Columns/Angle/CulDeSac* ci-dessus, tous ignorés quand actif, sauf
        /// CulDeSacCapSize/CapStyle réutilisés pour le cercle de retournement des culs-de-sac
        /// de laço), génère des collectrices éparses (CollectorSpacingMeters) délimitant des
        /// super-îlots, chacun rempli d'un laço interne.
        /// </summary>
        [SettingsUIHidden]
        public bool LoopMode { get; set; }

        /// <summary>Espacement (m) des collectrices éparses en mode Loop — voir LoopMode.</summary>
        [SettingsUIHidden]
        public float CollectorSpacingMeters { get; set; }

        /// <summary>
        /// Plancher (m) de CollectorSpacingMeters. Retour utilisateur : en dessous, la grille de
        /// collectrices devient trop dense (illogique visuellement, en plus d'être coûteuse à
        /// régénérer).
        /// </summary>
        public const float CollectorSpacingMetersMin = 200f;

        /// <summary>Fréquence (0–100 %) à laquelle un laço reçoit une ramification cul-de-sac — voir LoopMode.</summary>
        [SettingsUIHidden]
        public float LoopCulDeSacRatio { get; set; }

        /// <summary>Mode "super-quarteirão" (mode Loop uniquement, voir GridGenerator.GridParameters.SuperblockMode).</summary>
        [SettingsUIHidden]
        public bool SuperblockMode { get; set; }

        /// <summary>Mode "Concêntrico" (famille Loop, exclusif avec SuperblockMode) — voir Core.ConcentricGenerator.</summary>
        [SettingsUIHidden]
        public bool ConcentricMode { get; set; }

        /// <summary>Nombre d'anneaux intérieurs (ConcentricMode).</summary>
        [SettingsUIHidden]
        public int ConcentricLayers { get; set; }

        /// <summary>Nombre de rayons par anneau le plus intérieur (ConcentricMode).</summary>
        [SettingsUIHidden]
        public int ConcentricConnections { get; set; }

        public const int ConcentricLayersDefault = 3;
        public const int ConcentricConnectionsDefault = 4;

        /// <summary>Taille visée (m) d'une zone en mode super-quarteirão — voir GridParameters.SuperblockZoneMeters.</summary>
        [SettingsUIHidden]
        public float SuperblockZoneMeters { get; set; }

        public const float SuperblockZoneMetersMax = 400f;
        public const float SuperblockZoneMetersDefault = 150f;

        /// <summary>Valeur effective : une config sauvegardée avant ce réglage vaut 0 — retombe sur le défaut.</summary>
        public float EffectiveSuperblockZoneMeters => SuperblockZoneMeters >= GridGenerator.SuperblockZoneMetersMin
            ? math.min(SuperblockZoneMeters, SuperblockZoneMetersMax)
            : SuperblockZoneMetersDefault;

        /// <summary>
        /// Melhoramentos automáticos (mode Loop uniquement, voir GridRoadToolSystem — appliqués
        /// via Game.Net.Upgraded/CompositionFlags au moment de la création du tronçon) : réseau
        /// Coletor/Avenida (séparateur central : sans notion de côté ; bermas : indépendant par
        /// côté) et réseau Principal/Laço (sans séparateur, seulement Esquerda/Direita). "Esquerda"/
        /// "Direita" correspondent à Left/Right côté jeu — relatif au sens de tracé du tronçon, pas
        /// à un côté fixe du monde (voir CompositionFlags.Side).
        /// </summary>
        [SettingsUIHidden]
        public bool AvenueMiddleTrees { get; set; }
        [SettingsUIHidden]
        public bool AvenueMiddleGrass { get; set; }
        [SettingsUIHidden]
        public bool AvenueSideTreesLeft { get; set; }
        [SettingsUIHidden]
        public bool AvenueSideTreesRight { get; set; }
        [SettingsUIHidden]
        public bool AvenueBikeLaneLeft { get; set; }
        [SettingsUIHidden]
        public bool AvenueBikeLaneRight { get; set; }
        [SettingsUIHidden]
        public bool PrincipalSideTreesLeft { get; set; }
        [SettingsUIHidden]
        public bool PrincipalSideTreesRight { get; set; }
        [SettingsUIHidden]
        public bool PrincipalWideSidewalkLeft { get; set; }
        [SettingsUIHidden]
        public bool PrincipalWideSidewalkRight { get; set; }
        [SettingsUIHidden]
        public bool PrincipalBikeLaneLeft { get; set; }
        [SettingsUIHidden]
        public bool PrincipalBikeLaneRight { get; set; }

        /// <summary>
        /// Réseau choisi explicitement dans le sélecteur du panneau, au format
        /// "TypePrefab:Nom" (ex. "RoadPrefab:Small Road"). Vide = mode auto
        /// (suivre le prefab de l'outil route natif).
        /// </summary>
        [SettingsUIHidden]
        public string RoadPrefabName { get; set; }

        /// <summary>
        /// Réseau utilisé pour les tronçons "locaux" (impasses en mode CulDeSacMode) — voir
        /// RoadSegmentDef.IsCulDeSacEnd. Même format que
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

        /// <summary>
        /// Option du menu Options (retour utilisateur : "voltar a adicionar o anti colisões de
        /// antes, mas como opção a ativar/desativar") : si "Générer" est refusé pour collision,
        /// retire les tronçons générés en conflit et réessaie (voir
        /// GridRoadToolSystem._excludedSegments). Désactivée par défaut : elle peut laisser des
        /// trous dans le motif (vécu sur le Superblock).
        /// </summary>
        [SettingsUISection(GroupGeneral)]
        public bool AutoResolveCollisions { get; set; }

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
            LoopMode = false;
            CollectorSpacingMeters = d.CollectorSpacingMeters;
            LoopCulDeSacRatio = d.LoopCulDeSacRatio;
            SuperblockMode = d.SuperblockMode;
            SuperblockZoneMeters = SuperblockZoneMetersDefault;
            ConcentricMode = false;
            ConcentricLayers = ConcentricLayersDefault;
            ConcentricConnections = ConcentricConnectionsDefault;
            AvenueMiddleTrees = false;
            AvenueMiddleGrass = false;
            AvenueSideTreesLeft = false;
            AvenueSideTreesRight = false;
            AvenueBikeLaneLeft = false;
            AvenueBikeLaneRight = false;
            PrincipalSideTreesLeft = false;
            PrincipalSideTreesRight = false;
            PrincipalWideSidewalkLeft = false;
            PrincipalWideSidewalkRight = false;
            PrincipalBikeLaneLeft = false;
            PrincipalBikeLaneRight = false;
            RoadPrefabName = string.Empty;
            SecondaryRoadPrefabName = string.Empty;
            AvenueRoadPrefabName = string.Empty;
            // Comme CS2-NetworkTools : tout coché par défaut à la première ouverture.
            SelectedViews = ViewOption.All;
            AutoResolveCollisions = false;
        }

        /// <summary>
        /// Bouton "Repor valores" du panneau (retour utilisateur : "no painel em si podes colocar
        /// o botão para voltar a pôr os valores padrão, em todos os modos") : remet aux valeurs
        /// d'origine tous les paramètres de forme de tous les motifs (géométrie, cul-de-sac,
        /// avenue, Loop, Superblock, Concêntrico). Garde ce qui relève d'un choix plutôt que d'un
        /// réglage : le motif actif, les réseaux choisis, les melhoramentos, la vue et l'option
        /// anti-collisions du menu Options.
        /// </summary>
        public void ResetPanelParameters()
        {
            bool loopMode = LoopMode;
            bool superblockMode = SuperblockMode;
            bool concentricMode = ConcentricMode;
            string roadPrefab = RoadPrefabName;
            string secondaryRoadPrefab = SecondaryRoadPrefabName;
            string avenueRoadPrefab = AvenueRoadPrefabName;
            ViewOption views = SelectedViews;
            bool autoResolve = AutoResolveCollisions;
            bool[] upgrades =
            {
                AvenueMiddleTrees, AvenueMiddleGrass, AvenueSideTreesLeft, AvenueSideTreesRight,
                AvenueBikeLaneLeft, AvenueBikeLaneRight, PrincipalSideTreesLeft, PrincipalSideTreesRight,
                PrincipalWideSidewalkLeft, PrincipalWideSidewalkRight, PrincipalBikeLaneLeft, PrincipalBikeLaneRight,
            };

            SetDefaults();

            LoopMode = loopMode;
            SuperblockMode = superblockMode;
            ConcentricMode = concentricMode;
            RoadPrefabName = roadPrefab;
            SecondaryRoadPrefabName = secondaryRoadPrefab;
            AvenueRoadPrefabName = avenueRoadPrefab;
            SelectedViews = views;
            AutoResolveCollisions = autoResolve;
            AvenueMiddleTrees = upgrades[0];
            AvenueMiddleGrass = upgrades[1];
            AvenueSideTreesLeft = upgrades[2];
            AvenueSideTreesRight = upgrades[3];
            AvenueBikeLaneLeft = upgrades[4];
            AvenueBikeLaneRight = upgrades[5];
            PrincipalSideTreesLeft = upgrades[6];
            PrincipalSideTreesRight = upgrades[7];
            PrincipalWideSidewalkLeft = upgrades[8];
            PrincipalWideSidewalkRight = upgrades[9];
            PrincipalBikeLaneLeft = upgrades[10];
            PrincipalBikeLaneRight = upgrades[11];
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
            CollectorSpacingMeters = CollectorSpacingMeters,
            LoopCulDeSacRatio = LoopCulDeSacRatio,
            SuperblockMode = SuperblockMode,
            SuperblockZoneMeters = EffectiveSuperblockZoneMeters,
            // Une config sauvegardée avant ce mode vaut 0 : Generate borne à [Min, Max], mais
            // retomber sur le défaut est plus naturel que sur le minimum.
            ConcentricMode = ConcentricMode,
            ConcentricLayers = ConcentricLayers > 0 ? ConcentricLayers : ConcentricLayersDefault,
            ConcentricConnections = ConcentricConnections > 0 ? ConcentricConnections : ConcentricConnectionsDefault,
        };
    }
}
