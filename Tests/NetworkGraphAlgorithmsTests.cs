using System.Collections.Generic;
using GridRoadGenerator.Core;
using Unity.Mathematics;
using Xunit;

namespace GridRoadGenerator.Tests
{
    /// <summary>
    /// Graphe en mémoire pour les tests : nœuds entiers, arêtes explicites avec
    /// position 2D (sert à calculer coût et direction, comme le ferait l'adaptateur ECS).
    /// </summary>
    internal sealed class DictGraph : INetworkGraph<int>
    {
        private readonly Dictionary<int, float2> _positions = new Dictionary<int, float2>();
        private readonly Dictionary<int, List<int>> _adjacency = new Dictionary<int, List<int>>();

        public void AddNode(int id, float2 position)
        {
            _positions[id] = position;
            if (!_adjacency.ContainsKey(id))
            {
                _adjacency[id] = new List<int>();
            }
        }

        private readonly Dictionary<(int, int), float> _costOverrides = new Dictionary<(int, int), float>();

        /// <summary>Arête non orientée entre deux nœuds déjà ajoutés (coût = distance euclidienne).</summary>
        public void AddEdge(int a, int b)
        {
            _adjacency[a].Add(b);
            _adjacency[b].Add(a);
        }

        /// <summary>
        /// Arête non orientée avec un coût explicite, différent de la distance
        /// euclidienne (simule une route qui serpente : sa longueur réelle, comme
        /// Curve.m_Length côté jeu, peut dépasser la distance à vol d'oiseau).
        /// </summary>
        public void AddEdgeWithCost(int a, int b, float cost)
        {
            AddEdge(a, b);
            _costOverrides[(a, b)] = cost;
            _costOverrides[(b, a)] = cost;
        }

        public IReadOnlyList<GraphEdge<int>> GetNeighbors(int node)
        {
            var result = new List<GraphEdge<int>>();
            float2 from = _positions[node];
            foreach (int neighbor in _adjacency[node])
            {
                float2 delta = _positions[neighbor] - from;
                float length = math.length(delta);
                float2 direction = length > 1e-6f ? delta / length : new float2(1f, 0f);
                float cost = _costOverrides.TryGetValue((node, neighbor), out float overridden) ? overridden : length;
                result.Add(new GraphEdge<int>(neighbor, cost, direction));
            }
            return result;
        }
    }

    public class NetworkGraphAlgorithmsTests
    {
        // ------------------------------------------------------------------
        // Comportement 1 : remplissage automatique du chemin (FindShortestPath)
        // ------------------------------------------------------------------

        [Fact]
        public void FindShortestPath_IntermediateNodeBetweenAAndC_IsInsertedInOrder()
        {
            // A --- B --- C, en ligne : le plus court chemin de A à C passe par B.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));    // A
            graph.AddNode(2, new float2(50f, 0f));   // B
            graph.AddNode(3, new float2(100f, 0f));  // C
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);

            var path = new List<int>();
            bool found = NetworkGraphAlgorithms.FindShortestPath(graph, 1, 3, path);

            Assert.True(found);
            Assert.Equal(new[] { 1, 2, 3 }, path);
        }

        [Fact]
        public void FindShortestPath_DisconnectedNodes_ReturnsFalse()
        {
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));
            graph.AddNode(2, new float2(100f, 0f)); // aucune arête vers 1

            var path = new List<int>();
            bool found = NetworkGraphAlgorithms.FindShortestPath(graph, 1, 2, path);

            Assert.False(found);
            Assert.Empty(path);
        }

        [Fact]
        public void FindShortestPath_PrefersLowerCostRouteOverGeometricallyDirectOne()
        {
            // A-C existe directement mais serpente (coût 200, comme une route
            // détournée) ; A-B-C ne coûte que 100 au total : Dijkstra doit choisir
            // le détour par B malgré l'arête directe.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));     // A
            graph.AddNode(2, new float2(50f, 0f));    // B
            graph.AddNode(3, new float2(100f, 0f));   // C
            graph.AddEdge(1, 2);                      // coût 50
            graph.AddEdge(2, 3);                       // coût 50 => 100 via B
            graph.AddEdgeWithCost(1, 3, 200f);         // liaison directe mais coûteuse

            var path = new List<int>();
            bool found = NetworkGraphAlgorithms.FindShortestPath(graph, 1, 3, path);

            Assert.True(found);
            Assert.Equal(new[] { 1, 2, 3 }, path);
        }

        // ------------------------------------------------------------------
        // Comportement 2 : suivi de contour (TraceBoundary)
        // ------------------------------------------------------------------

        [Fact]
        public void TraceBoundary_ClosedRectangle_ReturnsFourCornersInOrder()
        {
            // Rectangle A(0,0) B(100,0) C(100,100) D(0,100), refermé A-B-C-D-A.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));     // A
            graph.AddNode(2, new float2(100f, 0f));   // B
            graph.AddNode(3, new float2(100f, 100f)); // C
            graph.AddNode(4, new float2(0f, 100f));   // D
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 1);

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop);

            Assert.True(found);
            Assert.Equal(4, loop.Count);
            // Ordre déterministe pour cette géométrie (angle horaire minimal depuis le
            // nord au premier pas) : A -> D -> C -> B, qui referme sur A.
            Assert.Equal(new[] { 1, 4, 3, 2 }, loop);
        }

        [Fact]
        public void TraceBoundary_DeadEnd_FailsCleanly()
        {
            // Chemin ouvert A - B - C, pas de boucle : le double-clic sur A doit
            // échouer proprement (pas de plantage, pas de blocage).
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));   // A
            graph.AddNode(2, new float2(50f, 0f));  // B
            graph.AddNode(3, new float2(50f, 50f)); // C
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop);

            Assert.False(found);
        }

        [Fact]
        public void TraceBoundary_StartNodeWithoutEdges_FailsCleanly()
        {
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f)); // nœud isolé

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop);

            Assert.False(found);
        }

        [Fact]
        public void TraceBoundary_SpurOffATriangle_FailsCleanlyInsteadOfLoopingForever()
        {
            // A pend de B (impasse), B-C-D forment un triangle qui ne repasse jamais
            // par A : démontre que le garde-fou "nœud déjà visité" empêche un blocage
            // silencieux quand le point de départ n'appartient à aucune boucle.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));     // A (spur)
            graph.AddNode(2, new float2(50f, 0f));    // B
            graph.AddNode(3, new float2(100f, 50f));  // C
            graph.AddNode(4, new float2(0f, 50f));    // D
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 2);

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop, maxNodes: 50);

            Assert.False(found);
        }

        [Fact]
        public void TraceBoundary_DeadEndSpurThatWinsTheAngleTiebreak_IsIgnoredInFavorOfTheMainLoop()
        {
            // Carré A-B-C-D-A ; un beco sem saída E pend de B, positionné pour "gagner"
            // l'angle horaire minimal contre la continuation C (90° contre 270° depuis la
            // référence d'arrivée en B) — sans la correction, le suivi partait dans E, une
            // vraie impasse (0 arête restante), et échouait complètement au lieu de continuer
            // sur le vrai anneau. Avec la correction (IgnoreShortDetours), E est reconnu comme
            // un détour court (impasse simple, 10 m) et écarté au profit de C.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));     // A
            graph.AddNode(2, new float2(0f, 100f));   // B
            graph.AddNode(3, new float2(100f, 100f)); // C
            graph.AddNode(4, new float2(100f, 0f));   // D
            graph.AddNode(5, new float2(-10f, 100f)); // E (beco sem saída pendu à B)
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 1);
            graph.AddEdge(2, 5);

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop);

            Assert.True(found);
            Assert.Equal(new[] { 1, 2, 3, 4 }, loop);
        }

        [Fact]
        public void TraceBoundary_TurnaroundLoopThatWinsTheAngleTiebreak_IsIgnoredInFavorOfTheMainLoop()
        {
            // Même carré A-B-C-D-A, mais cette fois B-E-F-B forme une petite boucle de
            // retournement (un rond-point en bout de beco sem saída, comme le "cul-de-sac"
            // du jeu) au lieu d'une simple impasse. Sans la correction, le suivi entrait dans
            // E, faisait le tour jusqu'à F, puis rebouclait sur B (déjà visité, pas le point de
            // départ A) — échec. Avec la correction, E et F sont tous deux reconnus comme un
            // détour court qui referme sur B (46,5 m cumulés) et écartés au profit de C.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));      // A
            graph.AddNode(2, new float2(0f, 100f));    // B
            graph.AddNode(3, new float2(100f, 100f));  // C
            graph.AddNode(4, new float2(100f, 0f));    // D
            graph.AddNode(5, new float2(-10f, 100f));  // E
            graph.AddNode(6, new float2(-20f, 110f));  // F
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 1);
            graph.AddEdge(2, 5);
            graph.AddEdge(5, 6);
            graph.AddEdge(6, 2);

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop);

            Assert.True(found);
            Assert.Equal(new[] { 1, 2, 3, 4 }, loop);
        }

        [Fact]
        public void TraceBoundary_LongDeadEndSpur_StillFailsCleanlyInsteadOfBeingSilentlyIgnored()
        {
            // Même configuration que le beco sem saída simple ci-dessus, mais à 160 m de B
            // (au-delà de ShortDetourMaxLength=150) : ce n'est plus un détour court, donc il
            // continue de rivaliser normalement pour l'angle horaire minimal (et le gagne,
            // même configuration géométrique) — le suivi part dedans et échoue proprement en
            // trouvant une impasse, exactement comme avant la correction. Garde-fou : la
            // correction ne doit jamais masquer un vrai problème de sélection manuelle en
            // ignorant indistinctement toute branche.
            var graph = new DictGraph();
            graph.AddNode(1, new float2(0f, 0f));      // A
            graph.AddNode(2, new float2(0f, 100f));    // B
            graph.AddNode(3, new float2(100f, 100f));  // C
            graph.AddNode(4, new float2(100f, 0f));    // D
            graph.AddNode(5, new float2(-160f, 100f)); // E, beco sem saída long (160 m)
            graph.AddEdge(1, 2);
            graph.AddEdge(2, 3);
            graph.AddEdge(3, 4);
            graph.AddEdge(4, 1);
            graph.AddEdge(2, 5);

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 1, loop);

            Assert.False(found);
        }

        [Fact]
        public void TraceBoundary_ExceedsMaxNodes_FailsCleanly()
        {
            // Longue chaîne fermée en boucle mais avec une limite de nœuds trop basse
            // pour la parcourir entièrement : le garde-fou doit stopper proprement.
            var graph = new DictGraph();
            const int ringSize = 20;
            for (int i = 0; i < ringSize; i++)
            {
                float angle = 2f * math.PI * i / ringSize;
                graph.AddNode(i, new float2(math.cos(angle) * 100f, math.sin(angle) * 100f));
            }
            for (int i = 0; i < ringSize; i++)
            {
                graph.AddEdge(i, (i + 1) % ringSize);
            }

            var loop = new List<int>();
            bool found = NetworkGraphAlgorithms.TraceBoundary(graph, 0, loop, maxNodes: 5);

            Assert.False(found);
        }
    }
}
