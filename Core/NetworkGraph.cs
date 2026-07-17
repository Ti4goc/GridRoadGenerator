using System.Collections.Generic;
using Unity.Mathematics;

namespace GridRoadGenerator.Core
{
    /// <summary>
    /// Arête sortante d'un nœud du graphe réseau : le voisin atteint, le coût pour
    /// l'algorithme de plus court chemin (longueur de l'arête) et la direction de
    /// départ du nœud vers ce voisin (utilisée par le suivi de contour).
    /// </summary>
    public readonly struct GraphEdge<TNode>
    {
        public readonly TNode Neighbor;
        public readonly float Cost;
        public readonly float2 Direction;

        public GraphEdge(TNode neighbor, float cost, float2 direction)
        {
            Neighbor = neighbor;
            Cost = cost;
            Direction = direction;
        }
    }

    /// <summary>
    /// Abstraction du réseau routier existant, indépendante du moteur ECS : permet de
    /// tester les algorithmes de graphe (plus court chemin, suivi de contour) sur un
    /// graphe minimal en mémoire, et de les brancher sur les entités du jeu via un
    /// adaptateur (voir GridRoadToolSystem.EcsNetworkGraph).
    /// </summary>
    public interface INetworkGraph<TNode>
    {
        /// <summary>Arêtes sortantes du nœud. Une nouvelle liste à chaque appel.</summary>
        IReadOnlyList<GraphEdge<TNode>> GetNeighbors(TNode node);
    }

    /// <summary>
    /// Algorithmes de graphe purs utilisés par la sélection intelligente du périmètre :
    /// remplissage automatique du chemin entre deux clics (comportement 1), et suivi du
    /// contour fermé au double-clic (comportement 2).
    /// </summary>
    public static class NetworkGraphAlgorithms
    {
        /// <summary>
        /// Plus court chemin (Dijkstra, coût = somme des longueurs d'arêtes) entre deux
        /// nœuds du réseau EXISTANT. Retourne false si aucun chemin n'existe (réseaux
        /// déconnectés) ou si la recherche dépasse maxVisitedNodes (garde-fou sur un
        /// très grand réseau). outPath inclut start et end.
        /// </summary>
        public static bool FindShortestPath<TNode>(INetworkGraph<TNode> graph, TNode start, TNode end,
            List<TNode> outPath, int maxVisitedNodes = 2000)
        {
            outPath.Clear();
            var comparer = EqualityComparer<TNode>.Default;
            if (comparer.Equals(start, end))
            {
                outPath.Add(start);
                return true;
            }

            var visited = new HashSet<TNode>();
            var cameFrom = new Dictionary<TNode, TNode>();
            var costSoFar = new Dictionary<TNode, float> { [start] = 0f };
            var frontier = new List<TNode> { start };

            while (frontier.Count > 0)
            {
                // Extraction du nœud de coût minimal (liste + scan linéaire : la borne
                // maxVisitedNodes garde ça largement assez rapide pour un usage au clic).
                int bestIndex = 0;
                for (int i = 1; i < frontier.Count; i++)
                {
                    if (costSoFar[frontier[i]] < costSoFar[frontier[bestIndex]])
                    {
                        bestIndex = i;
                    }
                }
                TNode current = frontier[bestIndex];
                frontier.RemoveAt(bestIndex);

                if (!visited.Add(current))
                {
                    continue;
                }
                if (comparer.Equals(current, end))
                {
                    break;
                }
                if (visited.Count > maxVisitedNodes)
                {
                    return false;
                }

                foreach (GraphEdge<TNode> edge in graph.GetNeighbors(current))
                {
                    if (visited.Contains(edge.Neighbor))
                    {
                        continue;
                    }
                    float cost = costSoFar[current] + edge.Cost;
                    if (!costSoFar.TryGetValue(edge.Neighbor, out float existing) || cost < existing)
                    {
                        costSoFar[edge.Neighbor] = cost;
                        cameFrom[edge.Neighbor] = current;
                        frontier.Add(edge.Neighbor);
                    }
                }
            }

            if (!visited.Contains(end))
            {
                return false;
            }

            var reversed = new List<TNode>();
            TNode node = end;
            while (!comparer.Equals(node, start))
            {
                reversed.Add(node);
                if (!cameFrom.TryGetValue(node, out node))
                {
                    return false; // ne devrait pas arriver : chemin incohérent
                }
            }
            reversed.Add(start);
            reversed.Reverse();
            outPath.AddRange(reversed);
            return true;
        }

        /// <summary>
        /// Suit le contour fermé du réseau à partir de startNode, en tournant
        /// systématiquement le plus possible vers la droite (angle horaire minimal
        /// depuis la direction inverse de l'arête empruntée) à chaque intersection —
        /// algorithme classique de suivi de face sur un graphe planaire ("hug the right
        /// wall"). outLoop contient startNode puis les nœuds du contour, dans l'ordre,
        /// SANS répéter startNode à la fin. Retourne false proprement (jamais de
        /// plantage ni de blocage) si :
        ///  - startNode n'a aucune arête ;
        ///  - un nœud du contour n'a aucune autre arête que celle empruntée (impasse) ;
        ///  - le contour revisite un nœud déjà parcouru sans revenir à startNode
        ///    (pas de boucle fermée simple depuis ce point) ;
        ///  - la recherche dépasse maxNodes (garde-fou).
        /// </summary>
        public static bool TraceBoundary<TNode>(INetworkGraph<TNode> graph, TNode startNode,
            List<TNode> outLoop, int maxNodes = 50)
        {
            outLoop.Clear();
            var comparer = EqualityComparer<TNode>.Default;

            IReadOnlyList<GraphEdge<TNode>> initialNeighbors = graph.GetNeighbors(startNode);
            if (initialNeighbors.Count == 0)
            {
                return false;
            }

            // Premier pas : direction déterministe indépendante de l'ordre du graphe
            // (angle horaire minimal depuis le nord, +Y) — seule la géométrie compte,
            // pour que le résultat ne dépende pas de l'ordre d'itération de l'adaptateur.
            GraphEdge<TNode> firstEdge = PickSmallestClockwiseAngle(initialNeighbors, new float2(0f, 1f));

            outLoop.Add(startNode);
            var visited = new HashSet<TNode> { startNode };

            TNode current = startNode;
            float2 arrivalDirection = firstEdge.Direction;
            TNode next = firstEdge.Neighbor;

            for (int step = 0; step < maxNodes; step++)
            {
                if (comparer.Equals(next, startNode))
                {
                    return true; // boucle refermée sur le point de départ
                }
                if (!visited.Add(next))
                {
                    return false; // reboucle sur un nœud déjà visité sans revenir au départ
                }
                outLoop.Add(next);

                var candidates = new List<GraphEdge<TNode>>();
                foreach (GraphEdge<TNode> edge in graph.GetNeighbors(next))
                {
                    // Exclut le nœud dont on vient (pas de demi-tour immédiat sur la
                    // même arête). Simplification : par identité de voisin, pas
                    // d'arête — insuffisant seulement en cas d'arêtes parallèles
                    // multiples entre les deux mêmes nœuds, cas non traité ici.
                    if (!comparer.Equals(edge.Neighbor, current))
                    {
                        candidates.Add(edge);
                    }
                }
                if (candidates.Count == 0)
                {
                    return false; // impasse : aucune autre arête que celle empruntée
                }

                float2 refDirection = -arrivalDirection;
                GraphEdge<TNode> chosen = PickSmallestClockwiseAngle(candidates, refDirection);

                current = next;
                arrivalDirection = chosen.Direction;
                next = chosen.Neighbor;
            }

            return false; // garde-fou : trop de nœuds sans refermer la boucle
        }

        /// <summary>Parmi les candidats, celui dont la direction demande la plus petite rotation horaire depuis referenceDirection.</summary>
        private static GraphEdge<TNode> PickSmallestClockwiseAngle<TNode>(
            IReadOnlyList<GraphEdge<TNode>> candidates, float2 referenceDirection)
        {
            GraphEdge<TNode> best = candidates[0];
            float bestAngle = ClockwiseAngle(referenceDirection, best.Direction);
            for (int i = 1; i < candidates.Count; i++)
            {
                float angle = ClockwiseAngle(referenceDirection, candidates[i].Direction);
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidates[i];
                }
            }
            return best;
        }

        /// <summary>Rotation horaire (radians, [0, 2π)) à appliquer à `from` pour l'amener sur `to`.</summary>
        private static float ClockwiseAngle(float2 from, float2 to)
        {
            const float TwoPi = 2f * math.PI;
            float theta = math.atan2(from.y, from.x) - math.atan2(to.y, to.x);
            theta %= TwoPi;
            if (theta < 0f)
            {
                theta += TwoPi;
            }
            return theta;
        }
    }
}
