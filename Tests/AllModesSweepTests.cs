using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using GridRoadGenerator.Core;
using Unity.Mathematics;
using Xunit;
using Xunit.Abstractions;

namespace GridRoadGenerator.Tests
{
    /// <summary>
    /// Balayage complet (demande utilisateur : "testa todos os modos com todas as ferramentas") : chaque
    /// motif, sur chaque façon de choisir la zone (périmètre existant, zone dessinée, zone peinte), avec
    /// les valeurs extrêmes et moyennes de ses curseurs. Aucune génération ne doit lever d'exception ;
    /// les temps sont rapportés (les plus lents en tête) pour repérer ce qui ne serait pas fluide.
    /// </summary>
    public class AllModesSweepTests
    {
        private readonly ITestOutputHelper _out;
        public AllModesSweepTests(ITestOutputHelper o) { _out = o; }

        private static List<float3> Poly(params float[] xz)
        {
            var list = new List<float3>();
            for (int i = 0; i < xz.Length; i += 2) list.Add(new float3(xz[i], 0f, xz[i + 1]));
            return list;
        }

        private static List<float3> Rotated(List<float3> shape, float degrees)
        {
            float a = math.radians(degrees), c = math.cos(a), s = math.sin(a);
            return shape.Select(p => new float3(p.x * c - p.z * s, 0f, p.x * s + p.z * c)).ToList();
        }

        private static List<float3> LoadRing(string file) =>
            File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fixtures", file))
                .Where(l => l.Trim().Length > 0).Select(l => l.Trim().Split(' '))
                .Select(q => new float3(float.Parse(q[0], CultureInfo.InvariantCulture), 0f, float.Parse(q[1], CultureInfo.InvariantCulture))).ToList();

        private static List<float3> Painted(float diameter, params float2[] stroke)
        {
            var mask = new BrushMask();
            mask.Stamp(stroke[0], diameter / 2f, false);
            for (int i = 1; i < stroke.Length; i++) mask.Stroke(stroke[i - 1], stroke[i], diameter / 2f, false);
            return mask.Outline();
        }

        private static IEnumerable<(string name, bool free, List<float3> shape)> Areas()
        {
            // Périmètre existant (nœuds de routes).
            yield return ("existente retângulo 600x400", false, Poly(0, 0, 600, 0, 600, 400, 0, 400));
            yield return ("existente em L", false, Poly(0, 0, 600, 0, 600, 300, 300, 300, 300, 600, 0, 600));
            yield return ("existente rodado 30°", false, Rotated(Poly(0, 0, 800, 0, 800, 500, 0, 500), 30f));
            yield return ("existente triângulo", false, Poly(0, 0, 700, 0, 200, 600));
            yield return ("existente pequeno 90x90", false, Poly(0, 0, 90, 0, 90, 90, 0, 90));
            yield return ("existente estreito 1200x90", false, Poly(0, 0, 1200, 0, 1200, 90, 0, 90));
            yield return ("existente 2 nós", false, Poly(0, 0, 500, 0));
            yield return ("existente curvo (jogo)", false, LoadRing("real-perimeter-backtrack.txt"));
            // Zona desenhada (pontos arredondados como no jogo).
            yield return ("desenhada hexágono", true, FreeAreaPerimeter.Smooth(Poly(0, 0, 900, -100, 1400, 400, 1100, 1000, 300, 1100, -200, 500)));
            yield return ("desenhada estreita", true, FreeAreaPerimeter.Smooth(Poly(0, 0, 1500, 0, 1500, 140, 0, 140)));
            yield return ("desenhada (jogo)", true, LoadRing("free-area-ring.txt"));
            // Zona pintada (contorno do pincel).
            yield return ("pintada pincel 100 m", true, Painted(100f, new float2(0, 0), new float2(300, 0), new float2(300, 200), new float2(0, 200)));
            yield return ("pintada pincel 1000 m", true, Painted(1000f, new float2(0, 0), new float2(2500, 0), new float2(2500, 900), new float2(500, 1600)));
        }

        private static IEnumerable<(string name, bool loop, GridParameters p)> Patterns()
        {
            GridParameters D() => GridParameters.Default;
            foreach (int n in new[] { 1, 6, 12 })
            {
                var p = D(); p.Columns = n; p.Rows = n; yield return ($"Grelha ajustar {n}x{n}", false, p);
            }
            foreach (float s in new[] { 60f, 150f, 300f })
            {
                var p = D(); p.Mode = SpacingMode.FixedSpacing; p.SpacingMeters = s; p.AngleOffsetDegrees = s == 150f ? 33f : 0f;
                yield return ($"Grelha espaçamento {s}", false, p);
            }
            foreach (CulDeSacAxis axis in new[] { CulDeSacAxis.Columns, CulDeSacAxis.Rows, CulDeSacAxis.Both })
            foreach (float ratio in new[] { 0f, 50f, 100f })
            {
                var p = D(); p.Columns = 6; p.Rows = 6; p.CulDeSacMode = true; p.CulDeSacAxis = axis; p.CulDeSacRatio = ratio; p.CulDeSacDepth = ratio == 50f ? 0.8f : 0.5f;
                yield return ($"Grelha becos {axis} {ratio}%", false, p);
            }
            {
                var p = D(); p.Columns = 8; p.Rows = 8; p.AvenueColumnEnabled = true; p.AvenueRowEnabled = true; p.AvenueColumnIndex = 23; p.AvenueRowIndex = 3;
                yield return ("Grelha avenidas + rotunda", false, p);
                var q = D(); q.Columns = 6; q.Rows = 6; q.AlignToTerrain = true; yield return ("Grelha alinhada ao relevo", false, q);
            }
            foreach (float b in new[] { 160f, 400f })
            foreach (float l in new[] { 40f, 150f })
            {
                var p = D(); p.TreeMode = true; p.TreeBranchSpacing = b; p.TreeCulDeSacSpacing = 60f + l / 2f; p.TreeCulDeSacLength = l;
                yield return ($"Árvore ramos {b} becos {l}", false, p);
            }
            foreach (float s in new[] { 60f, 150f })
            foreach (float c in new[] { 0f, 100f })
            {
                var p = D(); p.OrganicMode = true; p.OrganicStreetSpacing = s; p.OrganicCurviness = c; p.OrganicLoopShare = 100f - c; p.OrganicSeed = (int)s;
                yield return ($"Orgânico espaço {s} curvas {c}", false, p);
            }
            foreach (float s in new[] { 60f, 150f })
            foreach (float k in new[] { 150f, 500f })
            {
                var p = D(); p.ContourMode = true; p.ContourSpacing = s; p.ContourConnectorSpacing = k;
                yield return ($"Relevo curvas {s} ligações {k}", false, p);
            }
            foreach (float r in new[] { 100f, 250f, 400f })
            foreach (int a in new[] { 3, 16 })
            {
                var p = D(); p.MixedMode = true; p.MixedCoreRadius = r; p.RadialAvenues = a; p.RadialRoundaboutRadius = 40f; p.RadialLayers = 2;
                yield return ($"Misto centro {r} avenidas {a}", false, p);
            }
            foreach (float s in new[] { 200f, 400f })
            foreach (float r in new[] { 0f, 100f })
            {
                var p = D(); p.CollectorSpacingMeters = s; p.LoopCulDeSacRatio = r;
                yield return ($"Loop coletoras {s} becos {r}%", true, p);
            }
            foreach (float z in new[] { 100f, 400f })
            {
                var p = D(); p.SuperblockMode = true; p.SuperblockZoneMeters = z; yield return ($"Superquarteirão {z}", true, p);
            }
            foreach (int layers in new[] { 1, 10 })
            foreach (int links in new[] { 2, 12 })
            {
                var p = D(); p.ConcentricMode = true; p.ConcentricLayers = layers; p.ConcentricConnections = links;
                yield return ($"Concêntrico camadas {layers} ligações {links}", true, p);
            }
            foreach (int a in new[] { 3, 16 })
            foreach (float rb in new[] { 20f, 150f })
            foreach (int layers in new[] { 0, 5 })
            {
                var p = D(); p.ConcentricMode = true; p.RadialMode = true; p.RadialAvenues = a; p.RadialRoundaboutRadius = rb; p.RadialLayers = layers;
                yield return ($"Radial avenidas {a} rotunda {rb} camadas {layers}", true, p);
            }
        }

        [Fact]
        public void EveryPatternOnEveryAreaType_NeverThrows()
        {
            // Relevo sintético (colinas) : les motifs qui suivent le terrain ont quelque chose à suivre.
            Func<float2, float> hills = q => 500f + 40f * math.sin(q.x / 260f) * math.cos(q.y / 310f) + 0.04f * q.x;
            var failures = new List<string>();
            var timings = new List<(double ms, string what, int segments)>();
            foreach ((string area, bool free, List<float3> shape) in Areas())
            {
                foreach ((string pattern, bool loop, GridParameters template) in Patterns())
                {
                    GridParameters p = template;
                    p.HeightAt = hills;
                    List<float3> positions = shape.Select(q => new float3(q.x, hills(q.xz), q.z)).ToList();
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        List<RoadSegmentDef> segments = loop ? GridGenerator.GenerateLoopGrid(positions, p) : GridGenerator.GenerateGrid(positions, p);
                        if (free && positions.Count >= 3 && segments.Count > 0)
                        {
                            segments.AddRange(FreeAreaPerimeter.PerimeterRoad(positions, segments));
                        }
                        sw.Stop();
                        foreach (RoadSegmentDef s in segments)
                        {
                            if (!math.all(math.isfinite(s.Start)) || !math.all(math.isfinite(s.End)))
                            {
                                failures.Add($"{area} / {pattern} : ponto inválido (NaN)");
                                break;
                            }
                        }
                        timings.Add((sw.Elapsed.TotalMilliseconds, $"{area} / {pattern}", segments.Count));
                    }
                    catch (ArgumentException) when (positions.Count < 3 && loop)
                    {
                        // Loop et dérivés exigent un vrai polygone : refus attendu, pas une erreur.
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{area} / {pattern} : {e.GetType().Name} {e.Message} {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}");
                    }
                }
            }
            _out.WriteLine($"{timings.Count} gerações, {failures.Count} erro(s)");
            foreach (string f in failures) _out.WriteLine("ERRO " + f);
            _out.WriteLine("Mais lentas :");
            foreach (var t in timings.OrderByDescending(t => t.ms).Take(25)) _out.WriteLine($"  {t.ms,7:F0} ms  {t.segments,5} troços  {t.what}");
            Assert.Empty(failures);
        }
    }
}
