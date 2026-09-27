using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GridRoadGenerator.Core;
using Unity.Mathematics;
using Xunit;

namespace GridRoadGenerator.Tests
{
    public class ConcentricGeneratorTests
    {
        private static List<float3> Square(float size) => new List<float3>
        {
            new float3(0f, 0f, 0f), new float3(size, 0f, 0f), new float3(size, 0f, size), new float3(0f, 0f, size),
        };

        /// <summary>L de 600×600 amputé du quart supérieur droit : un coin concave.</summary>
        private static List<float3> LShape() => new List<float3>
        {
            new float3(0f, 0f, 0f), new float3(600f, 0f, 0f), new float3(600f, 0f, 300f),
            new float3(300f, 0f, 300f), new float3(300f, 0f, 600f), new float3(0f, 0f, 600f),
        };

        /// <summary>Deux carrés de 400 m reliés par un couloir de 60 m : les anneaux se séparent en deux.</summary>
        private static List<float3> Dumbbell() => new List<float3>
        {
            new float3(0f, 0f, 0f), new float3(400f, 0f, 0f), new float3(400f, 0f, 170f), new float3(600f, 0f, 170f),
            new float3(600f, 0f, 0f), new float3(1000f, 0f, 0f), new float3(1000f, 0f, 400f), new float3(600f, 0f, 400f),
            new float3(600f, 0f, 230f), new float3(400f, 0f, 230f), new float3(400f, 0f, 400f), new float3(0f, 0f, 400f),
        };

        private static List<float3> Circle(float radius, int points)
        {
            var result = new List<float3>();
            for (int i = 0; i < points; i++)
            {
                float a = i * 2f * math.PI / points;
                result.Add(new float3(radius * math.cos(a), 0f, radius * math.sin(a)));
            }
            return result;
        }

        private static bool PointInPolygon(float2 p, List<float3> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                float2 a = polygon[i].xz;
                float2 b = polygon[j].xz;
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        private static float DistanceToPolygon(float2 p, List<float3> polygon)
        {
            float best = float.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                float2 a = polygon[i].xz;
                float2 b = polygon[(i + 1) % polygon.Count].xz;
                float2 ab = b - a;
                float t = math.clamp(math.dot(p - a, ab) / math.lengthsq(ab), 0f, 1f);
                best = math.min(best, math.distance(p, a + t * ab));
            }
            return best;
        }

        private static bool Cross(float2 a1, float2 a2, float2 b1, float2 b2)
        {
            float d1 = Orient(b1, b2, a1), d2 = Orient(b1, b2, a2), d3 = Orient(a1, a2, b1), d4 = Orient(a1, a2, b2);
            return ((d1 > 1e-3f && d2 < -1e-3f) || (d1 < -1e-3f && d2 > 1e-3f))
                && ((d3 > 1e-3f && d4 < -1e-3f) || (d3 < -1e-3f && d4 > 1e-3f));
        }

        private static float Orient(float2 a, float2 b, float2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        /// <summary>
        /// Invariants communs à toute forme : rien hors du périmètre, aucun croisement de routes
        /// sans nœud partagé, et chaque extrémité est soit un nœud partagé, soit posée sur le
        /// périmètre (où MakeCoursePos la raccorde à la route existante) — jamais une impasse.
        /// </summary>
        private static void AssertWellFormed(List<RoadSegmentDef> segments, List<float3> perimeter)
        {
            Assert.NotEmpty(segments);
            foreach (RoadSegmentDef s in segments)
            {
                float2 mid = (s.Start.xz + s.End.xz) * 0.5f;
                Assert.True(PointInPolygon(mid, perimeter) || DistanceToPolygon(mid, perimeter) < 0.5f,
                    $"Tronçon hors du périmètre : {s.Start} -> {s.End}");
            }

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = i + 1; j < segments.Count; j++)
                {
                    Assert.False(Cross(segments[i].Start.xz, segments[i].End.xz, segments[j].Start.xz, segments[j].End.xz),
                        $"Croisement sans nœud : {segments[i].Start}->{segments[i].End} × {segments[j].Start}->{segments[j].End}");
                }
            }

            var counts = new Dictionary<(float, float), int>();
            foreach (RoadSegmentDef s in segments)
            {
                foreach (float3 p in new[] { s.Start, s.End })
                {
                    var key = (p.x, p.z);
                    counts[key] = counts.TryGetValue(key, out int c) ? c + 1 : 1;
                }
            }
            foreach (var kv in counts)
            {
                float2 p = new float2(kv.Key.Item1, kv.Key.Item2);
                Assert.True(kv.Value > 1 || DistanceToPolygon(p, perimeter) < 0.5f, $"Impasse en {p}");
            }

            // Tout le réseau doit être relié au périmètre (route existante) : aucun anneau isolé.
            var parent = new Dictionary<(float, float), (float, float)>();
            var root = (float.NaN, float.NaN);
            (float, float) Find((float, float) x)
            {
                while (!parent[x].Equals(x)) x = parent[x];
                return x;
            }
            void Union((float, float) a, (float, float) b) => parent[Find(a)] = Find(b);
            parent[root] = root;
            foreach (RoadSegmentDef s in segments)
            {
                var a = (s.Start.x, s.Start.z);
                var b = (s.End.x, s.End.z);
                if (!parent.ContainsKey(a)) parent[a] = a;
                if (!parent.ContainsKey(b)) parent[b] = b;
                Union(a, b);
                if (DistanceToPolygon(s.Start.xz, perimeter) < 0.5f) Union(a, root);
                if (DistanceToPolygon(s.End.xz, perimeter) < 0.5f) Union(b, root);
            }
            foreach (var node in parent.Keys.ToList())
            {
                Assert.True(Find(node).Equals(Find(root)), $"Nœud {node} non relié au périmètre");
            }
        }

        [Fact]
        public void Square_TwoLayers_TwoNestedSquaresEvenlySpacedToTheCentre()
        {
            // 600 m : profondeur max 300 m -> 2 anneaux à 100 m et 200 m du bord.
            var perimeter = Square(600f);
            var segments = ConcentricGenerator.Generate(perimeter, layers: 2, connections: 4);
            AssertWellFormed(segments, perimeter);

            // Chaque sommet d'anneau est à ±12 % de 100 m ou de 200 m du bord (anneaux parallèles
            // aux côtés, à une fraction constante de la demi-largeur).
            var ringDepths = segments
                .SelectMany(s => new[] { s.Start.xz, s.End.xz })
                .Select(p => DistanceToPolygon(p, perimeter))
                .Where(d => d > 1f)
                .ToList();
            Assert.Contains(ringDepths, d => d < 150f);
            Assert.Contains(ringDepths, d => d > 150f);
            Assert.All(ringDepths, d => Assert.True(math.abs(d - 100f) < 12f || math.abs(d - 200f) < 24f, $"Sommet à {d:F0} m du bord"));
        }

        [Fact]
        public void LayerCount_IsCappedWhenTheShapeIsTooNarrow()
        {
            // 300 m : profondeur 150 m -> au plus 2 anneaux à MinLayerSpacing (40 m).
            var perimeter = Square(300f);
            var many = ConcentricGenerator.Generate(perimeter, layers: 10, connections: 4);
            AssertWellFormed(many, perimeter);
            float minDepth = many.SelectMany(s => new[] { s.Start.xz, s.End.xz })
                .Select(p => DistanceToPolygon(p, perimeter)).Where(d => d > 1f).Min();
            Assert.True(minDepth >= ConcentricGenerator.MinLayerSpacing - 2f, $"Anneaux trop serrés : {minDepth} m");
        }

        [Fact]
        public void MaxLayers_DependsOnTheShape_AndGenerateNeverExceedsIt()
        {
            // Retour utilisateur : "é preciso haver um limite de anéis dependendo do tamanho do
            // perímetro, e no painel simplesmente bloqueia além do limite".
            int small = ConcentricGenerator.MaxLayers(Square(300f));
            int large = ConcentricGenerator.MaxLayers(Square(1200f));
            Assert.InRange(small, 1, 3);
            Assert.True(large > small, $"Un carré 4× plus grand devrait permettre plus d'anneaux ({large} vs {small})");
            Assert.Equal(0, ConcentricGenerator.MaxLayers(Square(80f)));

            foreach (var perimeter in new[] { Square(300f), Square(1200f), LShape(), Ellipse(600f, 220f, 400) })
            {
                int max = ConcentricGenerator.MaxLayers(perimeter);
                ConcentricGenerator.Generate(perimeter, layers: ConcentricGenerator.MaxLayersLimit, connections: 4);
                Assert.Equal(max, ConcentricGenerator.LastRingLoopCounts.Length);
            }
        }

        [Fact]
        public void ConcaveLShape_NeverSelfIntersectsNorLeavesThePerimeter()
        {
            var perimeter = LShape();
            foreach (int layers in new[] { 1, 2, 3 })
            {
                AssertWellFormed(ConcentricGenerator.Generate(perimeter, layers, connections: 4), perimeter);
            }
        }

        [Fact]
        public void WaistedShape_EveryRingGoesAllAround_TighterInTheNarrowPassage()
        {
            // Deux carrés de 400 m reliés par un passage de 300 m de large (demi-largeur 150 m).
            // Retours utilisateur : les anneaux ne doivent ni se couper ("vê porque divide"), ni
            // s'arrêter en pointe là où ils devraient continuer ("os bicos voltaram... onde é
            // suposto o anel continuar"), ni être plafonnés par le passage ("não vai além de 4
            // camadas") : chaque anneau fait le tour des deux carrés, plus serré dans le passage.
            var perimeter = new List<float3>
            {
                new float3(0f, 0f, 0f), new float3(400f, 0f, 0f), new float3(400f, 0f, 50f), new float3(600f, 0f, 50f),
                new float3(600f, 0f, 0f), new float3(1000f, 0f, 0f), new float3(1000f, 0f, 400f), new float3(600f, 0f, 400f),
                new float3(600f, 0f, 350f), new float3(400f, 0f, 350f), new float3(400f, 0f, 400f), new float3(0f, 0f, 400f),
            };
            var segments = ConcentricGenerator.Generate(perimeter, layers: 6, connections: 4);
            AssertWellFormed(segments, perimeter);

            int[] loopsPerRing = ConcentricGenerator.LastRingLoopCounts;
            // L'ancienne limite (anneaux à 40 m minimum, bornés par le passage) n'en laissait que 2.
            Assert.True(loopsPerRing.Length >= 2, $"Seulement {loopsPerRing.Length} anneaux");
            Assert.All(loopsPerRing, count => Assert.True(count == 1, $"Anneau coupé en {count} morceaux"));
            Assert.Contains(segments, s => s.Start.x < 400f && s.End.x < 400f);
            Assert.Contains(segments, s => s.Start.x > 600f && s.End.x > 600f);
        }

        [Fact]
        public void DenseCurvedPerimeter_IsFastAndWellFormed()
        {
            // Rond-point/avenue courbe densifiée : 525 points comme le vrai périmètre du log.
            var perimeter = Circle(500f, 525);
            var stopwatch = Stopwatch.StartNew();
            var segments = ConcentricGenerator.Generate(perimeter, layers: 4, connections: 6);
            stopwatch.Stop();
            AssertWellFormed(segments, perimeter);
            Assert.True(stopwatch.ElapsedMilliseconds < 600, $"Génération trop lente : {stopwatch.ElapsedMilliseconds} ms");
        }

        private static List<float3> Ellipse(float a, float b, int points)
        {
            var result = new List<float3>();
            for (int i = 0; i < points; i++)
            {
                float t = i * 2f * math.PI / points;
                result.Add(new float3(a * math.cos(t), 0f, b * math.sin(t)));
            }
            return result;
        }

        /// <summary>
        /// Écart de profondeur (m) toléré entre les deux bouts d'un tronçon d'anneau : les anneaux
        /// ondulent de quelques mètres, et un tronçon courbe couvre désormais une longue portion
        /// (voir CanSpanAsArc) ; un rayon, lui, relie deux anneaux distants d'au moins 40 m.
        /// </summary>
        private const float RingDepthTolerance = 10f;

        /// <summary>
        /// Plus grand virage (degrés) entre deux tronçons d'ANNEAU consécutifs (les rayons, qui
        /// relient deux profondeurs différentes, sont ignorés), en tenant compte des tangentes
        /// des tronçons courbes.
        /// </summary>
        private static float MaxRingTurnDegrees(List<RoadSegmentDef> segments, List<float3> perimeter)
        {
            var rings = segments.Where(s => math.abs(DistanceToPolygon(s.Start.xz, perimeter) - DistanceToPolygon(s.End.xz, perimeter)) < RingDepthTolerance).ToList();
            float2 TangentOut(RoadSegmentDef s) => s.IsArc ? math.normalize(s.StartTangent.xz) : math.normalize(s.End.xz - s.Start.xz);
            float2 TangentIn(RoadSegmentDef s) => s.IsArc ? math.normalize(s.EndTangent.xz) : math.normalize(s.End.xz - s.Start.xz);
            float max = 0f;
            foreach (RoadSegmentDef incoming in rings)
            {
                foreach (RoadSegmentDef outgoing in rings)
                {
                    if (math.all(outgoing.Start == incoming.End) && !math.all(outgoing.End == incoming.Start))
                    {
                        float cos = math.clamp(math.dot(TangentIn(incoming), TangentOut(outgoing)), -1f, 1f);
                        max = math.max(max, math.degrees(math.acos(cos)));
                    }
                }
            }
            return max;
        }

        [Fact]
        public void RoundedShape_InnerRingsKeepTheirCurvature_NoPointedTips()
        {
            // Retour utilisateur (capture) : sur une forme courbe, les anneaux profonds finissaient
            // "com bico" (l'offset exact devient pointu dès que la profondeur dépasse le rayon de
            // courbure du bout : ici 200²/500 = 80 m, anneaux à 50/100/150 m).
            var perimeter = Ellipse(500f, 200f, 400);
            var segments = ConcentricGenerator.Generate(perimeter, layers: 3, connections: 4);
            AssertWellFormed(segments, perimeter);
            float maxTurn = MaxRingTurnDegrees(segments, perimeter);
            Assert.True(maxTurn < 5f, $"Anneau cassé ou pointu : virage de {maxTurn:F0}°");
        }

        /// <summary>
        /// Lobe rond (rayon 300 m) prolongé à gauche par un bras étroit (150 m de large, bout
        /// arrondi) : aucune arête vive, mais une "taille" — les anneaux profonds du lobe se
        /// referment près du bras.
        /// </summary>
        private static List<float3> LobeWithArm()
        {
            var result = new List<float3>();
            float junctionAngle = math.asin(75f / 300f);
            // Cercle, de l'attache haute du bras à son attache basse (en passant par la droite).
            for (float a = math.PI - junctionAngle; a > -math.PI + junctionAngle; a -= 0.03f)
            {
                result.Add(new float3(300f * math.cos(a), 0f, 300f * math.sin(a)));
            }
            // Bras : bord bas vers la gauche, bout en demi-cercle, bord haut vers la droite.
            for (float x = -290f; x > -700f; x -= 20f) result.Add(new float3(x, 0f, -75f));
            for (float a = -math.PI / 2f; a > -3f * math.PI / 2f; a -= 0.1f)
            {
                result.Add(new float3(-700f + 75f * math.cos(a), 0f, 75f * math.sin(a)));
            }
            for (float x = -700f; x < -290f; x += 20f) result.Add(new float3(x, 0f, 75f));
            return result;
        }

        [Fact]
        public void LobeWithNarrowArm_DeepRingsInTheLobeHaveNoPointedTips()
        {
            // Retour utilisateur (capture) : "os bicos voltaram" — les anneaux qui ne tiennent
            // plus que dans le lobe finissaient en pointe vers le bras.
            var perimeter = LobeWithArm();
            var segments = ConcentricGenerator.Generate(perimeter, layers: 6, connections: 4);
            AssertWellFormed(segments, perimeter);
            float maxTurn = MaxRingTurnDegrees(segments, perimeter);
            Assert.True(maxTurn < 5f, $"Anneau pointu : virage de {maxTurn:F0}°");
        }

        /// <summary>Plus petit rayon (m) du cercle passant par trois sommets consécutifs d'un anneau fermé.</summary>
        private static float MinThreePointRadius(List<float2> loop)
        {
            float min = float.MaxValue;
            for (int i = 0; i < loop.Count; i++)
            {
                float2 a = loop[(i - 1 + loop.Count) % loop.Count], p = loop[i], b = loop[(i + 1) % loop.Count];
                float2 dIn = math.normalize(p - a), dOut = math.normalize(b - p);
                float sin = math.abs(dIn.x * dOut.y - dIn.y * dOut.x);
                float radius = math.dot(dIn, dOut) < 0f ? 0f : sin < 1e-6f ? float.MaxValue : math.distance(a, b) / (2f * sin);
                min = math.min(min, radius);
            }
            return min;
        }

        [Fact]
        public void LimitCurvature_RoundsAPointedTip_KeepsTrueCornersAndStaysInside()
        {
            // Retour utilisateur (capture, anneau qui finit en pointe face à une rue en U) :
            // "ainda existem alguns bicos em outras formas complexas". Filet de sécurité quelle
            // que soit la cause : une goutte à pointe de 40° (sommets tous les 25 m), et un vrai
            // coin (hérité du périmètre) sur l'arrondi opposé, qui doit rester en place.
            var loop = new List<float2>();
            var tip = new float2(0f, 0f);
            float half = math.radians(20f);
            float2 upper = new float2(math.cos(half), math.sin(half));
            float2 lower = new float2(math.cos(half), -math.sin(half));
            for (float d = 0f; d < 400f; d += 25f) loop.Add(tip + lower * d);
            float2 centre = new float2(400f * math.cos(half), 0f);
            float radius = 400f * math.sin(half);
            for (float a = -math.PI / 2f + 0.2f; a < math.PI / 2f - 0.1f; a += 0.2f) loop.Add(centre + radius * new float2(math.cos(a), math.sin(a)));
            for (float d = 400f; d > 0f; d -= 25f) loop.Add(tip + upper * d);
            var corners = new HashSet<(float, float)> { (loop[20].x, loop[20].y) };
            float2 corner = loop[20];
            var original = new List<float2>(loop);

            Assert.True(MinThreePointRadius(loop) < 10f, "La goutte de départ devrait avoir une pointe.");
            ConcentricGenerator.LimitCurvature(loop, corners, 60f);

            Assert.Equal(corner, loop[20]);
            // La pointe a reculé vers l'intérieur de sa courbe, sans jamais dépasser la forme d'origine.
            Assert.True(loop.Min(p => p.x) > 20f, $"Pointe non arrondie : x min = {loop.Min(p => p.x):F0}");
            var outline = original.Select(p => new float3(p.x, 0f, p.y)).ToList();
            Assert.All(loop, p => Assert.True(PointInPolygon(p, outline) || DistanceToPolygon(p, outline) < 0.5f, $"Sommet sorti : {p}"));
            // Aucun virage plus serré que le rayon minimal, hors du vrai coin et de ses voisins.
            var withoutCorner = loop.Where((p, i) => math.abs(i - 20) > 1).ToList();
            Assert.True(MinThreePointRadius(withoutCorner) > 40f, $"Encore une pointe : rayon {MinThreePointRadius(withoutCorner):F0} m");
        }

        [Fact]
        public void CurvedRings_UseFewLongArcs_NotANodeEvery25Metres()
        {
            // Retour utilisateur (capture) : "quando o mod gera as curvas, cria demasiados nós".
            // Cercle de 500 m, 3 anneaux (rayons ~375/250/125 m) : un nœud tous les 25 m en
            // faisait ~190 ; en arcs de 90° maximum coupés aux raccords des rayons, moins de 40 (28 mesurés).
            var perimeter = Circle(500f, 400);
            var segments = ConcentricGenerator.Generate(perimeter, layers: 3, connections: 4);
            AssertWellFormed(segments, perimeter);
            var rings = segments.Where(s => math.abs(DistanceToPolygon(s.Start.xz, perimeter) - DistanceToPolygon(s.End.xz, perimeter)) < RingDepthTolerance).ToList();
            Assert.True(rings.Count <= 40, $"{rings.Count} tronçons d'anneau");
            // Chaque tronçon reste fidèle au cercle : son milieu (FitCurve ≈ arc) est au bon rayon.
            foreach (RoadSegmentDef s in rings)
            {
                float r0 = math.length(s.Start.xz), r1 = math.length(s.End.xz);
                Assert.True(math.abs(r0 - r1) < RingDepthTolerance, $"Tronçon qui change de rayon : {r0:F0} -> {r1:F0}");
            }
        }

        [Fact]
        public void RealPerimeterWithBacktrack_RingsHaveNoPointedTips()
        {
            // Retour utilisateur (bicos persistants, "só aparecem quando passa uma estrada de
            // ligação") : périmètre réel de 990 points, exporté du jeu via le log [Diag concêntrico].
            // Au raccord de deux routes échantillonnées, le contour recule de ~5 m puis repart
            // (virage de 179°). Ce faux coin ultra-aigu "protégeait" toutes les pointes d'anneau de
            // la forme comme coins hérités — 17 cassures de 57° à 128°, jamais arrondies.
            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "real-perimeter-backtrack.txt");
            var perimeter = System.IO.File.ReadAllLines(path)
                .Where(l => l.Trim().Length > 0)
                .Select(l => l.Trim().Split(' '))
                .Select(a => new float3(float.Parse(a[0], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    float.Parse(a[1], System.Globalization.CultureInfo.InvariantCulture)))
                .ToList();
            foreach ((int layers, int connections) in new[] { (10, 2), (10, 4), (3, 2) })
            {
                var segments = ConcentricGenerator.Generate(perimeter, layers, connections);
                float maxTurn = MaxRingTurnDegrees(segments, perimeter);
                Assert.True(maxTurn < 20f, $"{layers} anneaux, {connections} rayons : anneau cassé à {maxTurn:F0}°");
            }

            // Retour utilisateur suivant : "está a criar formas cada vez mais circulares em vez de
            // guardar a forma original" (flou du champ de distance). Chaque anneau doit rester à
            // distance quasi constante du contour. Mesuré sur ce périmètre, 10 anneaux : distance
            // exacte -> écart max/min ≤ 1,13 par anneau ; avec le flou -> jusqu'à 3,4 (48 à 163 m
            // pour le premier anneau, qui coupait le lobe au lieu d'y entrer).
            ConcentricGenerator.Generate(perimeter, 10, 3); // autre réglage : pas de cache
            foreach (List<List<float2>> ring in ConcentricGenerator.LastRings)
            {
                var ringDepths = ring.SelectMany(loop => loop).Select(p => DistanceToPolygon(p, perimeter)).ToList();
                Assert.True(ringDepths.Max() < 1.25f * ringDepths.Min(),
                    $"Anneau déformé : de {ringDepths.Min():F0} à {ringDepths.Max():F0} m du périmètre");
            }
        }

        /// <summary>Copie de Game.Net.NetUtils.FitCurve (jeu) : la courbe réellement posée pour un tronçon IsArc.</summary>
        internal static float2[] GameCurve(RoadSegmentDef s, int samples = 24)
        {
            float2 a = s.Start.xz, dpos = s.End.xz;
            float2 p0 = a, p1, p2, p3 = dpos;
            if (!s.IsArc)
            {
                p1 = math.lerp(a, dpos, 1f / 3f); p2 = math.lerp(a, dpos, 2f / 3f);
            }
            else
            {
                float2 st = math.normalizesafe(s.StartTangent.xz), et = math.normalizesafe(s.EndTangent.xz);
                float num = math.distance(a, dpos);
                // Intersection de a + x*st et dpos - y*et.
                float2 r = st, q = -et; float den = r.x * q.y - r.y * q.x;
                float2 tt;
                if (math.abs(den) < 1e-6f) tt = new float2(num * 0.75f);
                else
                {
                    float2 w = dpos - a;
                    float x = (w.x * q.y - w.y * q.x) / den;
                    float y = (r.x * w.y - r.y * w.x) / den;
                    tt = math.clamp(new float2(x, y), num * 0.01f, num);
                }
                float num2 = math.dot(st, et);
                if (num2 > 0f) tt = math.lerp(tt, new float2(num / math.sqrt(2f * num2 + 2f)), math.min(1f, num2 * num2));
                else if (num2 < 0f) tt = math.lerp(tt, new float2(num * 1.2071068f), math.min(1f, num2 * num2));
                float l1 = tt.x, l2 = tt.y;
                float2 f5 = st, f6 = -et;
                float num3 = math.acos(math.saturate(-math.dot(f5, f6)));
                float num4 = math.tan(num3 / 2f);
                float num5 = (l1 + l2) / 6f;
                num5 = num4 >= 0.0001f ? num5 * (4f * math.tan(num3 / 4f) / num4) : num5 * 2f;
                p1 = a + f5 * math.min(l1, num5);
                p2 = dpos + f6 * math.min(l2, num5);
            }
            var pts = new float2[samples + 1];
            for (int i = 0; i <= samples; i++)
            {
                float u = (float)i / samples, v = 1f - u;
                pts[i] = v * v * v * p0 + 3f * v * v * u * p1 + 3f * v * u * u * p2 + u * u * u * p3;
            }
            return pts;
        }

        [Fact]
        public void RealPerimeter_JunctionsHaveOpenAnglesAndNoStubs()
        {
            // Retour utilisateur (capture en jeu, "Objetos sobrepostos" sur le lobe) : les rayons
            // suivaient l'axe du lobe et entraient dans la dobra de chaque anneau à ~44° (bords des
            // routes qui se chevauchent au carrefour), et des raccords tombaient à 3-15 m d'un
            // sommet voisin (tronçon minuscule collé au carrefour). Angles mesurés sur la courbe que
            // le jeu pose réellement (copie de NetUtils.FitCurve, voir GameCurve).
            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "real-perimeter-backtrack.txt");
            var perimeter = System.IO.File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => l.Trim().Split(' '))
                .Select(q => new float3(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture))).ToList();
            const float minJunctionAngle = 40f;
            // Largeur d'une route à deux voies : plus court, le carrefour mange tout le tronçon.
            const float minStubLength = 16f;
            foreach ((int layers, int connections) in new[] { (10, 2), (6, 4), (3, 2) })
            {
                var segs = ConcentricGenerator.Generate(perimeter, layers, connections);
                AssertWellFormed(segs, perimeter);
                // Direction de départ de chaque tronçon depuis chacune de ses extrémités.
                var leaving = new Dictionary<(float, float), List<float2>>();
                foreach (RoadSegmentDef s in segs)
                {
                    float2[] c = GameCurve(s);
                    var start = (s.Start.x, s.Start.z);
                    var end = (s.End.x, s.End.z);
                    if (!leaving.ContainsKey(start)) leaving[start] = new List<float2>();
                    if (!leaving.ContainsKey(end)) leaving[end] = new List<float2>();
                    leaving[start].Add(math.normalize(c[2] - c[0]));
                    leaving[end].Add(math.normalize(c[c.Length - 3] - c[c.Length - 1]));
                }
                foreach (var node in leaving)
                {
                    List<float2> dirs = node.Value;
                    for (int i = 0; i < dirs.Count; i++)
                    {
                        for (int j = i + 1; j < dirs.Count; j++)
                        {
                            float angle = math.degrees(math.acos(math.clamp(math.dot(dirs[i], dirs[j]), -1f, 1f)));
                            Assert.True(angle >= minJunctionAngle,
                                $"{layers} anneaux, {connections} rayons : deux routes à {angle:F0}° au nœud ({node.Key.Item1:F0}, {node.Key.Item2:F0})");
                        }
                    }
                }
                foreach (RoadSegmentDef s in segs)
                {
                    bool atJunction = leaving[(s.Start.x, s.Start.z)].Count >= 3 || leaving[(s.End.x, s.End.z)].Count >= 3;
                    float length = math.distance(s.Start.xz, s.End.xz);
                    Assert.True(!atJunction || length >= minStubLength || DistanceToPolygon(s.Start.xz, perimeter) < 0.5f || DistanceToPolygon(s.End.xz, perimeter) < 0.5f,
                        $"{layers} anneaux, {connections} rayons : tronçon de {length:F1} m collé à un carrefour ({s.Start.xz} -> {s.End.xz})");
                }
            }
        }

        /// <summary>
        /// Pour chaque carrefour (nœud à 3 routes ou plus) : plus petit angle entre deux routes qui
        /// en partent, mesuré de la position du nœud vers le point situé à `reach` mètres le long
        /// de chaque route (en suivant les tronçons au-delà des nœuds à 2 routes), pour chaque
        /// `reach` de la liste. Retourne (angle min, nœud, plus court tronçon collé à un carrefour).
        /// </summary>
        internal static (float minAngle, float2 at, float shortestStub) JunctionDivergence(List<RoadSegmentDef> segs, float[] reaches)
        {
            var curves = segs.Select(s => GameCurve(s, 48)).ToList();
            var byNode = new Dictionary<(float, float), List<int>>();
            for (int i = 0; i < segs.Count; i++)
            {
                foreach (var q in new[] { (segs[i].Start.x, segs[i].Start.z), (segs[i].End.x, segs[i].End.z) })
                {
                    if (!byNode.ContainsKey(q)) byNode[q] = new List<int>();
                    byNode[q].Add(i);
                }
            }
            // Polyligne d'une route qui part de `node` par le tronçon `first`, prolongée à travers les nœuds à 2 routes.
            List<float2> Walk((float, float) node, int first, float length)
            {
                var pts = new List<float2>();
                int seg = first;
                var from = node;
                float walked = 0f;
                for (int guard = 0; guard < 50 && walked < length; guard++)
                {
                    float2[] c = curves[seg];
                    bool forward = segs[seg].Start.x == from.Item1 && segs[seg].Start.z == from.Item2;
                    IEnumerable<float2> ordered = forward ? c : c.Reverse();
                    foreach (float2 q in ordered)
                    {
                        if (pts.Count > 0) walked += math.distance(pts[pts.Count - 1], q);
                        pts.Add(q);
                    }
                    var to = forward ? (segs[seg].End.x, segs[seg].End.z) : (segs[seg].Start.x, segs[seg].Start.z);
                    if (byNode[to].Count != 2) break;
                    int next = byNode[to][0] == seg ? byNode[to][1] : byNode[to][0];
                    from = to;
                    seg = next;
                }
                return pts;
            }
            float2 PointAt(List<float2> pts, float reach)
            {
                float walked = 0f;
                for (int i = 1; i < pts.Count; i++)
                {
                    float e = math.distance(pts[i - 1], pts[i]);
                    if (walked + e >= reach) return math.lerp(pts[i - 1], pts[i], (reach - walked) / math.max(e, 1e-4f));
                    walked += e;
                }
                return pts[pts.Count - 1];
            }
            float min = 180f;
            float2 at = default;
            float shortest = float.MaxValue;
            foreach (var kv in byNode.Where(kv => kv.Value.Count >= 3))
            {
                float2 n = new float2(kv.Key.Item1, kv.Key.Item2);
                var roads = kv.Value.Select(i => Walk(kv.Key, i, reaches.Max() + 5f)).ToList();
                foreach (int i in kv.Value)
                {
                    shortest = math.min(shortest, math.distance(segs[i].Start.xz, segs[i].End.xz));
                }
                foreach (float reach in reaches)
                {
                    var dirs = roads.Select(r => math.normalizesafe(PointAt(r, reach) - n)).ToList();
                    for (int a = 0; a < dirs.Count; a++)
                    for (int b = a + 1; b < dirs.Count; b++)
                    {
                        float ang = math.degrees(math.acos(math.clamp(math.dot(dirs[a], dirs[b]), -1f, 1f)));
                        if (ang < min) { min = ang; at = n; }
                    }
                }
            }
            return (min, at, shortest);
        }

        [Fact]
        public void RealPerimeter_RoadsDivergeFromEachJunction()
        {
            // Retour utilisateur (log [Diag colisão], 10 anneaux, 6 rayons, refus du jeu en trois
            // carrefours) : un rayon quittait l'anneau à 54°, mais l'anneau se recourbait vers lui
            // (33° à 40 m), et un tronçon d'anneau de 16 m restait collé au carrefour. Mesuré sur les
            // courbes que le jeu pose (voir GameCurve), jusqu'à 50 m de chaque carrefour.
            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "real-perimeter-backtrack.txt");
            var perimeter = System.IO.File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => l.Trim().Split(' '))
                .Select(q => new float3(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture))).ToList();
            foreach ((int layers, int connections) in new[] { (10, 6), (10, 2), (8, 3), (6, 4), (4, 6), (3, 2) })
            {
                var r = JunctionDivergence(ConcentricGenerator.Generate(perimeter, layers, connections), new[] { 10f, 20f, 35f, 50f });
                Assert.True(r.minAngle >= 40f,
                    $"{layers} anneaux, {connections} rayons : deux routes à {r.minAngle:F0}° près du carrefour ({r.at.x:F0}, {r.at.y:F0})");
                Assert.True(r.shortestStub >= 30f,
                    $"{layers} anneaux, {connections} rayons : tronçon de {r.shortestStub:F1} m collé à un carrefour");
            }
        }

        [Fact]
        public void RealPerimeter_ConnectionsAreContinuous()
        {
            // Retour utilisateur (capture en jeu) : "a estrada não é contínua" — un rayon bloqué
            // repartait d'un autre point de l'anneau et la ligação devenait un escalier de tronçons
            // décalés. Une chaîne ne repart jamais d'un autre point que celui où elle est arrivée :
            // sur un anneau intermédiaire, un nœud de rayon unique est au plus une FIN de chaîne
            // (rayon venu de l'extérieur), jamais un départ sans arrivée. Et jusqu'à 8 rayons,
            // aucune chaîne ne s'arrête avant le centre.
            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "real-perimeter-backtrack.txt");
            var perimeter = System.IO.File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => l.Trim().Split(' '))
                .Select(q => new float3(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture))).ToList();
            foreach ((int layers, int connections) in new[] { (10, 6), (10, 2), (10, 4), (10, 12), (8, 3), (6, 4), (6, 8), (4, 6), (3, 2) })
            {
                var segments = ConcentricGenerator.Generate(perimeter, layers, connections);
                var rings = ConcentricGenerator.LastRings;
                var levelOf = new Dictionary<(float, float), int>();
                for (int k = 0; k < rings.Count; k++)
                {
                    foreach (float2 q in rings[k].SelectMany(loop => loop))
                    {
                        levelOf[(q.x, q.y)] = k + 1;
                    }
                }
                int Level(float3 q) => DistanceToPolygon(q.xz, perimeter) < 0.5f ? 0 : levelOf.TryGetValue((q.x, q.z), out int l) ? l : -1;
                var spokes = segments.Where(s => Level(s.Start) != Level(s.End)).ToList();
                var spokesAt = new Dictionary<(float, float), List<RoadSegmentDef>>();
                foreach (RoadSegmentDef s in spokes)
                {
                    foreach (float3 q in new[] { s.Start, s.End })
                    {
                        var key = (q.x, q.z);
                        if (!spokesAt.ContainsKey(key)) spokesAt[key] = new List<RoadSegmentDef>();
                        spokesAt[key].Add(s);
                    }
                }
                foreach (var node in spokesAt.Where(n => n.Value.Count == 1))
                {
                    int level = levelOf.TryGetValue(node.Key, out int l) ? l : 0;
                    if (level <= 0 || level >= rings.Count)
                    {
                        continue;
                    }
                    RoadSegmentDef only = node.Value[0];
                    float3 other = only.Start.x == node.Key.Item1 && only.Start.z == node.Key.Item2 ? only.End : only.Start;
                    Assert.True(Level(other) < level,
                        $"{layers} anneaux, {connections} rayons : ligação qui repart d'ailleurs à l'anneau {level} en ({node.Key.Item1:F0}, {node.Key.Item2:F0})");
                    // Jusqu'à 8 rayons, tous vont jusqu'au centre ; au-delà, un rayon peut s'arrêter
                    // un peu avant faute de place dans une partie étroite (vu : 12 rayons, anneau 9/10).
                    Assert.True(connections > 8,
                        $"{layers} anneaux, {connections} rayons : ligação arrêtée à l'anneau {level} en ({node.Key.Item1:F0}, {node.Key.Item2:F0})");
                }
            }
        }

        [Fact]
        public void RealPerimeter_ConnectionsReachTheInnermostRing()
        {
            // Retour utilisateur (capture en jeu, 10 anneaux) : "a ligação não vai até ao centro".
            // Le rayon du lobe s'arrêtait à la première dobra d'anneau (angle fermé) ; il doit
            // maintenant la contourner et atteindre l'anneau le plus intérieur, pour chaque rayon.
            string path = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Fixtures", "real-perimeter-backtrack.txt");
            var perimeter = System.IO.File.ReadAllLines(path).Where(l => l.Trim().Length > 0).Select(l => l.Trim().Split(' '))
                .Select(q => new float3(float.Parse(q[0], System.Globalization.CultureInfo.InvariantCulture), 0f,
                    float.Parse(q[1], System.Globalization.CultureInfo.InvariantCulture))).ToList();
            foreach ((int layers, int connections) in new[] { (10, 2), (10, 4), (6, 3), (4, 2), (3, 2), (3, 4) })
            {
                var segments = ConcentricGenerator.Generate(perimeter, layers, connections);
                var innermost = new HashSet<(float, float)>(ConcentricGenerator.LastRings[ConcentricGenerator.LastRings.Count - 1]
                    .SelectMany(loop => loop).Select(q => (q.x, q.y)));
                int spokesIntoCentre = segments.Count(s => !s.IsArc
                    && (innermost.Contains((s.Start.x, s.Start.z)) ^ innermost.Contains((s.End.x, s.End.z))));
                // Deux rayons peuvent se rejoindre en chemin (à angle ouvert) et finir en une seule
                // route : au moins un rayon entre dans le centre, et aucun ne s'arrête en chemin
                // (voir RealPerimeter_ConnectionsAreContinuous).
                Assert.True(spokesIntoCentre >= 1,
                    $"{layers} anneaux, {connections} rayons : aucun rayon n'atteint l'anneau le plus intérieur");
            }
        }

        [Fact]
        public void Connections_AreSpreadAroundThePerimeter_NotBunchedInTheMiddle()
        {
            // Retour utilisateur (capture) : sur une forme allongée, les rayons partaient du petit
            // anneau intérieur et arrivaient tous groupés au milieu du périmètre.
            var perimeter = Ellipse(600f, 220f, 400);
            var segments = ConcentricGenerator.Generate(perimeter, layers: 3, connections: 8);
            AssertWellFormed(segments, perimeter);
            var onPerimeter = segments
                .SelectMany(s => new[] { s.Start, s.End })
                .Where(p => DistanceToPolygon(p.xz, perimeter) < 0.5f)
                .ToList();
            Assert.True(onPerimeter.Count >= 6, $"Seulement {onPerimeter.Count} rayons atteignent le périmètre");
            Assert.Contains(onPerimeter, p => p.x > 360f);
            Assert.Contains(onPerimeter, p => p.x < -360f);
        }

        [Fact]
        public void SharpCorneredShape_InnerRingsKeepSharpCorners()
        {
            // Un carré doit rester un carré (coins nets hérités du périmètre), pas devenir arrondi.
            var perimeter = Square(600f);
            var segments = ConcentricGenerator.Generate(perimeter, layers: 2, connections: 4);
            Assert.True(MaxRingTurnDegrees(segments, perimeter) > 70f, "Les coins du carré ont été arrondis.");
        }

        [Fact]
        public void ChangingLayersOnTheSamePerimeter_ReusesTheDistanceField()
        {
            // Retour utilisateur : "lentidão quando se tenta mudar parâmetros" — le champ de
            // distance (partie coûteuse) ne dépend que du périmètre.
            var perimeter = Circle(520f, 500);
            ConcentricGenerator.Generate(perimeter, layers: 2, connections: 4);
            var stopwatch = Stopwatch.StartNew();
            for (int layers = 3; layers <= 6; layers++)
            {
                ConcentricGenerator.Generate(perimeter, layers, connections: 5);
            }
            stopwatch.Stop();
            Assert.True(stopwatch.ElapsedMilliseconds < 400, $"4 changements de réglage en {stopwatch.ElapsedMilliseconds} ms");
        }

        [Fact]
        public void ThroughGenerateLoopGrid_ConcentricModeTakesOver()
        {
            var parameters = new GridParameters { CollectorSpacingMeters = 300f, ConcentricMode = true, ConcentricLayers = 2, ConcentricConnections = 4 };
            var segments = GridGenerator.GenerateLoopGrid(Square(600f), parameters);
            Assert.NotEmpty(segments);
            Assert.DoesNotContain(segments, s => s.IsAvenue || s.IsPedestrian || s.IsCulDeSacEnd);
        }
    }
}
