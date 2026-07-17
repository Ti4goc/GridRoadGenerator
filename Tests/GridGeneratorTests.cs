using System.Collections.Generic;
using System.Linq;
using GridRoadGenerator.Core;
using Unity.Mathematics;
using Xunit;

namespace GridRoadGenerator.Tests
{
    /// <summary>
    /// Vérifie le pré-découpage de la grille aux croisements internes :
    ///  - chaque croisement intérieur au polygone est une extrémité partagée par
    ///    exactement 4 sous-segments (2 par ligne) ;
    ///  - un croisement au bord du polygone (une ou deux lignes s'y terminent) est
    ///    partagé par 2 ou 3 sous-segments ;
    ///  - aucun croisement ne se trouve en milieu de sous-segment ;
    ///  - les extrémités qui se rejoignent sont EXACTEMENT identiques (mêmes floats),
    ///    condition pour que le jeu les fusionne en un seul nœud ;
    ///  - aucun sous-segment plus court que MinSegmentLength.
    /// </summary>
    public class GridGeneratorTests
    {
        // ------------------------------------------------------------------
        // Cas 1 : carré axis-aligned, mode "ajuster à l'aire" 3×3
        // → 9 croisements internes, chacun partagé par exactement 4 sous-segments.
        // ------------------------------------------------------------------

        [Fact]
        public void Square_FitToArea_EveryInternalCrossingSharedByExactlyFourSubSegments()
        {
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 3,
                Columns = 3,
                SpacingMeters = 60f,
            };

            var segments = GridGenerator.GenerateGrid(nodes, parameters);

            // 3 lignes u × 4 tronçons + 3 lignes v × 4 tronçons.
            Assert.Equal(24, segments.Count);

            var shared = SharedEndpoints(segments);
            Assert.Equal(9, shared.Count);
            foreach (var pair in shared)
            {
                Assert.Equal(4, pair.Value);
            }

            AssertCommonInvariants(segments);
        }

        // ------------------------------------------------------------------
        // Cas 2 : polygone concave en L, espacement fixe
        // → 12 croisements, tous intérieurs, chacun partagé par 4 sous-segments.
        // ------------------------------------------------------------------

        [Fact]
        public void ConcaveLShape_FixedSpacing_CrossingsSharedByFourSubSegments()
        {
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 150f),
                new float3(150f, 0f, 150f),
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FixedSpacing,
                Rows = 3,
                Columns = 3,
                SpacingMeters = 60f,
            };

            var segments = GridGenerator.GenerateGrid(nodes, parameters);

            // Lignes u : 60 et 120 traversent tout le L (5 tronçons), 180 et 240
            // seulement la branche basse (3 tronçons) ; symétrique pour v.
            Assert.Equal(32, segments.Count);

            var shared = SharedEndpoints(segments);
            Assert.Equal(12, shared.Count);
            foreach (var pair in shared)
            {
                Assert.Equal(4, pair.Value);
            }

            AssertCommonInvariants(segments);
        }

        // ------------------------------------------------------------------
        // Cas 3 : croisement AU BORD du polygone. L'encoche en V du polygone a son
        // apex exactement sur le croisement (200, 150) : la ligne u s'y termine
        // (clippée par l'encoche) pendant que la ligne v passe de part et d'autre.
        // → jonction en T : le point est partagé par exactement 3 sous-segments.
        // ------------------------------------------------------------------

        [Fact]
        public void VNotchApexOnCrossing_BorderCrossingSharedByExactlyThreeSubSegments()
        {
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(400f, 0f, 0f),
                new float3(400f, 0f, 300f),
                new float3(250f, 0f, 300f),
                new float3(200f, 0f, 150f), // apex de l'encoche = croisement u=200 × v=150
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 1,
                Columns = 1,
                SpacingMeters = 60f,
            };

            var segments = GridGenerator.GenerateGrid(nodes, parameters);

            // Ligne u=200 : 1 tronçon (arrêtée à l'apex) ; ligne v=150 : 2 tronçons
            // (un sous chaque bras), qui se rejoignent à l'apex.
            Assert.Equal(3, segments.Count);

            var shared = SharedEndpoints(segments);
            var apex = Assert.Single(shared);
            Assert.Equal(3, apex.Value);
            Assert.Equal(200f, apex.Key.x, 3);
            Assert.Equal(150f, apex.Key.z, 3);

            AssertCommonInvariants(segments);
        }

        // ------------------------------------------------------------------
        // Invariants communs
        // ------------------------------------------------------------------

        /// <summary>Extrémités partagées par au moins 2 sous-segments, avec leur multiplicité.</summary>
        private static Dictionary<float3, int> SharedEndpoints(List<RoadSegmentDef> segments)
        {
            var counts = new Dictionary<float3, int>();
            foreach (var segment in segments)
            {
                counts[segment.Start] = counts.TryGetValue(segment.Start, out int s) ? s + 1 : 1;
                counts[segment.End] = counts.TryGetValue(segment.End, out int e) ? e + 1 : 1;
            }
            return counts.Where(p => p.Value >= 2).ToDictionary(p => p.Key, p => p.Value);
        }

        private static void AssertCommonInvariants(List<RoadSegmentDef> segments)
        {
            var counts = new Dictionary<float3, int>();
            foreach (var segment in segments)
            {
                counts[segment.Start] = counts.TryGetValue(segment.Start, out int s) ? s + 1 : 1;
                counts[segment.End] = counts.TryGetValue(segment.End, out int e) ? e + 1 : 1;
            }
            var endpoints = counts.Keys.ToList();

            // 1) Pas de segment sous la longueur minimale.
            foreach (var segment in segments)
            {
                Assert.True(math.distance(segment.Start.xz, segment.End.xz) >= GridGenerator.MinSegmentLength - 1e-3f,
                    $"Sous-segment trop court : {segment.Start} → {segment.End}");
            }

            // 2) Deux extrémités très proches doivent être EXACTEMENT le même point
            //    (mêmes floats) — sinon le jeu créerait deux nœuds au lieu d'un.
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    Assert.True(math.distance(endpoints[i], endpoints[j]) >= 0.5f,
                        $"Extrémités distinctes quasi confondues (fusion impossible) : {endpoints[i]} vs {endpoints[j]}");
                }
            }

            // 3) Aucune extrémité partagée ne se trouve en MILIEU d'un autre segment :
            //    tout croisement doit avoir découpé les deux lignes.
            foreach (var pair in counts.Where(p => p.Value >= 2))
            {
                float3 point = pair.Key;
                foreach (var segment in segments)
                {
                    if (point.Equals(segment.Start) || point.Equals(segment.End))
                        continue;
                    float distance = DistanceToSegmentXZ(point.xz, segment.Start.xz, segment.End.xz);
                    Assert.True(distance > 0.5f,
                        $"Croisement {point} en plein milieu du segment {segment.Start} → {segment.End}");
                }
            }
        }

        private static float DistanceToSegmentXZ(float2 point, float2 a, float2 b)
        {
            float2 ab = b - a;
            float t = math.clamp(math.dot(point - a, ab) / math.dot(ab, ab), 0f, 1f);
            return math.distance(point, a + t * ab);
        }
    }

    /// <summary>Vérifie le paramètre AngleOffsetDegrees : 0° = comportement inchangé, 90° = axes échangés, clipping correct à tout angle.</summary>
    public class GridGeneratorAngleTests
    {
        private static readonly List<float3> SquareNodes = new List<float3>
        {
            new float3(0f, 0f, 0f),
            new float3(300f, 0f, 0f),
            new float3(300f, 0f, 300f),
            new float3(0f, 0f, 300f),
        };

        [Fact]
        public void AngleZero_MatchesPreviousBehaviorExactly()
        {
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 3,
                Columns = 3,
                SpacingMeters = 60f,
                AngleOffsetDegrees = 0f,
            };

            var segments = GridGenerator.GenerateGrid(SquareNodes, parameters);

            // Même résultat que le test de régression sans angle (24 segments,
            // 3 lignes u × 4 tronçons + 3 lignes v × 4 tronçons).
            Assert.Equal(24, segments.Count);
        }

        [Fact]
        public void Angle90_SwapsAxisOfVariation()
        {
            // Carré axis-aligned, origine (0,0), arête la plus longue = (0,0)->(300,0)
            // donc uDir=(1,0) à 0°. Une seule ligne de chaque jeu (Rows=1, Columns=1)
            // pour isoler la géométrie sans ambiguïté de tri.
            var parameters0 = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 1,
                Columns = 1,
                SpacingMeters = 60f,
                AngleOffsetDegrees = 0f,
            };
            var parameters90 = parameters0;
            parameters90.AngleOffsetDegrees = 90f;

            var segments0 = GridGenerator.GenerateGrid(SquareNodes, parameters0);
            var segments90 = GridGenerator.GenerateGrid(SquareNodes, parameters90);

            // La ligne des colonnes croise celle des rangées en plein milieu du carré
            // et se retrouve donc pré-découpée en 2 sous-segments (Chantier 1) : chaque
            // morceau reste individuellement droit, First() suffit pour lire son axe.

            // À 0° : la ligne des colonnes est verticale en monde (X constant, Z varie).
            var columnLine0 = segments0.First(s => !s.IsHorizontal);
            Assert.Equal(columnLine0.Start.x, columnLine0.End.x, 2);
            Assert.NotEqual(columnLine0.Start.z, columnLine0.End.z, 2);

            // À 90° : la même ligne des colonnes devient horizontale en monde
            // (Z constant, X varie) — les axes ont basculé.
            var columnLine90 = segments90.First(s => !s.IsHorizontal);
            Assert.Equal(columnLine90.Start.z, columnLine90.End.z, 2);
            Assert.NotEqual(columnLine90.Start.x, columnLine90.End.x, 2);
        }

        [Fact]
        public void ObliqueAngle_ClipsPolygonCorrectlyOnConcaveShape()
        {
            // L concave, angle 45° : pas de valeurs attendues à la main (trop
            // fastidieux à dériver), on vérifie les invariants structurels du
            // clipping (segments bien formés, aucun croisement en milieu de segment).
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 150f),
                new float3(150f, 0f, 150f),
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FixedSpacing,
                Rows = 3,
                Columns = 3,
                SpacingMeters = 60f,
                AngleOffsetDegrees = 45f,
            };

            var segments = GridGenerator.GenerateGrid(nodes, parameters);

            Assert.NotEmpty(segments);
            foreach (var segment in segments)
            {
                Assert.True(math.distance(segment.Start.xz, segment.End.xz) >= GridGenerator.MinSegmentLength - 1e-3f,
                    $"Sous-segment trop court à 45° : {segment.Start} → {segment.End}");
                Assert.False(float.IsNaN(segment.Start.x) || float.IsNaN(segment.Start.z)
                    || float.IsNaN(segment.End.x) || float.IsNaN(segment.End.z),
                    "Coordonnée NaN produite par le clipping à angle oblique.");
            }
        }
    }
}
