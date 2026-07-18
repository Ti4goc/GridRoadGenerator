// Rendu overlay (cercles/lignes via OverlayRenderSystem) inspiré de
// CS2-NetworkTools (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Net;
using Game.Rendering;
using Game.Tools;
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

        private const float CircleOutlineWidth = 0.35f;
        private const float PerimeterLineWidth = 0.6f;
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
            // existante entre deux nœuds (rond-point, virage...) quand elle en relie deux
            // directement, au lieu d'une corde droite qui ne représenterait pas la vraie
            // géométrie prise en compte par la génération (voir BuildCurveAwarePerimeterPositions).
            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                if (m_GridRoadToolSystem.TryGetPerimeterSegmentCurve(nodes[i], nodes[i + 1], out Bezier4x3 curve))
                {
                    buffer.DrawCurve(PerimeterLine, curve, PerimeterLineWidth);
                }
                else
                {
                    buffer.DrawLine(PerimeterLine, new Line3.Segment(positions[i], positions[i + 1]), PerimeterLineWidth);
                }
            }
            if (nodes.Count >= 3)
            {
                if (m_GridRoadToolSystem.TryGetPerimeterSegmentCurve(nodes[nodes.Count - 1], nodes[0], out Bezier4x3 closingCurve))
                {
                    buffer.DrawDashedCurve(PerimeterLine, closingCurve, PerimeterLineWidth, 2f, 2f);
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
