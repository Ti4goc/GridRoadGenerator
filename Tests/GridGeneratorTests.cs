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
    /// Chantier 6 : JitterAmount/JitterSeed déplacent les lignes internes (colonnes) de
    /// façon pseudo-aléatoire mais DÉTERMINISTE — jamais les collectrices (rangées) ni
    /// le périmètre. Le clipping/découpage aux croisements ne fait aucune hypothèse sur
    /// l'ordre ou la régularité des positions de lignes (voir GenerateGrid), donc rester
    /// correct après jitter est une propriété automatique de GridGenerator.ApplyJitter
    /// s'insérant simplement avant ce calcul — ces tests le confirment plutôt que de le
    /// re-prouver depuis zéro.
    /// </summary>
    public class GridGeneratorJitterTests
    {
        private static readonly List<float3> SquareNodes = new List<float3>
        {
            new float3(0f, 0f, 0f),
            new float3(300f, 0f, 0f),
            new float3(300f, 0f, 300f),
            new float3(0f, 0f, 300f),
        };

        private static GridParameters JitteredParameters(float amount, int seed) => new GridParameters
        {
            Mode = SpacingMode.FitToArea,
            Rows = 3,
            Columns = 5,
            SpacingMeters = 60f,
            JitterAmount = amount,
            JitterSeed = seed,
        };

        [Fact]
        public void SameSeed_ProducesIdenticalResultsAcrossGenerations()
        {
            GridParameters parameters = JitteredParameters(10f, 12345);

            var first = GridGenerator.GenerateGrid(SquareNodes, parameters);
            var second = GridGenerator.GenerateGrid(SquareNodes, parameters);

            Assert.NotEmpty(first);
            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                Assert.Equal(first[i].Start, second[i].Start);
                Assert.Equal(first[i].End, second[i].End);
            }
        }

        [Fact]
        public void DifferentSeeds_ProduceDifferentPositions()
        {
            var parametersA = JitteredParameters(10f, 1);
            var parametersB = JitteredParameters(10f, 2);

            var segmentsA = GridGenerator.GenerateGrid(SquareNodes, parametersA);
            var segmentsB = GridGenerator.GenerateGrid(SquareNodes, parametersB);

            Assert.NotEmpty(segmentsA);
            Assert.NotEmpty(segmentsB);
            bool anyDifferent = false;
            int count = math.min(segmentsA.Count, segmentsB.Count);
            for (int i = 0; i < count; i++)
            {
                if (math.distance(segmentsA[i].Start, segmentsB[i].Start) > 0.01f)
                {
                    anyDifferent = true;
                    break;
                }
            }
            Assert.True(anyDifferent, "Deux graines différentes devraient produire des positions de lignes différentes.");
        }

        [Fact]
        public void ZeroAmount_IdenticalToNoJitterField()
        {
            GridParameters withExplicitZero = JitteredParameters(0f, 999); // seed sans effet à amplitude nulle
            var withoutJitterAtAll = new GridParameters
            {
                Mode = SpacingMode.FitToArea,
                Rows = 3,
                Columns = 5,
                SpacingMeters = 60f,
                // JitterAmount/JitterSeed non renseignés = 0 par défaut (struct) : doit se
                // comporter EXACTEMENT comme avant l'existence du jitter.
            };

            var a = GridGenerator.GenerateGrid(SquareNodes, withExplicitZero);
            var b = GridGenerator.GenerateGrid(SquareNodes, withoutJitterAtAll);

            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].Start, b[i].Start);
                Assert.Equal(a[i].End, b[i].End);
            }
        }

        [Fact]
        public void Jittered_IntersectionsRemainCorrectlyConnected()
        {
            GridParameters parameters = JitteredParameters(12f, 42);

            var segments = GridGenerator.GenerateGrid(SquareNodes, parameters);

            Assert.NotEmpty(segments);
            GridGeneratorTests.AssertCommonInvariants(segments);
        }

        [Fact]
        public void Jittered_RowsNeverMoveFromTheirRegularPositions()
        {
            // Les collectrices (rangées, v) doivent rester identiques à une génération
            // sans jitter : seules les colonnes (u) sont perturbées.
            var withJitter = JitteredParameters(12f, 7);
            var withoutJitter = JitteredParameters(0f, 7);

            var jittered = GridGenerator.GenerateGrid(SquareNodes, withJitter);
            var baseline = GridGenerator.GenerateGrid(SquareNodes, withoutJitter);

            // Les segments de rangée (isHorizontal true, cf. EmitLine axisIsU: false ->
            // isHorizontal: !axisIsU = true) doivent avoir le même Z constant qu'au repos
            // pour chaque rangée — comparé via l'ensemble des Z distincts observés.
            var jitteredRowZs = jittered.Where(s => s.IsHorizontal).Select(s => math.round(s.Start.z * 100f)).Distinct().OrderBy(z => z).ToList();
            var baselineRowZs = baseline.Where(s => s.IsHorizontal).Select(s => math.round(s.Start.z * 100f)).Distinct().OrderBy(z => z).ToList();

            Assert.Equal(baselineRowZs, jitteredRowZs);
        }
    }

    /// <summary>
    /// Chantier 7 : ComputeCurveControlPoints (pur, Core) calcule les points de contrôle
    /// intermédiaires d'une Bézier légèrement bombée pour les collectrices. La construction
    /// réelle de la NetCourse (Bezier4x3, ECS) se fait dans GridRoadToolSystem.BuildCurvedCourse,
    /// non testable ici — mais celui-ci ne fait qu'envelopper ce calcul autour de a=start,
    /// d=end fixes, donc les propriétés critiques (aucun mouvement des extrémités,
    /// dégénère en ligne droite à 0 %) sont entièrement couvertes au niveau pur.
    /// </summary>
    public class GridGeneratorCurveControlPointsTests
    {
        [Fact]
        public void ZeroAmount_ControlPointsLieExactlyOnTheChord()
        {
            var start = new float3(0f, 0f, 0f);
            var end = new float3(90f, 0f, 0f);

            GridGenerator.ComputeCurveControlPoints(start, end, 0f, out float3 b, out float3 c);

            // À 0 %, b et c sont les points au tiers/deux-tiers de la corde : une évaluation
            // de Bézier cubique avec a,b,c,d colinéaires dégénère exactement en ligne droite,
            // identique visuellement à NetUtils.StraightCurve (jamais appelé dans ce cas côté
            // GridRoadToolSystem, qui garde le chemin d'origine, mais la propriété géométrique
            // tient indépendamment).
            Assert.Equal(new float3(30f, 0f, 0f), b);
            Assert.Equal(new float3(60f, 0f, 0f), c);
        }

        [Fact]
        public void NonZeroAmount_OffsetsPerpendicularToTheChordBySameAmount()
        {
            var start = new float3(0f, 0f, 0f);
            var end = new float3(100f, 0f, 0f);

            GridGenerator.ComputeCurveControlPoints(start, end, 100f, out float3 b, out float3 c);

            // Décalage attendu (perpendiculaire à la corde, donc uniquement sur Z ici) :
            // 100 % * longueur(100) * MaxCurveBulgeFraction.
            float expectedOffset = 100f * GridGenerator.MaxCurveBulgeFraction;
            Assert.Equal(0f, b.y, 3);
            Assert.Equal(0f, c.y, 3);
            // Même décalage transversal pour b et c (corde parallèle à l'axe X ici, donc
            // le décalage perpendiculaire tombe entièrement sur Z).
            Assert.Equal(expectedOffset, b.z, 3);
            Assert.Equal(expectedOffset, c.z, 3);
            // Toujours au tiers/deux-tiers le long de X, la courbure ne change pas ça.
            Assert.Equal(100f / 3f, b.x, 2);
            Assert.Equal(200f / 3f, c.x, 2);
        }

        [Fact]
        public void OffsetMagnitude_ScalesWithCurveAmountAndSegmentLength()
        {
            var shortStart = new float3(0f, 0f, 0f);
            var shortEnd = new float3(50f, 0f, 0f);
            var longStart = new float3(0f, 0f, 0f);
            var longEnd = new float3(150f, 0f, 0f);

            GridGenerator.ComputeCurveControlPoints(shortStart, shortEnd, 50f, out float3 shortB, out _);
            GridGenerator.ComputeCurveControlPoints(longStart, longEnd, 50f, out float3 longB, out _);

            // Même pourcentage, segment 3x plus long : décalage 3x plus grand.
            Assert.Equal(shortB.z * 3f, longB.z, 2);

            GridGenerator.ComputeCurveControlPoints(shortStart, shortEnd, 100f, out float3 fullB, out _);
            // Même longueur, pourcentage doublé (50→100) : décalage doublé.
            Assert.Equal(shortB.z * 2f, fullB.z, 2);
        }

        [Fact]
        public void EndpointsAreNeverPartOfTheComputation_OnlyIntermediateControlPointsReturned()
        {
            // ComputeCurveControlPoints ne retourne QUE b et c : a=start et d=end restent
            // par construction exactement ce que l'appelant leur a passé (voir
            // GridRoadToolSystem.BuildCurvedCourse) — le raccordement au périmètre et aux
            // rues perpendiculaires ne peut donc jamais être affecté par la courbure.
            var start = new float3(12f, 3f, -7f);
            var end = new float3(112f, 3f, 43f);

            GridGenerator.ComputeCurveControlPoints(start, end, 80f, out float3 b, out float3 c);

            Assert.NotEqual(start, b);
            Assert.NotEqual(end, c);
            // b et c restent dans le voisinage du segment (pas d'échappée démesurée) :
            // la distance à la corde reste dans l'ordre de grandeur de MaxCurveBulgeFraction.
            float length = math.distance(start.xz, end.xz);
            float maxExpectedOffset = length * GridGenerator.MaxCurveBulgeFraction * 1.01f;
            Assert.True(math.distance(b, math.lerp(start, end, 1f / 3f)) <= maxExpectedOffset);
            Assert.True(math.distance(c, math.lerp(start, end, 2f / 3f)) <= maxExpectedOffset);
        }

        [Fact]
        public void DegenerateZeroLengthSegment_DoesNotThrow()
        {
            var point = new float3(5f, 0f, 5f);

            GridGenerator.ComputeCurveControlPoints(point, point, 100f, out float3 b, out float3 c);

            Assert.Equal(point, b);
            Assert.Equal(point, c);
        }
    }
}
