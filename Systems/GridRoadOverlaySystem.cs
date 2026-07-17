// Rendu overlay (cercles/lignes via OverlayRenderSystem) inspiré de
// CS2-NetworkTools (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game;
using Game.Net;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Feedback visuel de la sélection du périmètre, dessiné chaque frame de rendu via
    /// l'OverlayRenderSystem natif (le composant Highlighted seul ne produit rien de
    /// visible sur les nœuds) :
    ///  - un cercle plein violet sur chaque nœud sélectionné ;
    ///  - un cercle blanc plus large sur le nœud survolé sélectionnable ;
    ///  - des lignes fines violettes reliant les nœuds dans l'ordre de clic, plus une
    ///    ligne pointillée refermant le périmètre à partir de 3 nœuds.
    /// Peu de nœuds (une poignée par périmètre) : dessin direct sur le thread principal,
    /// sans job, après complétion des dépendances du buffer.
    /// </summary>
    public partial class GridRoadOverlaySystem : GameSystemBase
    {
        // Palette calquée sur celle de NetworkTools (sélection violette, survol blanc).
        private static readonly Color SelectedBorder = new Color(0.7f, 0.35f, 1f, 0.9f);
        private static readonly Color SelectedFill = new Color(0.8f, 0.55f, 0.85f, 0.9f);
        private static readonly Color HoverBorder = new Color(1f, 1f, 1f, 0.9f);
        private static readonly Color HoverFill = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color PerimeterLine = new Color(0.7f, 0.35f, 1f, 0.8f);

        private const float CircleOutlineWidth = 0.35f;
        private const float PerimeterLineWidth = 0.6f;
        /// <summary>Diamètre de secours (m) quand la largeur des routes du nœud est inconnue.</summary>
        private const float FallbackDiameter = 10f;

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
            if (nodes.Count == 0 && hovered == Entity.Null)
            {
                return;
            }

            OverlayRenderSystem.Buffer buffer = m_OverlayRenderSystem.GetBuffer(out JobHandle dependencies);
            dependencies.Complete();

            // Périmètre en construction : lignes fines entre nœuds consécutifs,
            // et fermeture en pointillés dès qu'un polygone existe.
            for (int i = 0; i + 1 < positions.Count; i++)
            {
                buffer.DrawLine(PerimeterLine, new Line3.Segment(positions[i], positions[i + 1]), PerimeterLineWidth);
            }
            if (positions.Count >= 3)
            {
                buffer.DrawDashedLine(PerimeterLine,
                    new Line3.Segment(positions[positions.Count - 1], positions[0]),
                    PerimeterLineWidth, 2f, 2f);
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
