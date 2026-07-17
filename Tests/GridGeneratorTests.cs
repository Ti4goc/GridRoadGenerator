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

    /// <summary>
    /// Vérifie le mode culs-de-sac : les collectrices (lignes parallèles à l'axe
    /// principal, "rangées") restent toujours traversantes ; les colonnes
    /// (perpendiculaires) deviennent des impasses selon CulDeSacRatio, avec
    /// alternance haut/bas si Staggered et profondeur CulDeSacDepth.
    ///
    /// Géométrie commune : carré (0,0)-(300,0)-(300,300)-(0,300). Arête la plus
    /// longue (0,0)-(300,0) ⇒ uDir=(1,0) Est (axe principal), vDir=(0,1) Nord.
    /// Columns=1 place une seule colonne (u=150, monde X=150 constant) ; Rows=N
    /// place N collectrices équiréparties (v=const, monde Z=const). Le point monde
    /// d'une colonne à la coordonnée t vaut donc (150, t) — sert à vérifier
    /// précisément les extrémités des impasses.
    /// </summary>
    public class GridGeneratorCulDeSacTests
    {
        private static readonly List<float3> SquareNodes = new List<float3>
        {
            new float3(0f, 0f, 0f),
            new float3(300f, 0f, 0f),
            new float3(300f, 0f, 300f),
            new float3(0f, 0f, 300f),
        };

        private static GridParameters BaseParameters(int rows) => new GridParameters
        {
            Mode = SpacingMode.FitToArea,
            Rows = rows,
            Columns = 1,
            SpacingMeters = 60f,
            CulDeSacMode = true,
            CulDeSacDepth = 0.75f,
            Staggered = true,
            CulDeSacRatio = 100f,
        };

        /// <summary>Segments de la colonne (u-line) uniquement : IsHorizontal=false leur est propre, les collectrices ont IsHorizontal=true.</summary>
        private static List<RoadSegmentDef> ColumnSegments(List<RoadSegmentDef> segments) =>
            segments.Where(s => !s.IsHorizontal).ToList();

        [Fact]
        public void Disabled_ProducesExactlySameOutputAsWithoutCulDeSac()
        {
            var withFlag = BaseParameters(3);
            withFlag.CulDeSacMode = false;
            withFlag.Columns = 3; // reproduit exactement le test de régression 3x3

            var withoutField = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 3,
                Columns = 3,
                SpacingMeters = 60f,
            };

            var segments1 = GridGenerator.GenerateGrid(SquareNodes, withFlag);
            var segments2 = GridGenerator.GenerateGrid(SquareNodes, withoutField);

            Assert.Equal(24, segments1.Count);
            Assert.Equal(segments2.Count, segments1.Count);
        }

        [Fact]
        public void Ratio100_NoColumnBlockRemainsFullLength()
        {
            // Rows=2 : collectrices à v=100 et v=200. La colonne u=150 traverse
            // [0,300] ⇒ chaîne [0(bord),100(collectrice),200(collectrice),300(bord)]
            // ⇒ 3 blocs. Bloc [0,100] : départ=bord (pas de vraie collectrice) ⇒
            // supprimé. Bloc [100,200] : alternance impaire ⇒ part de 200 vers 100,
            // s'arrête à 200+(100-200)*0.75=125. Bloc [200,300] : alternance paire
            // ⇒ part de 200 vers 300, s'arrête à 200+(300-200)*0.75=275.
            var parameters = BaseParameters(2);

            var segments = GridGenerator.GenerateGrid(SquareNodes, parameters);
            var columns = ColumnSegments(segments);

            Assert.Equal(2, columns.Count); // le bloc de bord est supprimé

            foreach (var segment in columns)
            {
                float length = math.distance(segment.Start.xz, segment.End.xz);
                Assert.True(length < 100f - 1f, $"Impasse pas plus courte que le bloc complet (100 m) : {length}");
                Assert.Equal(75f, length, 1); // 0.75 * 100
            }

            // Les deux impasses partent bien de la collectrice centrale (v=200,
            // monde (150,200)) et s'arrêtent chacune à 75 m dans une direction opposée.
            var endpoints = columns.SelectMany(s => new[] { s.Start, s.End }).ToList();
            Assert.Contains(endpoints, p => math.distance(p.xz, new float2(150f, 200f)) < 0.5f);
            Assert.Contains(endpoints, p => math.distance(p.xz, new float2(150f, 125f)) < 0.5f);
            Assert.Contains(endpoints, p => math.distance(p.xz, new float2(150f, 275f)) < 0.5f);
        }

        [Fact]
        public void StaggeredTrueVersusFalse_ProduceDifferentStartingCollectors()
        {
            // Même géométrie que Ratio100_NoColumnBlockRemainsFullLength. Sans
            // alternance, le bloc [100,200] doit partir de la collectrice basse
            // (v=100) au lieu de la haute (v=200).
            var staggered = BaseParameters(2);
            var notStaggered = BaseParameters(2);
            notStaggered.Staggered = false;

            var columnsStaggered = ColumnSegments(GridGenerator.GenerateGrid(SquareNodes, staggered));
            var columnsPlain = ColumnSegments(GridGenerator.GenerateGrid(SquareNodes, notStaggered));

            var staggeredEndpoints = columnsStaggered.SelectMany(s => new[] { s.Start, s.End }).ToList();
            var plainEndpoints = columnsPlain.SelectMany(s => new[] { s.Start, s.End }).ToList();

            // Avec Staggered=true, une impasse part de la collectrice haute (v=200)
            // vers le bas et s'arrête à v=125.
            Assert.Contains(staggeredEndpoints, p => math.distance(p.xz, new float2(150f, 125f)) < 0.5f);
            // Avec Staggered=false, le même bloc part de la collectrice basse (v=100)
            // vers le haut et s'arrête à v=175 (100+(200-100)*0.75) — un résultat différent.
            Assert.Contains(plainEndpoints, p => math.distance(p.xz, new float2(150f, 175f)) < 0.5f);
            Assert.DoesNotContain(plainEndpoints, p => math.distance(p.xz, new float2(150f, 125f)) < 0.5f);
        }

        [Fact]
        public void Ratio50_AlternatesThroughAndCulDeSacBlocksDeterministically()
        {
            // Rows=4 : collectrices à v=60,120,180,240 ⇒ chaîne à 6 points, 5 blocs
            // de 60 m chacun (idx 0..4). Motif "une fois sur 2" (ratio=50, N=2) :
            // blocs pairs (0,2,4) tentent une impasse, impairs (1,3) restent
            // traversants. Le bloc 0 est en bord (supprimé) ; 2 et 4 réussissent
            // (départ = vraie collectrice) ⇒ 2 impasses (45 m = 0.75*60) + 2
            // traversées complètes (60 m) survivent, soit 4 segments de colonne.
            var parameters = BaseParameters(4);
            parameters.CulDeSacRatio = 50f;

            var columns = ColumnSegments(GridGenerator.GenerateGrid(SquareNodes, parameters));

            Assert.Equal(4, columns.Count);

            int fullLength = columns.Count(s => math.distance(s.Start.xz, s.End.xz) > 59f);
            int stubLength = columns.Count(s =>
            {
                float length = math.distance(s.Start.xz, s.End.xz);
                return length > 44f && length < 46f;
            });

            Assert.Equal(2, fullLength); // blocs 1 et 3 : traversants (60 m)
            Assert.Equal(2, stubLength); // blocs 2 et 4 : impasses (45 m)
        }

        [Fact]
        public void DepthOutOfRange_IsClampedToValidBounds()
        {
            // CulDeSacDepth hors [0.5, 0.9] (ex. valeur non passée par l'UI/settings) :
            // clampé plutôt que de produire une impasse dégénérée (longueur nulle ou négative).
            var tooLow = BaseParameters(2);
            tooLow.CulDeSacDepth = 0f;
            var tooHigh = BaseParameters(2);
            tooHigh.CulDeSacDepth = 1f;

            var columnsLow = ColumnSegments(GridGenerator.GenerateGrid(SquareNodes, tooLow));
            var columnsHigh = ColumnSegments(GridGenerator.GenerateGrid(SquareNodes, tooHigh));

            foreach (var segment in columnsLow.Concat(columnsHigh))
            {
                float length = math.distance(segment.Start.xz, segment.End.xz);
                Assert.InRange(length, 50f - 1f, 90f + 1f); // 0.5..0.9 * bloc de 100 m
            }
        }
    }
}
