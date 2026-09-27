// Rendu overlay (cercles/lignes via OverlayRenderSystem) inspiré de
// CS2-NetworkTools (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System.Collections.Generic;
using System.Diagnostics;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Net;
using Game.Rendering;
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

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            m_GridRoadToolSystem = World.GetOrCreateSystemManaged<GridRoadToolSystem>();
            m_OverlayRenderSystem = World.GetOrCreateSystemManaged<OverlayRenderSystem>();
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
            if ((!isDragOverride && !m_GridRoadToolSystem.ShowSketchOnly) || m_GridRoadToolSystem.SelectedPositions.Count < 2)
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
                }
            }

            List<RoadSegmentDef> segments;
            RoundaboutInfo roundabout = default;
            try
            {
                segments = settings.LoopMode
                    ? GridGenerator.GenerateLoopGrid(positions, parameters)
                    : GridGenerator.GenerateGrid(positions, parameters, out _, out roundabout);
            }
            catch
            {
                return; // périmètre dégénéré cette frame : pas de croquis, rien d'autre.
            }

            // Couleur/largeur par TYPE de réseau (voir RoadSegmentDef.IsAvenue/IsPedestrian) —
            // retour utilisateur en jeu : "poder destinguir cada tipo de arteria coletor, rotunda
            // etc" puis, en mode SuperblockMode, "o mod não esta a considerar bem os tipos de
            // estradas" (le piéton tombait dans la même catégorie blanche que le reste, croquis
            // illisible) — même hiérarchie visuelle (le plus large = le plus important) que les
            // vraies routes de l'aperçu réel, même si le croquis reste en lignes pointillées
            // légères. IsPedestrian testé AVANT IsAvenue par construction (jamais les deux à la
            // fois, voir RoadSegmentDef), mais l'ordre explicite documente l'intention.
            foreach (RoadSegmentDef segment in segments)
            {
                (Color color, float width) = segment.IsPedestrian ? (LiveSketchPedestrian, LiveSketchPedestrianWidth)
                    : segment.IsAvenue ? (LiveSketchAvenue, LiveSketchAvenueWidth)
                    : (LiveSketchLine, LiveSketchLineWidth);
                if (segment.IsArc)
                {
                    // Même courbe que celle posée côté ECS (GridRoadToolSystem.CreateGridDefinitions),
                    // sinon un anneau Concêntrico apparaîtrait en facettes droites dans le croquis.
                    Bezier4x3 curve = NetUtils.FitCurve(segment.Start, segment.StartTangent, segment.EndTangent, segment.End);
                    buffer.DrawDashedCurve(color, curve, width, 1.5f, 1f);
                }
                else
                {
                    buffer.DrawDashedLine(color, new Line3.Segment(segment.Start, segment.End), width, 1.5f, 1f);
                }
            }

            // Rotonde avenue×avenue (Grille classique uniquement, voir ComputeAvenueRoundabout —
            // GenerateLoopGrid n'a pas cette notion) : simple cercle pointillé à la bonne position/
            // taille, pas l'îlot décoratif réel (aucune entité pendant le croquis).
            if (roundabout.HasRoundabout)
            {
                buffer.DrawCircle(LiveSketchRoundabout, default, LiveSketchLineWidth, 0, new float2(0f, 1f), roundabout.Center, roundabout.Radius * 2f);
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
