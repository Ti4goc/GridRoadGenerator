using System;
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

        internal static void AssertCommonInvariants(List<RoadSegmentDef> segments)
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
        public void CulDeSacBlock_MarksOnlyTheFreeEndAsCulDeSacEnd()
        {
            // Même géométrie que Ratio100_NoColumnBlockRemainsFullLength : 2 impasses,
            // chacune partant d'une vraie collectrice (Start, jamais marqué) et
            // s'arrêtant à son bout libre (End, doit être marqué IsCulDeSacEnd).
            var segments = GridGenerator.GenerateGrid(SquareNodes, BaseParameters(2));
            var columns = ColumnSegments(segments);

            Assert.Equal(2, columns.Count);
            foreach (var segment in columns)
            {
                Assert.True(segment.IsCulDeSacEnd, "Le bout libre d'une impasse doit être marqué IsCulDeSacEnd.");
            }

            // Les collectrices traversantes (lignes horizontales) ne sont jamais des impasses.
            var collectors = segments.Where(s => s.IsHorizontal).ToList();
            Assert.NotEmpty(collectors);
            Assert.All(collectors, s => Assert.False(s.IsCulDeSacEnd));
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

        [Fact]
        public void AxisRows_AppliesCulDeSacToRowsInsteadOfColumns()
        {
            // Transposé de Ratio100_NoColumnBlockRemainsFullLength : Columns=2 (au lieu de
            // Rows=2) fournit les 2 croisements internes à l'UNIQUE ligne de rangée
            // (Rows=1), qui devient la candidate aux impasses avec CulDeSacAxis.Rows.
            var parameters = BaseParameters(1);
            parameters.Columns = 2;
            parameters.CulDeSacAxis = CulDeSacAxis.Rows;

            var segments = GridGenerator.GenerateGrid(SquareNodes, parameters);
            var columns = ColumnSegments(segments); // axisIsU (colonnes) : ne doivent JAMAIS être des impasses ici.
            var rows = segments.Except(columns).ToList();

            Assert.NotEmpty(columns);
            Assert.All(columns, s => Assert.False(s.IsCulDeSacEnd));
            Assert.Contains(rows, s => s.IsCulDeSacEnd);
        }

        [Fact]
        public void AxisBoth_AppliesCulDeSacToBothColumnsAndRows()
        {
            // Rows=2 ET Columns=2 : les deux axes ont des croisements internes à
            // proposer comme collectrices de départ pour l'autre axe.
            var parameters = BaseParameters(2);
            parameters.Columns = 2;
            parameters.CulDeSacAxis = CulDeSacAxis.Both;

            var segments = GridGenerator.GenerateGrid(SquareNodes, parameters);
            var columns = ColumnSegments(segments);
            var rows = segments.Except(columns).ToList();

            Assert.Contains(columns, s => s.IsCulDeSacEnd);
            Assert.Contains(rows, s => s.IsCulDeSacEnd);
        }
    }

    /// <summary>
    /// Vérifie MinNodeDistance : un nœud généré (croisement, ou bout d'impasse) qui
    /// tomberait à moins de MinNodeDistance d'un autre nœud déjà établi est purement
    /// omis, jamais fusionné ni décalé.
    ///
    /// La quasi-coïncidence la plus simple à provoquer de façon déterministe est une
    /// impasse dont la profondeur (proche de la borne haute 90 %) laisse un bloc court
    /// se terminer à moins de MinNodeDistance de la collectrice qu'elle approche sans
    /// jamais l'atteindre : bloc de 70 m à 90 % de profondeur ⇒ écart de 7 m &lt; 8 m.
    /// </summary>
    public class GridGeneratorMinNodeDistanceTests
    {
        private static readonly List<float3> SquareNodes = new List<float3>
        {
            new float3(0f, 0f, 0f),
            new float3(300f, 0f, 0f),
            new float3(300f, 0f, 300f),
            new float3(0f, 0f, 300f),
        };

        [Fact]
        public void NormalGrid_NoOmission()
        {
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 3,
                Columns = 3,
                SpacingMeters = 60f,
            };

            var segments = GridGenerator.GenerateGrid(SquareNodes, parameters, out int omittedNodeCount);

            Assert.Equal(0, omittedNodeCount);
            Assert.Equal(24, segments.Count); // identique au test de régression sans MinNodeDistance
        }

        [Fact]
        public void CulDeSacStubTooCloseToTargetCollector_IsOmittedNotShortened()
        {
            // Rows=3 (collectrices tous les 70 m sur une bande [80,290] volontairement
            // hors-grille — on force plutôt la géométrie via FixedSpacing) : plus simple,
            // on compose directement un rectangle où l'espacement des collectrices vaut
            // exactement 70 m pour isoler un seul bloc du bon gabarit.
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 210f), // 3 x 70 m
                new float3(0f, 0f, 210f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FixedSpacing,
                Columns = 1,
                Rows = 1,
                SpacingMeters = 70f, // collectrices à v=70 et v=140
                CulDeSacMode = true,
                CulDeSacDepth = 0.9f, // profondeur max : écart de 10 % du bloc = 7 m
                Staggered = true,
                CulDeSacRatio = 100f,
            };

            var segments = GridGenerator.GenerateGrid(nodes, parameters, out int omittedNodeCount);
            var columns = segments.Where(s => !s.IsHorizontal).ToList();

            // Bloc [0,70] (bord, départ non-collectrice) : supprimé comme avant.
            // Bloc [70,140] : impair ⇒ part de la collectrice v=140 vers v=70,
            // s'arrêterait à 140+(70-140)*0.9=77, à 7 m de la collectrice v=70
            // (< MinNodeDistance=8) ⇒ omis pour quasi-coïncidence, PAS raccourci.
            // Bloc [140,210] : pair ⇒ part de v=140 vers v=210 (bord, non-collecteur),
            // s'arrête à 140+(210-140)*0.9=203, 7 m du bord — le bord n'étant pas un
            // nœud "déjà établi", ce bloc-ci N'EST PAS concerné par MinNodeDistance.
            Assert.True(omittedNodeCount >= 1, "Le bloc [70,140] aurait dû être omis pour quasi-coïncidence.");

            // Aucun segment de colonne ne doit s'arrêter à moins de MinNodeDistance
            // d'une collectrice sans être en réalité fusionné avec elle (même point
            // bit-exact) : pas de quasi-doublon flottant.
            var collectorPositions = new[] { new float2(150f, 70f), new float2(150f, 140f) };
            foreach (var segment in columns)
            {
                foreach (float2 collector in collectorPositions)
                {
                    float distToStart = math.distance(segment.Start.xz, collector);
                    float distToEnd = math.distance(segment.End.xz, collector);
                    // Soit confondu (segment qui rejoint réellement la collectrice),
                    // soit largement au-delà de MinNodeDistance — jamais entre les deux.
                    Assert.True(distToStart < 0.5f || distToStart >= GridGenerator.MinNodeDistance - 0.01f,
                        $"Extrémité de départ à {distToStart} m d'une collectrice : ni fusionnée, ni assez loin.");
                    Assert.True(distToEnd < 0.5f || distToEnd >= GridGenerator.MinNodeDistance - 0.01f,
                        $"Extrémité d'arrivée à {distToEnd} m d'une collectrice : ni fusionnée, ni assez loin.");
                }
            }
        }

        [Fact]
        public void NearCoincidence_NeverCrashesAndRestOfGridStaysWellFormed()
        {
            // Grille dense sur un polygone tourné à angle oblique, avec culs-de-sac :
            // combine plusieurs sources possibles de quasi-coïncidence (croisements
            // rapprochés, impasses profondes) sans qu'aucune n'entraîne de plantage,
            // de doublon, ni de segment sous MinSegmentLength.
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(280f, 0f, 90f),
                new float3(190f, 0f, 370f),
                new float3(-90f, 0f, 280f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Columns = 6,
                Rows = 4,
                SpacingMeters = 60f,
                AngleOffsetDegrees = 33f,
                CulDeSacMode = true,
                CulDeSacDepth = 0.9f,
                Staggered = true,
                CulDeSacRatio = 100f,
            };

            List<RoadSegmentDef> segments = null;
            Exception thrown = null;
            try
            {
                segments = GridGenerator.GenerateGrid(nodes, parameters, out int omittedNodeCount);
            }
            catch (Exception e)
            {
                thrown = e;
            }

            Assert.Null(thrown);
            Assert.NotNull(segments);

            // Pas de doublon : toutes les extrémités identiques doivent être EXACTEMENT
            // les mêmes floats (fusion valide), jamais deux points distincts séparés de
            // moins de MinNodeDistance (ce serait la quasi-coïncidence non nettoyée).
            var endpoints = segments.SelectMany(s => new[] { s.Start, s.End }).Distinct().ToList();
            for (int i = 0; i < endpoints.Count; i++)
            {
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    float distance = math.distance(endpoints[i].xz, endpoints[j].xz);
                    Assert.True(distance >= GridGenerator.MinNodeDistance - 0.5f,
                        $"Deux nœuds distincts quasi confondus non nettoyés : {endpoints[i]} vs {endpoints[j]} ({distance} m).");
                }
            }

            foreach (var segment in segments)
            {
                float length = math.distance(segment.Start.xz, segment.End.xz);
                Assert.True(length >= MinAcceptableLength(parameters),
                    $"Segment sous la longueur minimale acceptable : {length} m.");
            }
        }

        [Fact]
        public void NearCoincidentCrossings_MergeIntoASharedNodeInsteadOfDroppingALine()
        {
            // Bug corrigé (comme le "Super nó" de NetworkTools) : un croisement à moins de
            // MinNodeDistance d'un croisement déjà accepté était auparavant purement omis — sa
            // ligne u ET sa ligne v ne recevaient alors AUCUNE subdivision à cet endroit,
            // contrairement à toutes les autres lignes de la grille (une des deux, voire les
            // deux, "perdue"). Il doit maintenant être FUSIONNÉ avec ce point existant : les
            // DEUX lignes rejoignent le nœud partagé (même position bit-exacte).
            //
            // FitToArea sur un petit périmètre avec beaucoup de colonnes/lignes : l'espacement
            // RÉSULTANT (côté / nombre de colonnes) tombe sous MinNodeDistance (80/12 ≈ 6,7 m
            // < 8 m), donc deux croisements adjacents sur une même ligne (même u ou même v,
            // l'autre coordonnée décalée d'un seul pas de grille) tombent nécessairement à
            // moins de MinNodeDistance l'un de l'autre — contrairement à FixedSpacing avec un
            // espacement normal (>= 10 m côté UI), où deux croisements DISTINCTS ne peuvent
            // jamais être plus proches que l'espacement lui-même (réseau parfaitement régulier).
            // Reproduit ce qu'un joueur peut obtenir via l'UI : petit périmètre + beaucoup de
            // colonnes/lignes (jusqu'à 12, voir gridPanel.tsx), pas besoin d'un espacement
            // inaccessible depuis le panneau (min 10 m).
            var nodes = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(80f, 0f, 0f),
                new float3(80f, 0f, 80f),
                new float3(0f, 0f, 80f),
            };
            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Columns = 12,
                Rows = 12,
            };

            var segments = GridGenerator.GenerateGrid(nodes, parameters, out int mergedCount);
            Assert.True(mergedCount > 0, "Ce périmètre dense/oblique devrait produire au moins une fusion de croisements.");

            // Regroupe les extrémités par position bit-exacte (une fusion réussie donne des
            // floats identiques, pas juste "proches" — voir BuildSubSegments). Tout nœud
            // partagé par 3 segments ou plus est un vrai croisement multi-branches (ou un
            // croisement fusionné) : il doit alors avoir des segments des DEUX orientations,
            // jamais une seule — la signature exacte du bug (une ligne "oubliée" par l'omission).
            var byPosition = new Dictionary<string, (bool hasHorizontal, bool hasVertical, int count)>();
            void Record(float3 pos, bool isHorizontal)
            {
                string key = $"{pos.x:F3}_{pos.z:F3}";
                (bool hasHorizontal, bool hasVertical, int count) entry = byPosition.TryGetValue(key, out var existing)
                    ? existing
                    : (false, false, 0);
                byPosition[key] = (entry.hasHorizontal || isHorizontal, entry.hasVertical || !isHorizontal, entry.count + 1);
            }
            foreach (RoadSegmentDef s in segments)
            {
                Record(s.Start, s.IsHorizontal);
                Record(s.End, s.IsHorizontal);
            }

            foreach (var kvp in byPosition)
            {
                if (kvp.Value.count >= 3)
                {
                    Assert.True(kvp.Value.hasHorizontal && kvp.Value.hasVertical,
                        $"Nœud à {kvp.Key} avec {kvp.Value.count} segments mais une seule orientation représentée — ligne perdue au lieu de fusionnée.");
                }
            }
        }

        private static float MinAcceptableLength(GridParameters parameters)
        {
            // Une impasse peut être aussi courte que MinSegmentLength ; une traversée
            // normale aussi. On garde une marge de sécurité modeste sur l'assertion.
            return GridGenerator.MinSegmentLength - 0.5f;
        }
    }

    /// <summary>
    /// Chantier 1 (bug prioritaire) : le polygone du périmètre doit suivre la courbe
    /// RÉELLE d'une arête existante entre deux nœuds consécutifs (rond-point, virage...),
    /// pas la corde droite entre eux — sinon la grille générée peut déborder sur la route
    /// courbe elle-même. GridGenerator.SampleCurve (pur, sans dépendance ECS/Colossal.Mathematics
    /// — voir son en-tête) échantillonne cette courbe ; GridRoadToolSystem.BuildCurveAwarePerimeterPositions
    /// (non testable ici, dépend de l'ECS) l'insère dans la liste de points avant génération.
    /// </summary>
    public class GridGeneratorCurveSamplingTests
    {
        // ------------------------------------------------------------------
        // SampleCurve seul : les points échantillonnés doivent suivre la courbe,
        // pas la corde droite entre les extrémités.
        // ------------------------------------------------------------------

        [Fact]
        public void SampleCurve_StraightBezier_ReturnsPointsOnTheChord()
        {
            // Points de contrôle alignés : la "courbe" est en fait une droite.
            var a = new float3(0f, 0f, 0f);
            var b = new float3(33f, 0f, 0f);
            var c = new float3(66f, 0f, 0f);
            var d = new float3(100f, 0f, 0f);

            List<float3> samples = GridGenerator.SampleCurve(a, b, c, d);

            Assert.NotEmpty(samples);
            foreach (float3 p in samples)
            {
                // Doit rester sur la droite Y=0, Z=0, avec X strictement entre 0 et 100.
                Assert.True(p.x > 0f && p.x < 100f);
                Assert.Equal(0f, p.z, 3);
            }
        }

        [Fact]
        public void SampleCurve_BulgingBezier_PointsDeviateSignificantlyFromTheChord()
        {
            // Courbe qui s'écarte fortement de la corde a→d (renflement de 75 m sur 200 m
            // de long) : simule une arête de rond-point entre deux nœuds sélectionnés.
            var a = new float3(200f, 0f, 0f);
            var b = new float3(100f, 0f, 0f);
            var c = new float3(100f, 0f, 200f);
            var d = new float3(200f, 0f, 200f);

            List<float3> samples = GridGenerator.SampleCurve(a, b, c, d);

            Assert.NotEmpty(samples);
            // La corde droite a→d serait X=200 partout : au moins un point échantillonné
            // doit s'en écarter nettement (le renflement, pas une corde).
            Assert.Contains(samples, p => p.x < 170f);
            // Le point le plus proche du milieu (z≈100) doit être proche du renflement
            // maximal théorique (x=125, cf. formule du point médian d'une cubique).
            float3 middle = samples.OrderBy(p => math.abs(p.z - 100f)).First();
            Assert.True(middle.x < 150f, $"Point médian attendu proche du renflement (x<150), obtenu x={middle.x}.");
        }

        [Fact]
        public void SampleCurve_ShortEdge_NeverExceedsMaxSamples()
        {
            // Courbe très longue (garde-fou MaxCurveSamplesPerEdge) : ne doit jamais
            // produire un nombre de points disproportionné.
            var a = new float3(0f, 0f, 0f);
            var b = new float3(0f, 0f, 1000f);
            var c = new float3(2000f, 0f, 1000f);
            var d = new float3(2000f, 0f, 2000f);

            List<float3> samples = GridGenerator.SampleCurve(a, b, c, d);

            Assert.True(samples.Count <= GridGenerator.MaxCurveSamplesPerEdge);
        }

        // ------------------------------------------------------------------
        // Intégration : un polygone dont un côté est échantillonné le long d'une
        // courbe (au lieu de la corde droite entre ses deux extrémités) doit exclure
        // la zone entre la corde et la courbe — la grille générée ne doit pas y
        // déborder, contrairement à ce qui se produirait avec la corde seule.
        // ------------------------------------------------------------------

        [Fact]
        public void CurveSampledBoundary_ExcludesBulgeArea_ComparedToStraightChord()
        {
            // Carré 200×200, mais le côté droit (x=200, de z=0 à z=200) est en réalité
            // une arête de rond-point qui se renfonce jusqu'à x≈125 vers z=100 (voir
            // SampleCurve_BulgingBezier ci-dessus pour la même courbe) — comme si la
            // route courbe mordait sur ce qui serait autrement une zone constructible.
            var a = new float3(200f, 0f, 0f);
            var b = new float3(100f, 0f, 0f);
            var c = new float3(100f, 0f, 200f);
            var d = new float3(200f, 0f, 200f);
            List<float3> curveSamples = GridGenerator.SampleCurve(a, b, c, d);

            var straightChordPerimeter = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(200f, 0f, 0f),
                new float3(200f, 0f, 200f),
                new float3(0f, 0f, 200f),
            };
            var curveAwarePerimeter = new List<float3> { new float3(0f, 0f, 0f), a };
            curveAwarePerimeter.AddRange(curveSamples);
            curveAwarePerimeter.Add(d);
            curveAwarePerimeter.Add(new float3(0f, 0f, 200f));

            var parameters = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 9,
                Columns = 9,
                SpacingMeters = 60f,
            };

            var straightSegments = GridGenerator.GenerateGrid(straightChordPerimeter, parameters);
            var curvedSegments = GridGenerator.GenerateGrid(curveAwarePerimeter, parameters);

            Assert.NotEmpty(straightSegments);
            Assert.NotEmpty(curvedSegments);

            // Zone du renflement (autour de z=100) : la corde droite laisse la génération
            // atteindre x proche de 200 ; le polygone qui suit la courbe doit rester
            // nettement en retrait de cette limite dans la même zone.
            float MaxXNearBulge(List<RoadSegmentDef> segments) => segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => math.abs(p.z - 100f) < 20f)
                .Select(p => p.x)
                .DefaultIfEmpty(float.MinValue)
                .Max();

            float straightMaxX = MaxXNearBulge(straightSegments);
            float curvedMaxX = MaxXNearBulge(curvedSegments);

            Assert.True(curvedMaxX < straightMaxX - 30f,
                $"Le polygone qui suit la courbe devrait exclure le renflement (x max attendu nettement < {straightMaxX}), obtenu {curvedMaxX}.");
        }
    }

    /// <summary>
    /// Mode "Adaptativo" (GenerateAdaptiveGrid) : anneaux concentriques par offset inward du
    /// polygone du périmètre — voir la doc de OffsetPolygonInward pour l'algorithme (miter
    /// join avec clamp, pas de vrai straight-skeleton). Les trois cas demandés : carré convexe
    /// (coins nets, distance d'offset exacte), forme en L concave (pas de casse/NaN au coin
    /// concave), périmètre courbe (les anneaux suivent la courbe plutôt que sa corde).
    /// </summary>
    public class GridGeneratorAdaptiveGridTests
    {
        private static readonly List<float3> SquareNodes = new List<float3>
        {
            new float3(0f, 0f, 0f),
            new float3(300f, 0f, 0f),
            new float3(300f, 0f, 300f),
            new float3(0f, 0f, 300f),
        };

        private static GridParameters AdaptiveParams(float spacingMeters, int radialConnections,
            bool roundedCorners = false, bool culDeSacMode = false, float culDeSacRatio = 100f, float culDeSacDepth = 0.75f) => new GridParameters
            {
                SpacingMeters = spacingMeters,
                RadialConnections = radialConnections,
                AdaptiveRoundedCorners = roundedCorners,
                CulDeSacMode = culDeSacMode,
                CulDeSacRatio = culDeSacRatio,
                CulDeSacDepth = culDeSacDepth,
            };

        [Fact]
        public void SmallFullRoundaboutPerimeter_SucceedsAtReasonableSpacing()
        {
            // Régression : un petit giratoire (rayon 25) composé QUE d'arcs (aucun coin net,
            // périmètre "lisse") échouait totalement (0 anneau, quel que soit l'espacement) avant
            // la correction de ResamplePolygon — un bug de boucle qui ne parcourait aucun point
            // pour le tronçon "ancré arbitrairement" d'un polygone entièrement lisse, faisant
            // silencieusement retomber sur le polygone brut (arêtes trop courtes pour l'offset).
            float radius = 25f;
            float k = 0.5522847f; // 4/3*(sqrt(2)-1), quart de cercle en Bézier
            var center = new float2(0f, 0f);
            float3 QuarterPoint(float deg) => new float3(
                center.x + radius * math.cos(math.radians(deg)), 0f, center.y + radius * math.sin(math.radians(deg)));

            var quarterNodes = new List<float3> { QuarterPoint(0), QuarterPoint(90), QuarterPoint(180), QuarterPoint(270) };
            var roundabout = new List<float3>();
            for (int i = 0; i < 4; i++)
            {
                float3 p0 = quarterNodes[i];
                float3 p1 = quarterNodes[(i + 1) % 4];
                roundabout.Add(p0);
                float startDeg = i * 90f;
                float endDeg = startDeg + 90f;
                float3 controlB = new float3(
                    center.x + radius * math.cos(math.radians(startDeg)) - radius * k * math.sin(math.radians(startDeg)), 0f,
                    center.y + radius * math.sin(math.radians(startDeg)) + radius * k * math.cos(math.radians(startDeg)));
                float3 controlC = new float3(
                    center.x + radius * math.cos(math.radians(endDeg)) + radius * k * math.sin(math.radians(endDeg)), 0f,
                    center.y + radius * math.sin(math.radians(endDeg)) - radius * k * math.cos(math.radians(endDeg)));
                roundabout.AddRange(GridGenerator.SampleCurve(p0, controlB, controlC, p1));
            }

            // Au moins un espacement raisonnable (plus petit que le rayon) doit réussir à produire
            // un premier anneau valide — avant la correction, AUCUN espacement n'y arrivait sur ce
            // périmètre entièrement lisse (sans coin net) : un bug de boucle (AppendResampledRun)
            // faisait retomber silencieusement ResamplePolygon sur le polygone brut (89 arêtes de
            // quelques mètres chacune), bien trop fines pour tout espacement d'anneau raisonnable.
            var poly = roundabout.Select(p => new float2(p.x, p.z)).ToList();
            bool anySucceeded = false;
            foreach (float spacing in new[] { 8f, 10f, 12f, 15f, 20f })
            {
                if (GridGenerator.OffsetPolygonInward(poly, spacing) != null)
                {
                    anySucceeded = true;
                    break;
                }
            }
            Assert.True(anySucceeded, "Aucun espacement raisonnable n'a produit de premier anneau valide sur ce petit giratoire entièrement lisse.");
        }

        [Fact]
        public void Square_FirstRingCornersAreOffsetInwardByExactlySpacing()
        {
            // Carré axis-aligned, coin convexe à 90° : le miter join dégénère exactement en un
            // décalage perpendiculaire simple sur chaque arête, donc le coin (0,0) doit se
            // retrouver exactement à (40,40) après un anneau à 40 m d'espacement — vérifiable
            // à la main, pas seulement "dans le bon sens".
            var segments = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 0));

            Assert.NotEmpty(segments);

            bool HasPointNear(float x, float z) => segments.Any(s =>
                math.distance(s.Start.xz, new float2(x, z)) < 0.5f
                || math.distance(s.End.xz, new float2(x, z)) < 0.5f);

            Assert.True(HasPointNear(40f, 40f), "Coin du premier anneau attendu à (40, 40).");
            Assert.True(HasPointNear(260f, 40f));
            Assert.True(HasPointNear(260f, 260f));
            Assert.True(HasPointNear(40f, 260f));
        }

        [Fact]
        public void Square_RingsShrinkUntilDegenerateThenStop()
        {
            // Côté 300, espacement 40 : anneaux à côté 220, 140, 60 (tous valides), puis un
            // 4e anneau à côté 60-80=-20 (dégénéré, aire trop petite/signe inversé) — ne doit
            // jamais être émis. Voir OffsetPolygonInward pour les critères d'arrêt.
            var segments = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 0));

            bool HasPointNear(float x, float z) => segments.Any(s =>
                math.distance(s.Start.xz, new float2(x, z)) < 0.5f
                || math.distance(s.End.xz, new float2(x, z)) < 0.5f);

            Assert.True(HasPointNear(80f, 80f), "Coin du 2e anneau attendu à (80, 80).");
            Assert.True(HasPointNear(120f, 120f), "Coin du 3e anneau (le plus intérieur valide) attendu à (120, 120).");
            Assert.False(HasPointNear(160f, 160f), "Un 4e anneau (dégénéré) ne devrait jamais être émis.");
        }

        [Fact]
        public void Square_NeverProducesPointsOutsideTheOriginalPerimeter()
        {
            // Un offset INWARD ne doit jamais faire sortir un point du polygone d'origine,
            // même avec le clamp de miter limit sur des coins à 90° (non concerné ici, mais
            // la propriété doit tenir pour tout spacing raisonnable).
            var segments = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(25f, 4));

            Assert.NotEmpty(segments);
            Assert.All(segments, s =>
            {
                Assert.InRange(s.Start.x, -0.5f, 300.5f);
                Assert.InRange(s.Start.z, -0.5f, 300.5f);
                Assert.InRange(s.End.x, -0.5f, 300.5f);
                Assert.InRange(s.End.z, -0.5f, 300.5f);
            });
        }

        [Fact]
        public void Square_RadialConnectionsLinkOuterPerimeterToInnerRings()
        {
            // radialConnections > 0 : au moins un segment doit partir d'un sommet du périmètre
            // d'ORIGINE (les 4 coins du carré) vers l'intérieur — sinon les anneaux resteraient
            // des boucles isolées sans connexion, ce que le paramètre existe pour éviter.
            var withoutRadials = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 0));
            var withRadials = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 4));

            Assert.True(withRadials.Count > withoutRadials.Count,
                "Des connexions radiales devraient ajouter des segments par rapport à radialConnections=0.");

            bool startsAtOriginalCorner = withRadials.Any(s =>
                SquareNodes.Any(corner => math.distance(corner.xz, s.Start.xz) < 0.5f)
                || SquareNodes.Any(corner => math.distance(corner.xz, s.End.xz) < 0.5f));
            Assert.True(startsAtOriginalCorner, "Au moins une connexion radiale devrait partir d'un coin du périmètre d'origine.");
        }

        [Fact]
        public void Square_OnlyRadialConnectorsAreMarkedIsRadial()
        {
            // IsRadial distingue les rayons (EmitRadialConnections) des anneaux (EmitRingSegments) —
            // voir RoadSegmentDef.IsRadial, utilisé par GridRoadToolSystem pour poser le réseau
            // secondaire uniquement sur les rayons (et les impasses, IsCulDeSacEnd, hors de ce test).
            var withRadials = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 4));
            var withoutRadials = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 0));

            Assert.Contains(withRadials, s => s.IsRadial);
            Assert.DoesNotContain(withoutRadials, s => s.IsRadial);

            // Les anneaux eux-mêmes (segments communs aux deux générations) ne sont jamais radiaux.
            int ringSegmentCount = withRadials.Count(s => !s.IsRadial);
            Assert.Equal(withoutRadials.Count, ringSegmentCount);
        }

        [Fact]
        public void ConcaveLShape_RadialConnectionSegmentsAreCollinear()
        {
            // Bug "zigzag" corrigé : EmitRadialConnections choisissait le sommet le plus proche
            // de l'anneau suivant à chaque étape (NearestPoint), ce qui pouvait faire dériver le
            // rayon d'un anneau à l'autre au lieu de rester une ligne droite — surtout visible
            // sur un périmètre irrégulier/concave (carré simple trop symétrique pour exposer le
            // bug : NearestPoint y retombe presque toujours sur le bon coin par coïncidence).
            // Chaque rayon a maintenant une direction fixe (ComputeRadialDirection) : tous ses
            // segments consécutifs doivent rester parfaitement colinéaires (produit vectoriel des
            // directions ≈ 0), du premier anneau jusqu'au dernier.
            var lShape = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 150f),
                new float3(150f, 0f, 150f),
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };
            var segments = GridGenerator.GenerateAdaptiveGrid(lShape, AdaptiveParams(20f, 5));
            var radials = segments.Where(s => s.IsRadial).ToList();
            Assert.True(radials.Count >= 2, "Il faut plusieurs anneaux pour tester la colinéarité d'un rayon sur plus d'un segment.");

            // Segments d'un même rayon = suite consécutive où la fin de l'un touche le début du
            // suivant (émis dans cet ordre par EmitRadialConnections, un rayon après l'autre).
            int chainStart = 0;
            for (int i = 1; i <= radials.Count; i++)
            {
                bool chainBreaks = i == radials.Count || math.distance(radials[i - 1].End.xz, radials[i].Start.xz) > 0.5f;
                if (!chainBreaks) continue;

                for (int j = chainStart + 1; j < i; j++)
                {
                    float2 dirPrev = math.normalize(radials[j - 1].End.xz - radials[j - 1].Start.xz);
                    float2 dirNext = math.normalize(radials[j].End.xz - radials[j].Start.xz);
                    float cross = dirPrev.x * dirNext.y - dirPrev.y * dirNext.x;
                    Assert.True(math.abs(cross) < 0.01f,
                        $"Segments {j - 1} et {j} d'un même rayon devraient être colinéaires (cross={cross}).");
                }
                chainStart = i;
            }
        }

        [Fact]
        public void RoundedCorners_ProducesMorePointsThanMiterAtEachSquareCorner()
        {
            // Coins arrondis : un arc de plusieurs points remplace chaque pointe nette, donc le
            // premier anneau doit avoir sensiblement plus de sommets qu'en miter (par défaut) —
            // et chacun de ses points doit rester à ~distance de son sommet d'origine le plus
            // proche (rayon de l'arc), jamais au-delà (voir RoundCorners).
            var miter = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 0, roundedCorners: false));
            var rounded = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 0, roundedCorners: true));

            Assert.NotEmpty(miter);
            Assert.NotEmpty(rounded);
            Assert.True(rounded.Count > miter.Count,
                $"Les coins arrondis devraient ajouter des segments par rapport au miter (miter={miter.Count}, rounded={rounded.Count}).");

            // Toujours à l'intérieur du périmètre d'origine (un arrondi ne doit jamais faire
            // sortir un point, comme pour le miter — voir Square_NeverProducesPointsOutsideTheOriginalPerimeter).
            Assert.All(rounded, s =>
            {
                Assert.InRange(s.Start.x, -0.5f, 300.5f);
                Assert.InRange(s.Start.z, -0.5f, 300.5f);
                Assert.InRange(s.End.x, -0.5f, 300.5f);
                Assert.InRange(s.End.z, -0.5f, 300.5f);
            });
        }

        [Fact]
        public void ConcaveLShape_RoundedCorners_LeavesTheConcaveVertexUntouched()
        {
            // Bug corrigé : RoundCorners appliquait l'arc de round-join même à un coin
            // concave/réflexe (le coin intérieur du L, (150,150)) — géométriquement invalide
            // pour l'offsetting de polygone (le round join ne s'applique qu'aux coins
            // convexes), ce qui produisait une boucle vers l'intérieur de la forme (artefact en
            // dents de scie), jamais détectée car HasSelfIntersection valide la version miter,
            // avant l'arrondi. Le point le plus proche du coin concave doit donc rester
            // EXACTEMENT le même point miter, avec ou sans l'option activée.
            var lShape = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 150f),
                new float3(150f, 0f, 150f),
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };

            var miter = GridGenerator.GenerateAdaptiveGrid(lShape, AdaptiveParams(30f, 0, roundedCorners: false));
            var rounded = GridGenerator.GenerateAdaptiveGrid(lShape, AdaptiveParams(30f, 0, roundedCorners: true));

            Assert.NotEmpty(miter);
            Assert.NotEmpty(rounded);
            // Les coins convexes (5 des 6 sommets du L) doivent quand même gagner des points.
            Assert.True(rounded.Count > miter.Count,
                $"Les coins convexes du L devraient gagner des points avec l'arrondi (miter={miter.Count}, rounded={rounded.Count}).");

            float2 concaveVertex = new float2(150f, 150f);
            float2 nearestMiter = NearestVertex(miter, concaveVertex);
            float2 nearestRounded = NearestVertex(rounded, concaveVertex);
            Assert.True(math.distance(nearestMiter, nearestRounded) < 0.1f,
                $"Le point le plus proche du coin concave devrait être identique avec/sans arrondi (miter={nearestMiter}, rounded={nearestRounded}).");
        }

        private static float2 NearestVertex(List<RoadSegmentDef> segments, float2 target)
        {
            float2 best = default;
            float bestDist = float.MaxValue;
            foreach (RoadSegmentDef s in segments)
            {
                float d1 = math.distance(s.Start.xz, target);
                if (d1 < bestDist) { bestDist = d1; best = s.Start.xz; }
                float d2 = math.distance(s.End.xz, target);
                if (d2 < bestDist) { bestDist = d2; best = s.End.xz; }
            }
            return best;
        }

        [Fact]
        public void CulDeSacMode_SomeRadialsStopBeforeTheInnermostRingAsImpasses()
        {
            // CulDeSacMode réutilisé pour le mode Adaptativo (voir EmitRadialConnections) : à
            // ratio 50 %, environ un rayon sur deux devrait s'arrêter avant le dernier anneau et
            // porter IsCulDeSacEnd — jamais les anneaux eux-mêmes (toujours traversants).
            var withCulDeSac = GridGenerator.GenerateAdaptiveGrid(SquareNodes,
                AdaptiveParams(40f, 8, culDeSacMode: true, culDeSacRatio: 50f, culDeSacDepth: 0.75f));

            Assert.Contains(withCulDeSac, s => s.IsCulDeSacEnd);
            Assert.All(withCulDeSac.Where(s => s.IsCulDeSacEnd), s => Assert.True(s.IsRadial, "Seuls des rayons devraient porter IsCulDeSacEnd en mode Adaptativo."));

            var withoutCulDeSac = GridGenerator.GenerateAdaptiveGrid(SquareNodes, AdaptiveParams(40f, 8, culDeSacMode: false));
            Assert.True(withCulDeSac.Count < withoutCulDeSac.Count,
                "Des rayons raccourcis en impasse devraient produire moins de segments que des rayons complets.");
        }

        [Fact]
        public void ConcaveLShape_DoesNotProduceDegenerateOrNaNGeometry()
        {
            // Forme en L (coin réflexe/concave en (150,150)) : le point délicat de
            // OffsetVertex (miter limit) et de OffsetPolygonInward (détection
            // d'auto-intersection) doit empêcher toute géométrie cassée, jamais planter.
            var lShape = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 150f),
                new float3(150f, 0f, 150f),
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };

            var segments = GridGenerator.GenerateAdaptiveGrid(lShape, AdaptiveParams(30f, 3));

            Assert.NotEmpty(segments);
            Assert.All(segments, s =>
            {
                Assert.True(math.all(math.isfinite(s.Start)), "Segment.Start doit être fini (pas de NaN/Infinity) même au coin concave.");
                Assert.True(math.all(math.isfinite(s.End)), "Segment.End doit être fini même au coin concave.");
                // Marge généreuse : le clamp de miter limit peut légèrement déborder d'un coin
                // très aigu, mais jamais s'échapper loin de la bounding box du périmètre.
                Assert.InRange(s.Start.x, -50f, 350f);
                Assert.InRange(s.Start.z, -50f, 350f);
            });
        }

        [Fact]
        public void TinyPerimeter_LargeSpacing_StillEmitsOriginalPerimeterInsteadOfNothing()
        {
            // Bug corrigé ("la grille disparaît au-delà de X m") : si l'espacement dépasse la
            // demi-largeur du périmètre choisi, le tout premier anneau intérieur est déjà
            // dégénéré (OffsetPolygonInward retourne null immédiatement) — la boucle de
            // GenerateAdaptiveGrid ne tournait alors jamais, et RIEN n'était émis, alors que le
            // périmètre d'origine lui-même (déjà validé plus haut dans la fonction) reste une
            // route parfaitement valide. Pas un plafond codé en dur (aucune valeur de ce genre
            // trouvée dans le code, vérifié) : une vraie dégénérescence géométrique propre à la
            // taille du périmètre choisi — mais qui ne doit plus vider le résultat pour autant.
            var tinySquare = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(100f, 0f, 0f),
                new float3(100f, 0f, 100f),
                new float3(0f, 0f, 100f),
            };

            // Demi-largeur 50 m : un espacement de 60 m dégénère dès le premier anneau intérieur.
            var segments = GridGenerator.GenerateAdaptiveGrid(tinySquare, AdaptiveParams(60f, 0));

            Assert.NotEmpty(segments);
            // Doit correspondre au périmètre d'origine tel quel (4 arêtes de 100 m, boucle fermée).
            Assert.Equal(4, segments.Count);
            Assert.All(segments, s => Assert.False(s.IsRadial));
        }

        [Fact]
        public void ConcaveLShape_EventuallyDegeneratesWithoutInfiniteRings()
        {
            // Garde-fou pratique : même sur une forme concave, la boucle de génération doit
            // s'arrêter (dégénérescence détectée) bien avant MaxAdaptiveRings, jamais tourner
            // en rond jusqu'à la limite de sécurité.
            var lShape = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(300f, 0f, 0f),
                new float3(300f, 0f, 150f),
                new float3(150f, 0f, 150f),
                new float3(150f, 0f, 300f),
                new float3(0f, 0f, 300f),
            };

            var segments = GridGenerator.GenerateAdaptiveGrid(lShape, AdaptiveParams(30f, 0));

            // Le bras le plus étroit du L fait 150 m : largement moins de
            // MaxAdaptiveRings * 30 m d'anneaux possibles avant dégénérescence.
            int approxRingCount = segments.Count / 6; // 6 arêtes par anneau sur cette forme
            Assert.True(approxRingCount < GridGenerator.MaxAdaptiveRings,
                "La génération devrait s'arrêter bien avant la limite de sécurité sur cette forme concave.");
        }

        [Fact]
        public void CurvedPerimeter_RingsFollowTheCurveInsteadOfItsChord()
        {
            // Renflement doux et tangent-continu (arc de cercle, rayon 75, quasi-circulaire via
            // k=4/3*(sqrt(2)-1)) plutôt qu'une cuspide extrême (control point Bézier placé
            // derrière le sommet, comme utilisé par GridGeneratorCurveSamplingTests — pertinent
            // pour l'ancien clipping pair-impair, qui ne dépend d'aucune continuité de tangente,
            // mais pas représentatif d'une vraie route courbe pour un algorithme d'offset).
            float radius = 75f;
            float k = 0.5522847f;
            var center = new float2(200f, 100f);
            float3 a = new float3(center.x, 0f, center.y - radius);
            float3 d = new float3(center.x - radius, 0f, center.y);
            float3 b = new float3(center.x, 0f, center.y - radius + radius * k);
            float3 c = new float3(center.x - radius + radius * k, 0f, center.y);
            List<float3> curveSamples = GridGenerator.SampleCurve(a, b, c, d);

            var straightChordPerimeter = new List<float3>
            {
                new float3(0f, 0f, 0f),
                new float3(200f, 0f, 0f),
                new float3(200f, 0f, 200f),
                new float3(0f, 0f, 200f),
            };
            var curveAwarePerimeter = new List<float3> { new float3(0f, 0f, 0f), a };
            curveAwarePerimeter.AddRange(curveSamples);
            curveAwarePerimeter.Add(d);
            curveAwarePerimeter.Add(new float3(0f, 0f, 200f));

            // Comparaison directe du PREMIER anneau (OffsetPolygonInward, internal) plutôt que
            // de chercher des sommets près de z=100 dans la sortie complète : un anneau carré
            // n'a que 4 coins, presque jamais pile à z=100, donc regarder les sommets seuls ne
            // dirait rien d'utile ici. On calcule plutôt où le contour de l'anneau CROISE la
            // ligne z=100 (interpolation le long de chaque arête), une mesure directement
            // comparable entre les deux périmètres.
            var straightPoly = straightChordPerimeter.Select(p => new float2(p.x, p.z)).ToList();
            var curvedPoly = curveAwarePerimeter.Select(p => new float2(p.x, p.z)).ToList();

            var straightRing1 = GridGenerator.OffsetPolygonInward(straightPoly, 15f);
            var curvedRing1 = GridGenerator.OffsetPolygonInward(curvedPoly, 15f);

            Assert.NotNull(straightRing1);
            Assert.NotNull(curvedRing1);

            float RingXAtZ(List<float2> ring, float z)
            {
                float maxX = float.MinValue;
                int n = ring.Count;
                for (int i = 0; i < n; i++)
                {
                    float2 a = ring[i];
                    float2 b = ring[(i + 1) % n];
                    bool crosses = (a.y <= z && z < b.y) || (b.y <= z && z < a.y);
                    if (!crosses) continue;
                    float t = (z - a.y) / (b.y - a.y);
                    maxX = math.max(maxX, a.x + t * (b.x - a.x));
                }
                return maxX;
            }

            float straightX = RingXAtZ(straightRing1, 100f);
            float curvedX = RingXAtZ(curvedRing1, 100f);

            Assert.True(curvedX < straightX - 20f,
                $"Le premier anneau du périmètre courbe devrait croiser z=100 nettement en retrait du renflement (x attendu nettement < {straightX}), obtenu {curvedX}.");
        }

        [Fact]
        public void DegenerateInput_FewerThanTwoNodes_ReturnsEmptyWithoutThrowing()
        {
            var segments = GridGenerator.GenerateAdaptiveGrid(new List<float3> { new float3(0f, 0f, 0f) }, AdaptiveParams(40f, 0));
            Assert.Empty(segments);
        }
    }
}
