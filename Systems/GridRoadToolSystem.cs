// Patterns de sélection (survol/surbrillance/résolution de nœud) adaptés de
// CS2-NetworkTools (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Outil personnalisé : le joueur survole un nœud de route (surbrillance), clique pour
    /// l'ajouter au périmètre (polygone dans l'ordre de clic), puis valide (Entrée ou bouton
    /// du panneau) pour générer la grille interne.
    ///
    /// Placement : pipeline natif de construction réseau, comme NetToolSystem. À chaque frame
    /// de prévisualisation, les définitions (CreationDefinition + NetCourse + Updated) sont
    /// détruites puis recréées via le ToolOutputBarrier avec applyMode = Clear — la cadence
    /// exacte du NetTool vanilla, ce qui garantit qu'aucune entité Temp ne s'empile. À la
    /// validation, applyMode = Apply concrétise les Temp de la frame précédente.
    /// </summary>
    public partial class GridRoadToolSystem : ToolBaseSystem
    {
        /// <summary>Distance max (m) entre le point survolé sur une arête et un nœud pour le sélectionner.</summary>
        private const float MaxSelectDistance = 16f;
        /// <summary>
        /// Distance (m) sous laquelle une extrémité de segment est raccordée à un nœud
        /// sélectionné. Alignée sur GridGenerator.MinNodeDistance (la même distance sert déjà
        /// à fusionner les points calculés côté géométrie pure) : sans ça, un point que le
        /// générateur a déjà fusionné avec un sommet voisin peut rester à quelques mètres d'un
        /// nœud réel existant sans jamais s'y raccorder — le jeu refuse alors de construire deux
        /// nœuds aussi proches sans Anarchy activé ("Objetos sobrepostos").
        /// </summary>
        private const float NodeSnapDistance = GridGenerator.MinNodeDistance;
        /// <summary>Distance (m) sous laquelle une extrémité de segment est raccordée à une route du périmètre — même raison que NodeSnapDistance.</summary>
        private const float EdgeSnapDistance = GridGenerator.MinNodeDistance;
        /// <summary>
        /// Nombre PLANCHER/PLAFOND de morceaux de polyligne pour approximer une courbe existante
        /// (PerimeterEdge.m_Curve, Bezier4x3) — voir ComputeInteriorEdgeSampleCount. Ne sert plus
        /// qu'au test "cette arête passe-t-elle par l'intérieur du polygone ?" de
        /// FindInteriorExistingEdges (un booléen approximatif reste acceptable là : un échec
        /// coûte au pire un croisement raté, pas une jonction cassée). Le calcul du point de
        /// croisement RÉEL, lui, ne passe plus par une polyligne du tout — voir
        /// InteriorEdgeIntersectIterations et son commentaire dans
        /// SplitSegmentsCrossingInteriorEdges pour l'historique du bug que ceci corrigeait
        /// avant d'être remplacé par une intersection exacte courbe-droite.
        /// </summary>
        private const int MinInteriorEdgeSamples = 16;
        private const int MaxInteriorEdgeSamples = 128;
        /// <summary>Longueur (m) cible par morceau de polyligne — voir ComputeInteriorEdgeSampleCount.</summary>
        private const float InteriorEdgeSampleSpacing = 5f;
        /// <summary>
        /// Profondeur de récursion (subdivision De Casteljau) pour
        /// MathUtils.Intersect(Bezier4x2, Line2.Segment, out float2, int) dans
        /// SplitSegmentsCrossingInteriorEdges — reprend la valeur utilisée par le jeu lui-même
        /// pour ce même appel (Game.Net.SecondaryLaneSystem, Game.Zones.BlockSystem, confirmé en
        /// décompilant Game.dll), pas une valeur inventée.
        /// </summary>
        private const int InteriorEdgeIntersectIterations = 4;
        /// <summary>
        /// Seuil "croisement déjà couvert par MakeCoursePos, ne pas le couper ici" dans
        /// SplitSegmentsCrossingInteriorEdges — voir le commentaire de méthode pour l'historique
        /// du bug (zone morte) que la valeur EdgeSnapDistance * 0.5 corrige, au lieu de
        /// GridGenerator.MinSegmentLength (qui, à valeur ÉGALE à EdgeSnapDistance, ouvrait cette
        /// zone morte).
        /// </summary>
        private const float InteriorCrossingEndpointSkipDistance = EdgeSnapDistance * 0.5f;

        /// <summary>
        /// Nombre de subdivisions à utiliser pour approximer CETTE courbe existante en polyligne,
        /// proportionnel à sa longueur réelle (MathUtils.Length, même API que NetCourse.m_Length
        /// plus haut) plutôt qu'un compte fixe — pour qu'un morceau de polyligne reste toujours
        /// autour de InteriorEdgeSampleSpacing mètres, même quand l'arête entière fait plusieurs
        /// centaines de mètres et que seule une petite portion croise la zone sélectionnée.
        /// </summary>
        private static int ComputeInteriorEdgeSampleCount(Bezier4x3 curve)
        {
            float length = MathUtils.Length(curve);
            return math.clamp((int)math.ceil(length / InteriorEdgeSampleSpacing), MinInteriorEdgeSamples, MaxInteriorEdgeSamples);
        }
        /// <summary>Fenêtre (s) entre deux clics sur le même nœud pour détecter un double-clic.</summary>
        private const float DoubleClickWindow = 0.35f;
        /// <summary>Garde-fou : nombre max de nœuds visités par la recherche de chemin entre deux clics.</summary>
        private const int MaxPathfindNodes = 2000;
        /// <summary>Garde-fou : nombre max de nœuds du contour détecté au double-clic.</summary>
        private const int MaxPerimeterNodes = 50;

        // Cercles de retournement (props CulDeSac<Taille><Style>, ex. "CulDeSacMedium02")
        // posés en bout de chaque impasse. Taille choisie explicitement dans le panneau
        // (CulDeSacCapSize) ou, en mode Auto, déduite de la largeur du réseau
        // (NetGeometryData.m_DefaultWidth) selon les seuils ci-dessous ; approximatifs,
        // à affiner selon retour visuel en jeu — pas de valeur officielle exposée par
        // le jeu pour ces props.
        /// <summary>Mode Auto : largeur (m) sous laquelle la taille "Small" est choisie.</summary>
        private const float CulDeSacCapSmallMaxWidth = 7f;
        /// <summary>Mode Auto : largeur (m) sous laquelle la taille "Medium" est choisie (sinon "Large").</summary>
        private const float CulDeSacCapMediumMaxWidth = 14f;
        /// <summary>Mode Auto : largeur (m) sous laquelle la taille "Large" est choisie (sinon "XL").</summary>
        private const float CulDeSacCapLargeMaxWidth = 22f;

        // Asset de rotonde complet (avenue, voir ComputeAvenueRoundabout) : StaticObjectPrefab
        // natif du jeu, "<Taille>Roundabout01" (ex. "MediumRoundabout01" — trouvé par
        // recherche dans les assets du jeu, aucune valeur officielle exposée) : déjà une
        // rotonde entière (anneau pavé + marquages), pas un simple îlot central décoratif —
        // posé par-dessus le croisement en + normal des deux avenues, jamais construit à
        // partir de segments de route. Taille déduite du rayon voulu (voir
        // ComputeAvenueRoundabout, dérivé de SpacingMeters) selon les seuils ci-dessous ;
        // approximatifs, à affiner selon retour visuel en jeu.
        private const float RoundaboutIslandSmallMaxRadius = 10f;
        private const float RoundaboutIslandMediumMaxRadius = 15f;
        private const float RoundaboutIslandLargeMaxRadius = 20f;

        public override string toolID => "Grid Road Tool";

        private readonly List<Entity> _selectedNodes = new List<Entity>();
        private readonly List<float3> _selectedPositions = new List<float3>();
        private Entity _hoveredNode = Entity.Null;
        private bool _applyRequested;
        private bool _invalidLogged;
        /// <summary>Empêche le spam du log d'omission de nœuds trop proches (MinNodeDistance) : un avis par sélection.</summary>
        private bool _omittedNodesLogged;
        /// <summary>Même principe que _omittedNodesLogged, pour le diagnostic détaillé de FindInteriorExistingEdges.</summary>
        private int _lastLoggedCandidateCount = -1;
        /// <summary>Même principe, pour le diagnostic détaillé de SplitSegmentsCrossingInteriorEdges.</summary>
        private int _lastLoggedPreSplitSegmentCount = -1;

        // ------------------------------------------------------------------
        // Détection de geste "sélection/paramètre changé" (retour utilisateur en jeu, "adiciona
        // um log de performance que deteta cada gesto que faço") : le log de performance ne
        // s'écrit QUE sur un événement discret réel, jamais deux fois pour la même frame
        // identique, même si CreateGridDefinitions() continue de tourner à chaque frame en
        // dessous (voir sa doc — ESSAI ABANDONNÉ de ne recréer QUE sur un vrai changement,
        // impossible : le pipeline natif exige une recréation chaque frame pour rester visible.
        // La vraie protection contre le coût d'un gros laço en continu est ShowSketchOnly, pas
        // ce flag). GridParameters a une égalité de struct triviale (aucun champ référence) ;
        // les positions sont comparées élément par élément (float3 n'a pas d'Equals utile par
        // défaut) — le nombre de nœuds seul ne suffit pas : un nœud retiré puis un autre ajouté
        // au même endroit dans le clic suivant laisserait le compte inchangé.
        // ------------------------------------------------------------------
        private readonly List<float3> _lastGesturePositions = new List<float3>();
        /// <summary>
        /// Mis à vrai par MarkPreviewDirty() — appelé par GridRoadUISystem.MarkSettingsDirty()
        /// (donc sur TOUT changement de réglage déclenché depuis le panneau, quel que soit le
        /// champ) et directement par SetRoadPrefab/SetSecondaryRoadPrefab/SetAvenueRoadPrefab
        /// ci-dessous (le sélecteur de réseau du panneau modifie _settings SANS passer par
        /// MarkSettingsDirty). Volontairement PAS une comparaison champ par champ de
        /// GridParameters : cette dernière ratait déjà les réglages hors GridParameters
        /// (prefabs, FollowTerrain, CulDeSacCapSize/CapStyle, melhoramentos Avenue/Principal...)
        /// — un aperçu figé qui ignore un changement réel de réglage serait un bug bien pire
        /// (silencieux, contre-intuitif) que la lenteur que ce correctif corrige.
        /// </summary>
        private bool _previewDirty;

        /// <summary>
        /// Signale qu'un réglage affectant l'aperçu vient de changer — appelé par
        /// GridRoadUISystem.MarkSettingsDirty() (tout SET_* du panneau) et directement par
        /// SetRoadPrefab/SetSecondaryRoadPrefab/SetAvenueRoadPrefab ci-dessous.
        /// </summary>
        public void MarkPreviewDirty()
        {
            _previewDirty = true;
            _settingsVersion++; // un réglage a changé : pas d'historique (voir UpdateHistory)
        }

        // ------------------------------------------------------------------
        // Aperçu léger par défaut en mode Loop (retour utilisateur en jeu, suite au correctif
        // abandonné ci-dessus) : recréer CHAQUE FRAME les centaines d'entités ECS d'un gros laço
        // — même strictement identiques — est une exigence dure du pipeline natif (voir la doc
        // de CreateGridDefinitions), donc pas contournable par une simple détection de
        // changement. La VRAIE économie consiste à ne matérialiser le vrai aperçu ECS QUE
        // pendant la confirmation (Générer) : le reste du temps, ShowSketchOnly est vrai et
        // GridRoadOverlaySystem.DrawLiveSketch dessine un simple croquis (déjà utilisé pendant
        // un drag de slider, voir LivePreviewOverride) à partir des réglages COURANTS (pas d'un
        // LivePreviewOverride). Appliqué aux DEUX modes (Grille classique ET Loop) — un premier
        // essai limitait ça au mode Loop (la Grille classique restant en aperçu réel continu,
        // jugée assez bon marché) mais retour utilisateur en jeu : "no modo grelha ainda tem a
        // pré visualização com as estradas reais" — l'utilisateur veut la même cohérence dans
        // les deux modes, indépendamment du coût réel.
        // ------------------------------------------------------------------

        /// <summary>
        /// Nombre de frames de matérialisation réelle avant d'autoriser Générer à conclure —
        /// voir _confirming. Volontairement généreux (pas juste 1-2) : CreateGridDefinitions
        /// écrit via un EntityCommandBuffer (m_ToolOutputBarrier), dont le "playback" réel
        /// (création effective des entités interrogeables via m_DefinitionQuery) n'est pas
        /// synchrone — il a lieu à un point ultérieur du pipeline ECS, potentiellement une frame
        /// plus tard. La validation native des collisions (GetAllowApply) tourne elle-même sur
        /// des entités déjà matérialisées, donc encore une frame de plus. Avant ce correctif
        /// (retour utilisateur : rejet systématique même Anarchy activé), 2 frames se sont
        /// avérées insuffisantes — impossible de savoir a priori combien il en faut exactement
        /// sans télémétrie native, donc marge large plutôt que deviner au plus juste.
        /// </summary>
        private const int ConfirmMaterializeFrames = 6;

        /// <summary>Attente maximale (frames) de la validation native quand des erreurs restent affichées — voir _confirming.</summary>
        private const int ConfirmMaxFrames = 45;

        /// <summary>
        /// Vrai entre la 1ʳᵉ pression de "Générer" (en mode Loop, aperçu jusque-là en croquis
        /// seul) et l'application effective ou le rejet — le temps que CreateGridDefinitions()
        /// tourne réellement pendant ConfirmMaterializeFrames frames consécutives, pour que le
        /// pipeline natif ait eu le temps de valider les collisions (voir GetAllowApply) avant
        /// de conclure. Pendant cette fenêtre, ShowSketchOnly repasse à faux : le vrai aperçu
        /// ECS remplace le croquis, exactement comme si le mod tournait déjà en continu.
        /// </summary>
        private bool _confirming;
        private int _confirmFramesElapsed;

        /// <summary>
        /// Résolution automatique des collisions (option "AutoResolveCollisions" du menu Options,
        /// désactivée par défaut — retour utilisateur : d'abord demandée, puis retirée car elle
        /// trouait le Superblock, puis redemandée comme option à activer/désactiver) : quand la
        /// confirmation échoue, les tronçons générés qui portent un Game.Tools.Error (voir
        /// TryExcludeErrorSegments) sont ajoutés ici et filtrés par CreateGridDefinitions, puis
        /// la matérialisation recommence — jusqu'à MaxAutoResolveAttempts fois avant de retomber
        /// sur le rejet classique. Clés = extrémités arrondies (voir SegmentKey), stables d'une
        /// frame à l'autre puisque la génération est déterministe pour une même sélection/
        /// configuration. Vidé dès que la sélection ou un réglage change.
        /// </summary>
        private readonly HashSet<(int, int, int, int)> _excludedSegments = new HashSet<(int, int, int, int)>();
        private int _autoResolveAttempts;
        private const int MaxAutoResolveAttempts = 5;

        /// <summary>Courbe réellement posée (après snapping) de chaque tronçon créé à la dernière frame, pour relier un Error natif au tronçon généré qui l'a produit.</summary>
        private readonly List<((int, int, int, int) key, Bezier4x3 curve)> _lastCreatedCurves = new List<((int, int, int, int), Bezier4x3)>();

        /// <summary>Tolérance (m) entre un point d'une entité en erreur et la courbe d'un tronçon généré pour les considérer comme le même.</summary>
        private const float ErrorMatchDistance = 2f;

        /// <summary>
        /// Vrai après un rejet de "Générer" pour cause de collision (voir la fin de la
        /// matérialisation ci-dessous) — retour utilisateur en jeu : en croquis, aucune manière
        /// de VOIR où est la collision avant de presser Générer, qui ne fait alors "rien" de
        /// visible (juste un avertissement dans le log, jamais montré en jeu). Tant que ce
        /// drapeau est vrai, ShowSketchOnly reste faux : le vrai aperçu ECS continue de se
        /// matérialiser à chaque frame (donc la surbrillance rouge native des collisions reste
        /// visible) au lieu de retomber sur le croquis. Effacé dès que la sélection ou un
        /// réglage change à nouveau (voir gestureChanged plus bas) — l'utilisateur vient
        /// d'ajuster quelque chose pour corriger, pas la peine de garder l'ancien rejet affiché.
        /// </summary>
        private bool _showCollisionPreview;

        /// <summary>Vrai depuis le dernier rejet de "Générer" pour collision — voir _showCollisionPreview. Lu par le panneau pour afficher un message clair (PERIMETER_COLLISION).</summary>
        public bool PerimeterCollision { get; private set; }

        /// <summary>
        /// Vrai si l'aperçu affiché est actuellement le croquis léger, pas de vraies entités ECS
        /// — voir la doc ci-dessus. S'applique aux DEUX modes (Grille classique ET Loop).
        /// </summary>
        public bool ShowSketchOnly => !_confirming && !_showCollisionPreview;
        private LivePreviewField? _dragField;
        private int _dragFrameCount;
        private double _dragTotalMs;
        private double _dragMaxMs;

        private Entity _lastClickedNode = Entity.Null;
        private float _lastClickTime = -1f;
        private readonly List<Entity> _pathScratch = new List<Entity>();

        /// <summary>Cache "CulDeSac&lt;Taille&gt;&lt;Style&gt;" → entité résolue (Entity.Null = introuvable, mémorisé pour ne pas répéter la recherche).</summary>
        private readonly Dictionary<string, Entity> _culDeSacCapPrefabCache = new Dictionary<string, Entity>();
        /// <summary>Empêche le spam du log de prefab de cercle introuvable : un avis par nom manquant.</summary>
        private readonly HashSet<string> _culDeSacCapMissingLogged = new HashSet<string>();

        /// <summary>Cache "&lt;Taille&gt;Roundabout01" → entité résolue, même principe que _culDeSacCapPrefabCache.</summary>
        private readonly Dictionary<string, Entity> _roundaboutIslandPrefabCache = new Dictionary<string, Entity>();
        /// <summary>Empêche le spam du log d'îlot de rotonde introuvable : un avis par nom manquant.</summary>
        private readonly HashSet<string> _roundaboutIslandMissingLogged = new HashSet<string>();

        /// <summary>Vrai si le dernier double-clic n'a pas trouvé de contour fermé (affiché en tooltip).</summary>
        public bool PerimeterDetectionFailed { get; private set; }

        /// <summary>Nombre de nœuds actuellement sélectionnés (lu par l'UI et les tooltips).</summary>
        public int NodeCount => _selectedNodes.Count;

        private readonly List<float3> _maxLayersPositions = new List<float3>();
        private int _concentricMaxLayers = ConcentricGenerator.MaxLayersLimit;

        /// <summary>
        /// Nombre maximal d'anneaux Concêntrico pour la sélection actuelle (voir
        /// ConcentricGenerator.MaxLayers) — le panneau borne le slider Camadas à cette valeur.
        /// Recalculé seulement quand la sélection change ; sans périmètre fermé, la limite
        /// absolue (le slider reste libre tant qu'il n'y a rien à mesurer).
        /// </summary>
        public int ConcentricMaxLayers
        {
            get
            {
                if (ActivePositions.Count < 3)
                {
                    _maxLayersPositions.Clear();
                    return ConcentricGenerator.MaxLayersLimit;
                }
                if (!PositionsEqual(ActivePositions, _maxLayersPositions))
                {
                    try
                    {
                        _concentricMaxLayers = ConcentricGenerator.MaxLayers(BuildCurveAwarePerimeterPositions());
                    }
                    catch (Exception e)
                    {
                        Mod.Log.Warn($"Limite de camadas impossible à calculer : {e.Message}");
                        _concentricMaxLayers = ConcentricGenerator.MaxLayersLimit;
                    }
                    _maxLayersPositions.Clear();
                    _maxLayersPositions.AddRange(ActivePositions);
                }
                return _concentricMaxLayers;
            }
        }
        private readonly List<float3> _radialMaxLayersPositions = new List<float3>();
        private (int avenues, float radius) _radialMaxLayersSettings;
        private int _radialMaxLayers = ConcentricGenerator.MaxLayersLimit;

        /// <summary>
        /// Nombre maximal d'anneaux du motif Radial pour la sélection et les réglages actuels (voir
        /// ConcentricGenerator.RadialMaxLayers) — borne haute du slider Camadas en Radial. Recalculé
        /// seulement quand la sélection, le nombre d'avenues ou le rayon de la rotonde change.
        /// </summary>
        /// <summary>
        /// Hauteur du vrai terrain au point (x, z) — pour le motif Relevo (GridParameters.HeightAt).
        /// Données de hauteur lues une fois par appel : un échantillonneur par génération.
        /// </summary>
        public Func<float2, float> MakeTerrainSampler()
        {
            TerrainHeightData data = m_TerrainSystem.GetHeightData();
            return p =>
            {
                TerrainHeightData copy = data;
                return TerrainUtils.SampleHeight(ref copy, new float3(p.x, 0f, p.y));
            };
        }

        private object _heightSnapshotKey;
        private Func<float2, float> _heightSnapshot;

        /// <summary>
        /// Relief de la zone relevé dans une grille (au plus ~400 x 400 points), lisible depuis un thread
        /// de fond (croquis pendant un drag) — TerrainHeightData ne se lit que sur le thread principal.
        /// </summary>
        public Func<float2, float> HeightSnapshot(List<float3> positions)
        {
            float3 sum = float3.zero;
            float2 min = new float2(float.MaxValue), max = new float2(float.MinValue);
            foreach (float3 p in positions)
            {
                sum += p;
                min = math.min(min, p.xz);
                max = math.max(max, p.xz);
            }
            object key = (positions.Count, sum);
            if (_heightSnapshot != null && key.Equals(_heightSnapshotKey)) return _heightSnapshot;
            min -= 50f;
            max += 50f;
            float cell = math.max(8f, math.cmax(max - min) / 400f);
            int nx = (int)math.ceil((max.x - min.x) / cell) + 1, nz = (int)math.ceil((max.y - min.y) / cell) + 1;
            var heights = new float[nx * nz];
            TerrainHeightData data = m_TerrainSystem.GetHeightData();
            for (int j = 0; j < nz; j++)
            {
                for (int i = 0; i < nx; i++)
                {
                    heights[j * nx + i] = TerrainUtils.SampleHeight(ref data, new float3(min.x + i * cell, 0f, min.y + j * cell));
                }
            }
            float2 origin = min;
            _heightSnapshot = q =>
            {
                float fx = math.clamp((q.x - origin.x) / cell, 0f, nx - 1.001f), fz = math.clamp((q.y - origin.y) / cell, 0f, nz - 1.001f);
                int i = (int)fx, j = (int)fz;
                float tx = fx - i, tz = fz - j;
                float a = heights[j * nx + i], b = heights[j * nx + i + 1], c = heights[(j + 1) * nx + i], d = heights[(j + 1) * nx + i + 1];
                return math.lerp(math.lerp(a, b, tx), math.lerp(c, d, tx), tz);
            };
            _heightSnapshotKey = key;
            return _heightSnapshot;
        }

        private readonly List<float3> _contourFlatPositions = new List<float3>();
        private bool _contourFlat;

        /// <summary>
        /// Vrai si le terrain de la sélection est trop plat pour le motif Relevo (rien à suivre) —
        /// affiché dans le panneau. Recalculé seulement quand la sélection change.
        /// </summary>
        public bool ContourTerrainFlat
        {
            get
            {
                if (!_settings.ContourMode || _settings.LoopMode || ActivePositions.Count < 3)
                {
                    _contourFlatPositions.Clear();
                    return false;
                }
                if (!PositionsEqual(ActivePositions, _contourFlatPositions))
                {
                    try
                    {
                        _contourFlat = GridGenerator.IsTerrainFlat(BuildCurveAwarePerimeterPositions(), MakeTerrainSampler());
                    }
                    catch (Exception e)
                    {
                        Mod.Log.Warn($"Relief du motif Relevo impossible à lire : {e.Message}");
                        _contourFlat = false;
                    }
                    _contourFlatPositions.Clear();
                    _contourFlatPositions.AddRange(ActivePositions);
                }
                return _contourFlat;
            }
        }


        public int RadialMaxLayers
        {
            get
            {
                if (ActivePositions.Count < 3)
                {
                    _radialMaxLayersPositions.Clear();
                    return ConcentricGenerator.MaxLayersLimit;
                }
                var settings = (_settings.RadialAvenues, _settings.RadialRoundaboutRadius);
                if (!PositionsEqual(ActivePositions, _radialMaxLayersPositions) || !_radialMaxLayersSettings.Equals(settings))
                {
                    try
                    {
                        _radialMaxLayers = ConcentricGenerator.RadialMaxLayers(BuildCurveAwarePerimeterPositions(), settings.Item1, settings.Item2);
                    }
                    catch (Exception e)
                    {
                        Mod.Log.Warn($"Limite de camadas (Radial) impossible à calculer : {e.Message}");
                        _radialMaxLayers = ConcentricGenerator.MaxLayersLimit;
                    }
                    _radialMaxLayersPositions.Clear();
                    _radialMaxLayersPositions.AddRange(ActivePositions);
                    _radialMaxLayersSettings = settings;
                }
                return _radialMaxLayers;
            }
        }

        /// <summary>Nœuds sélectionnés, dans l'ordre de clic (lus par le rendu overlay).</summary>
        public IReadOnlyList<Entity> SelectedNodes => _selectedNodes;
        /// <summary>Positions des nœuds sélectionnés, dans l'ordre de clic (lues par le rendu overlay).</summary>
        public IReadOnlyList<float3> SelectedPositions => _selectedPositions;
        /// <summary>Nœud actuellement survolé et sélectionnable (Entity.Null sinon).</summary>
        public Entity HoveredNode => _hoveredNode;
        /// <summary>Vrai si une grille prévisualisée existe (définitions créées à la dernière frame).</summary>
        public bool HasPreview { get; private set; }
        /// <summary>Vrai si le périmètre sélectionné ne produit aucune grille (polygone dégénéré).</summary>
        public bool PerimeterInvalid { get; private set; }
        /// <summary>Vrai si la grille prévisualisée peut être construite (pas d'erreur de placement).</summary>
        public bool CanApply { get; private set; }

        /// <summary>
        /// Un champ de GridParameters à la fois (voir LivePreviewField) — retour utilisateur en
        /// jeu, "coloca as linhas em todas as opções" : généralise LiveSpacingPreview (limité au
        /// seul slider Espaçamento) à TOUS les sliders du panneau. Réglé par SliderControl côté
        /// React (prop onDragPreview) pendant un drag — retour utilisateur en jeu : régénérer la
        /// vraie grille (entités ECS complètes) à chaque pixel parcouru est trop coûteux avec une
        /// grille dense (voir l'enquête de performance complète). NE touche JAMAIS _settings ni
        /// ne déclenche CreateGridDefinitions — seul GridRoadOverlaySystem le lit, pour dessiner
        /// un simple esquisse de lignes (buffer.DrawDashedLine, aucune entité créée) qui suit le
        /// doigt en direct. Effacé (null) dès que le drag se termine (le SET_* correspondant,
        /// la vraie valeur, prend le relais et régénère la grille réelle une seule fois).
        /// </summary>
        public (LivePreviewField Field, float Value)? LivePreviewOverride { get; set; }

        /// <summary>
        /// Un slider par valeur — voir LivePreviewOverride. L'ORDRE/les valeurs DOIVENT rester
        /// synchronisés avec LiveField côté bindings.ts (un entier brut transite sur le binding,
        /// pas de partage de type possible entre C# et TS).
        /// </summary>
        public enum LivePreviewField
        {
            Spacing = 0,
            Columns = 1,
            Rows = 2,
            Angle = 3,
            // 4 = ArterialSpacing, supprimé avec le niveau Arterial (voir GridGenerator.
            // GenerateLoopGrid) — valeur volontairement non réutilisée pour rester synchronisée
            // avec LiveField côté bindings.ts.
            CollectorSpacing = 5,
            LoopCulDeSacRatio = 6,
            CulDeSacDepth = 7,
            CulDeSacRatio = 8,
            AvenueColumnIndex = 9,
            AvenueRowIndex = 10,
            SuperblockZone = 11,
            ConcentricLayers = 12,
            ConcentricConnections = 13,
            RadialAvenues = 14,
            RadialRoundabout = 15,
            RadialLayers = 16,
            // 17 = FishboneRibSpacing, motif retiré — valeur volontairement non réutilisée.
            TreeBranchSpacing = 18,
            TreeCulDeSacSpacing = 19,
            TreeCulDeSacLength = 20,
            OrganicStreetSpacing = 21,
            OrganicCurviness = 22,
            OrganicLoopShare = 23,
            OrganicSeed = 24,
            ContourSpacing = 25,
            ContourConnectorSpacing = 26,
            MixedCoreRadius = 27,
        }

        /// <summary>
        /// Geste "drag de slider" démarré/poursuivi — voir GridRoadUISystem, SET_LIVE_PREVIEW.
        /// Logge le DÉBUT une seule fois (nouveau champ ou aucun drag en cours) ; les frames
        /// individuelles pendant le drag sont accumulées (RecordDragFrame), JAMAIS logguées une
        /// par une (voir l'enquête de performance précédente sur le danger du log par-frame).
        /// </summary>
        public void BeginOrContinueDrag(LivePreviewField field)
        {
            if (_dragField == field)
            {
                return;
            }
            if (_dragField.HasValue)
            {
                LogDragEnd(); // changement de champ en plein drag (rare) : clôture proprement l'ancien.
            }
            _dragField = field;
            _dragFrameCount = 0;
            _dragTotalMs = 0;
            _dragMaxMs = 0;
            Mod.Log.Info($"[Perf] gesto=arrasto início campo={field}");
        }

        /// <summary>Une frame de croquis dessinée pendant le drag en cours — voir GridRoadOverlaySystem.DrawLiveSketch. Accumulé, pas loggué individuellement.</summary>
        public void RecordDragFrame(double durationMs)
        {
            if (!_dragField.HasValue)
            {
                return;
            }
            _dragFrameCount++;
            _dragTotalMs += durationMs;
            if (durationMs > _dragMaxMs)
            {
                _dragMaxMs = durationMs;
            }
        }

        /// <summary>Fin du drag (relâchement) — voir GridRoadUISystem, CLEAR_LIVE_PREVIEW. Logge le résumé (frames/moyenne/max) UNE fois.</summary>
        public void EndDrag()
        {
            if (!_dragField.HasValue)
            {
                return;
            }
            LogDragEnd();
            _dragField = null;
        }

        private void LogDragEnd()
        {
            double avgMs = _dragFrameCount > 0 ? _dragTotalMs / _dragFrameCount : 0;
            Mod.Log.Info($"[Perf] gesto=arrasto fim campo={_dragField} frames={_dragFrameCount} médiaMs={avgMs:F2} máxMs={_dragMaxMs:F2}");
        }

        /// <summary>
        /// Vue active (Underground/ZoneGrid/InvisibleNetworks) tant que l'outil tourne — voir
        /// RefreshViews. Restaurée depuis les settings à l'activation (OnStartRunning).
        /// </summary>
        public ViewOption SelectedViews { get; set; }

        private ProxyAction _confirmAction;
        private GridRoadGeneratorSettings _settings;

        private NetToolSystem m_NetToolSystem;
        private TerrainSystem m_TerrainSystem;
        private ToolOutputBarrier m_ToolOutputBarrier;
        private RenderingSystem m_RenderingSystem;
        /// <summary>
        /// Arbre spatial des arêtes/nœuds de route existants (voir FindInteriorExistingEdges) —
        /// utilisé UNIQUEMENT en lecture directe hors job (JobHandle complété avant accès, voir
        /// cette méthode), jamais planifié comme job Burst depuis ce système.
        /// </summary>
        private Game.Net.SearchSystem m_NetSearchSystem;
        private EntityQuery m_DefinitionQuery;
        /// <summary>
        /// Tous les nœuds routiers sélectionnables (jamais les nœuds Temp d'aperçu), lus par
        /// GridRoadOverlaySystem pour dessiner un point semi-transparent sur chacun, avant même
        /// le survol — comme CS2-NetworkTools (NT_Eligible). Le composant vanilla Highlighted
        /// seul ne produit aucun rendu visible sur un nœud (vérifié : NetworkTools dessine aussi
        /// ces points lui-même, via OverlaySystem.DrawNodesJob, pas via Highlighted), d'où ce
        /// choix de rester sur notre propre dessin (déjà en place pour la sélection/le survol)
        /// plutôt que de compter sur un composant ECS vanilla qui ne fait rien ici.
        /// </summary>
        public EntityQuery EligibleRoadNodesQuery => m_EligibleRoadNodesQuery;
        private EntityQuery m_EligibleRoadNodesQuery;

        private PrefabBase _fallbackPrefab;
        private bool _fallbackSearched;
        /// <summary>Repli par défaut du réseau "secundária" en mode SuperblockMode (voir GetSecondaryRoadPrefab) — un vrai chemin piéton plutôt que le même prefab que la coletora.</summary>
        private PrefabBase _pedestrianFallbackPrefab;
        private bool _pedestrianFallbackSearched;
        private PrefabBase _overridePrefab;
        private bool _overrideResolved;
        private PrefabBase _secondaryOverridePrefab;
        private bool _secondaryOverrideResolved;
        private PrefabBase _avenueOverridePrefab;
        private bool _avenueOverrideResolved;
        private PrefabBase _roundaboutOverridePrefab;
        private bool _roundaboutOverrideResolved;
        private PrefabBase _pathOverridePrefab;
        private bool _pathOverrideResolved;
        private Game.City.CityConfigurationSystem m_CityConfigurationSystem;

        /// <summary>Arête du périmètre (route existante entre deux nœuds sélectionnés consécutifs).</summary>
        private struct PerimeterEdge
        {
            public Entity m_Entity;
            public Bezier4x3 m_Curve;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_NetToolSystem = World.GetOrCreateSystemManaged<NetToolSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_RenderingSystem = World.GetOrCreateSystemManaged<RenderingSystem>();
            m_NetSearchSystem = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            m_CityConfigurationSystem = World.GetOrCreateSystemManaged<Game.City.CityConfigurationSystem>();
            m_DefinitionQuery = GetDefinitionQuery();
            m_EligibleRoadNodesQuery = GetEntityQuery(
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Road>(),
                ComponentType.Exclude<Temp>());

            _settings = Mod.Instance.Settings;
            _confirmAction = _settings.GetAction(GridRoadGeneratorSettings.ActionConfirmGrid);
        }

        /// <summary>
        /// Aucun prefab annoncé au jeu (retour utilisateur : "porquê que o painel do jogo também se abre
        /// quando abro o mod?") : avec un prefab de route, le jeu traitait l'outil comme l'outil route et
        /// ouvrait son menu des routes. Les réseaux se choisissent dans le panneau du mod (GetMainPrefab).
        /// </summary>
        public override PrefabBase GetPrefab() => null;

        /// <summary>Réseau principal résolu (voir GetRoadPrefab) — lu par GridRoadUISystem pour le panneau.</summary>
        public PrefabBase GetMainPrefab() => GetRoadPrefab();

        /// <summary>Réseau secondaire résolu (voir GetSecondaryRoadPrefab) — lu par GridRoadUISystem pour la barre de sélection.</summary>
        public PrefabBase GetSecondaryPrefab() => GetSecondaryRoadPrefab();

        /// <summary>Réseau des liaisons piétonnes résolu (voir GetPathRoadPrefab) — lu par GridRoadUISystem.</summary>
        public PrefabBase GetPathPrefab() => GetPathRoadPrefab();

        /// <summary>Réseau de la rotonde résolu (voir GetRoundaboutRoadPrefab) — lu par GridRoadUISystem.</summary>
        public PrefabBase GetRoundaboutPrefab() => GetRoundaboutRoadPrefab();

        /// <summary>Réseau avenue résolu (voir GetAvenueRoadPrefab) — lu par GridRoadUISystem pour la barre de sélection.</summary>
        public PrefabBase GetAvenuePrefab() => GetAvenueRoadPrefab();

        /// <summary>
        /// Motif Relevo : courbes de niveau du jeu ("Topografia") affichées d'office (retour
        /// utilisateur) — même mécanique que l'outil des routes (Snap.ContourLines, que
        /// UndergroundViewSystem lit sur l'outil actif), sans toucher au réglage global du joueur.
        /// </summary>
        public override void GetAvailableSnapMask(out Snap onMask, out Snap offMask)
        {
            base.GetAvailableSnapMask(out onMask, out offMask);
            if (_settings != null && _settings.ContourMode && !_settings.LoopMode)
            {
                onMask |= Snap.ContourLines;
                offMask |= Snap.ContourLines;
            }
        }

        /// <summary>L'outil ne s'active que par son raccourci ou le panneau, jamais via un prefab.</summary>
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // Zone libre : les clics visent le terrain, pas les routes.
            if (_settings.FreeAreaMode)
            {
                m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
                return;
            }

            // Réseaux routiers uniquement, nœuds ciblables via SubElements.
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            ResetState();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            if (_confirmAction != null)
            {
                _confirmAction.shouldBeEnabled = true;
            }
            SelectedViews = _settings.SelectedViews;
            RefreshViews();
        }

        protected override void OnStopRunning()
        {
            ResetState();
            ClearHistory();
            if (_confirmAction != null)
            {
                _confirmAction.shouldBeEnabled = false;
            }
            // Purge les définitions restantes pour ne pas laisser d'aperçu fantôme derrière soi.
            // PAS via DestroyDefinitions : il crée un EntityCommandBuffer sur m_ToolOutputBarrier,
            // que le jeu interdit hors de la phase de mise à jour de l'outil — fermer le panneau
            // pendant l'aperçu réel (seul cas où il reste des définitions) levait "Trying to create
            // EntityCommandBuffer when it's not allowed!" en erreur critique (retour utilisateur,
            // Player.log). Suppression directe sur le thread principal, jobs en cours terminés.
            try
            {
                if (!m_DefinitionQuery.IsEmptyIgnoreFilter)
                {
                    Dependency.Complete();
                    EntityManager.DestroyEntity(m_DefinitionQuery);
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Impossible de purger l'aperçu à la fermeture de l'outil.");
            }
            // Nettoie l'état de rendu (requireUnderground/requireZones sont des champs
            // ToolBaseSystem propres à cette instance, jamais relus une fois l'outil inactif ;
            // markersVisible vit sur le RenderingSystem partagé du monde, donc explicitement
            // remis à false ici — même pattern que CS2-NetworkTools BaseToolSystem.OnStopRunning).
            m_RenderingSystem.markersVisible = false;
            base.OnStopRunning();
        }

        /// <summary>
        /// Applique SelectedViews aux champs de rendu vanilla (ToolBaseSystem.requireUnderground/
        /// requireZones) et au RenderingSystem (markersVisible, réseaux normalement invisibles) —
        /// pattern repris de CS2-NetworkTools BaseToolSystem.RefreshViews. Appelé à l'activation
        /// de l'outil et à chaque changement depuis le panneau (GridRoadUISystem).
        /// </summary>
        public void RefreshViews()
        {
            requireUnderground = (SelectedViews & ViewOption.Underground) != 0;
            requireZones = (SelectedViews & ViewOption.ZoneGrid) != 0;
            m_RenderingSystem.markersVisible = (SelectedViews & ViewOption.InvisibleNetworks) != 0;
        }

        /// <summary>Active/désactive l'outil (raccourci clavier global ou panneau UI).</summary>
        public void ToggleTool()
        {
            m_ToolSystem.activeTool = m_ToolSystem.activeTool == this ? (ToolBaseSystem)m_DefaultToolSystem : this;
        }

        /// <summary>Demande la construction de la grille prévisualisée (bouton "Générer" du panneau).</summary>
        public void RequestApply() => _applyRequested = true;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // Cadence vanilla du NetTool : par défaut on repart de zéro chaque frame
            // (Temp détruits + régénérés depuis les définitions recréées ci-dessous).
            applyMode = ApplyMode.Clear;

            try
            {
                // Zonage automatique après Générer : quelques frames réservées (voir UpdateZoning).
                if (UpdateZoning(ref inputDeps))
                {
                    return inputDeps;
                }

                // Échap : annule la sélection en cours (1er appui) ; sans sélection,
                // désactive l'outil — le panneau se ferme et le bouton toolbar se
                // relâche via TOOL_ACTIVE (même cadence que les outils vanilla).
                if (cancelAction.WasPressedThisFrame())
                {
                    bool hadSelection = _selectedNodes.Count > 0 || HasFreeAreaSelection;
                    ResetState();
                    if (!hadSelection)
                    {
                        m_ToolSystem.activeTool = m_DefaultToolSystem;
                    }
                    return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                }

                // Entrée ou bouton "Générer".
                bool confirm = _applyRequested || (_confirmAction != null && _confirmAction.WasPressedThisFrame());
                _applyRequested = false;
                if (confirm && HasPreview && !_confirming)
                {
                    if (!ShowSketchOnly)
                    {
                        // N'arrive ici QUE si _showCollisionPreview est vrai (un rejet précédent
                        // a laissé le vrai aperçu matérialisé pour que le joueur voie la
                        // collision, voir sa doc) : l'aperçu réel tourne déjà en continu depuis
                        // des frames, donc déjà validé — concrétise directement l'aperçu de la
                        // frame précédente sans repasser par la matérialisation de 6 frames.
                        if (GetAllowApply() && !m_DefinitionQuery.IsEmptyIgnoreFilter)
                        {
                            // Pas de durée mesurée ici : la pose réelle (CourseSplitSystem et le
                            // reste du pipeline ECS natif) est asynchrone sur les frames
                            // suivantes, pas synchrone dans cet appel — seul le geste lui-même
                            // est loggué.
                            Mod.Log.Info($"[Perf] gesto=generate nós={ActivePositions.Count}");
                            applyMode = ApplyMode.Apply;
                            StartZoning();
                            ResetState();
                            ClearHistory();
                            return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                        }
                        Mod.Log.Warn("Grille refusée : l'aperçu contient des erreurs de placement (collisions, pente...). Ajuste le périmètre ou l'espacement.");
                        // Voir _showCollisionPreview/PerimeterCollision : garde le vrai aperçu
                        // visible (surbrillance native des collisions) au lieu de retomber sur
                        // le croquis, et affiche un message clair dans le panneau — sans ça,
                        // "Générer" ne fait rien de visible pour le joueur.
                        _showCollisionPreview = true;
                        PerimeterCollision = true;
                    }
                    else
                    {
                        // Aperçu jusqu'ici en croquis seul (ShowSketchOnly) : démarre la
                        // matérialisation réelle (voir _confirming/ConfirmMaterializeFrames) au
                        // lieu de conclure tout de suite — le bloc de reconstruction plus bas
                        // matérialise un vrai aperçu ECS dès CETTE frame (ShowSketchOnly devient
                        // faux), mais le pipeline natif a besoin de quelques frames pour valider
                        // les collisions avant qu'on puisse se fier à GetAllowApply().
                        _confirming = true;
                        _confirmFramesElapsed = 0;
                    }
                }

                if (_settings.FreeAreaMode)
                {
                    HandleFreeAreaInput();
                }
                else
                {
                // Survol : surbrillance du nœud sous le curseur.
                Entity hovered = ResolveHoveredNode();
                if (hovered != _hoveredNode)
                {
                    if (!_selectedNodes.Contains(_hoveredNode))
                    {
                        SetHighlight(_hoveredNode, false);
                    }
                    SetHighlight(hovered, true);
                    _hoveredNode = hovered;
                }

                // Clic gauche : sélection/désélection du nœud survolé. Un double-clic sur
                // le premier nœud sélectionné tente de détecter le contour fermé complet.
                if (applyAction.WasPressedThisFrame() && _hoveredNode != Entity.Null)
                {
                    float now = UnityEngine.Time.unscaledTime;
                    bool isDoubleClick = _hoveredNode == _lastClickedNode && (now - _lastClickTime) <= DoubleClickWindow;
                    _lastClickedNode = _hoveredNode;
                    _lastClickTime = now;
                    PerimeterDetectionFailed = false;

                    if (isDoubleClick && _selectedNodes.Count > 0 && _hoveredNode == _selectedNodes[0])
                    {
                        TryAutoSelectPerimeter();
                    }
                    else
                    {
                        ToggleNodeSelection(_hoveredNode);
                    }
                }

                // Clic droit : retire le dernier nœud sélectionné.
                if (secondaryApplyAction.WasPressedThisFrame() && _selectedNodes.Count > 0)
                {
                    Entity last = _selectedNodes[_selectedNodes.Count - 1];
                    _selectedNodes.RemoveAt(_selectedNodes.Count - 1);
                    _selectedPositions.RemoveAt(_selectedPositions.Count - 1);
                    if (last != _hoveredNode)
                    {
                        SetHighlight(last, false);
                    }
                }
                }

                UpdateHistory();

                // Reconstruction de l'aperçu (chaque frame, comme le NetTool — pas d'empilement,
                // voir la doc de CreateGridDefinitions pour pourquoi une recréation à chaque
                // frame n'est PAS optionnelle dès qu'un vrai aperçu ECS doit rester visible/
                // validé). PENDANT un drag de slider (LivePreviewOverride non-null, voir
                // GridRoadUISystem/GridRoadOverlaySystem.DrawLiveSketch) OU hors confirmation
                // (ShowSketchOnly, voir sa doc — s'applique aux DEUX modes) : détruit l'aperçu
                // réel SANS le recréer — le croquis léger le remplace (drag) ou en tient lieu par
                // défaut. Le vrai aperçu revient dès le relâchement du drag ou dès "Générer"
                // (voir _confirming ci-dessus).
                bool suppressRealPreview = LivePreviewOverride.HasValue || ShowSketchOnly;
                inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                HasPreview = false;
                PerimeterInvalid = false;
                if (ActivePositions.Count >= 2 && suppressRealPreview)
                {
                    // Croquis (drag ou ShowSketchOnly) : aucune entité ECS créée, juste un calcul
                    // pur (GridGenerator, déjà vérifié rapide même sur de grandes zones — voir
                    // GridGeneratorPerformanceTests) pour savoir si le périmètre produit une
                    // grille valide, et activer/désactiver Générer en conséquence.
                    HasPreview = ComputePreviewValidityCheap();
                    PerimeterInvalid = !HasPreview;
                }
                else if (ActivePositions.Count >= 2)
                {
                    // Geste "sélection/paramètre changé" (voir _lastGesture*) : ne logge le temps
                    // de CreateGridDefinitions QUE si la configuration a réellement changé depuis
                    // le dernier log — jamais à chaque frame identique (même discipline que le
                    // drag, voir BeginOrContinueDrag/RecordDragFrame). La création elle-même,
                    // contrairement au log, tourne bien à CHAQUE frame (voir ci-dessus).
                    bool gestureChanged = _previewDirty || !PositionsEqual(ActivePositions, _lastGesturePositions);
                    Stopwatch gestureStopwatch = gestureChanged ? Stopwatch.StartNew() : null;
                    if (gestureChanged)
                    {
                        // Nouvelle sélection/configuration : les tronçons retirés pour
                        // l'ancienne (voir _excludedSegments) n'ont plus de sens.
                        _excludedSegments.Clear();
                        _autoResolveAttempts = 0;
                    }
                    int created = CreateGridDefinitions();
                    if (gestureChanged)
                    {
                        gestureStopwatch.Stop();
                        Mod.Log.Info($"[Perf] gesto=seleção/parâmetro nós={ActivePositions.Count} loop={_settings.LoopMode} criados={created} duraçãoMs={gestureStopwatch.Elapsed.TotalMilliseconds:F2}");
                        _previewDirty = false;
                        _lastGesturePositions.Clear();
                        _lastGesturePositions.AddRange(ActivePositions);
                        // Un vrai changement efface un rejet précédent (voir _showCollisionPreview/
                        // PerimeterCollision) : l'utilisateur vient d'ajuster quelque chose, plus la
                        // peine de garder affiché "Générer" a été refusé pour l'ancienne config.
                        _showCollisionPreview = false;
                        PerimeterCollision = false;
                    }
                    HasPreview = created > 0;
                    PerimeterInvalid = created == 0;
                }
                if (PerimeterInvalid && !_invalidLogged)
                {
                    _invalidLogged = true;
                    Mod.Log.Info("Périmètre dégénéré (aire trop petite ou points alignés) : aucune grille générée.");
                }
                else if (!PerimeterInvalid)
                {
                    _invalidLogged = false;
                }

                // Conclusion de la confirmation démarrée plus haut (_confirming) : après
                // ConfirmMaterializeFrames frames de vrai aperçu ECS (matérialisé ci-dessus
                // puisque ShowSketchOnly est faux tant que _confirming est vrai), le pipeline
                // natif a eu le temps de valider les collisions — applique ou rejette.
                if (_confirming)
                {
                    _confirmFramesElapsed++;
                    if (_confirmFramesElapsed >= ConfirmMaterializeFrames)
                    {
                        bool hasPreviewNow = HasPreview;
                        bool allowApplyNow = GetAllowApply();
                        bool queryEmptyNow = m_DefinitionQuery.IsEmptyIgnoreFilter;
                        if (hasPreviewNow && allowApplyNow && !queryEmptyNow)
                        {
                            Mod.Log.Info($"[Perf] gesto=generate nós={ActivePositions.Count}");
                            applyMode = ApplyMode.Apply;
                            StartZoning();
                            ResetState();
                            ClearHistory();
                            return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                        }
                        // Erreurs peut-être passagères (retour utilisateur : "Générer" refusé, puis
                        // accepté tel quel en réappuyant 5 s plus tard) : la validation native d'une
                        // grande grille s'étale sur plusieurs frames — on attend qu'elle se stabilise,
                        // jusqu'à ConfirmMaxFrames, avant de conclure au rejet.
                        if (hasPreviewNow && !queryEmptyNow && _confirmFramesElapsed < ConfirmMaxFrames)
                        {
                            CanApply = false;
                            return inputDeps;
                        }
                        // Résolution automatique (option du menu Options, voir _excludedSegments) :
                        // retire les tronçons générés en erreur et relance la matérialisation, au
                        // lieu de rejeter toute la grille pour quelques tronçons en conflit.
                        if (_settings.AutoResolveCollisions && hasPreviewNow && !allowApplyNow
                            && _autoResolveAttempts < MaxAutoResolveAttempts)
                        {
                            int removed = TryExcludeErrorSegments();
                            if (removed > 0)
                            {
                                _autoResolveAttempts++;
                                _confirmFramesElapsed = 0;
                                Mod.Log.Info($"Collision détectée : {removed} tronçon(s) en conflit retiré(s) automatiquement (tentative {_autoResolveAttempts}/{MaxAutoResolveAttempts}, {_excludedSegments.Count} au total).");
                                CanApply = false;
                                return inputDeps;
                            }
                        }
                        Mod.Log.Info("Grille refusée : collision non résolue automatiquement.");
                        LogCollisionDetails();
                        // Voir _showCollisionPreview/PerimeterCollision : garde le vrai aperçu
                        // visible (surbrillance native des collisions) au lieu de retomber sur
                        // le croquis, et affiche un message clair dans le panneau.
                        _confirming = false;
                        _showCollisionPreview = true;
                        PerimeterCollision = true;
                    }
                }
                CanApply = HasPreview && GetAllowApply();
            }
            catch (Exception e)
            {
                // Jamais de crash du jeu : on log, on nettoie, et l'outil reste utilisable.
                Mod.Log.Error(e, "Erreur dans GridRoadToolSystem, sélection annulée.");
                ResetState();
            }

            return inputDeps;
        }

        // ------------------------------------------------------------------
        // Survol et sélection des nœuds
        // ------------------------------------------------------------------

        /// <summary>
        /// Résout le nœud sous le curseur, à la manière de NetworkTools : si le raycast touche
        /// directement un nœud on le prend ; s'il touche une arête, on prend le nœud d'extrémité
        /// le plus proche du point survolé sur la courbe (hit.m_Position), dans la limite de
        /// MaxSelectDistance. La position stockée vient toujours du composant Node, jamais du
        /// point d'impact du rayon.
        /// </summary>
        private Entity ResolveHoveredNode()
        {
            if (!GetRaycastResult(out Entity entity, out RaycastHit hit))
            {
                return Entity.Null;
            }

            if (EntityManager.HasComponent<Game.Net.Node>(entity))
            {
                return entity;
            }
            if (hit.m_HitEntity != entity && EntityManager.HasComponent<Game.Net.Node>(hit.m_HitEntity))
            {
                return hit.m_HitEntity;
            }
            if (EntityManager.TryGetComponent(entity, out Edge edge)
                && EntityManager.TryGetComponent(edge.m_Start, out Game.Net.Node startNode)
                && EntityManager.TryGetComponent(edge.m_End, out Game.Net.Node endNode))
            {
                float distToStart = math.distance(hit.m_Position, startNode.m_Position);
                float distToEnd = math.distance(hit.m_Position, endNode.m_Position);
                Entity closest = distToStart <= distToEnd ? edge.m_Start : edge.m_End;
                if (math.min(distToStart, distToEnd) < MaxSelectDistance)
                {
                    return closest;
                }
            }
            return Entity.Null;
        }

        private void ToggleNodeSelection(Entity node)
        {
            if (!EntityManager.TryGetComponent(node, out Game.Net.Node nodeData))
            {
                return;
            }

            int index = _selectedNodes.IndexOf(node);
            if (index >= 0)
            {
                _selectedNodes.RemoveAt(index);
                _selectedPositions.RemoveAt(index);
                if (node != _hoveredNode)
                {
                    SetHighlight(node, false);
                }
                return;
            }

            // S'il existe un chemin sur le réseau EXISTANT entre le dernier nœud
            // sélectionné et celui-ci, insère les nœuds intermédiaires dans l'ordre
            // (comme si le joueur avait cliqué dessus un par un). Réseaux déconnectés :
            // comportement direct conservé (ajoute simplement le nœud cliqué).
            if (_selectedNodes.Count > 0
                && NetworkGraphAlgorithms.FindShortestPath(new EcsNetworkGraph(EntityManager),
                    _selectedNodes[_selectedNodes.Count - 1], node, _pathScratch, MaxPathfindNodes))
            {
                for (int i = 1; i < _pathScratch.Count; i++)
                {
                    Entity pathNode = _pathScratch[i];
                    if (_selectedNodes.Contains(pathNode)
                        || !EntityManager.TryGetComponent(pathNode, out Game.Net.Node pathNodeData))
                    {
                        continue;
                    }
                    AddSelectedNode(pathNode, pathNodeData.m_Position);
                }
                return;
            }

            // Position exacte du nœud (composant Node), pas le point d'impact du raycast.
            AddSelectedNode(node, nodeData.m_Position);
        }

        /// <summary>
        /// Ajoute un nœud à la sélection du périmètre, SAUF s'il est à moins de
        /// GridGenerator.MinNodeDistance du dernier nœud déjà sélectionné — deux vrais nœuds du
        /// jeu quasi confondus (fréquent près d'une intersection complexe/un raccord de voie)
        /// produiraient sinon un faux petit cran dans le périmètre, qui perturbe ensuite le
        /// décalage de polygone en mode Adaptativo. Le nœud "en trop" est fusionné avec le
        /// précédent : reste surligné (retour visuel du clic inchangé), mais ne devient pas un
        /// sommet de périmètre distinct — même principe que le "Super nó" de NetworkTools,
        /// appliqué ici à la sélection plutôt qu'à une fusion en jeu du réseau lui-même.
        /// </summary>
        private void AddSelectedNode(Entity node, float3 position)
        {
            if (_selectedPositions.Count > 0
                && math.distance(_selectedPositions[_selectedPositions.Count - 1], position) < GridGenerator.MinNodeDistance)
            {
                SetHighlight(node, true);
                return; // fusionné avec le sommet précédent : pas de doublon quasi confondu
            }

            _selectedNodes.Add(node);
            _selectedPositions.Add(position);
            SetHighlight(node, true);
        }

        /// <summary>
        /// Double-clic sur le premier nœud sélectionné : tente de détecter le contour
        /// fermé du réseau à partir de ce point (suivi de la face, "hug the right
        /// wall") et remplace toute la sélection par les nœuds du contour dans
        /// l'ordre. Échec propre (aucun crash, aucun blocage) si le point de départ
        /// n'appartient à aucune boucle fermée : la sélection en cours est conservée
        /// et PerimeterDetectionFailed est levé pour le tooltip contextuel.
        /// </summary>
        private void TryAutoSelectPerimeter()
        {
            var loop = new List<Entity>();
            if (!NetworkGraphAlgorithms.TraceBoundary(new EcsNetworkGraph(EntityManager),
                _selectedNodes[0], loop, MaxPerimeterNodes))
            {
                PerimeterDetectionFailed = true;
                Mod.Log.Info("Périmètre non détecté depuis ce nœud : continue la sélection manuellement.");
                return;
            }

            foreach (Entity node in _selectedNodes)
            {
                if (node != loop[0])
                {
                    SetHighlight(node, false);
                }
            }
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            foreach (Entity node in loop)
            {
                if (!EntityManager.TryGetComponent(node, out Game.Net.Node nodeData))
                {
                    continue;
                }
                AddSelectedNode(node, nodeData.m_Position);
            }
        }

        /// <summary>
        /// Adaptateur ECS de l'abstraction de graphe pure (Core/NetworkGraph.cs) : lit
        /// les arêtes sortantes d'un nœud via ConnectedEdge/Edge/Curve, en coût
        /// (longueur réelle de l'arête) et direction (dans le plan XZ, pour le suivi
        /// de contour). Une nouvelle liste à chaque appel — pas d'état partagé.
        /// </summary>
        private readonly struct EcsNetworkGraph : INetworkGraph<Entity>
        {
            private readonly EntityManager _entityManager;

            public EcsNetworkGraph(EntityManager entityManager) => _entityManager = entityManager;

            public IReadOnlyList<GraphEdge<Entity>> GetNeighbors(Entity node)
            {
                var result = new List<GraphEdge<Entity>>();
                if (!_entityManager.TryGetComponent(node, out Game.Net.Node nodeData)
                    || !_entityManager.TryGetBuffer(node, true, out DynamicBuffer<ConnectedEdge> connectedEdges))
                {
                    return result;
                }

                for (int i = 0; i < connectedEdges.Length; i++)
                {
                    Entity edgeEntity = connectedEdges[i].m_Edge;
                    if (!_entityManager.TryGetComponent(edgeEntity, out Edge edge))
                    {
                        continue;
                    }
                    Entity neighbor = edge.m_Start == node ? edge.m_End : edge.m_Start;
                    if (neighbor == node || !_entityManager.TryGetComponent(neighbor, out Game.Net.Node neighborData))
                    {
                        continue; // arête dégénérée ou voisin invalide, ignorée
                    }

                    float cost = _entityManager.TryGetComponent(edgeEntity, out Curve curve) ? curve.m_Length : 0f;

                    float2 delta = neighborData.m_Position.xz - nodeData.m_Position.xz;
                    float length = math.length(delta);
                    float2 direction = length > 1e-4f ? delta / length : new float2(1f, 0f);

                    result.Add(new GraphEdge<Entity>(neighbor, cost, direction));
                }
                return result;
            }
        }

        private void ResetState()
        {
            foreach (Entity node in _selectedNodes)
            {
                SetHighlight(node, false);
            }
            SetHighlight(_hoveredNode, false);
            _hoveredNode = Entity.Null;
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            _applyRequested = false;
            HasPreview = false;
            PerimeterInvalid = false;
            CanApply = false;
            SummarySegments = 0;
            SummaryLength = 0f;
            SummaryCost = 0;
            _invalidLogged = false;
            _omittedNodesLogged = false;
            PerimeterDetectionFailed = false;
            // Force une régénération complète à la prochaine sélection valide, même si elle
            // reproduit EXACTEMENT les mêmes positions que la précédente (voir PositionsEqual).
            _lastGesturePositions.Clear();
            _previewDirty = true;
            _confirming = false;
            _confirmFramesElapsed = 0;
            _showCollisionPreview = false;
            PerimeterCollision = false;
            _excludedSegments.Clear();
            _autoResolveAttempts = 0;
            _lastCreatedCurves.Clear();
            ClearFreeArea();
        }

        /// <summary>
        /// Refus de Générer : chaque entité que le jeu marque en erreur — position, genre (arête/nœud/
        /// objet), réseau, généré ou existant, type d'erreur — pour rejouer le cas en test.
        /// </summary>
        private void LogCollisionDetails()
        {
            try
            {
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                using (NativeArray<Entity> errorEntities = m_ErrorQuery.ToEntityArray(Allocator.Temp))
                {
                    Mod.Log.Warn($"[Diag colisão] {errorEntities.Length} entidade(s) com erro ; padrão organic={_settings.OrganicMode} tree={_settings.TreeMode} contour={_settings.ContourMode} mixed={_settings.MixedMode} loop={_settings.LoopMode} áreaLivre={_settings.FreeAreaMode}");
                    int shown = 0;
                    foreach (Entity e in errorEntities)
                    {
                        if (shown++ >= 40)
                        {
                            Mod.Log.Warn($"[Diag colisão] ... {errorEntities.Length - 40} erro(s) a mais");
                            break;
                        }
                        string origin = (EntityManager.HasComponent<Temp>(e) ? "gerado" : "existente") + ErrorTypes(e);
                        string prefabName = EntityManager.TryGetComponent(e, out PrefabRef prefabRef) && m_PrefabSystem.TryGetPrefab(prefabRef.m_Prefab, out PrefabBase errorPrefab)
                            ? errorPrefab.name : "?";
                        if (EntityManager.TryGetComponent(e, out Curve curve))
                        {
                            float3 a = curve.m_Bezier.a, d = curve.m_Bezier.d;
                            Mod.Log.Warn(string.Format(inv, "[Diag colisão] aresta {0} {1} de ({2:F1} {3:F1}) a ({4:F1} {5:F1}) comprimento={6:F1}",
                                origin, prefabName, a.x, a.z, d.x, d.z, MathUtils.Length(curve.m_Bezier)));
                        }
                        else if (EntityManager.TryGetComponent(e, out Game.Net.Node node))
                        {
                            Mod.Log.Warn(string.Format(inv, "[Diag colisão] nó {0} {1} em ({2:F1} {3:F1})", origin, prefabName, node.m_Position.x, node.m_Position.z));
                        }
                        else if (EntityManager.TryGetComponent(e, out Game.Objects.Transform transform))
                        {
                            Mod.Log.Warn(string.Format(inv, "[Diag colisão] objeto {0} {1} em ({2:F1} {3:F1})", origin, prefabName, transform.m_Position.x, transform.m_Position.z));
                        }
                        else
                        {
                            Mod.Log.Warn($"[Diag colisão] entidade {origin} {prefabName} sem posição conhecida");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log.Error(e, "Diagnóstico de colisão falhou.");
            }
        }

        /// <summary>Type(s) d'erreur natif(s) d'une entité (icônes de notification portant ToolErrorData).</summary>
        private string ErrorTypes(Entity e)
        {
            if (!EntityManager.TryGetBuffer(e, true, out DynamicBuffer<Game.Notifications.IconElement> icons) || icons.Length == 0)
            {
                return "";
            }
            var types = new List<string>();
            for (int k = 0; k < icons.Length; k++)
            {
                if (EntityManager.TryGetComponent(icons[k].m_Icon, out PrefabRef iconPrefab)
                    && EntityManager.TryGetComponent(iconPrefab.m_Prefab, out ToolErrorData errorData))
                {
                    types.Add(errorData.m_Error.ToString());
                }
            }
            return types.Count > 0 ? " [" + string.Join(",", types) + "]" : "";
        }

        private static (int, int, int, int) SegmentKey(RoadSegmentDef segment)
        {
            // Indépendante du sens : un tronçon peut être émis dans un sens ou dans l'autre.
            (int, int) a = ((int)math.round(segment.Start.x * 10f), (int)math.round(segment.Start.z * 10f));
            (int, int) b = ((int)math.round(segment.End.x * 10f), (int)math.round(segment.End.z * 10f));
            if (a.Item1 > b.Item1 || (a.Item1 == b.Item1 && a.Item2 > b.Item2))
            {
                (a, b) = (b, a);
            }
            return (a.Item1, a.Item2, b.Item1, b.Item2);
        }

        /// <summary>
        /// Relie chaque entité portant Game.Tools.Error (surbrillance rouge native) au(x)
        /// tronçon(s) généré(s) de la dernière frame qu'elle recouvre, et les ajoute à
        /// _excludedSegments. Arête en erreur : un de ses points doit tomber sur la courbe du
        /// tronçon (une arête peut n'être qu'un morceau du tronçon, découpé sur un croisement).
        /// Nœud ou objet en erreur : tout tronçon qui passe par sa position. Retourne le nombre de
        /// NOUVEAUX tronçons exclus — 0 = aucune erreur attribuable à la grille générée (ex.
        /// erreur sur une route existante), inutile de réessayer.
        /// </summary>
        private int TryExcludeErrorSegments()
        {
            if (_lastCreatedCurves.Count == 0)
            {
                return 0;
            }
            var probes = new List<float3>();
            using (NativeArray<Entity> errorEntities = m_ErrorQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity e in errorEntities)
                {
                    if (EntityManager.TryGetComponent(e, out Curve curve))
                    {
                        probes.Add(MathUtils.Position(curve.m_Bezier, 0.5f));
                        probes.Add(MathUtils.Position(curve.m_Bezier, 0.25f));
                        probes.Add(MathUtils.Position(curve.m_Bezier, 0.75f));
                    }
                    else if (EntityManager.TryGetComponent(e, out Game.Net.Node node))
                    {
                        probes.Add(node.m_Position);
                    }
                    else if (EntityManager.TryGetComponent(e, out Game.Objects.Transform transform))
                    {
                        probes.Add(transform.m_Position);
                    }
                }
            }

            int added = 0;
            foreach (((int, int, int, int) key, Bezier4x3 curve) in _lastCreatedCurves)
            {
                if (_excludedSegments.Contains(key))
                {
                    continue;
                }
                foreach (float3 probe in probes)
                {
                    if (MathUtils.Distance(curve.xz, probe.xz, out float _) < ErrorMatchDistance)
                    {
                        _excludedSegments.Add(key);
                        added++;
                        break;
                    }
                }
            }
            return added;
        }


        private void SetHighlight(Entity entity, bool highlighted)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
            {
                return;
            }
            if (highlighted)
            {
                if (!EntityManager.HasComponent<Highlighted>(entity))
                {
                    EntityManager.AddComponent<Highlighted>(entity);
                }
            }
            else if (EntityManager.HasComponent<Highlighted>(entity))
            {
                EntityManager.RemoveComponent<Highlighted>(entity);
            }
            if (!EntityManager.HasComponent<BatchesUpdated>(entity))
            {
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
        }

        // ------------------------------------------------------------------
        // Prefab de route
        // ------------------------------------------------------------------

        /// <summary>Vrai si aucun réseau n'a été choisi explicitement (suivre l'outil route natif).</summary>
        public bool RoadPrefabIsAuto => string.IsNullOrEmpty(_settings.RoadPrefabName);

        /// <summary>
        /// Fixe le réseau utilisé pour la grille (choisi dans le sélecteur du panneau)
        /// et le mémorise dans les settings. null = revenir au mode auto (suivre le NetTool).
        /// </summary>
        public void SetRoadPrefab(PrefabBase prefab)
        {
            _overridePrefab = prefab;
            _overrideResolved = true;
            _settings.RoadPrefabName = prefab != null ? $"{prefab.GetType().Name}:{prefab.name}" : string.Empty;
            _settings.ApplyAndSave();
            MarkPreviewDirty();
        }

        /// <summary>
        /// Prefab utilisé pour la grille, par priorité : le réseau choisi explicitement
        /// dans le sélecteur (mémorisé entre les sessions), sinon celui de l'outil route
        /// natif s'il s'agit d'une route, sinon la petite route deux voies par défaut.
        /// </summary>
        private PrefabBase GetRoadPrefab()
        {
            if (!_overrideResolved)
            {
                _overrideResolved = true;
                _overridePrefab = ResolveSavedPrefab(_settings.RoadPrefabName);
            }
            if (_overridePrefab != null)
            {
                return _overridePrefab;
            }

            PrefabBase current = m_NetToolSystem.GetPrefab();
            if (current is RoadPrefab)
            {
                return current;
            }

            if (!_fallbackSearched)
            {
                _fallbackSearched = true;
                if (!m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(RoadPrefab), "Small Road"), out _fallbackPrefab))
                {
                    Mod.Log.Warn("Prefab par défaut \"Small Road\" introuvable ; sélectionne une route dans l'outil route natif avant de valider une grille.");
                }
            }
            return _fallbackPrefab;
        }

        /// <summary>
        /// Résout le réseau mémorisé "TypePrefab:Nom" (ex. "RoadPrefab:Small Road").
        /// Retourne null (mode auto) si la chaîne est vide ou le prefab introuvable
        /// (ex. mod d'assets désinstallé) — jamais d'erreur bloquante.
        /// </summary>
        private PrefabBase ResolveSavedPrefab(string saved)
        {
            if (string.IsNullOrEmpty(saved))
            {
                return null;
            }
            int colon = saved.IndexOf(':');
            if (colon <= 0 || colon >= saved.Length - 1)
            {
                return null;
            }
            string typeName = saved.Substring(0, colon);
            string prefabName = saved.Substring(colon + 1);
            if (m_PrefabSystem.TryGetPrefab(new PrefabID(typeName, prefabName), out PrefabBase prefab))
            {
                return prefab;
            }
            Mod.Log.Info($"Réseau mémorisé introuvable ({saved}), retour au mode auto.");
            return null;
        }

        /// <summary>Vrai si aucun réseau secondaire n'a été choisi explicitement (suit alors GetRoadPrefab).</summary>
        public bool SecondaryRoadPrefabIsAuto => string.IsNullOrEmpty(_settings.SecondaryRoadPrefabName);

        /// <summary>Fixe le réseau "local" (impasses — voir RoadSegmentDef.IsCulDeSacEnd). null = mode auto.</summary>
        public void SetSecondaryRoadPrefab(PrefabBase prefab)
        {
            _secondaryOverridePrefab = prefab;
            _secondaryOverrideResolved = true;
            _settings.SecondaryRoadPrefabName = prefab != null ? $"{prefab.GetType().Name}:{prefab.name}" : string.Empty;
            _settings.ApplyAndSave();
            MarkPreviewDirty();
        }

        /// <summary>
        /// Prefab utilisé pour les tronçons locaux : le réseau secondaire choisi explicitement,
        /// sinon GetRoadPrefab() (le réseau principal) — pas de recherche de repli indépendante
        /// (NetTool/"Small Road") : tant que rien n'est choisi explicitement pour le secondaire,
        /// les deux réseaux restent synchronisés, comportement identique à avant l'existence du
        /// second réseau.
        ///
        /// EXCEPTION en mode SuperblockMode : ce même emplacement sert de réseau PIÉTON (voir
        /// RoadSegmentDef.IsPedestrian) — retour utilisateur en jeu, capture d'écran : sans
        /// prefab choisi explicitement, "auto" retombe sur GetRoadPrefab() (le MÊME prefab que
        /// la coletora), rendant la grille intérieure visuellement indissociable d'une simple
        /// grille classique uniforme — pas du tout le rendu attendu (rues piétonnes distinctes
        /// autour d'îlots à bâtir). Tant que rien n'est choisi explicitement, on cherche d'abord
        /// un vrai chemin piéton ("Pedestrian Path", même mécanisme de repli que "Small Road"
        /// pour GetRoadPrefab) avant de retomber sur GetRoadPrefab() si introuvable.
        /// </summary>
        private PrefabBase GetSecondaryRoadPrefab()
        {
            if (!_secondaryOverrideResolved)
            {
                _secondaryOverrideResolved = true;
                _secondaryOverridePrefab = ResolveSavedPrefab(_settings.SecondaryRoadPrefabName);
            }
            if (_secondaryOverridePrefab != null)
            {
                return _secondaryOverridePrefab;
            }
            if (_settings.LoopMode && _settings.SuperblockMode)
            {
                if (!_pedestrianFallbackSearched)
                {
                    _pedestrianFallbackSearched = true;
                    if (!m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(RoadPrefab), "Pedestrian Path"), out _pedestrianFallbackPrefab))
                    {
                        Mod.Log.Warn("Prefab par défaut \"Pedestrian Path\" introuvable ; choisis un réseau piéton dans l'onglet Redes > Pedestrian.");
                    }
                }
                if (_pedestrianFallbackPrefab != null)
                {
                    return _pedestrianFallbackPrefab;
                }
            }
            return GetRoadPrefab();
        }

        /// <summary>Vrai si aucun réseau avenue n'a été choisi explicitement (suit alors GetRoadPrefab).</summary>
        public bool AvenueRoadPrefabIsAuto => string.IsNullOrEmpty(_settings.AvenueRoadPrefabName);

        /// <summary>Fixe le réseau "avenue" (voir RoadSegmentDef.IsAvenue). null = mode auto.</summary>
        public void SetAvenueRoadPrefab(PrefabBase prefab)
        {
            _avenueOverridePrefab = prefab;
            _avenueOverrideResolved = true;
            _settings.AvenueRoadPrefabName = prefab != null ? $"{prefab.GetType().Name}:{prefab.name}" : string.Empty;
            _settings.ApplyAndSave();
            MarkPreviewDirty();
        }

        /// <summary>
        /// Prefab utilisé pour les tronçons avenue (RoadSegmentDef.IsAvenue) : le réseau avenue
        /// choisi explicitement, sinon GetRoadPrefab() (le réseau principal) — même principe
        /// que GetSecondaryRoadPrefab, indépendant de lui (une avenue n'est ni une impasse ni
        /// un rayon).
        /// </summary>
        private PrefabBase GetAvenueRoadPrefab()
        {
            if (!_avenueOverrideResolved)
            {
                _avenueOverrideResolved = true;
                _avenueOverridePrefab = ResolveSavedPrefab(_settings.AvenueRoadPrefabName);
            }
            return _avenueOverridePrefab != null ? _avenueOverridePrefab : GetRoadPrefab();
        }

        /// <summary>Vrai si aucun réseau de liaison piétonne n'a été choisi ("Pedestrian Path" du jeu).</summary>
        public bool PathRoadPrefabIsAuto => string.IsNullOrEmpty(_settings.PathPrefabName);

        /// <summary>Fixe le réseau des liaisons piétonnes (chemin, piste cyclable…). null = "Pavement Path".</summary>
        public void SetPathRoadPrefab(PrefabBase prefab)
        {
            _pathOverridePrefab = prefab;
            _pathOverrideResolved = true;
            _settings.PathPrefabName = prefab != null ? $"{prefab.GetType().Name}:{prefab.name}" : string.Empty;
            _settings.ApplyAndSave();
            MarkPreviewDirty();
        }

        /// <summary>Réseau des liaisons piétonnes : choisi explicitement, sinon le chemin piéton du jeu, sinon les rues.</summary>
        // Repli propre aux liaisons (distinct de celui du super-quarteirão, qui garde ses rues).
        private bool _pathFallbackSearched;
        private PrefabBase _pathFallbackPrefab;

        private PrefabBase GetPathRoadPrefab()
        {
            if (!_pathOverrideResolved)
            {
                _pathOverrideResolved = true;
                _pathOverridePrefab = ResolveSavedPrefab(_settings.PathPrefabName);
            }
            if (_pathOverridePrefab != null)
            {
                return _pathOverridePrefab;
            }
            if (!_pathFallbackSearched)
            {
                _pathFallbackSearched = true;
                // Chemin piéton du jeu ("Caminho pavimentado", PathwayPrefab) — retour utilisateur :
                // l'ancien nom ("Pedestrian Path", RoadPrefab) n'existe pas, les liaisons devenaient des rues.
                if (!m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(PathwayPrefab), "Pavement Path"), out _pathFallbackPrefab))
                {
                    _pathFallbackPrefab = null;
                }
            }
            return _pathFallbackPrefab ?? GetRoadPrefab();
        }

        /// <summary>Vrai si aucun réseau de rotonde n'a été choisi (suit alors GetRoadPrefab).</summary>
        public bool RoundaboutRoadPrefabIsAuto => string.IsNullOrEmpty(_settings.RoundaboutRoadPrefabName);

        /// <summary>Fixe le réseau de la rotonde du motif Radial (RoadSegmentDef.IsRoundabout). null = mode auto.</summary>
        public void SetRoundaboutRoadPrefab(PrefabBase prefab)
        {
            _roundaboutOverridePrefab = prefab;
            _roundaboutOverrideResolved = true;
            _settings.RoundaboutRoadPrefabName = prefab != null ? $"{prefab.GetType().Name}:{prefab.name}" : string.Empty;
            _settings.ApplyAndSave();
            MarkPreviewDirty();
        }

        /// <summary>Réseau de la rotonde : choisi explicitement, sinon celui des rues (GetRoadPrefab).</summary>
        private PrefabBase GetRoundaboutRoadPrefab()
        {
            if (!_roundaboutOverrideResolved)
            {
                _roundaboutOverrideResolved = true;
                _roundaboutOverridePrefab = ResolveSavedPrefab(_settings.RoundaboutRoadPrefabName);
            }
            return _roundaboutOverridePrefab != null ? _roundaboutOverridePrefab : GetRoadPrefab();
        }

        /// <summary>Résumé de la grille prévisualisée, affiché au-dessus de Générer : tronçons, longueur (m), coût estimé.</summary>
        public int SummarySegments { get; private set; }
        public float SummaryLength { get; private set; }
        public long SummaryCost { get; private set; }

        /// <summary>
        /// Résumé de `segments` : longueur réelle (courbe du jeu) et coût de construction estimé à partir du
        /// coût par défaut de chaque réseau (PlaceableNetData, par case de 8 m) — le jeu ajoute les
        /// ponts/tunnels et les carrefours, d'où "≈" dans le panneau.
        /// </summary>
        private void UpdateSummary(List<RoadSegmentDef> segments)
        {
            bool singleNetwork = _settings.ContourMode && !_settings.LoopMode;
            float length = 0f;
            double cost = 0;
            foreach (RoadSegmentDef segment in segments)
            {
                float segmentLength = segment.IsArc
                    ? MathUtils.Length(NetUtils.FitCurve(segment.Start, segment.StartTangent, segment.EndTangent, segment.End))
                    : math.distance(segment.Start, segment.End);
                length += segmentLength;
                PrefabBase prefab = segment.IsRoundabout ? GetRoundaboutRoadPrefab()
                    : segment.IsPedestrian && !(_settings.LoopMode && _settings.SuperblockMode) ? GetPathRoadPrefab()
                    : segment.IsAvenue && !singleNetwork ? GetAvenueRoadPrefab()
                    : ((segment.IsCulDeSacEnd && !segment.KeepNetwork) || segment.IsLocal || segment.IsPedestrian) && !_settings.LoopMode ? GetSecondaryRoadPrefab()
                    : segment.IsPedestrian ? GetSecondaryRoadPrefab()
                    : GetRoadPrefab();
                if (prefab != null && EntityManager.TryGetComponent(m_PrefabSystem.GetEntity(prefab), out PlaceableNetData placeable))
                {
                    cost += segmentLength / 8f * placeable.m_DefaultConstructionCost;
                }
            }
            SummarySegments = segments.Count;
            SummaryLength = length;
            SummaryCost = (long)math.round(cost);
        }

        /// <summary>
        /// Calcule SEULEMENT si le périmètre actuel produit une grille non vide — aucune entité
        /// ECS créée, juste GridGenerator (pure C#, voir GridGeneratorPerformanceTests pour la
        /// garantie de rapidité même sur une grande zone). Utilisé par ShowSketchOnly (mode Loop
        /// hors confirmation) pour activer/désactiver Générer sans payer le coût de
        /// CreateGridDefinitions — voir sa doc pour pourquoi ce dernier ne peut pas tourner en
        /// continu dans ce cas.
        /// </summary>
        private int _validityVersion = -1;
        private int _validitySettingsVersion = -1;
        private bool _validityResult;
        private object _sketchKey;
        private List<RoadSegmentDef> _sketchSegments;
        private RoundaboutInfo _sketchRoundabout;

        private object _seedKey;
        private List<RoadSegmentDef> _seedSegments;
        private RoundaboutInfo _seedRoundabout;

        /// <summary>Dernière grille calculée en fond pendant un drag (même clé que GenerateSketch) : évite de la refaire au relâchement.</summary>
        public void SeedSketch(object key, List<RoadSegmentDef> segments, RoundaboutInfo roundabout)
        {
            _seedKey = key;
            _seedSegments = new List<RoadSegmentDef>(segments);
            _seedRoundabout = roundabout;
        }

        /// <summary>Change à chaque nouvelle grille de croquis (voir GenerateSketch).</summary>
        public int SketchVersion { get; private set; }

        /// <summary>
        /// Grille du croquis, recalculée seulement quand le périmètre ou un réglage change : le croquis
        /// (GridRoadOverlaySystem) et la validité de Générer la redemandaient à chaque frame, soit
        /// plusieurs centaines de ms par frame sur une grande zone peinte. Copie renvoyée (les
        /// appelants retirent ou ajoutent des tronçons).
        /// </summary>
        public List<RoadSegmentDef> GenerateSketch(List<float3> positions, GridParameters parameters, out RoundaboutInfo roundabout)
        {
            GridParameters keyParameters = parameters;
            keyParameters.HeightAt = null;
            float3 sum = float3.zero;
            foreach (float3 p in positions) sum += p;
            object key = (positions.Count, sum, _settings.LoopMode, keyParameters);
            if ((_sketchSegments == null || !key.Equals(_sketchKey)) && _seedSegments != null && key.Equals(_seedKey))
            {
                // Grille déjà calculée en fond pendant le drag du slider : reprise telle quelle.
                _sketchSegments = _seedSegments;
                _sketchRoundabout = _seedRoundabout;
                _sketchKey = key;
                _seedSegments = null;
                SketchVersion++;
            }
            if (_sketchSegments == null || !key.Equals(_sketchKey))
            {
                parameters.HeightAt = MakeTerrainSampler();
                _sketchRoundabout = default;
                try
                {
                    _sketchSegments = _settings.LoopMode
                        ? GridGenerator.GenerateLoopGrid(positions, parameters)
                        : GridGenerator.GenerateGrid(positions, parameters, out _, out _sketchRoundabout);
                }
                catch (Exception e)
                {
                    // Grille impossible pour ces réglages : grille vide mémorisée (pas de nouvel essai,
                    // ni d'erreur, à chaque frame — log en jeu : erreur critique répétée).
                    Mod.Log.Warn($"Grille impossible pour ce périmètre et ces réglages : {e}");
                    _sketchSegments = new List<RoadSegmentDef>();
                    _sketchRoundabout = default;
                }
                _sketchKey = key;
                SketchVersion++;
            }
            roundabout = _sketchRoundabout;
            return new List<RoadSegmentDef>(_sketchSegments);
        }

        private bool ComputePreviewValidityCheap()
        {
            try
            {
                List<float3> perimeterPositions = BuildCurveAwarePerimeterPositions();
                GridParameters parameters = _settings.ToGridParameters();
                List<RoadSegmentDef> segments = GenerateSketch(perimeterPositions, parameters, out _);
                // Même grille qu'à la frame précédente : même réponse (retour utilisateur : jeu lent
                // après avoir peint une grande zone — génération et route de périmètre refaites à
                // chaque frame).
                // (réglage sans effet sur la grille, ex. type de route : résumé à refaire)
                if (_validityVersion == SketchVersion && _validitySettingsVersion == _settingsVersion)
                {
                    return _validityResult;
                }
                _validityVersion = SketchVersion;
                _validitySettingsVersion = _settingsVersion;
                _validityResult = false;
                if (_settings.FreeAreaMode && _freeRing.Count >= 3 && segments.Count > 0)
                {
                    segments.AddRange(FreeAreaPerimeter.PerimeterRoad(_freeRing, segments));
                    RemoveObstacleSegments(segments);
                }
                UpdateSummary(segments);
                _validityResult = segments.Count > 0;
                return _validityResult;
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Aperçu (croquis) impossible : {e.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Création des définitions réseau (aperçu fantôme + pose réelle)
        // ------------------------------------------------------------------

        /// <summary>
        /// Compare deux listes de positions élément par élément — float3 n'a pas d'Equals utile
        /// par défaut (comparaison de référence héritée d'object). Utilisé par OnUpdate pour
        /// détecter un VRAI changement de sélection (voir _lastGesturePositions/sa doc) : le
        /// nombre de nœuds seul ne suffit pas (un nœud retiré puis un autre ajouté ailleurs au
        /// clic suivant laisse le compte inchangé, mais change bien la grille attendue).
        /// </summary>
        private static bool PositionsEqual(List<float3> a, List<float3> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (!math.all(a[i] == b[i]))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Génère la grille (logique pure de Core) et crée pour chaque segment une entité de
        /// définition CreationDefinition + NetCourse, comme NetToolSystem lors d'un tracé
        /// manuel. Le jeu en dérive les Temp (aperçu), découpe les croisements
        /// (CourseSplitSystem) et crée les intersections avec le périmètre.
        /// Retourne le nombre de segments créés (0 = polygone dégénéré ou trop petit).
        /// </summary>
        private int CreateGridDefinitions()
        {
            PrefabBase roadPrefab = GetRoadPrefab();
            if (roadPrefab == null)
            {
                return 0;
            }

            Entity prefabEntity = m_PrefabSystem.GetEntity(roadPrefab);
            float roadWidth = EntityManager.TryGetComponent(prefabEntity, out NetGeometryData geometryData)
                ? geometryData.m_DefaultWidth
                : 0f;

            // Réseau "local" (impasses) : voir GetSecondaryRoadPrefab — identique au
            // principal tant qu'aucun n'est choisi explicitement, donc aucun changement visible
            // par défaut. Le cercle de retournement (roadWidth ci-dessous) se dimensionne sur CE
            // réseau, puisque c'est lui qui pose effectivement les impasses.
            PrefabBase secondaryRoadPrefab = GetSecondaryRoadPrefab();
            Entity secondaryPrefabEntity = secondaryRoadPrefab != null ? m_PrefabSystem.GetEntity(secondaryRoadPrefab) : prefabEntity;
            float secondaryRoadWidth = EntityManager.TryGetComponent(secondaryPrefabEntity, out NetGeometryData secondaryGeometryData)
                ? secondaryGeometryData.m_DefaultWidth
                : roadWidth;

            // Troisième réseau (RoadSegmentDef.IsAvenue) : voir GetAvenueRoadPrefab. Aucun
            // dimensionnement particulier n'en dépend (contrairement au secondaire, l'avenue
            // ne pose pas de cercle de retournement).
            PrefabBase avenueRoadPrefab = GetAvenueRoadPrefab();
            Entity avenuePrefabEntity = avenueRoadPrefab != null ? m_PrefabSystem.GetEntity(avenueRoadPrefab) : prefabEntity;
            PrefabBase roundaboutRoadPrefab = GetRoundaboutRoadPrefab();
            Entity roundaboutPrefabEntity = roundaboutRoadPrefab != null ? m_PrefabSystem.GetEntity(roundaboutRoadPrefab) : prefabEntity;
            // Liaisons piétonnes entre impasses (hors super-quarteirão, qui a son propre réseau piéton).
            bool superblock = _settings.LoopMode && _settings.SuperblockMode;
            PrefabBase pathPrefab = superblock ? null : GetPathRoadPrefab();
            Entity pathPrefabEntity = pathPrefab != null ? m_PrefabSystem.GetEntity(pathPrefab) : secondaryPrefabEntity;
            // Motif Relevo : un seul type de route (retour utilisateur : "quero apenas um tipo de
            // estrada nesse modo") — les montées (IsAvenue côté Core) prennent le réseau des rues.
            bool singleNetwork = _settings.ContourMode && !_settings.LoopMode;
            // Rotonde émise dans le sens trigonométrique (circulation à droite) : retournée si la
            // ville roule à gauche, sinon une route à sens unique y tournerait à contresens.
            bool leftHandTraffic = m_CityConfigurationSystem != null && m_CityConfigurationSystem.leftHandTraffic;

            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();

            List<RoadSegmentDef> segments;
            RoundaboutInfo roundabout = default;
            try
            {
                List<float3> perimeterPositions = BuildCurveAwarePerimeterPositions();
                GridParameters parameters = _settings.ToGridParameters();
                parameters.HeightAt = MakeTerrainSampler();
                if (_settings.LoopMode)
                {
                    // Collectrices éparses + laço interne par super-îlot — voir
                    // GridGenerator.GenerateLoopGrid. Pas de rotonde/comptage d'omission ici
                    // (uniquement pertinents pour la grille classique).
                    segments = GridGenerator.GenerateLoopGrid(perimeterPositions, parameters);
                }
                else
                {
                    segments = GridGenerator.GenerateGrid(perimeterPositions, parameters, out int omittedNodeCount, out roundabout);
                    if (omittedNodeCount > 0 && !_omittedNodesLogged)
                    {
                        _omittedNodesLogged = true;
                        Mod.Log.Info($"{omittedNodeCount} nœud(s) trop proche(s) (< {GridGenerator.MinNodeDistance} m) fusionné(s) avec un nœud voisin ou omis (culs-de-sac).");
                    }
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Génération de grille impossible : {e.Message}");
                return 0;
            }

            if (segments.Count == 0)
            {
                return 0;
            }

            List<PerimeterEdge> perimeter = BuildPerimeterEdges();
            // BuildPerimeterEdges() ne connaît QUE le contour cliqué (nœuds sélectionnés
            // consécutifs) : si une route existante traverse l'INTÉRIEUR du bloc choisi (pas
            // seulement son bord), elle n'apparaît jamais dans "perimeter" et MakeCoursePos
            // n'a alors aucun moyen de l'utiliser pour raccorder/splitter — les extrémités de
            // segment qui devraient s'y accrocher retombent en règle 3 (point libre) et la
            // grille générée entre en collision physique avec cette route bien réelle
            // ("Objetos sobrepostos"). Confirmé par captures d'écran : les croisements tombant
            // près d'un nœud déjà existant de la route intérieure s'en sortaient par chance
            // (snapping propre du jeu), ceux tombant en milieu de segment échouaient toujours.
            // FindInteriorExistingEdges comble ce trou en cherchant, via l'arbre spatial du
            // réseau, les arêtes réelles qui passent par l'intérieur du polygone sélectionné.
            List<PerimeterEdge> interiorEdges = FindInteriorExistingEdges(perimeter);
            perimeter.AddRange(interiorEdges);

            // Suite directe du correctif ci-dessus : FindInteriorExistingEdges ne résout QUE le
            // cas où une extrémité de segment généré tombe assez près d'une route intérieure
            // pour s'y raccorder (règle 2 de MakeCoursePos). Mais une collectrice/laço qui n'a
            // jamais eu de sommet prévu à cet endroit passe souvent tout DROIT par-dessus la
            // route existante, en PLEIN MILIEU du segment — rien à "snapper" puisqu'aucune
            // extrémité n'est concernée. Le résultat est le même bug "Objetos sobrepostos",
            // mais au milieu d'une ligne au lieu d'un bout. SplitSegmentsCrossingInteriorEdges
            // scinde ces segments droits au(x) point(s) de croisement AVANT toute création ECS,
            // pour que chaque moitié se termine pile sur la route réelle (et se raccorde
            // proprement via MakeCoursePos, comme n'importe quelle autre extrémité).
            //
            // Passe "perimeter" (BOUNDARY + interior, déjà fusionnés ci-dessus), PAS seulement
            // interiorEdges : diagnostic en jeu (voir le "[Diag croisements] rejeté (déjà
            // périmètre)" dans FindInteriorExistingEdges) a confirmé qu'une route réelle traversant
            // le milieu de la sélection peut très bien apparaître comme arête de PÉRIMÈTRE (deux
            // nœuds cliqués consécutifs directement reliés par cette route, ex. détection
            // automatique au double-clic qui longe cette route en traçant le contour) sans jamais
            // être classée "intérieure" — auquel cas elle n'était testée que pour le RACCORD
            // d'extrémité (MakeCoursePos, règle 2), jamais pour un croisement en PLEIN MILIEU d'un
            // segment généré, laissant exactement le même bug "Objetos sobrepostos" pour cette
            // arête-là. Tester TOUTE arête connue (périmètre ou intérieure) couvre les deux cas.
            var trueInteriorEntities = new HashSet<Entity>();
            foreach (PerimeterEdge ie in interiorEdges)
            {
                trueInteriorEntities.Add(ie.m_Entity);
            }
            segments = SplitSegmentsCrossingInteriorEdges(segments, perimeter, trueInteriorEntities);

            // Zone libre : pas de routes existantes autour — la route de périmètre est posée ici,
            // avec un nœud à chaque rue qui la rejoint (même position exacte) et à chaque route
            // existante qui traverse le contour (MakeCoursePos s'y raccorde en coupant cette route).
            if (_settings.FreeAreaMode && _freeRing.Count >= 3)
            {
                segments.AddRange(FreeAreaPerimeter.PerimeterRoad(_freeRing, segments, RingCrossings(interiorEdges)));
            }
            RemoveObstacleSegments(segments);

            UpdateSummary(segments);
            EntityCommandBuffer commandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            Unity.Mathematics.Random random = RandomSeed.Next().GetRandom(0);
            int created = 0;
            _lastCreatedCurves.Clear();

            foreach (RoadSegmentDef source in segments)
            {
                RoadSegmentDef segment = source;
                if (singleNetwork)
                {
                    segment.IsAvenue = false;
                }
                if (segment.IsRoundabout && leftHandTraffic)
                {
                    (segment.Start, segment.End) = (segment.End, segment.Start);
                    (segment.StartTangent, segment.EndTangent) = (-segment.EndTangent, -segment.StartTangent);
                }
                // Tronçon retiré par la résolution automatique des collisions (voir
                // _excludedSegments/TryExcludeErrorSegments).
                (int, int, int, int) segmentKey = SegmentKey(segment);
                if (_excludedSegments.Count > 0 && _excludedSegments.Contains(segmentKey))
                {
                    continue;
                }

                CoursePos start = MakeCoursePos(segment.Start, ref heightData, perimeter);
                CoursePos end = MakeCoursePos(segment.End, ref heightData, perimeter);

                // Après snapping les deux extrémités peuvent s'être rejointes : segment inutile.
                if (math.distance(start.m_Position.xz, end.m_Position.xz) < GridGenerator.MinSegmentLength)
                {
                    continue;
                }

                NetCourse course = default;
                // Facette courbe (mode Loop, voir RoadSegmentDef.IsArc/GridGenerator.
                // EmitLoopBlock) : vraie courbe ajustée sur les tangentes calculées côté Core,
                // pas une corde droite. Tout le reste (grille classique, culs-de-sac, avenue)
                // garde la ligne droite habituelle.
                course.m_Curve = segment.IsArc
                    ? NetUtils.FitCurve(start.m_Position, segment.StartTangent, segment.EndTangent, end.m_Position)
                    : NetUtils.StraightCurve(start.m_Position, end.m_Position);
                course.m_Length = MathUtils.Length(course.m_Curve);
                course.m_FixedIndex = -1;

                start.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(course.m_Curve));
                end.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(course.m_Curve));
                start.m_CourseDelta = 0f;
                end.m_CourseDelta = 1f;
                start.m_Flags |= CoursePosFlags.IsFirst | CoursePosFlags.IsLeft | CoursePosFlags.IsRight;
                end.m_Flags |= CoursePosFlags.IsLast | CoursePosFlags.IsLeft | CoursePosFlags.IsRight;
                course.m_StartPosition = start;
                course.m_EndPosition = end;

                // Tronçon "local" (impasse en mode CulDeSacMode) : réseau secondaire — voir
                // GetSecondaryRoadPrefab. Tronçon avenue (voir GetAvenueRoadPrefab) : troisième
                // réseau indépendant, jamais local (EmitLine exclut le cul-de-sac sur une ligne
                // avenue). Identique au principal tant qu'aucun n'est choisi explicitement.
                //
                // En mode Loop (GridGenerator.GenerateLoopGrid) : la collectrice (IsAvenue)
                // garde l'avenue, comme avant. Laço ET beco sem saída partagent le MÊME prefab
                // (celui du principal), jamais deux réseaux séparés — sans cette exception,
                // l'impasse utilisait le réseau secondaire, resté un réseau à part entière mais
                // uniquement en Grille classique où cette distinction garde un sens. En mode
                // super-quarteirão (SuperblockMode, voir RoadSegmentDef.IsPedestrian), le laço
                // devient piéton et réutilise CE MÊME emplacement secondaire — inutilisé par
                // ailleurs en Loop (l'Arterial qui l'occupait a été retiré) — plutôt qu'un
                // quatrième emplacement dédié.
                bool isLocalSegment = ((segment.IsCulDeSacEnd && !segment.KeepNetwork) || segment.IsLocal) && !_settings.LoopMode;
                Entity segmentPrefabEntity;
                if (segment.IsRoundabout)
                {
                    segmentPrefabEntity = roundaboutPrefabEntity;
                }
                else if (segment.IsPedestrian && !superblock)
                {
                    segmentPrefabEntity = pathPrefabEntity;
                }
                else if (segment.IsAvenue)
                {
                    segmentPrefabEntity = avenuePrefabEntity;
                }
                else if (isLocalSegment || segment.IsPedestrian)
                {
                    segmentPrefabEntity = secondaryPrefabEntity;
                }
                else
                {
                    segmentPrefabEntity = prefabEntity;
                }
                Entity definition = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(definition, new CreationDefinition
                {
                    m_Prefab = segmentPrefabEntity,
                    m_RandomSeed = random.NextInt()
                });
                commandBuffer.AddComponent(definition, default(Updated));
                commandBuffer.AddComponent(definition, course);

                // Melhoramentos automáticos (tous les motifs, Grelha comprise depuis que la
                // section Redes y est proposée — retour utilisateur : "adiciona a opção de
                // networks no modo grid") :
                // Game.Net.Upgraded est lu par CourseSplitSystem sur CETTE MÊME entité
                // (CreationDefinition/NetCourse), puis reporté tel quel sur l'Edge réelle créée —
                // CompositionSelectSystem s'occupe ensuite de résoudre la composition visuelle
                // (arbres/relva/ciclovia/passeio), y compris de l'ignorer silencieusement si le
                // prefab choisi n'a pas la pièce correspondante (jamais d'erreur). Le beco sem
                // saída reçoit maintenant lui aussi les melhoramentos du principal (même
                // réseau désormais, voir plus haut) : plus d'exclusion IsCulDeSacEnd ici.
                {
                    // Réseau piéton (SuperblockMode) : aucun melhoramento (trees/passeio/ciclovia
                    // du réseau principal n'ont pas de sens sur un chemin piéton dédié). En
                    // Grelha, les impasses (réseau secondaire) reçoivent ceux de la rue.
                    CompositionFlags upgradeFlags = segment.IsAvenue ? BuildAvenueUpgradeFlags()
                        : segment.IsPedestrian ? default
                        : BuildPrincipalUpgradeFlags();
                    if (upgradeFlags != default)
                    {
                        commandBuffer.AddComponent(definition, new Upgraded { m_Flags = upgradeFlags });
                    }
                }

                created++;
                _lastCreatedCurves.Add((segmentKey, course.m_Curve));

                // Cercle de retournement : posé comme un objet libre à la position/rotation
                // déjà calculées pour ce même bout de segment, dans le même lot de
                // définitions que la route — pas besoin d'attendre que le nœud réel existe
                // (contrairement à l'ancienne tentative avec Game.Net.Roundabout, ce n'est
                // pas un composant réseau posé après coup, mais un objet indépendant).
                if (segment.IsCulDeSacEnd
                    && TryResolveCulDeSacCapPrefab(secondaryRoadWidth, _settings.CulDeSacCapSize, _settings.CulDeSacCapStyle, out Entity capPrefab))
                {
                    Entity capDefinition = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent(capDefinition, new CreationDefinition
                    {
                        m_Prefab = capPrefab,
                        m_RandomSeed = random.NextInt()
                    });
                    commandBuffer.AddComponent(capDefinition, default(Updated));
                    commandBuffer.AddComponent(capDefinition, new ObjectDefinition
                    {
                        m_Position = end.m_Position,
                        m_LocalPosition = end.m_Position,
                        m_Rotation = end.m_Rotation,
                        m_LocalRotation = end.m_Rotation,
                        m_Scale = 1f,
                        m_Intensity = 1f,
                        m_Probability = 100,
                        m_PrefabSubIndex = -1,
                        m_ParentMesh = -1
                    });
                }
            }

            // Asset décoratif complet de la rotonde (voir TryResolveRoundaboutIslandPrefab) :
            // posé une seule fois, une fois tous les segments de route créés, par-dessus le
            // croisement en + normal des deux avenues — même principe que le cercle de
            // retournement d'un cul-de-sac (objet indépendant, pas de composant réseau posé
            // après coup).
            if (roundabout.HasRoundabout && TryResolveRoundaboutIslandPrefab(roundabout.Radius, out Entity islandPrefab))
            {
                // roundabout.Center vient de GridGenerator avec la hauteur MOYENNE plate du
                // périmètre (jamais reprojetée côté Core, voir son commentaire) — alors que les
                // routes de la rotonde, elles, SONT reprojetées sur le vrai terrain via
                // MakeCoursePos ci-dessus quand FollowTerrain est actif. Sans ce même
                // rajustement ici, l'îlot décoratif flotte au-dessus ou s'enfonce sous la route
                // réelle dès que le terrain n'est pas plat à cet endroit — le jeu refuse alors
                // Générer ("Ligação rodoviária necessária"), même avec Anarchy (pas un simple
                // avertissement de chevauchement qu'Anarchy supprimerait).
                float3 roundaboutCenter = roundabout.Center;
                if (_settings.FollowTerrain || _settings.ContourMode || _settings.FreeAreaMode)
                {
                    roundaboutCenter.y = TerrainUtils.SampleHeight(ref heightData, roundaboutCenter);
                }
                Entity islandDefinition = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(islandDefinition, new CreationDefinition
                {
                    m_Prefab = islandPrefab,
                    m_RandomSeed = random.NextInt()
                });
                commandBuffer.AddComponent(islandDefinition, default(Updated));
                commandBuffer.AddComponent(islandDefinition, new ObjectDefinition
                {
                    m_Position = roundaboutCenter,
                    m_LocalPosition = roundaboutCenter,
                    m_Rotation = quaternion.identity,
                    m_LocalRotation = quaternion.identity,
                    m_Scale = 1f,
                    m_Intensity = 1f,
                    m_Probability = 100,
                    m_PrefabSubIndex = -1,
                    m_ParentMesh = -1
                });
            }

            return created;
        }

        /// <summary>
        /// Melhoramentos du réseau Coletor/Avenida (voir GridRoadGeneratorSettings) : séparateur
        /// central (General, sans notion de côté) + bermas indépendantes par côté (Side). "Direita"
        /// = Right (bit "par défaut" côté jeu), "Esquerda" = Left (bit "Opposite") — voir
        /// CompositionFlags.Side, relatif au sens de tracé du tronçon.
        /// </summary>
        private CompositionFlags BuildAvenueUpgradeFlags()
        {
            var general = default(CompositionFlags.General);
            if (_settings.AvenueMiddleGrass) general |= CompositionFlags.General.PrimaryMiddleBeautification;
            if (_settings.AvenueMiddleTrees) general |= CompositionFlags.General.SecondaryMiddleBeautification;

            var left = default(CompositionFlags.Side);
            var right = default(CompositionFlags.Side);
            if (_settings.AvenueSideTreesLeft) left |= CompositionFlags.Side.SecondaryBeautification;
            if (_settings.AvenueSideTreesRight) right |= CompositionFlags.Side.SecondaryBeautification;
            if (_settings.AvenueSideGrassLeft) left |= CompositionFlags.Side.PrimaryBeautification;
            if (_settings.AvenueSideGrassRight) right |= CompositionFlags.Side.PrimaryBeautification;
            if (_settings.AvenueBikeLaneLeft) left |= CompositionFlags.Side.SecondaryLane;
            if (_settings.AvenueBikeLaneRight) right |= CompositionFlags.Side.SecondaryLane;

            return new CompositionFlags(general, left, right);
        }

        /// <summary>
        /// Melhoramentos du réseau Principal/Laço : sans séparateur central, seulement Esquerda/
        /// Direita (arbres, passeio largo, ciclovia) — voir BuildAvenueUpgradeFlags pour la
        /// convention Left/Right.
        /// </summary>
        private CompositionFlags BuildPrincipalUpgradeFlags()
        {
            var left = default(CompositionFlags.Side);
            var right = default(CompositionFlags.Side);
            if (_settings.PrincipalSideTreesLeft) left |= CompositionFlags.Side.SecondaryBeautification;
            if (_settings.PrincipalSideTreesRight) right |= CompositionFlags.Side.SecondaryBeautification;
            // Relva na berma incompatible avec le passeio largo du même côté (voir GridRoadUISystem) :
            // si une config sauvegardée a les deux, le passeio largo l'emporte.
            if (_settings.PrincipalSideGrassLeft && !_settings.PrincipalWideSidewalkLeft) left |= CompositionFlags.Side.PrimaryBeautification;
            if (_settings.PrincipalSideGrassRight && !_settings.PrincipalWideSidewalkRight) right |= CompositionFlags.Side.PrimaryBeautification;
            if (_settings.PrincipalWideSidewalkLeft) left |= CompositionFlags.Side.WideSidewalk;
            if (_settings.PrincipalWideSidewalkRight) right |= CompositionFlags.Side.WideSidewalk;
            if (_settings.PrincipalBikeLaneLeft) left |= CompositionFlags.Side.SecondaryLane;
            if (_settings.PrincipalBikeLaneRight) right |= CompositionFlags.Side.SecondaryLane;

            return new CompositionFlags(default(CompositionFlags.General), left, right);
        }

        /// <summary>
        /// Résout (avec cache) le prefab "CulDeSac&lt;Taille&gt;&lt;Style&gt;" pour la taille et
        /// le style demandés. sizeSetting == Auto : la taille est déduite de la largeur de
        /// route (Small/Medium/Large/XL selon CulDeSacCap*MaxWidth) ; sinon la taille choisie
        /// explicitement dans le panneau est utilisée telle quelle. Dans les deux cas, le style
        /// Asphalt n'a pas de variante XL (repli sur Large). Retourne false (avec un avis loggé
        /// une seule fois par nom manquant) si le prefab n'existe pas dans cette version du jeu —
        /// la grille reste posée sans cercle plutôt que d'échouer entièrement.
        /// </summary>
        private bool TryResolveCulDeSacCapPrefab(float roadWidth, CulDeSacCapSize sizeSetting, CulDeSacCapStyle style, out Entity prefabEntity)
        {
            string size = sizeSetting switch
            {
                CulDeSacCapSize.Small => "Small",
                CulDeSacCapSize.Medium => "Medium",
                CulDeSacCapSize.Large => "Large",
                CulDeSacCapSize.XL => "XL",
                _ => roadWidth < CulDeSacCapSmallMaxWidth ? "Small"
                    : roadWidth < CulDeSacCapMediumMaxWidth ? "Medium"
                    : roadWidth < CulDeSacCapLargeMaxWidth ? "Large"
                    : "XL"
            };
            string styleCode = style switch
            {
                CulDeSacCapStyle.Asphalt => "01",
                CulDeSacCapStyle.Trees => "03",
                _ => "02"
            };
            if (size == "XL" && styleCode == "01")
            {
                size = "Large"; // pas de variante "CulDeSacXL01" (asphalte pur)
            }

            string name = $"CulDeSac{size}{styleCode}";
            if (_culDeSacCapPrefabCache.TryGetValue(name, out prefabEntity))
            {
                return prefabEntity != Entity.Null;
            }

            bool found = m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(StaticObjectPrefab), name), out PrefabBase prefab);
            prefabEntity = found ? m_PrefabSystem.GetEntity(prefab) : Entity.Null;
            _culDeSacCapPrefabCache[name] = prefabEntity;
            if (!found && _culDeSacCapMissingLogged.Add(name))
            {
                Mod.Log.Warn($"Prefab de cercle de retournement introuvable : {name} (StaticObjectPrefab). Impasses posées sans cercle pour cette taille/style.");
            }
            return found;
        }

        /// <summary>
        /// Résout (avec cache) l'îlot central "&lt;Taille&gt;Roundabout01" pour le rayon réel de
        /// la rotonde générée (EmitAvenueRoundabout) — même principe que
        /// TryResolveCulDeSacCapPrefab, taille déduite du rayon plutôt que d'une largeur de
        /// route. Un seul style (01) : contrairement au cercle de retournement, aucun réglage
        /// de style n'est exposé pour l'instant. Retourne false (avec un avis loggé une seule
        /// fois par nom manquant) si le prefab n'existe pas dans cette version du jeu — la
        /// rotonde reste posée sans îlot plutôt que d'échouer entièrement.
        /// </summary>
        private bool TryResolveRoundaboutIslandPrefab(float radius, out Entity prefabEntity)
        {
            string size = radius < RoundaboutIslandSmallMaxRadius ? "Small"
                : radius < RoundaboutIslandMediumMaxRadius ? "Medium"
                : radius < RoundaboutIslandLargeMaxRadius ? "Large"
                : "XL";

            string name = $"{size}Roundabout01";
            if (_roundaboutIslandPrefabCache.TryGetValue(name, out prefabEntity))
            {
                return prefabEntity != Entity.Null;
            }

            bool found = m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(StaticObjectPrefab), name), out PrefabBase prefab);
            prefabEntity = found ? m_PrefabSystem.GetEntity(prefab) : Entity.Null;
            _roundaboutIslandPrefabCache[name] = prefabEntity;
            if (!found && _roundaboutIslandMissingLogged.Add(name))
            {
                Mod.Log.Warn($"Prefab d'îlot de rotonde introuvable : {name} (StaticObjectPrefab). Rotonde posée sans îlot pour cette taille.");
            }
            return found;
        }

        /// <summary>
        /// Construit une extrémité de course, raccordée au périmètre sélectionné :
        ///  - sur un nœud sélectionné s'il est assez proche (la grille rejoint le coin) ;
        ///  - sinon sur la route existante entre deux nœuds consécutifs (split de l'arête,
        ///    comme quand le joueur termine un tracé au milieu d'une route) ;
        ///  - sinon extrémité libre, reprojetée sur la hauteur du terrain (sauf si
        ///    FollowTerrain est désactivé : voir le point 3 ci-dessous).
        /// </summary>
        private CoursePos MakeCoursePos(float3 position, ref TerrainHeightData heightData, List<PerimeterEdge> perimeter)
        {
            CoursePos coursePos = default;
            coursePos.m_ParentMesh = -1;

            // 1) Raccord sur un nœud sélectionné.
            for (int i = 0; i < _selectedPositions.Count; i++)
            {
                if (math.distance(position.xz, _selectedPositions[i].xz) < NodeSnapDistance
                    && EntityManager.Exists(_selectedNodes[i]))
                {
                    coursePos.m_Entity = _selectedNodes[i];
                    coursePos.m_Position = _selectedPositions[i];
                    return coursePos;
                }
            }

            // 2) Raccord sur une route du périmètre (split de l'arête au point d'arrivée).
            foreach (PerimeterEdge edge in perimeter)
            {
                float distance = MathUtils.Distance(edge.m_Curve.xz, position.xz, out float t);
                if (distance >= EdgeSnapDistance)
                {
                    continue;
                }
                if (EntityManager.TryGetComponent(edge.m_Entity, out Edge edgeData))
                {
                    if (t <= 0.01f)
                    {
                        coursePos.m_Entity = edgeData.m_Start;
                        coursePos.m_SplitPosition = 0f;
                    }
                    else if (t >= 0.99f)
                    {
                        coursePos.m_Entity = edgeData.m_End;
                        coursePos.m_SplitPosition = 1f;
                    }
                    else
                    {
                        coursePos.m_Entity = edge.m_Entity;
                        coursePos.m_SplitPosition = t;
                    }
                }
                coursePos.m_Position = MathUtils.Position(edge.m_Curve, t);
                return coursePos;
            }

            // 3) Extrémité libre (ex. périmètre virtuel du mode 2 nœuds) : reprojetée sur la
            // hauteur du terrain si FollowTerrain est activé (défaut). Sinon, position.y garde
            // la hauteur moyenne du périmètre déjà calculée par GridGenerator.GenerateGrid —
            // la grille reste plate à cette altitude.
            // Motif Relevo : les rues de niveau n'ont de sens que posées sur le vrai terrain.
            if (_settings.FollowTerrain || _settings.ContourMode || _settings.FreeAreaMode)
            {
                position.y = TerrainUtils.SampleHeight(ref heightData, position);
            }
            coursePos.m_Position = position;
            return coursePos;
        }

        /// <summary>
        /// Retrouve les routes existantes reliant les nœuds sélectionnés consécutifs
        /// (dans l'ordre de clic, en refermant le polygone), pour y raccorder la grille.
        /// TryFindConnectingPath (au lieu d'une seule arête directe) : une arête PAR nœud
        /// intermédiaire réel est ajoutée, chacune étant un point de raccord valide pour
        /// MakeCoursePos — sans ça, tout nœud intermédiaire entre deux clics (courbe/rond-point
        /// découpé en plusieurs arêtes) n'offrait AUCUN point de raccord sur cette portion de la
        /// route réelle.
        /// </summary>
        private List<PerimeterEdge> BuildPerimeterEdges()
        {
            var result = new List<PerimeterEdge>();
            int count = _selectedNodes.Count;
            if (count < 2)
            {
                return result;
            }

            int pairCount = count == 2 ? 1 : count;
            for (int i = 0; i < pairCount; i++)
            {
                Entity nodeA = _selectedNodes[i];
                Entity nodeB = _selectedNodes[(i + 1) % count];
                if (!TryFindConnectingPath(nodeA, nodeB, out List<(Entity edge, bool reversed)> path))
                {
                    continue;
                }
                foreach ((Entity edgeEntity, bool _) in path)
                {
                    if (EntityManager.TryGetComponent(edgeEntity, out Curve curve))
                    {
                        result.Add(new PerimeterEdge { m_Entity = edgeEntity, m_Curve = curve.m_Bezier });
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Itérateur quadtree (arbre du réseau, voir Game.Net.SearchSystem.GetNetSearchTree)
        /// pour FindInteriorExistingEdges : ne fait qu'accumuler tout candidat dont la boîte
        /// englobante croise la zone de recherche — le filtrage précis (type d'entité,
        /// doublon avec le périmètre, intérieur réel du polygone) se fait ensuite en dehors
        /// de l'itérateur, sur la liste obtenue, car il a besoin de l'EntityManager et de la
        /// liste des nœuds sélectionnés, non disponibles ici sans complexifier l'itérateur
        /// pour un gain nul (la recherche spatiale reste un simple filtre large phase).
        /// </summary>
        private struct InteriorEdgeSearchIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public Bounds3 m_QueryBounds;
            public List<Entity> m_Candidates;

            public bool Intersect(QuadTreeBoundsXZ bounds) => MathUtils.Intersect(bounds.m_Bounds.xz, m_QueryBounds.xz);

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (MathUtils.Intersect(bounds.m_Bounds.xz, m_QueryBounds.xz))
                {
                    m_Candidates.Add(item);
                }
            }
        }

        /// <summary>
        /// Trouve les arêtes de route EXISTANTES qui traversent l'INTÉRIEUR du polygone
        /// sélectionné (pas seulement son contour, déjà couvert par BuildPerimeterEdges) —
        /// voir le commentaire sur son site d'appel (CreateGridDefinitions) pour le bug
        /// "Objetos sobrepostos" que ceci corrige. Boîte englobante des nœuds sélectionnés
        /// (élargie d'une marge) comme filtre large phase via l'arbre spatial du réseau, puis
        /// pour chaque candidat : vérifie que c'est bien une arête de route utilisable (Edge +
        /// Curve, ni Deleted ni Temp), qu'elle n'est pas déjà une arête du périmètre (dédoublonnage
        /// par Entity), et enfin que son point milieu tombe réellement à l'intérieur du polygone
        /// (la boîte englobante seule ne suffit pas : une arête peut croiser la boîte sans jamais
        /// entrer dans le polygone, ex. le long d'un bord concave).
        /// </summary>
        private List<PerimeterEdge> FindInteriorExistingEdges(List<PerimeterEdge> boundaryPerimeter)
        {
            var result = new List<PerimeterEdge>();

            // Mode "2 nœuds = rectangle" (voir BuildCurveAwarePerimeterPositions) : aucun vrai
            // polygone tracé, donc aucune notion d'"intérieur" à interroger.
            // Zone libre : le contour dessiné/peint, sinon les nœuds cliqués.
            List<float3> polygon = ActivePositions;
            if (polygon.Count < 3)
            {
                return result;
            }

            float3 min = polygon[0];
            float3 max = polygon[0];
            for (int i = 1; i < polygon.Count; i++)
            {
                min = math.min(min, polygon[i]);
                max = math.max(max, polygon[i]);
            }
            // Marge de sécurité : une arête dont la géométrie déborde légèrement de la boîte
            // stricte des nœuds cliqués (courbe, largeur de chaussée) ne doit pas être ratée
            // par le filtre large phase — le vrai test d'intérieur (PointInsideSelectedPolygon)
            // vient de toute façon éliminer les faux positifs ensuite.
            const float margin = 16f;
            var queryBounds = new Bounds3(min - margin, max + margin);

            // Lecture directe hors job : cette méthode tourne entièrement sur le thread
            // principal (boucle C# classique, pas de job Burst planifié). readOnly: true donne
            // les dépendances des jobs qui ÉCRIVENT dans l'arbre (UpdateNetSearchTreeJob) ; il
            // faut les compléter avant de toucher le NativeQuadTree directement, sans quoi le
            // système de sécurité des NativeContainer lève une exception en jeu (invisible à
            // dotnet build/test, uniquement au runtime) — même exigence que
            // SearchSystem.PreDeserialize, qui complète ses propres dépendances avant de
            // manipuler l'arbre. Comme l'accès se termine ici même (aucun job asynchrone laissé
            // en suspens), il n'y a rien à enregistrer via AddNetSearchTreeReader.
            NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree = m_NetSearchSystem.GetNetSearchTree(readOnly: true, out JobHandle deps);
            deps.Complete();

            var iterator = new InteriorEdgeSearchIterator { m_QueryBounds = queryBounds, m_Candidates = new List<Entity>() };
            netSearchTree.Iterate(ref iterator);

            // Diagnostic temporaire (voir _lastLoggedInteriorEdgeCount dans CreateGridDefinitions) :
            // compte où chaque candidat est éliminé, pour localiser l'étage exact qui rate une
            // arête réelle plutôt que de le déduire d'une capture d'écran.
            int rejectedNotEdge = 0;
            int rejectedBoundary = 0;
            int rejectedOutside = 0;

            foreach (Entity candidate in iterator.m_Candidates)
            {
                if (!EntityManager.TryGetComponent(candidate, out Edge _)
                    || !EntityManager.TryGetComponent(candidate, out Curve curve)
                    || !EntityManager.HasComponent<Road>(candidate) // ni canalisation ni ligne électrique
                    || EntityManager.HasComponent<Deleted>(candidate)
                    || EntityManager.HasComponent<Temp>(candidate))
                {
                    rejectedNotEdge++;
                    continue; // pas une arête de route utilisable (nœud, entité supprimée/temporaire...)
                }

                bool alreadyBoundary = false;
                for (int i = 0; i < boundaryPerimeter.Count; i++)
                {
                    if (boundaryPerimeter[i].m_Entity == candidate)
                    {
                        alreadyBoundary = true;
                        break;
                    }
                }
                if (alreadyBoundary)
                {
                    rejectedBoundary++;
                    if (rejectedBoundary <= 5 && iterator.m_Candidates.Count != _lastLoggedCandidateCount)
                    {
                        Mod.Log.Info($"[Diag croisements]   rejeté (déjà périmètre) entité={candidate} a={curve.m_Bezier.a} d={curve.m_Bezier.d}");
                    }
                    continue; // déjà couverte par BuildPerimeterEdges — pas de doublon
                }

                // Un seul point milieu (t=0.5) RATE les arêtes réelles qui ne font QUE clipper un
                // coin/bord de la zone choisie sans passer par son centre : les nœuds d'une route
                // du jeu sont souvent très espacés (centaines de mètres entre deux intersections),
                // donc si la zone sélectionnée est plus petite que cette arête, son milieu tombe
                // fréquemment HORS du polygone même quand une bonne partie de la courbe le
                // traverse bel et bien — confirmé par les captures d'écran où la collision
                // "Objetos sobrepostos" persistait toujours au MÊME endroit (route réelle) malgré
                // FindInteriorExistingEdges : cette arête n'était simplement jamais détectée.
                // On échantillonne donc plusieurs points le long de la courbe (même densité que
                // SplitSegmentsCrossingInteriorEdges) et on garde l'arête dès qu'UN SEUL tombe
                // à l'intérieur.
                int sampleCount = ComputeInteriorEdgeSampleCount(curve.m_Bezier);
                bool anyPointInside = false;
                for (int i = 0; i <= sampleCount; i++)
                {
                    float3 sample = MathUtils.Position(curve.m_Bezier, (float)i / sampleCount);
                    if (PointInsidePolygon(polygon, sample.xz))
                    {
                        anyPointInside = true;
                        break;
                    }
                }
                if (!anyPointInside)
                {
                    rejectedOutside++;
                    continue; // dans la boîte englobante mais hors du vrai polygone (filtre large phase seulement)
                }

                result.Add(new PerimeterEdge { m_Entity = candidate, m_Curve = curve.m_Bezier });
            }

            if (iterator.m_Candidates.Count != _lastLoggedCandidateCount)
            {
                _lastLoggedCandidateCount = iterator.m_Candidates.Count;
                Mod.Log.Info($"[Diag croisements] recherche intérieure : boîte=[{queryBounds.min} .. {queryBounds.max}], périmètre={boundaryPerimeter.Count} arête(s), {iterator.m_Candidates.Count} candidat(s) brut(s) (arbre spatial), {rejectedNotEdge} rejeté(s) (pas Edge/Curve ou Deleted/Temp), {rejectedBoundary} rejeté(s) (déjà périmètre), {rejectedOutside} rejeté(s) (hors polygone), {result.Count} retenu(s).");
            }

            return result;
        }

        /// <summary>
        /// Prétraitement de la liste de segments générés, APPELÉ UNE SEULE FOIS avant la boucle
        /// de création ECS (voir le commentaire sur son site d'appel, CreateGridDefinitions, pour
        /// le rappel du bug "Objetos sobrepostos" en PLEIN MILIEU d'un segment que ceci corrige —
        /// suite directe du correctif FindInteriorExistingEdges, qui ne couvrait que les
        /// extrémités).
        ///
        /// Ne traite QUE les segments DROITS (IsArc == false) : une facette courbe de coin (mode
        /// Loop) a une géométrie ajustée sur des tangentes précises (StartTangent/EndTangent) —
        /// la scinder correctement demanderait de recalculer ces tangentes pour les deux
        /// morceaux, un risque de géométrie cassée pour un cas de coin dont la probabilité de
        /// croiser proprement une route réelle est de toute façon faible. Ces segments ressortent
        /// inchangés.
        ///
        /// Pour chaque segment droit, chaque arête intérieure (interiorEdges, déjà calculée une
        /// seule fois par FindInteriorExistingEdges — AUCUNE nouvelle requête spatiale ici) est
        /// testée directement contre la ligne du segment avec
        /// MathUtils.Intersect(Bezier4x2 curve, Line2.Segment line, out float2 t, int iterations)
        /// — PAS une approximation en polyligne. Trouvé en décompilant le tool natif "Grid" du
        /// jeu (NetToolSystem.CreateGrid) : lui non plus n'approxime jamais une courbe existante
        /// en polyligne fixe pour détecter un croisement — que ce soit pour un point (Distance)
        /// ou pour une ligne entière (Intersect), il découpe récursivement la courbe en deux par
        /// De Casteljau (Divide), élague les moitiés dont la boîte englobante ne croise pas la
        /// ligne, et ne redescend que dans celles qui la croisent VRAIMENT — la précision ne
        /// dépend donc jamais de la longueur réelle de la courbe (contrairement à l'ancien
        /// correctif ComputeInteriorEdgeSampleCount ci-dessus, qui ne faisait que réduire l'erreur
        /// sans l'éliminer). SecondaryLaneSystem et BlockSystem, en jeu, appellent tous deux cette
        /// même surcharge avec iterations=4 (16 sous-divisions de profondeur) — repris tel quel
        /// (InteriorEdgeIntersectIterations) plutôt qu'une valeur inventée. t.x = position sur la
        /// courbe (curve), t.y = position sur la ligne (segmentLine) — ordre confirmé par Divide
        /// dans l'assembly décompilée (t.x remis à l'échelle de la moitié testée, jamais t.y).
        ///
        /// Un croisement à moins de InteriorCrossingEndpointSkipDistance d'une extrémité VRAIE
        /// (Start/End d'origine) est ignoré : c'est déjà un cas de raccord d'extrémité couvert
        /// par MakeCoursePos, pas un vrai croisement de milieu.
        ///
        /// Ce seuil est DÉLIBÉRÉMENT plus petit que EdgeSnapDistance (celui que MakeCoursePos
        /// utilise pour décider s'il raccroche), pas égal — bug confirmé en jeu (retour
        /// utilisateur : en ajustant Espaçamento arterial/coletoras, certains nœuds se
        /// connectent et d'autres pas, de façon instable) quand les deux valaient
        /// GridGenerator.MinSegmentLength (8 m) : ce filtre-ci mesure la distance du point de
        /// croisement JUSQU'À segment.Start/End (deux points), alors que MakeCoursePos mesure la
        /// distance de segment.Start/End JUSQU'À LA COURBE (point-courbe, perpendiculaire) — deux
        /// mesures différentes qui, à seuil égal, peuvent diverger de quelques centimètres près
        /// de la limite et laisser un croisement qui n'est NI coupé ici NI raccroché par
        /// MakeCoursePos (zone morte). Avec un seuil ici deux fois plus petit
        /// (EdgeSnapDistance * 0.5), tout croisement qu'on choisit d'ignorer est GARANTI d'être
        /// bien en-deçà du rayon de raccord de MakeCoursePos, donc raccroché à coup sûr ; tout le
        /// reste est explicitement coupé ici, sans zone morte possible entre les deux.
        ///
        /// Entre deux croisements retenus trop rapprochés l'un de l'autre (la pièce du milieu
        /// serait dégénérée), on garde GridGenerator.MinSegmentLength comme seuil (préoccupation
        /// différente : éviter un tronçon quasi nul, pas un raccord d'extrémité) — on garde le
        /// premier rencontré en parcourant du Start vers l'End et on ignore les suivants trop
        /// proches.
        /// </summary>
        private List<RoadSegmentDef> SplitSegmentsCrossingInteriorEdges(List<RoadSegmentDef> segments, List<PerimeterEdge> interiorEdges, HashSet<Entity> trueInteriorEntities)
        {
            if (interiorEdges.Count == 0)
            {
                return segments;
            }

            var result = new List<RoadSegmentDef>(segments.Count);
            var crossings = new List<(float t, float3 point)>();

            // Diagnostic temporaire (voir _lastLoggedInteriorEdgeCount) : distingue les
            // croisements trouvés contre une arête VRAIMENT intérieure (trueInteriorEntities,
            // sortie de FindInteriorExistingEdges) de ceux trouvés contre une arête de
            // PÉRIMÈTRE (l'anneau du contour cliqué/laço) — pour vérifier si le fait de tester
            // aussi contre le périmètre (nécessaire pour le cas confirmé en jeu où une route
            // intérieure était classée périmètre) ne produit pas, en plus, des coupures parasites
            // près de l'anneau lui-même (pièces dégénérées → "Forma inválida").
            int arcsSkipped = 0;
            int arcsSkippedButChordCrosses = 0;
            int crossingsAgainstInterior = 0;
            int crossingsAgainstBoundary = 0;
            int piecesCreated = 0;

            foreach (RoadSegmentDef segment in segments)
            {
                if (segment.IsArc)
                {
                    arcsSkipped++;
                    // Diagnostic seul (voir arcsSkippedButChordCrosses ci-dessous) : teste la
                    // CORDE (Start-End) de l'arc contre les mêmes arêtes, juste pour savoir si un
                    // arc de coin (jamais scindé, voir le commentaire de méthode) est une source
                    // plausible de la collision encore observée en jeu malgré le reste du
                    // correctif — n'affecte PAS le résultat, l'arc ressort inchangé dans tous les
                    // cas (le scinder correctement demanderait de recalculer ses tangentes).
                    var arcChord = new Line2.Segment(segment.Start.xz, segment.End.xz);
                    for (int edgeIndex = 0; edgeIndex < interiorEdges.Count; edgeIndex++)
                    {
                        if (MathUtils.Intersect(interiorEdges[edgeIndex].m_Curve.xz, arcChord, out float2 _, InteriorEdgeIntersectIterations))
                        {
                            arcsSkippedButChordCrosses++;
                            break;
                        }
                    }
                    result.Add(segment);
                    continue;
                }

                var segmentLine = new Line2.Segment(segment.Start.xz, segment.End.xz);

                crossings.Clear();
                for (int edgeIndex = 0; edgeIndex < interiorEdges.Count; edgeIndex++)
                {
                    Bezier4x2 curveXz = interiorEdges[edgeIndex].m_Curve.xz;
                    if (MathUtils.Intersect(curveXz, segmentLine, out float2 hitT, InteriorEdgeIntersectIterations))
                    {
                        crossings.Add((hitT.y, math.lerp(segment.Start, segment.End, hitT.y)));
                        if (trueInteriorEntities.Contains(interiorEdges[edgeIndex].m_Entity))
                        {
                            crossingsAgainstInterior++;
                        }
                        else
                        {
                            crossingsAgainstBoundary++;
                        }
                    }
                }

                if (crossings.Count == 0)
                {
                    result.Add(segment);
                    continue;
                }

                crossings.Sort((a, b) => a.t.CompareTo(b.t));

                var acceptedPoints = new List<float3>();
                float3 lastAccepted = segment.Start;
                foreach ((float _, float3 point) in crossings)
                {
                    if (math.distance(point.xz, segment.Start.xz) < InteriorCrossingEndpointSkipDistance
                        || math.distance(point.xz, segment.End.xz) < InteriorCrossingEndpointSkipDistance
                        || math.distance(point.xz, lastAccepted.xz) < GridGenerator.MinSegmentLength)
                    {
                        continue; // trop près d'une extrémité vraie (déjà géré par MakeCoursePos) ou d'un croisement déjà retenu (pièce dégénérée)
                    }
                    acceptedPoints.Add(point);
                    lastAccepted = point;
                }

                if (acceptedPoints.Count == 0)
                {
                    result.Add(segment);
                    continue;
                }

                float3 pieceStart = segment.Start;
                for (int i = 0; i < acceptedPoints.Count; i++)
                {
                    RoadSegmentDef piece = segment; // struct : copie IsHorizontal/IsAvenue/IsArc(faux)/tangentes(défaut)
                    piece.Start = pieceStart;
                    piece.End = acceptedPoints[i];
                    // Un croisement de milieu n'est jamais un vrai cul-de-sac : seule la
                    // DERNIÈRE pièce (dont l'End reste l'End d'origine) garde IsCulDeSacEnd.
                    // Sans ça, un cercle de retournement se retrouverait posé en PLEIN MILIEU
                    // de la ligne, sur la route existante elle-même (voir la pose du cap dans
                    // CreateGridDefinitions, qui se fie justement à ce flag).
                    piece.IsCulDeSacEnd = false;
                    result.Add(piece);
                    pieceStart = acceptedPoints[i];
                    piecesCreated++;
                }

                RoadSegmentDef lastPiece = segment;
                lastPiece.Start = pieceStart;
                lastPiece.End = segment.End;
                result.Add(lastPiece);
            }

            if (segments.Count != _lastLoggedPreSplitSegmentCount)
            {
                _lastLoggedPreSplitSegmentCount = segments.Count;
                Mod.Log.Info($"[Diag croisements] scission : {arcsSkipped} arc(s) ignoré(s) (jamais testés, dont {arcsSkippedButChordCrosses} dont la corde croise une arête connue), {crossingsAgainstInterior} croisement(s) contre arête intérieure, {crossingsAgainstBoundary} croisement(s) contre arête de périmètre, {piecesCreated} pièce(s) créée(s) par scission.");
            }

            return result;
        }

        /// <summary>
        /// Test point-dans-polygone (règle pair-impair, ray casting horizontal) sur le polygone
        /// WORLD XZ formé par les nœuds sélectionnés, dans l'ordre de clic — même algorithme que
        /// GridGenerator.PointInPolygon (repère local u/v, privé à Core et donc inutilisable
        /// ici : voir la consigne de ne pas toucher à Core), appliqué directement aux positions
        /// monde puisqu'on teste une arête RÉELLE du réseau, pas un point de la grille générée.
        /// </summary>
        /// <summary>
        /// Points où des routes existantes traversent le contour de la zone libre : nœuds imposés de
        /// la route de périmètre, pour qu'elle s'y raccorde au lieu de passer par-dessus.
        /// </summary>
        private List<float3> RingCrossings(List<PerimeterEdge> edges)
        {
            var result = new List<float3>();
            int n = _freeRing.Count;
            foreach (PerimeterEdge edge in edges)
            {
                int samples = math.max(2, (int)math.ceil(MathUtils.Length(edge.m_Curve.xz) / 4f));
                float2 previous = edge.m_Curve.a.xz;
                for (int k = 1; k <= samples; k++)
                {
                    float2 next = MathUtils.Position(edge.m_Curve, (float)k / samples).xz;
                    for (int i = 0; i < n; i++)
                    {
                        float2 a = _freeRing[i].xz, b = _freeRing[(i + 1) % n].xz;
                        if (SegmentsCross(a, b, previous, next, out float t))
                        {
                            float2 hit = math.lerp(a, b, t);
                            result.Add(new float3(hit.x, math.lerp(_freeRing[i].y, _freeRing[(i + 1) % n].y, t), hit.y));
                        }
                    }
                    previous = next;
                }
            }
            return result;
        }

        /// <summary>Intersection stricte des segments a–b et c–d ; t = position sur a–b.</summary>
        private static bool SegmentsCross(float2 a, float2 b, float2 c, float2 d, out float t)
        {
            t = 0f;
            float2 r = b - a, q = d - c;
            float den = r.x * q.y - r.y * q.x;
            if (math.abs(den) < 1e-6f) return false;
            float2 w = c - a;
            t = (w.x * q.y - w.y * q.x) / den;
            float u = (w.x * r.y - w.y * r.x) / den;
            return t > 0f && t < 1f && u >= 0f && u < 1f;
        }

        private static bool PointInsidePolygon(List<float3> polygon, float2 point)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float2 a = polygon[i].xz;
                float2 b = polygon[j].xz;
                bool crosses = (a.y > point.y) != (b.y > point.y);
                if (crosses)
                {
                    float t = (point.y - a.y) / (b.y - a.y);
                    float xCross = a.x + t * (b.x - a.x);
                    if (point.x < xCross)
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }

        /// <summary>
        /// Cherche le chemin d'arêtes existantes reliant deux nœuds consécutifs du périmètre —
        /// raccord de la grille (BuildPerimeterEdges), échantillonnage du polygone
        /// (BuildCurveAwarePerimeterPositions) et rendu de l'aperçu (TryGetPerimeterSegmentCurve,
        /// lu par GridRoadOverlaySystem). Capable de suivre PLUSIEURS arêtes consécutives entre
        /// nodeA et nodeB au lieu d'exiger une seule arête DIRECTE — une route réelle longue/courbe
        /// (ex. "Rua da Ponte") est presque toujours composée de plusieurs arêtes séparées par des
        /// nœuds intermédiaires (simples points de forme, pas de vraies intersections), que
        /// TryFindConnectingEdge ignorait totalement : tout ce qui tombait entre deux nœuds
        /// cliqués non reliés par UNE SEULE arête retombait sur une CORDE DROITE
        /// (BuildCurveAwarePerimeterPositions) et sur AUCUN point de raccord pour MakeCoursePos
        /// (BuildPerimeterEdges) — la grille générée ignorait alors la vraie forme de la courbe à
        /// cet endroit ET n'avait rien à quoi se raccrocher, produisant de petits segments de laço
        /// posés en plein sur la chaussée réelle (retour utilisateur en jeu avec capture d'écran :
        /// "os pontos que vês são pequenos segmentos do laço que são gerados por cima da rua da
        /// ponte"). S'arrête (renvoie faux) dès qu'un nœud intermédiaire a plus d'une arête
        /// utilisable sans que l'une d'elles ne mène directement à nodeB (vraie intersection,
        /// branche ambiguë) : mieux vaut retomber sur l'ancien comportement (corde droite) à cet
        /// endroit précis que deviner la mauvaise branche.
        /// </summary>
        private bool TryFindConnectingPath(Entity nodeA, Entity nodeB, out List<(Entity edge, bool reversed)> path)
        {
            path = new List<(Entity, bool)>();
            if (nodeA == nodeB)
            {
                return false;
            }

            Entity current = nodeA;
            Entity cameFromEdge = Entity.Null;
            // Plafond généreux (une route réelle découpée finement peut avoir beaucoup de nœuds
            // intermédiaires) tout en bornant le pire cas (données réseau incohérentes/boucle).
            const int maxHops = 256;

            for (int hop = 0; hop < maxHops; hop++)
            {
                if (!EntityManager.TryGetBuffer(current, true, out DynamicBuffer<ConnectedEdge> connectedEdges))
                {
                    return false;
                }

                Entity fallbackEdge = Entity.Null;
                Entity fallbackNext = Entity.Null;
                Entity directEdge = Entity.Null;
                int usableCount = 0;

                for (int j = 0; j < connectedEdges.Length; j++)
                {
                    Entity candidate = connectedEdges[j].m_Edge;
                    if (candidate == cameFromEdge || !EntityManager.TryGetComponent(candidate, out Edge edge))
                    {
                        continue;
                    }
                    Entity other = edge.m_Start == current ? edge.m_End : edge.m_End == current ? edge.m_Start : Entity.Null;
                    if (other == Entity.Null)
                    {
                        continue;
                    }
                    usableCount++;
                    fallbackEdge = candidate;
                    fallbackNext = other;
                    if (other == nodeB)
                    {
                        directEdge = candidate;
                        break;
                    }
                }

                Entity chosenEdge;
                Entity chosenNext;
                if (directEdge != Entity.Null)
                {
                    chosenEdge = directEdge;
                    chosenNext = nodeB;
                }
                else if (usableCount == 1)
                {
                    chosenEdge = fallbackEdge;
                    chosenNext = fallbackNext;
                }
                else
                {
                    // Impasse (usableCount==0), ou vraie intersection (plusieurs branches) sans
                    // qu'aucune ne mène directement à nodeB : abandonne plutôt que deviner.
                    return false;
                }

                EntityManager.TryGetComponent(chosenEdge, out Edge chosenEdgeData);
                path.Add((chosenEdge, chosenEdgeData.m_Start != current));
                if (chosenNext == nodeB)
                {
                    return true;
                }
                cameFromEdge = chosenEdge;
                current = chosenNext;
            }
            return false;
        }

        /// <summary>
        /// Résout la/les courbe(s) existante(s) reliant deux nœuds consécutifs du périmètre en
        /// cours de sélection, pour le rendu de l'aperçu overlay — une liste (pas une seule
        /// Bezier4x3) depuis que TryFindConnectingPath peut traverser plusieurs arêtes/nœuds
        /// intermédiaires réels entre les deux, au lieu d'exiger une arête directe. Liste vide si
        /// aucun chemin trouvé, auquel cas l'appelant retombe sur une ligne droite (cohérent avec
        /// la génération, qui ferait de même dans ce cas : voir BuildCurveAwarePerimeterPositions).
        /// </summary>
        public bool TryGetPerimeterSegmentCurve(Entity nodeA, Entity nodeB, out List<Bezier4x3> beziers)
        {
            beziers = new List<Bezier4x3>();
            if (!TryFindConnectingPath(nodeA, nodeB, out List<(Entity edge, bool reversed)> path))
            {
                return false;
            }
            foreach ((Entity edgeEntity, bool reversed) in path)
            {
                if (!EntityManager.TryGetComponent(edgeEntity, out Curve curve))
                {
                    continue;
                }
                Bezier4x3 bez = curve.m_Bezier;
                beziers.Add(reversed ? new Bezier4x3 { a = bez.d, b = bez.c, c = bez.b, d = bez.a } : bez);
            }
            return beziers.Count > 0;
        }

        /// <summary>
        /// Construit la liste de points du périmètre utilisée pour générer la grille :
        /// les nœuds sélectionnés, plus des points échantillonnés le long de chaque arête
        /// EXISTANTE courbe qui relie deux nœuds consécutifs (rond-point, virage...) — sans
        /// quoi le polygone couperait tout droit (corde) à travers la courbe, et la grille
        /// générée pourrait déborder sur la route courbe elle-même.
        ///
        /// Le mode "2 nœuds = rectangle" de GridGenerator (coins opposés, voir son en-tête)
        /// reste intentionnellement inchangé : l'échantillonnage ne s'applique qu'à partir
        /// de 3 nœuds (un vrai périmètre tracé), jamais au raccourci 2 points.
        /// </summary>
        /// <summary>Accès interne pour GridRoadOverlaySystem.DrawLiveSketch — le croquis pendant un drag DOIT utiliser exactement le même périmètre que la vraie génération (voir le retour utilisateur "as linhas não correspondem com a pré-visualização"), pas les positions cliquées brutes.</summary>
        internal List<float3> GetCurveAwarePerimeterPositions() => BuildCurveAwarePerimeterPositions();

        private List<float3> BuildCurveAwarePerimeterPositions()
        {
            if (_settings.FreeAreaMode)
            {
                return new List<float3>(_freeRing);
            }
            int count = _selectedNodes.Count;
            var result = new List<float3>(_selectedPositions.Count);
            if (count == 0)
            {
                return result;
            }

            int pairCount = count >= 3 ? count : 0;
            for (int i = 0; i < count; i++)
            {
                result.Add(_selectedPositions[i]);
                if (i >= pairCount)
                {
                    continue;
                }
                Entity nodeA = _selectedNodes[i];
                Entity nodeB = _selectedNodes[(i + 1) % count];
                // TryFindConnectingPath (au lieu d'une seule arête directe) : une route réelle
                // longue/courbe entre deux nœuds cliqués est presque toujours composée de
                // PLUSIEURS arêtes séparées par des nœuds intermédiaires (simples points de forme)
                // — les ignorer coupait tout droit (corde) à travers la courbe réelle sur toute
                // cette portion, d'où la grille générée qui débordait en plein sur la chaussée
                // réelle (retour utilisateur en jeu : petits segments de laço posés sur la Rua da
                // Ponte). Chaque arête du chemin est échantillonnée dans l'ordre nodeA -> nodeB
                // (voir `reversed`, sens a->d ou d->a selon que l'arête parte de "current" ou non
                // à cette étape du parcours, voir TryFindConnectingPath) et concaténée.
                if (!TryFindConnectingPath(nodeA, nodeB, out List<(Entity edge, bool reversed)> path))
                {
                    continue;
                }
                foreach ((Entity edgeEntity, bool reversed) in path)
                {
                    if (!EntityManager.TryGetComponent(edgeEntity, out Curve curve))
                    {
                        continue;
                    }
                    Bezier4x3 bezier = curve.m_Bezier;
                    result.AddRange(reversed
                        ? GridGenerator.SampleCurve(bezier.d, bezier.c, bezier.b, bezier.a)
                        : GridGenerator.SampleCurve(bezier.a, bezier.b, bezier.c, bezier.d));
                }
            }
            // Les échantillons d'une courbe peuvent dépasser de quelques mètres le nœud suivant
            // (aller-retour du contour), voir GridGenerator.RemoveBacktracks. Seulement pour un
            // vrai périmètre tracé (le mode 2 nœuds = rectangle reste intact).
            if (pairCount > 0)
            {
                GridGenerator.RemoveBacktracks(result);
            }
            return result;
        }

    }
}
