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

        /// <summary>Motif "Radial" (avec ConcentricMode) : rotonde au centre + avenues droites, voir GridParameters.RadialMode.</summary>
        [SettingsUIHidden]
        public bool RadialMode { get; set; }

        /// <summary>Nombre d'avenues droites du motif Radial.</summary>
        [SettingsUIHidden]
        public int RadialAvenues { get; set; }

        public const int RadialAvenuesDefault = 8;

        /// <summary>Rayon demandé (m) de la rotonde centrale du motif Radial.</summary>
        [SettingsUIHidden]
        public float RadialRoundaboutRadius { get; set; }

        public const float RadialRoundaboutRadiusDefault = 50f;

        /// <summary>Anneaux circulaires autour de la rotonde du motif Radial (0 = aucun), indépendants de ConcentricLayers.</summary>
        [SettingsUIHidden]
        public int RadialLayers { get; set; }

        public const int RadialLayersDefault = 2;

        /// <summary>Motif "Cul-de-sac em árvore" (famille de la Grelha) : voir GridParameters.TreeMode.</summary>
        [SettingsUIHidden]
        public bool TreeMode { get; set; }

        /// <summary>Distance (m) entre deux branches d'un même côté de la collectrice (motif árvore).</summary>
        [SettingsUIHidden]
        public float TreeBranchSpacing { get; set; }

        /// <summary>Distance (m) entre deux paires d'impasses le long d'une branche (motif árvore).</summary>
        [SettingsUIHidden]
        public float TreeCulDeSacSpacing { get; set; }

        /// <summary>Longueur (m) visée des impasses (motif árvore).</summary>
        [SettingsUIHidden]
        public float TreeCulDeSacLength { get; set; }

        /// <summary>Motif "Orgânico" (famille de la Grelha) : voir GridParameters.OrganicMode.</summary>
        [SettingsUIHidden]
        public bool OrganicMode { get; set; }

        /// <summary>Distance (m) visée entre deux rues voisines (motif orgânico).</summary>
        [SettingsUIHidden]
        public float OrganicStreetSpacing { get; set; }

        /// <summary>Courbure des rues, 0–100 % (motif orgânico).</summary>
        [SettingsUIHidden]
        public float OrganicCurviness { get; set; }

        /// <summary>Part des branches qui se referment en boucle, 0–100 % (motif orgânico).</summary>
        [SettingsUIHidden]
        public float OrganicLoopShare { get; set; }

        /// <summary>Variante du tirage aléatoire, 1–100 (motif orgânico).</summary>
        [SettingsUIHidden]
        public int OrganicSeed { get; set; }

        /// <summary>Sélection "Área livre" : points cliqués sur le terrain au lieu de nœuds de routes existantes (voir GridRoadToolSystem.FreeArea).</summary>
        [SettingsUIHidden]
        public bool FreeAreaMode { get; set; }

        /// <summary>Variante "Pincel" de la zone libre : zone peinte au lieu de points cliqués (voir BrushMask).</summary>
        [SettingsUIHidden]
        public bool FreeAreaBrush { get; set; }

        /// <summary>Diamètre (m) du pinceau, BrushMask.MinDiameter–MaxDiameter.</summary>
        [SettingsUIHidden]
        public float BrushDiameter { get; set; }

        /// <summary>Pinceau carré (aligné sur les axes du monde) au lieu de rond.</summary>
        [SettingsUIHidden]
        public bool BrushSquare { get; set; }

        /// <summary>Rotation (degrés, 0–90) du pinceau carré — Shift + souris en jeu, ou le panneau.</summary>
        [SettingsUIHidden]
        public float BrushAngle { get; set; }

        /// <summary>Motif "Relevo" (famille de la Grelha) : voir GridParameters.ContourMode.</summary>
        [SettingsUIHidden]
        public bool ContourMode { get; set; }

        /// <summary>Distance (m) entre deux rues de niveau voisines (motif relevo).</summary>
        [SettingsUIHidden]
        public float ContourSpacing { get; set; }

        /// <summary>Distance (m) entre deux montées le long d'une rue de niveau (motif relevo).</summary>
        [SettingsUIHidden]
        public float ContourConnectorSpacing { get; set; }

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
        /// <summary>Relva na berma (CompositionFlags.Side.PrimaryBeautification, pièce SideGrass/OppositeGrass du jeu).</summary>
        [SettingsUIHidden]
        public bool AvenueSideGrassLeft { get; set; }
        [SettingsUIHidden]
        public bool AvenueSideGrassRight { get; set; }
        [SettingsUIHidden]
        public bool PrincipalSideGrassLeft { get; set; }
        [SettingsUIHidden]
        public bool PrincipalSideGrassRight { get; set; }

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
        /// Réseau de la rotonde centrale du motif Radial (RoadSegmentDef.IsRoundabout). Vide = mode
        /// auto : même réseau que les rues (principal).
        /// </summary>
        [SettingsUIHidden]
        public string RoundaboutRoadPrefabName { get; set; }


        /// <summary>Zone posée automatiquement le long des routes générées (nom du prefab ; vide = aucune).</summary>
        [SettingsUIHidden]
        public string ZoningPrefabName { get; set; }

        /// <summary>Grelha/Loop : un axe suit les courbes de niveau dominantes (voir GridParameters.AlignToTerrain).</summary>
        [SettingsUIHidden]
        public bool AlignToTerrain { get; set; }

        /// <summary>Liaisons piétonnes du bout des impasses (Árvore, Orgânico, Grelha avec impasses).</summary>
        [SettingsUIHidden]
        public bool PedestrianLinks { get; set; }

        /// <summary>Motif "Misto" (famille de la Grelha) : voir GridParameters.MixedMode.</summary>
        [SettingsUIHidden]
        public bool MixedMode { get; set; }

        /// <summary>Rayon (m) du cercle central du motif Misto.</summary>
        [SettingsUIHidden]
        public float MixedCoreRadius { get; set; }

        /// <summary>Réseau des liaisons piétonnes ; vide = "Pedestrian Path" du jeu.</summary>
        [SettingsUIHidden]
        public string PathPrefabName { get; set; }

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
            RadialMode = false;
            RadialAvenues = RadialAvenuesDefault;
            RadialRoundaboutRadius = RadialRoundaboutRadiusDefault;
            RadialLayers = RadialLayersDefault;
            TreeMode = false;
            TreeBranchSpacing = GridGenerator.TreeBranchSpacingDefault;
            TreeCulDeSacSpacing = GridGenerator.TreeCulDeSacSpacingDefault;
            TreeCulDeSacLength = GridGenerator.TreeCulDeSacLengthDefault;
            OrganicMode = false;
            OrganicStreetSpacing = GridGenerator.OrganicStreetSpacingDefault;
            OrganicCurviness = GridGenerator.OrganicCurvinessDefault;
            OrganicLoopShare = GridGenerator.OrganicLoopShareDefault;
            OrganicSeed = GridGenerator.MinOrganicSeed;
            ContourMode = false;
            FreeAreaMode = false;
            FreeAreaBrush = false;
            BrushDiameter = GridRoadGenerator.Core.BrushMask.DefaultDiameter;
            BrushSquare = false;
            BrushAngle = 0f;
            ContourSpacing = GridGenerator.ContourSpacingDefault;
            ContourConnectorSpacing = GridGenerator.ContourConnectorSpacingDefault;
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
            AvenueSideGrassLeft = false;
            AvenueSideGrassRight = false;
            PrincipalSideGrassLeft = false;
            PrincipalSideGrassRight = false;
            RoadPrefabName = string.Empty;
            SecondaryRoadPrefabName = string.Empty;
            AvenueRoadPrefabName = string.Empty;
            RoundaboutRoadPrefabName = string.Empty;
            ZoningPrefabName = string.Empty;
            AlignToTerrain = false;
            PedestrianLinks = false;
            MixedMode = false;
            MixedCoreRadius = GridRoadGenerator.Core.GridGenerator.MixedCoreRadiusDefault;
            PathPrefabName = string.Empty;
            // Comme CS2-NetworkTools : tout coché par défaut à la première ouverture.
            SelectedViews = ViewOption.All;
            AutoResolveCollisions = false;
        }

        /// <summary>
        /// Bouton "Repor valores" du panneau (retour utilisateur : "no painel em si podes colocar
        /// o botão para voltar a pôr os valores padrão, em todos os modos") : remet aux valeurs
        /// d'origine tous les paramètres de forme de tous les motifs (géométrie, cul-de-sac,
        /// avenue, Loop, Superblock, Concêntrico, Radial) et désactive tous les melhoramentos
        /// (retour utilisateur : "desliga qualquer melhoramento por padrão" — des ciclovias
        /// activées une fois restaient sur toutes les routes, session après session). Garde le
        /// motif actif, les réseaux choisis, la vue et l'option anti-collisions du menu Options.
        /// </summary>
        public void ResetPanelParameters()
        {
            bool loopMode = LoopMode;
            bool superblockMode = SuperblockMode;
            bool concentricMode = ConcentricMode;
            bool radialMode = RadialMode;
            bool treeMode = TreeMode;
            bool organicMode = OrganicMode;
            bool mixedMode = MixedMode;
            bool contourMode = ContourMode;
            bool freeAreaMode = FreeAreaMode;
            bool freeAreaBrush = FreeAreaBrush;
            float brushDiameter = BrushDiameter;
            bool brushSquare = BrushSquare;
            float brushAngle = BrushAngle;
            string roadPrefab = RoadPrefabName;
            string secondaryRoadPrefab = SecondaryRoadPrefabName;
            string avenueRoadPrefab = AvenueRoadPrefabName;
            string roundaboutRoadPrefab = RoundaboutRoadPrefabName;
            ViewOption views = SelectedViews;
            bool autoResolve = AutoResolveCollisions;

            SetDefaults();

            LoopMode = loopMode;
            SuperblockMode = superblockMode;
            ConcentricMode = concentricMode;
            RadialMode = radialMode;
            TreeMode = treeMode;
            OrganicMode = organicMode;
            MixedMode = mixedMode;
            ContourMode = contourMode;
            FreeAreaMode = freeAreaMode;
            FreeAreaBrush = freeAreaBrush;
            BrushDiameter = brushDiameter;
            BrushSquare = brushSquare;
            BrushAngle = brushAngle;
            RoadPrefabName = roadPrefab;
            SecondaryRoadPrefabName = secondaryRoadPrefab;
            AvenueRoadPrefabName = avenueRoadPrefab;
            RoundaboutRoadPrefabName = roundaboutRoadPrefab;
            SelectedViews = views;
            AutoResolveCollisions = autoResolve;
            // Melhoramentos : laissés à leur valeur par défaut (tous désactivés, voir SetDefaults).
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
            RadialMode = RadialMode,
            // 0 dans une config sauvegardée avant ce motif : défaut plutôt que minimum.
            RadialAvenues = RadialAvenues > 0 ? RadialAvenues : RadialAvenuesDefault,
            RadialRoundaboutRadius = RadialRoundaboutRadius > 0f ? RadialRoundaboutRadius : RadialRoundaboutRadiusDefault,
            // Pas de "0 = défaut" ici : 0 anneau est un choix valable (rotonde + avenues seules).
            RadialLayers = RadialLayers,
            TreeMode = TreeMode,
            // 0 dans une config sauvegardée avant ce motif : GenerateTree retombe sur les défauts.
            TreeBranchSpacing = TreeBranchSpacing,
            TreeCulDeSacSpacing = TreeCulDeSacSpacing,
            TreeCulDeSacLength = TreeCulDeSacLength,
            OrganicMode = OrganicMode,
            // 0 dans une config sauvegardée avant ce motif : GenerateOrganic retombe sur les défauts
            // (courbure et boucles : 0 est un choix valable, voir OrganicValue côté UI).
            OrganicStreetSpacing = OrganicStreetSpacing,
            OrganicCurviness = OrganicCurviness,
            OrganicLoopShare = OrganicLoopShare,
            OrganicSeed = OrganicSeed,
            ContourMode = ContourMode,
            AlignToTerrain = AlignToTerrain,
            // Liaisons piétonnes retirées (le jeu effaçait les chemins quelques secondes après la pose).
            PedestrianLinks = false,
            MixedMode = MixedMode,
            MixedCoreRadius = MixedCoreRadius > 0f ? MixedCoreRadius : GridRoadGenerator.Core.GridGenerator.MixedCoreRadiusDefault,
            ContourSpacing = ContourSpacing,
            ContourConnectorSpacing = ContourConnectorSpacing,
        };
    }
}
