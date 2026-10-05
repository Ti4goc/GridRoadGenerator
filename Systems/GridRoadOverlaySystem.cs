// Rendu overlay (cercles/lignes via OverlayRenderSystem) inspiré de
// CS2-NetworkTools (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Net;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Feedback visuel de la sélection du périmètre, dessiné chaque frame de rendu via
    /// l'OverlayRenderSystem natif (le composant Highlighted seul ne produit rien de
    /// visible sur les nœuds — CS2-NetworkTools dessine lui aussi ces points à la main,
    /// via OverlaySystem.DrawNodesJob, jamais via Highlighted) :
    ///  - un point discret sur tous les nœuds routiers sélectionnables, avant même le
    ///    survol (pas ceux déjà sélectionnés/survolés, qui ont leur propre cercle) ;
    ///  - un cercle plein violet sur chaque nœud sélectionné ;
    ///  - un cercle blanc plus large sur le nœud survolé sélectionnable ;
    ///  - des lignes fines violettes reliant les nœuds dans l'ordre de clic, plus une
    ///    ligne pointillée refermant le périmètre à partir de 3 nœuds.
    /// Peu de nœuds sélectionnés (une poignée par périmètre) : dessin direct sur le thread
    /// principal, sans job, après complétion des dépendances du buffer. Les nœuds
    /// sélectionnables (potentiellement toute la carte) passent par une simple boucle sur
    /// le résultat d'EntityQuery.ToEntityArray — à revoir en job Burst si ça s'avère coûteux.
    /// </summary>
    public partial class GridRoadOverlaySystem : GameSystemBase
    {
        // Palette calquée sur celle de NetworkTools (sélection violette, survol blanc).
        private static readonly Color SelectedBorder = new Color(0.7f, 0.35f, 1f, 0.9f);
        private static readonly Color SelectedFill = new Color(0.8f, 0.55f, 0.85f, 0.9f);
        private static readonly Color HoverBorder = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color HoverFill = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color PerimeterLine = new Color(0.7f, 0.35f, 1f, 0.8f);
        // Point discret sur chaque nœud routier sélectionnable, avant même le survol —
        // comme CS2-NetworkTools (OverlaySystem.DrawNodesJob, NodeEligible.Rest) : plus
        // petit et plus transparent que le cercle de survol, pour ne pas dominer l'écran
        // sur une carte avec beaucoup de nœuds.
        private static readonly Color EligibleBorder = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color EligibleFill = new Color(1f, 1f, 1f, 0.12f);
        // Croquis pendant un drag de slider (voir DrawLiveSketch) : blanc pointillé pour le
        // réseau local/principal (base), pour se distinguer nettement du violet plein du
        // périmètre/aperçu réel — retour utilisateur en jeu : "linhas mais largas e... poder
        // destinguir cada tipo de arteria coletor, rotunda". Une couleur/largeur par NIVEAU de
        // hiérarchie (le plus large = le plus important), même logique que les vraies routes.
        private static readonly Color LiveSketchLine = new Color(1f, 1f, 1f, 0.65f);
        private static readonly Color LiveSketchAvenue = new Color(0.3f, 0.8f, 1f, 0.75f);
        /// <summary>
        /// Réseau piéton (RoadSegmentDef.IsPedestrian, mode SuperblockMode) — retour utilisateur
        /// en jeu, capture d'écran : sans couleur propre, ces tronçons tombaient dans la même
        /// catégorie "blanche" que le reste, rendant le croquis illisible (impossible de
        /// distinguer coletora/piéton d'un coup d'œil). Vert, distinct du blanc/bleu déjà utilisés
        /// pour collectrice/laço classique — évoque un chemin piéton/végétalisé.
        /// </summary>
        private static readonly Color LiveSketchPedestrian = new Color(0.4f, 0.9f, 0.4f, 0.7f);
        private static readonly Color LiveSketchRoundabout = new Color(1f, 0.85f, 0.2f, 0.7f);

        private const float CircleOutlineWidth = 0.35f;
        private const float PerimeterLineWidth = 0.6f;
        /// <summary>Largeur du contour de zone libre refermée (future route de périmètre).</summary>
        private const float FreeAreaRoadWidth = 2f;
        /// <summary>Zone libre, style des outils de zone du jeu (bairros) : remplissage, bord, nœuds, pinceau.</summary>
        private static readonly Color AreaFill = new Color(0.3f, 0.62f, 1f, 0.22f);
        private static readonly Color AreaOutline = new Color(0.8f, 0.92f, 1f, 0.95f);
        private static readonly Color AreaNodeFill = new Color(0.3f, 0.62f, 1f, 0.9f);
        private static readonly Color BrushFill = new Color(0.3f, 0.62f, 1f, 0.12f);
        private const float AreaEdgeWidth = 1.2f;
        private const float AreaNodeDiameter = 9f;
        private const float LiveSketchLineWidth = 0.6f;
        private const float LiveSketchAvenueWidth = 0.85f;
        private const float LiveSketchPedestrianWidth = 0.45f;
        /// <summary>Diamètre de secours (m) quand la largeur des routes du nœud est inconnue.</summary>
        private const float FallbackDiameter = 10f;
        /// <summary>Fraction du diamètre du nœud utilisée pour le point "sélectionnable" discret.</summary>
        private const float EligibleDiameterFactor = 0.4f;

        private ToolSystem m_ToolSystem;
        private GridRoadToolSystem m_GridRoadToolSystem;
        private OverlayRenderSystem m_OverlayRenderSystem;
        private TerrainSystem m_TerrainSystem;

        /// <summary>Longueur (m) des morceaux d'une ligne du croquis posée sur le relief.</summary>
        private const float SketchTerrainStep = 10f;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_GridRoadToolSystem = World.GetOrCreateSystemManaged<GridRoadToolSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
        }

        protected override void OnUpdate()
        {
            if (m_ToolSystem.activeTool != m_GridRoadToolSystem)
            {
                return;
            }

            IReadOnlyList<Entity> nodes = m_GridRoadToolSystem.SelectedNodes;
            IReadOnlyList<float3> positions = m_GridRoadToolSystem.SelectedPositions;
            Entity hovered = m_GridRoadToolSystem.HoveredNode;

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            if (m_GridRoadToolSystem.FreeAreaMode)
            {
                DrawFreeArea(buffer);
                DrawLiveSketch(buffer);
                return;
            }

            // Point discret sur tous les nœuds routiers sélectionnables, avant même le survol
            // (jamais ceux déjà sélectionnés ou survolés, qui ont déjà leur propre cercle plus
            // marqué ci-dessous). Simple boucle sur le thread principal comme le reste de ce
            // système : suffisant pour l'ordre de grandeur de nœuds routiers d'une carte CS2 ;
            // à revoir en job Burst si ça s'avère coûteux en jeu.
            using (NativeArray<Entity> eligibleNodes = m_GridRoadToolSystem.EligibleRoadNodesQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity candidate in eligibleNodes)
                {
                    if (candidate == hovered || IsAlreadySelected(candidate, nodes))
                    {
                        continue;
                    }
                    if (!EntityManager.TryGetComponent(candidate, out Node candidateNode))
                    {
                        continue;
                    }
                    buffer.DrawCircle(EligibleBorder, EligibleFill, CircleOutlineWidth, 0,
                        new float2(0f, 1f), candidateNode.m_Position, GetNodeDiameter(candidate) * EligibleDiameterFactor);
                }
            }

            // Périmètre en construction : lignes fines entre nœuds consécutifs, et fermeture
            // en pointillés dès qu'un polygone existe. Suit la courbe réelle de la route
            // existante entre deux nœuds (rond-point, virage...), y compris à travers plusieurs
            // arêtes/nœuds intermédiaires réels (voir TryFindConnectingPath), au lieu d'une corde
            // droite qui ne représenterait pas la vraie géométrie prise en compte par la
            // génération (voir BuildCurveAwarePerimeterPositions).
            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                if (m_GridRoadToolSystem.TryGetPerimeterSegmentCurve(nodes[i], nodes[i + 1], out List<Bezier4x3> curves))
                {
                    foreach (Bezier4x3 curve in curves)
                    {
                        buffer.DrawCurve(PerimeterLine, curve, PerimeterLineWidth);
                    }
                }
                else
                {
                    buffer.DrawLine(PerimeterLine, new Line3.Segment(positions[i], positions[i + 1]), PerimeterLineWidth);
                }
            }
            if (nodes.Count >= 3)
            {
                if (m_GridRoadToolSystem.TryGetPerimeterSegmentCurve(nodes[nodes.Count - 1], nodes[0], out List<Bezier4x3> closingCurves))
                {
                    foreach (Bezier4x3 closingCurve in closingCurves)
                    {
                        buffer.DrawDashedCurve(PerimeterLine, closingCurve, PerimeterLineWidth, 2f, 2f);
                    }
                }
                else
                {
                    buffer.DrawDashedLine(PerimeterLine,
                        new Line3.Segment(positions[positions.Count - 1], positions[0]),
                        PerimeterLineWidth, 2f, 2f);
                }
            }

            // Nœuds sélectionnés : cercle plein violet.
            for (int i = 0; i < nodes.Count; i++)
            {
                buffer.DrawCircle(SelectedBorder, SelectedFill, CircleOutlineWidth, 0,
                    new float2(0f, 1f), positions[i], GetNodeDiameter(nodes[i]));
            }

            // Nœud survolé sélectionnable : cercle blanc plus large.
            if (hovered != Entity.Null && EntityManager.TryGetComponent(hovered, out Node hoveredNode))
            {
                buffer.DrawCircle(HoverBorder, HoverFill, CircleOutlineWidth, 0,
                    new float2(0f, 1f), hoveredNode.m_Position, GetNodeDiameter(hovered) * 1.35f);
            }

            DrawLiveSketch(buffer);
        }

        /// <summary>
        /// Zone libre (voir GridRoadToolSystem.FreeArea), dans le style des outils du jeu (retour
        /// utilisateur : "segue a estética do jogo", outil des bairros / pinceaux du terrain) : tout est
        /// projeté sur le relief (StyleFlags.Projected) au lieu de rester à plat.
        ///  - Pintar área : cercle du pinceau et zone peinte remplie, collés au terrain ;
        ///  - Desenhar área : zone remplie, côtés, points ronds, côté vers le curseur en pointillés ;
        ///  - zone refermée : contour lissé épais (future route de périmètre) et zone remplie.
        /// </summary>
        private void DrawFreeArea(OverlayRenderSystem.Buffer buffer)
        {
            IReadOnlyList<float3> points = m_GridRoadToolSystem.FreeAreaPoints;
            IReadOnlyList<float3> ring = m_GridRoadToolSystem.FreeAreaRing;
            float3? cursor = m_GridRoadToolSystem.FreeAreaCursor;
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            bool closed = ring.Count >= 3 && !m_GridRoadToolSystem.BrushPainting;

            if (closed)
            {
                DrawFill(buffer, ref heightData, RingFill(ring), AreaFill, _ringFillStep);
                for (int i = 0; i < ring.Count; i++)
                {
                    DrawProjected(buffer, ref heightData, AreaOutline, ring[i], ring[(i + 1) % ring.Count], FreeAreaRoadWidth, false);
                }
            }

            if (m_GridRoadToolSystem.BrushMode)
            {
                if (!closed)
                {
                    float fillWidth = m_GridRoadToolSystem.BrushFillWidth;
                    foreach ((float3 a, float3 b) in m_GridRoadToolSystem.BrushFill)
                    {
                        buffer.DrawLine(AreaFill, AreaFill, 0f, OverlayRenderSystem.StyleFlags.Projected, new Line3.Segment(a, b), fillWidth, new float2(0f, 0f));
                    }
                    foreach ((float3 a, float3 b) in m_GridRoadToolSystem.BrushBoundary)
                    {
                        buffer.DrawLine(AreaOutline, AreaOutline, 0f, OverlayRenderSystem.StyleFlags.Projected, new Line3.Segment(a, b), AreaEdgeWidth, new float2(1f, 1f));
                    }
                }
                if (cursor.HasValue)
                {
                    float radius = m_GridRoadToolSystem.BrushRadius;
                    float outline = math.max(1f, radius * 0.016f);
                    if (m_GridRoadToolSystem.BrushSquare)
                    {
                        // Carré : remplissage en bandes (collé au relief), puis les quatre côtés.
                        float3 c = cursor.Value;
                        float angle = math.radians(m_GridRoadToolSystem.BrushAngle);
                        float3 ax = new float3(math.cos(angle), 0f, math.sin(angle)), az = new float3(-ax.z, 0f, ax.x);
                        // Au plus ~24 bandes de ~20 morceaux, quelle que soit la taille (pinceau de 1000 m :
                        // 6000 lignes par frame auparavant).
                        float band = math.max(BrushMask.Cell * 4f, 2f * radius / 24f);
                        float piece = math.max(SketchTerrainStep, 2f * radius / 20f);
                        for (float z = -radius + band * 0.5f; z < radius; z += band)
                        {
                            DrawProjected(buffer, ref heightData, BrushFill, c - radius * ax + z * az, c + radius * ax + z * az, band, false, 0f, piece);
                        }
                        float3 a = c - radius * ax - radius * az, b = c + radius * ax - radius * az;
                        float3 d = c - radius * ax + radius * az, e = c + radius * ax + radius * az;
                        DrawProjected(buffer, ref heightData, AreaOutline, a, b, outline, false);
                        DrawProjected(buffer, ref heightData, AreaOutline, b, e, outline, false);
                        DrawProjected(buffer, ref heightData, AreaOutline, e, d, outline, false);
                        DrawProjected(buffer, ref heightData, AreaOutline, d, a, outline, false);
                    }
                    else
                    {
                        buffer.DrawCircle(AreaOutline, BrushFill, outline, OverlayRenderSystem.StyleFlags.Projected,
                            new float2(0f, 1f), cursor.Value, 2f * radius);
                    }
                }
                return;
            }

            if (closed)
            {
                // Points déplaçables : celui sous le curseur grossit (on peut le saisir).
                foreach (float3 p in points)
                {
                    bool hover = m_GridRoadToolSystem.FreeAreaCanGrab && cursor.HasValue && math.distance(p.xz, cursor.Value.xz) < 14f;
                    DrawNode(buffer, ref heightData, p, hover ? AreaNodeDiameter * 1.3f : AreaNodeDiameter * 0.8f, hover ? AreaOutline : AreaNodeFill);
                }
                return;
            }

            if (m_GridRoadToolSystem.FreeAreaDragging && points.Count >= 3)
            {
                var dragged = new List<float3>(points);
                DrawFill(buffer, ref heightData, FillRuns(dragged, out float draggedStep), AreaFill, draggedStep);
                for (int i = 0; i < points.Count; i++)
                {
                    DrawProjected(buffer, ref heightData, AreaOutline, points[i], points[(i + 1) % points.Count], AreaEdgeWidth, false);
                    DrawNode(buffer, ref heightData, points[i], AreaNodeDiameter, AreaNodeFill);
                }
                return;
            }

            // Tracé en cours : la zone suit le curseur (comme l'outil des bairros).
            bool closeHover = cursor.HasValue && points.Count >= 3
                && math.distance(cursor.Value.xz, points[0].xz) < FreeAreaPerimeter.CloseDistance;
            var shape = new List<float3>(points);
            if (cursor.HasValue && !closeHover && points.Count > 0)
            {
                shape.Add(cursor.Value);
            }
            if (shape.Count >= 3)
            {
                DrawFill(buffer, ref heightData, FillRuns(shape, out float shapeStep), AreaFill, shapeStep);
            }
            for (int i = 0; i + 1 < points.Count; i++)
            {
                DrawProjected(buffer, ref heightData, AreaOutline, points[i], points[i + 1], AreaEdgeWidth, false);
            }
            if (cursor.HasValue && points.Count > 0)
            {
                DrawProjected(buffer, ref heightData, AreaOutline, points[points.Count - 1], closeHover ? points[0] : cursor.Value, AreaEdgeWidth, !closeHover);
                if (points.Count >= 2 && !closeHover)
                {
                    DrawProjected(buffer, ref heightData, AreaOutline, cursor.Value, points[0], AreaEdgeWidth, true);
                }
            }
            for (int i = 0; i < points.Count; i++)
            {
                bool highlight = i == 0 && closeHover;
                DrawNode(buffer, ref heightData, points[i], highlight ? AreaNodeDiameter * 1.5f : AreaNodeDiameter, highlight ? AreaOutline : AreaNodeFill);
            }
            if (cursor.HasValue && !closeHover)
            {
                DrawNode(buffer, ref heightData, cursor.Value, AreaNodeDiameter * 0.8f, BrushFill);
            }
        }

        /// <summary>Remplissage du contour refermé, recalculé seulement quand il change.</summary>
        private List<(float2 a, float2 b)> RingFill(IReadOnlyList<float3> ring)
        {
            if (_ringFillCount != ring.Count || !_ringFillFirst.Equals(ring[0]) || !_ringFillLast.Equals(ring[ring.Count - 1]))
            {
                _ringFillCount = ring.Count;
                _ringFillFirst = ring[0];
                _ringFillLast = ring[ring.Count - 1];
                _ringFill = FillRuns(ring, out _ringFillStep);
            }
            return _ringFill;
        }

        /// <summary>
        /// Bandes de remplissage d'un polygone, espacées de `step` : 4 m, plus sur une grande zone —
        /// retour utilisateur : "ao desenhar uma área muito grande, o jogo fica lento" (bandes de 4 m
        /// coupées tous les 64 m, recalculées et dessinées à chaque frame : ~100 000 lignes pour 25 km²).
        /// </summary>
        private static List<(float2 a, float2 b)> FillRuns(IReadOnlyList<float3> polygon, out float step)
        {
            float2 min = new float2(float.MaxValue), max = new float2(float.MinValue);
            foreach (float3 p in polygon)
            {
                min = math.min(min, p.xz);
                max = math.max(max, p.xz);
            }
            float2 size = math.max(max - min, 0f);
            float scale = math.max(1f, math.sqrt(size.x * size.y / (BrushMask.Cell * 64f * GridRoadToolSystem.FillLineBudget)));
            step = BrushMask.Cell * scale;
            return FreeAreaPerimeter.FillRuns(polygon, step, 64f * scale);
        }

        private int _ringFillCount;
        private float3 _ringFillFirst, _ringFillLast;
        private float _ringFillStep = BrushMask.Cell;
        private List<(float2 a, float2 b)> _ringFill = new List<(float2, float2)>();

        /// <summary>Bandes de remplissage (xz) projetées sur le relief, jointives (largeur = pas des bandes).</summary>
        private static void DrawFill(OverlayRenderSystem.Buffer buffer, ref TerrainHeightData heightData, List<(float2 a, float2 b)> runs, Color color, float width)
        {
            foreach ((float2 a, float2 b) in runs)
            {
                float3 a3 = OnTerrain(ref heightData, new float3(a.x, 0f, a.y));
                float3 b3 = OnTerrain(ref heightData, new float3(b.x, 0f, b.y));
                buffer.DrawLine(color, color, 0f, OverlayRenderSystem.StyleFlags.Projected, new Line3.Segment(a3, b3), width, new float2(0f, 0f));
            }
        }

        /// <summary>Côté projeté sur le relief (morceaux de SketchTerrainStep m), plein ou en pointillés.</summary>
        private static void DrawProjected(OverlayRenderSystem.Buffer buffer, ref TerrainHeightData heightData, Color color, float3 a, float3 b, float width, bool dashed,
            float roundness = 1f, float step = SketchTerrainStep)
        {
            int pieces = math.max(1, (int)math.ceil(math.distance(a.xz, b.xz) / step));
            float3 previous = OnTerrain(ref heightData, a);
            for (int k = 1; k <= pieces; k++)
            {
                float3 next = OnTerrain(ref heightData, math.lerp(a, b, (float)k / pieces));
                var segment = new Line3.Segment(previous, next);
                if (dashed)
                {
                    buffer.DrawDashedLine(color, color, 0f, OverlayRenderSystem.StyleFlags.Projected, segment, width, 3f, 2f, new float2(1f, 1f));
                }
                else
                {
                    buffer.DrawLine(color, color, 0f, OverlayRenderSystem.StyleFlags.Projected, segment, width, new float2(roundness, roundness));
                }
                previous = next;
            }
        }

        /// <summary>Point de la zone (rond, projeté), comme les nœuds de l'outil des bairros.</summary>
        private static void DrawNode(OverlayRenderSystem.Buffer buffer, ref TerrainHeightData heightData, float3 p, float diameter, Color fill)
        {
            buffer.DrawCircle(AreaOutline, fill, diameter * 0.18f, OverlayRenderSystem.StyleFlags.Projected, new float2(0f, 1f),
                OnTerrain(ref heightData, p), diameter);
        }

        /// <summary>
        /// Esquisse légère (lignes seulement, AUCUNE entité créée) de la grille pendant un drag
        /// de N'IMPORTE QUEL slider du panneau — voir GridRoadToolSystem.LivePreviewOverride/
        /// LivePreviewField ("coloca as linhas em todas as opções", généralisé depuis le seul
        /// slider Espaçamento). Recalcule GridGenerator.GenerateGrid/GenerateLoopGrid (pur C#,
        /// rapide même sur une grande zone depuis le correctif SpatialPointIndex) à CHAQUE frame
        /// pendant le drag, mais ne passe JAMAIS par CreateGridDefinitions (aucune entité ECS
        /// créée/détruite) — c'est cette étape-là, coûteuse avec une grille dense, que ce
        /// croquis permet d'éviter pendant le drag tout en gardant un retour visuel en direct.
        /// GetCurveAwarePerimeterPositions() (PAS SelectedPositions brut) — retour utilisateur en
        /// jeu, "as linhas não correspondem com a pré-visualização" : la vraie génération
        /// (CreateGridDefinitions) échantillonne la courbe réelle entre deux nœuds cliqués (rond-
        /// point, virage...) au lieu de couper tout droit en corde, le croquis DOIT faire pareil
        /// pour dessiner le même polygone.
        /// </summary>
        private void DrawLiveSketch(OverlayRenderSystem.Buffer buffer)
        {
            // Deux cas dessinent ce même croquis léger (aucune entité ECS créée) : (1) pendant
            // un drag de slider (preview.HasValue, valeur en cours de glissement — voir
            // GridRoadUISystem SET_LIVE_PREVIEW) ; (2) par défaut, dans les DEUX modes (Grille
            // classique et Loop), hors confirmation (ShowSketchOnly, voir sa doc —
            // GridRoadToolSystem.CreateGridDefinitions ne tourne plus en continu dans ce cas, le
            // vrai aperçu ECS n'a pas besoin de tourner à chaque frame juste pour être regardé).
            // Dans le 2ᵉ cas, aucun champ n'est en cours de glissement — les réglages COURANTS
            // (settings.ToGridParameters(), sans override) sont utilisés tels quels.
            (GridRoadToolSystem.LivePreviewField Field, float Value)? preview = m_GridRoadToolSystem.LivePreviewOverride;
            bool isDragOverride = preview.HasValue;
            if ((!isDragOverride && !m_GridRoadToolSystem.ShowSketchOnly) || !m_GridRoadToolSystem.HasPerimeter)
            {
                return;
            }
            List<float3> positions = m_GridRoadToolSystem.GetCurveAwarePerimeterPositions();
            if (positions.Count < 2)
            {
                return;
            }

            // Log de performance par geste (voir GridRoadToolSystem.RecordDragFrame) : mesure
            // CETTE frame de croquis, accumulée en mémoire — jamais loggué individuellement
            // (le résumé frames/moyenne/max sort une seule fois au relâchement, voir EndDrag).
            // Uniquement pertinent pendant un VRAI drag (BeginOrContinueDrag/EndDrag) — le
            // croquis par défaut hors drag (ShowSketchOnly) n'a pas de geste "arrasto" à mesurer.
            Stopwatch sketchStopwatch = isDragOverride ? Stopwatch.StartNew() : null;
            try
            {
            GridRoadGeneratorSettings settings = Mod.Instance.Settings;
            GridParameters parameters = settings.ToGridParameters();
            if (isDragOverride)
            {
                float value = preview.Value.Value;
                switch (preview.Value.Field)
                {
                    case GridRoadToolSystem.LivePreviewField.Spacing:
                        parameters.SpacingMeters = value;
                        parameters.Mode = SpacingMode.FixedSpacing;
                        break;
                    case GridRoadToolSystem.LivePreviewField.Columns:
                        parameters.Columns = (int)math.round(value);
                        parameters.Mode = SpacingMode.FitToArea;
                        break;
                    case GridRoadToolSystem.LivePreviewField.Rows:
                        parameters.Rows = (int)math.round(value);
                        parameters.Mode = SpacingMode.FitToArea;
                        break;
                    case GridRoadToolSystem.LivePreviewField.Angle:
                        parameters.AngleOffsetDegrees = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.CollectorSpacing:
                        parameters.CollectorSpacingMeters = math.max(value, GridRoadGeneratorSettings.CollectorSpacingMetersMin);
                        break;
                    case GridRoadToolSystem.LivePreviewField.LoopCulDeSacRatio:
                        parameters.LoopCulDeSacRatio = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.CulDeSacDepth:
                        // value reçu en pourcentage affiché (50-100) — même conversion que
                        // SET_CULDESAC_DEPTH (voir GridRoadGeneratorSettings.CulDeSacDepthUiToReal).
                        parameters.CulDeSacDepth = GridRoadGeneratorSettings.CulDeSacDepthUiToReal(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.CulDeSacRatio:
                        parameters.CulDeSacRatio = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.AvenueColumnIndex:
                        parameters.AvenueColumnIndex = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.AvenueRowIndex:
                        parameters.AvenueRowIndex = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.SuperblockZone:
                        parameters.SuperblockZoneMeters = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.ConcentricLayers:
                        parameters.ConcentricLayers = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.ConcentricConnections:
                        parameters.ConcentricConnections = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.RadialAvenues:
                        parameters.RadialAvenues = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.RadialRoundabout:
                        parameters.RadialRoundaboutRadius = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.RadialLayers:
                        parameters.RadialLayers = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.TreeBranchSpacing:
                        parameters.TreeBranchSpacing = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.TreeCulDeSacSpacing:
                        parameters.TreeCulDeSacSpacing = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.TreeCulDeSacLength:
                        parameters.TreeCulDeSacLength = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.OrganicStreetSpacing:
                        parameters.OrganicStreetSpacing = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.OrganicCurviness:
                        parameters.OrganicCurviness = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.OrganicLoopShare:
                        parameters.OrganicLoopShare = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.OrganicSeed:
                        parameters.OrganicSeed = (int)math.round(value);
                        break;
                    case GridRoadToolSystem.LivePreviewField.ContourSpacing:
                        parameters.ContourSpacing = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.ContourConnectorSpacing:
                        parameters.ContourConnectorSpacing = value;
                        break;
                    case GridRoadToolSystem.LivePreviewField.MixedCoreRadius:
                        parameters.MixedCoreRadius = value;
                        break;
                }
            }

            // Croquis par défaut (pas de drag) : lignes calculées une fois par grille, puis
            // seulement redessinées (retour utilisateur : jeu lent après avoir peint une grande zone).
            if (!isDragOverride)
            {
                _wasDragging = false;
                if (_sketchLinesVersion != m_GridRoadToolSystem.SketchVersion || _sketchLinesVersion < 0)
                {
                    List<RoadSegmentDef> cachedSegments;
                    try
                    {
                        cachedSegments = m_GridRoadToolSystem.GenerateSketch(positions, parameters, out RoundaboutInfo cachedRoundabout);
                        _sketchRoundabout = cachedRoundabout;
                    }
                    catch
                    {
                        return;
                    }
                    m_GridRoadToolSystem.RemoveObstacleSegments(cachedSegments);
                    BuildSketchLines(cachedSegments, settings, _sketchLines, _sketchRoundabout, out _sketchRoundaboutCentre);
                    _sketchLinesVersion = m_GridRoadToolSystem.SketchVersion;
                }
                else
                {
                    // Même grille : GenerateSketch ne recalcule rien, mais garde la clé à jour.
                    m_GridRoadToolSystem.GenerateSketch(positions, parameters, out _);
                    if (_sketchLinesVersion != m_GridRoadToolSystem.SketchVersion)
                    {
                        return; // la grille vient de changer : lignes refaites à la frame suivante
                    }
                }
                foreach ((Color color, float width, float3 a, float3 b) in _sketchLines)
                {
                    buffer.DrawDashedLine(color, new Line3.Segment(a, b), width, 1.5f, 1f);
                }
                if (_sketchRoundabout.HasRoundabout)
                {
                    buffer.DrawCircle(LiveSketchRoundabout, default, LiveSketchLineWidth, 0, new float2(0f, 1f), _sketchRoundaboutCentre, _sketchRoundabout.Radius * 2f);
                }
                return;
            }

            // Drag : grille calculée sur un thread de fond (retour utilisateur : Misto lent, ~0,5 s par
            // valeur) ; le dernier résultat reste affiché, l'image du jeu ne se fige plus.
            if (!_wasDragging)
            {
                _dragLines.Clear();
                _dragLines.AddRange(_sketchLines);
                _dragRoundabout = _sketchRoundabout;
                _dragRoundaboutCentre = _sketchRoundaboutCentre;
                _dragLinesKey = null;
            }
            _wasDragging = true;
            object key = GridRoadToolSystem.SketchKey(positions, parameters, settings.LoopMode);
            if (m_GridRoadToolSystem.TryReadySketch(key, out List<RoadSegmentDef> ready, out RoundaboutInfo readyRoundabout))
            {
                if (!key.Equals(_dragLinesKey))
                {
                    var segments = new List<RoadSegmentDef>(ready);
                    m_GridRoadToolSystem.RemoveObstacleSegments(segments);
                    _dragRoundabout = readyRoundabout;
                    BuildSketchLines(segments, settings, _dragLines, _dragRoundabout, out _dragRoundaboutCentre);
                    _dragLinesKey = key;
                }
            }
            else
            {
                m_GridRoadToolSystem.RequestSketch(key, positions, parameters, settings.LoopMode);
            }
            foreach ((Color color, float width, float3 a, float3 b) in _dragLines)
            {
                buffer.DrawDashedLine(color, new Line3.Segment(a, b), width, 1.5f, 1f);
            }
            if (_dragRoundabout.HasRoundabout)
            {
                buffer.DrawCircle(LiveSketchRoundabout, default, LiveSketchLineWidth, 0, new float2(0f, 1f), _dragRoundaboutCentre, _dragRoundabout.Radius * 2f);
            }
            }
            finally
            {
                if (isDragOverride)
                {
                    m_GridRoadToolSystem.RecordDragFrame(sketchStopwatch.Elapsed.TotalMilliseconds);
                }
            }
        }

        private int _sketchLinesVersion = -1;
        private readonly List<(Color color, float width, float3 a, float3 b)> _sketchLines = new List<(Color, float, float3, float3)>();
        private RoundaboutInfo _sketchRoundabout;
        private float3 _sketchRoundaboutCentre;
        /// <summary>Nombre de morceaux de croquis visé : au-delà, morceaux plus longs (grande zone).</summary>
        private const int SketchLineBudget = 6000;

        private bool _wasDragging;
        private object _dragLinesKey;
        private readonly List<(Color color, float width, float3 a, float3 b)> _dragLines = new List<(Color, float, float3, float3)>();
        private RoundaboutInfo _dragRoundabout;
        private float3 _dragRoundaboutCentre;

        /// <summary>Lignes du croquis (couleur, largeur, morceau), sur le relief si les routes le suivent.</summary>
        private void BuildSketchLines(List<RoadSegmentDef> segments, GridRoadGeneratorSettings settings,
            List<(Color color, float width, float3 a, float3 b)> lines, RoundaboutInfo roundabout, out float3 roundaboutCentre)
        {
            lines.Clear();
            bool onTerrain = settings.FollowTerrain || settings.ContourMode || settings.FreeAreaMode;
            TerrainHeightData heightData = onTerrain ? m_TerrainSystem.GetHeightData() : default;
            bool singleNetwork = settings.ContourMode && !settings.LoopMode;
            float total = 0f;
            foreach (RoadSegmentDef segment in segments) total += math.distance(segment.Start.xz, segment.End.xz);
            float step = math.max(SketchTerrainStep, total / SketchLineBudget);
            foreach (RoadSegmentDef segment in segments)
            {
                (Color color, float width) = segment.IsPedestrian ? (LiveSketchPedestrian, LiveSketchPedestrianWidth)
                    : segment.IsRoundabout ? (LiveSketchRoundabout, LiveSketchAvenueWidth)
                    : segment.IsAvenue && !singleNetwork ? (LiveSketchAvenue, LiveSketchAvenueWidth)
                    : (LiveSketchLine, LiveSketchLineWidth);
                Bezier4x3 path = segment.IsArc
                    ? NetUtils.FitCurve(segment.Start, segment.StartTangent, segment.EndTangent, segment.End)
                    : NetUtils.StraightCurve(segment.Start, segment.End);
                int pieces = onTerrain || segment.IsArc ? math.max(1, (int)math.ceil(math.distance(segment.Start.xz, segment.End.xz) / step)) : 1;
                float3 previous = MathUtils.Position(path, 0f);
                if (onTerrain) previous = OnTerrain(ref heightData, previous);
                for (int k = 1; k <= pieces; k++)
                {
                    float3 next = MathUtils.Position(path, (float)k / pieces);
                    if (onTerrain) next = OnTerrain(ref heightData, next);
                    lines.Add((color, width, previous, next));
                    previous = next;
                }
            }
            roundaboutCentre = onTerrain && roundabout.HasRoundabout ? OnTerrain(ref heightData, roundabout.Center) : roundabout.Center;
        }

        private static float3 OnTerrain(ref TerrainHeightData heightData, float3 p)
        {
            p.y = TerrainUtils.SampleHeight(ref heightData, p);
            return p;
        }

        /// <summary>Peu de nœuds sélectionnés (poignée par périmètre) : recherche linéaire suffisante.</summary>
        private static bool IsAlreadySelected(Entity candidate, IReadOnlyList<Entity> selectedNodes)
        {
            for (int i = 0; i < selectedNodes.Count; i++)
            {
                if (selectedNodes[i] == candidate)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Diamètre du marqueur : largeur moyenne des routes connectées au nœud
        /// (même heuristique que NetworkTools), pour que le cercle épouse l'intersection.
        /// </summary>
        private float GetNodeDiameter(Entity node)
        {
            if (!EntityManager.TryGetBuffer(node, true, out DynamicBuffer<ConnectedEdge> connectedEdges))
            {
                return FallbackDiameter;
            }

            float widthSum = 0f;
            int edgeCount = 0;
            for (int i = 0; i < connectedEdges.Length; i++)
            {
                Entity edgeEntity = connectedEdges[i].m_Edge;
                if (!EntityManager.TryGetComponent(edgeEntity, out Edge edge)
                    || !EntityManager.TryGetComponent(edgeEntity, out EdgeGeometry geometry))
                {
                    continue;
                }
                widthSum += edge.m_Start == node
                    ? math.distance(geometry.m_Start.m_Left.a, geometry.m_Start.m_Right.a)
                    : math.distance(geometry.m_End.m_Left.a, geometry.m_End.m_Right.a);
                edgeCount++;
            }
            return edgeCount == 0 ? FallbackDiameter : widthSum / edgeCount;
        }
    }
}
