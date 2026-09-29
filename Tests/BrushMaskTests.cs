using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GridRoadGenerator.Core;
using Unity.Mathematics;
using Xunit;

namespace GridRoadGenerator.Tests
{
    public class BrushMaskTests
    {
        private static float Area(List<float3> ring)
        {
            float a = 0f;
            for (int i = 0; i < ring.Count; i++)
            {
                float2 p = ring[i].xz, q = ring[(i + 1) % ring.Count].xz;
                a += p.x * q.y - q.x * p.y;
            }
            return math.abs(a) * 0.5f;
        }

        private static void Path(BrushMask mask, float radius, bool erase, params float[] xz)
        {
            for (int i = 2; i + 1 < xz.Length; i += 2)
            {
                if (i == 2) mask.Stamp(new float2(xz[0], xz[1]), radius, erase);
                mask.Stroke(new float2(xz[i - 2], xz[i - 1]), new float2(xz[i], xz[i + 1]), radius, erase);
            }
        }

        private static BrushMask Sample()
        {
            var mask = new BrushMask();
            Path(mask, 70f, false, 120, 380, 200, 300, 300, 270, 400, 300, 470, 220);
            Path(mask, 45f, false, 300, 270, 310, 150, 260, 90);
            Path(mask, 30f, true, 330, 330, 360, 345);
            return mask;
        }

        [Fact]
        public void Outline_IsSimpleAndMatchesThePaintedArea()
        {
            BrushMask mask = Sample();
            List<float3> ring = mask.Outline();
            Assert.True(ring.Count >= 8);
            Assert.True(FreeAreaPerimeter.IsSimple(ring));
            Assert.InRange(Area(ring), mask.Area * 0.9f, mask.Area * 1.05f);
            // Lisse : pas de marche de grille (virages doux entre points voisins).
            for (int i = 0; i < ring.Count; i++)
            {
                float2 a = ring[i].xz, b = ring[(i + 1) % ring.Count].xz, c = ring[(i + 2) % ring.Count].xz;
                float turn = math.degrees(math.acos(math.clamp(math.dot(math.normalize(b - a), math.normalize(c - b)), -1f, 1f)));
                Assert.True(turn < 60f, $"virage de {turn:F0}° en {b}");
            }

            string outDir = Environment.GetEnvironmentVariable("GRG_OUT");
            if (!string.IsNullOrEmpty(outDir))
            {
                var height = new Func<float2, float>(p => 40f * math.exp(-math.lengthsq(p - new float2(300f, 260f)) / (2f * 160f * 160f)));
                var ring3 = ring.Select(p => new float3(p.x, height(p.xz), p.z)).ToList();
                GridParameters parameters = GridParameters.Default;
                parameters.HeightAt = height;
                parameters.OrganicMode = true;
                var interior = GridGenerator.GenerateGrid(ring3, parameters);
                var road = FreeAreaPerimeter.PerimeterRoad(ring3, interior);
                var inv = System.Globalization.CultureInfo.InvariantCulture;
                var lines = new List<string> { "ring " + string.Join(";", ring.Select(p => $"{p.x.ToString(inv)} {p.z.ToString(inv)}")) };
                lines.AddRange(mask.RawBoundary().Select(s => $"B {s.a.x.ToString(inv)} {s.a.y.ToString(inv)};{s.b.x.ToString(inv)} {s.b.y.ToString(inv)}"));
                foreach (RoadSegmentDef s in interior.Concat(road))
                {
                    string kind = road.Contains(s) ? "P" : s.IsAvenue ? "A" : "S";
                    lines.Add(kind + " " + string.Join(";", ConcentricGeneratorTests.GameCurve(s, 16).Select(q => $"{q.x.ToString(inv)} {q.y.ToString(inv)}")));
                }
                File.WriteAllLines(System.IO.Path.Combine(outDir, "brush.txt"), lines);
            }
        }

        [Fact]
        public void Outline_KeepsTheLargestIslandAndFillsHoles()
        {
            var mask = new BrushMask();
            mask.Stamp(new float2(0f, 0f), 150f, false);
            mask.Stamp(new float2(0f, 0f), 40f, true);        // trou
            mask.Stamp(new float2(600f, 0f), 60f, false);     // petite île séparée
            List<float3> ring = mask.Outline();
            Assert.True(FreeAreaPerimeter.IsSimple(ring));
            Assert.InRange(Area(ring), math.PI * 150f * 150f * 0.93f, math.PI * 150f * 150f * 1.03f);
            Assert.All(ring, p => Assert.True(p.x < 200f));
        }

        [Fact]
        public void Outline_HandlesCornerPinches()
        {
            var mask = new BrushMask();
            // Deux carrés reliés par un seul coin, plus un pont plus loin (pincement dans la même tache).
            for (int i = 0; i < 20; i++)
                for (int j = 0; j < 20; j++)
                {
                    mask.Stamp(new float2(i * 4f + 2f, j * 4f + 2f), 1f, false);
                    mask.Stamp(new float2(80f + i * 4f + 2f, 80f + j * 4f + 2f), 1f, false);
                }
            Path(mask, 12f, false, 70, 20, 140, 20, 140, 90);
            List<float3> ring = mask.Outline();
            Assert.NotEmpty(ring);
            Assert.True(FreeAreaPerimeter.IsSimple(ring));
        }

        [Fact]
        public void RawBoundary_MatchesTheEdgeOfThePaintAfterErasing()
        {
            BrushMask mask = Sample();
            // Longueur du bord = côtés de cases entre peint et non peint, recomptés à la main sur une grille.
            float expected = 0f;
            for (int i = -20; i < 200; i++)
                for (int j = -20; j < 200; j++)
                {
                    if (!IsPainted(mask, i, j)) continue;
                    foreach (int2 d in new[] { new int2(1, 0), new int2(-1, 0), new int2(0, 1), new int2(0, -1) })
                        if (!IsPainted(mask, i + d.x, j + d.y)) expected += BrushMask.Cell;
                }
            float actual = mask.RawBoundary().Sum(s => math.distance(s.a, s.b));
            Assert.Equal(expected, actual, 1);
            Assert.All(mask.RawBoundary(), s => Assert.True(math.distance(s.a, s.b) <= 12 * BrushMask.Cell + 0.01f));
        }

        private static bool IsPainted(BrushMask mask, int i, int j) => mask.IsPainted(i, j);

        [Fact]
        public void PaintingABigArea_StaysFast()
        {
            var mask = new BrushMask();
            // Préchauffage (compilation JIT) hors mesure.
            new BrushMask().Stroke(float2.zero, new float2(100f, 0f), 200f, false);
            new BrushMask().RawBoundary();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var strokeTime = new System.Diagnostics.Stopwatch();
            int frames = 0;
            // Un trait de 1,6 km au pinceau de 400 m, 30 m par frame, bord redessiné à chaque frame.
            float2 last = new float2(0f, 0f);
            mask.Stamp(last, 200f, false);
            for (float x = 30f; x <= 1600f; x += 30f, frames++)
            {
                float2 p = new float2(x, 300f * math.sin(x / 400f));
                strokeTime.Start();
                mask.Stroke(last, p, 200f, false);
                strokeTime.Stop();
                last = p;
                mask.RawBoundary();
            }
            double perFrame = stopwatch.Elapsed.TotalMilliseconds / frames;
            Assert.True(perFrame < 3.0, $"{perFrame:F2} ms par frame de peinture (dont trait {strokeTime.Elapsed.TotalMilliseconds / frames:F2} ms)");
            Assert.True(mask.RawBoundary().Count < 1500, $"{mask.RawBoundary().Count} segments de bord");
        }

        [Fact]
        public void FillRuns_CoverExactlyThePaintedCells()
        {
            BrushMask mask = Sample();
            var runs = mask.FillRuns();
            float covered = runs.Sum(r => math.distance(r.a, r.b)) * BrushMask.Cell;
            Assert.Equal(mask.Area, covered, 1);
            Assert.All(runs, r => Assert.True(math.distance(r.a, r.b) <= 16 * BrushMask.Cell + 0.01f));
            foreach (var r in runs)
            {
                float2 mid = (r.a + r.b) * 0.5f;
                Assert.True(mask.IsPainted((int)math.floor(mid.x / BrushMask.Cell), (int)math.floor(mid.y / BrushMask.Cell)));
            }
        }

        [Fact]
        public void PolygonFillRuns_CoverThePolygon()
        {
            var square = new List<float3> { new float3(0, 0, 0), new float3(200, 0, 0), new float3(200, 0, 100), new float3(0, 0, 100) };
            var runs = FreeAreaPerimeter.FillRuns(square);
            float covered = runs.Sum(r => math.distance(r.a, r.b)) * 4f;
            Assert.Equal(200f * 100f, covered, 0);
            Assert.All(runs, r => Assert.True(math.distance(r.a, r.b) <= 64.01f));
        }

        [Fact]
        public void SquareStamp_PaintsASquare()
        {
            var mask = new BrushMask();
            mask.Stamp(new float2(100f, 100f), 50f, false, square: true);
            Assert.InRange(mask.Area, 96f * 96f, 104f * 104f); // à une case (4 m) près
            Assert.True(mask.IsPainted((int)math.floor(146f / BrushMask.Cell), (int)math.floor(146f / BrushMask.Cell)));   // coin
            Assert.False(mask.IsPainted((int)math.floor(156f / BrushMask.Cell), (int)math.floor(100f / BrushMask.Cell)));  // dehors
            List<float3> ring = mask.Outline();
            Assert.True(FreeAreaPerimeter.IsSimple(ring));
            mask.Stamp(new float2(100f, 100f), 20f, true, square: true);                                                     // gomme carrée
            Assert.InRange(mask.Area, 96f * 96f - 44f * 44f, 104f * 104f - 36f * 36f);
        }

        [Fact]
        public void RotatedSquareStamp_PaintsARotatedSquare()
        {
            var mask = new BrushMask();
            mask.Stamp(new float2(0f, 0f), 50f, false, square: true, angleDegrees: 45f);
            Assert.InRange(mask.Area, 100f * 100f * 0.93f, 100f * 100f * 1.07f);
            // À 45°, les coins sont sur les axes : (68, 0) est dedans, (45, 45) dehors.
            Assert.True(mask.IsPainted((int)math.floor(66f / BrushMask.Cell), 0));
            Assert.False(mask.IsPainted((int)math.floor(46f / BrushMask.Cell), (int)math.floor(46f / BrushMask.Cell)));
        }

        [Fact]
        public void EmptyOrTinyPaint_GivesNoOutline()
        {
            Assert.Empty(new BrushMask().Outline());
            var mask = new BrushMask();
            mask.Stamp(new float2(10f, 10f), 5f, false);
            Assert.Empty(mask.Outline());
        }
    }
}
