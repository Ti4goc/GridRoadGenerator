using System.Collections.Generic;
using Unity.Mathematics;

namespace GridRoadGenerator.Core
{
    /// <summary>
    /// Mode "Pincel" : zone peinte au pinceau sur le terrain, en cases de Cell mètres. Outline() en
    /// tire le contour (plus grande tache, trous bouchés), lissé, qui sert de périmètre comme la
    /// zone libre cliquée (voir FreeAreaPerimeter).
    /// </summary>
    public sealed class BrushMask
    {
        public const float Cell = 4f;
        public const float MinDiameter = 1f;
        public const float MaxDiameter = 1000f;
        public const float DefaultDiameter = 150f;

        // Cases peintes : tuiles de 64 × 64 cases (tableaux), bien plus rapides à interroger qu'un
        // ensemble de clés — un tampon de 400 m touche ~8000 cases à chaque frame de peinture.
        private const int TileBits = 6;
        private const int TileSize = 1 << TileBits;
        private readonly Dictionary<long, bool[]> _tiles = new Dictionary<long, bool[]>();
        private long _lastTileKey = long.MinValue;
        private bool[] _lastTile;
        private int _count;
        /// <summary>
        /// Cases peintes au bord de la zone (un voisin non peint), tenues à jour à chaque tampon :
        /// l'aperçu pendant la peinture (RawBoundary) ne parcourt qu'elles, jamais toute la zone.
        /// </summary>
        private readonly HashSet<long> _edgeCells = new HashSet<long>();
        /// <summary>Longueur maximale (cases) d'un segment fusionné de RawBoundary : suit encore le relief.</summary>
        private const int MaxRun = 12;

        /// <summary>Incrémenté à chaque changement (caches du contour et du dessin).</summary>
        public int Version { get; private set; }
        public bool IsEmpty => _count == 0;
        /// <summary>Surface peinte (m²).</summary>
        public float Area => _count * Cell * Cell;

        private bool[] Tile(int i, int j, bool create)
        {
            long key = Key(i >> TileBits, j >> TileBits);
            if (key == _lastTileKey) return _lastTile;
            if (!_tiles.TryGetValue(key, out bool[] tile))
            {
                if (!create) return null;
                _tiles[key] = tile = new bool[TileSize * TileSize];
            }
            _lastTileKey = key;
            _lastTile = tile;
            return tile;
        }

        /// <summary>Vrai si la case (i, j) est peinte.</summary>
        public bool IsPainted(int i, int j)
        {
            bool[] tile = Tile(i, j, false);
            return tile != null && tile[((j & (TileSize - 1)) << TileBits) | (i & (TileSize - 1))];
        }

        /// <summary>Peint/efface une case ; vrai si elle a changé.</summary>
        private bool Set(int i, int j, bool painted)
        {
            bool[] tile = Tile(i, j, painted);
            if (tile == null) return false;
            int index = ((j & (TileSize - 1)) << TileBits) | (i & (TileSize - 1));
            if (tile[index] == painted) return false;
            tile[index] = painted;
            _count += painted ? 1 : -1;
            return true;
        }

        /// <summary>Toutes les cases peintes.</summary>
        private IEnumerable<int2> PaintedCells()
        {
            foreach (var pair in _tiles)
            {
                int2 origin = FromKey(pair.Key) * TileSize;
                bool[] tile = pair.Value;
                for (int index = 0; index < tile.Length; index++)
                {
                    if (tile[index]) yield return origin + new int2(index & (TileSize - 1), index >> TileBits);
                }
            }
        }

        private static long Key(int i, int j) => ((long)i << 32) ^ (uint)j;
        private static int2 FromKey(long k) => new int2((int)(k >> 32), (int)(uint)k);

        /// <summary>Copie de la zone peinte (annuler/refaire).</summary>
        public object Save()
        {
            var copy = new Dictionary<long, bool[]>(_tiles.Count);
            foreach (var pair in _tiles)
            {
                copy[pair.Key] = (bool[])pair.Value.Clone();
            }
            return (copy, _count, new HashSet<long>(_edgeCells));
        }

        /// <summary>Remet une copie faite par Save.</summary>
        public void Restore(object state)
        {
            var (tiles, count, edges) = ((Dictionary<long, bool[]>, int, HashSet<long>))state;
            _tiles.Clear();
            foreach (var pair in tiles)
            {
                _tiles[pair.Key] = (bool[])pair.Value.Clone();
            }
            _count = count;
            _edgeCells.Clear();
            _edgeCells.UnionWith(edges);
            _lastTileKey = long.MinValue;
            _lastTile = null;
            Version++;
        }

        public void Clear()
        {
            if (_count > 0)
            {
                _tiles.Clear();
                _lastTileKey = long.MinValue;
                _lastTile = null;
                _count = 0;
                _edgeCells.Clear();
                Version++;
            }
        }

        /// <summary>
        /// Tampon (centre en xz, rayon en m = demi-côté pour le carré, aligné sur les axes du monde) :
        /// ajoute ou retire les cases dont le centre est dedans.
        /// </summary>
        public void Stamp(float2 centre, float radius, bool erase, bool square = false, float angleDegrees = 0f)
        {
            // Pinceau de 1 m (comme dans le jeu) : au moins la case sous le curseur.
            radius = math.max(radius, 0.5f * Cell);
            if (square && math.abs(angleDegrees % 90f) > 0.01f)
            {
                StampRotatedSquare(centre, radius, erase, math.radians(angleDegrees));
                return;
            }
            int2 min = (int2)math.floor((centre - radius) / Cell), max = (int2)math.floor((centre + radius) / Cell);
            _changed.Clear();
            float2 local = centre / Cell - 0.5f;
            float r2 = radius * radius / (Cell * Cell);
            for (int i = min.x; i <= max.x; i++)
            {
                // Étendue de la colonne i dans le tampon : pas de test case par case.
                float dx = i - local.x, rest = r2 - dx * dx;
                if (rest < 0f) continue;
                float half = square ? radius / Cell : math.sqrt(rest);
                int j0 = math.max(min.y, (int)math.ceil(local.y - half)), j1 = math.min(max.y, (int)math.floor(local.y + half));
                for (int j = j0; j <= j1; j++)
                {
                    if (Set(i, j, !erase)) _changed.Add(new int2(i, j));
                }
            }
            CommitChanges();
        }

        /// <summary>Carré tourné de `angle` (radians) : cases dont le centre est dans le carré.</summary>
        private void StampRotatedSquare(float2 centre, float radius, bool erase, float angle)
        {
            _changed.Clear();
            float2 axisX = new float2(math.cos(angle), math.sin(angle)), axisZ = new float2(-axisX.y, axisX.x);
            float reach = radius * math.SQRT2;
            int2 min = (int2)math.floor((centre - reach) / Cell), max = (int2)math.floor((centre + reach) / Cell);
            for (int i = min.x; i <= max.x; i++)
            {
                for (int j = min.y; j <= max.y; j++)
                {
                    float2 d = (new float2(i, j) + 0.5f) * Cell - centre;
                    if (math.abs(math.dot(d, axisX)) > radius || math.abs(math.dot(d, axisZ)) > radius) continue;
                    if (Set(i, j, !erase)) _changed.Add(new int2(i, j));
                }
            }
            CommitChanges();
        }

        /// <summary>Version et cases du bord mises à jour après un tampon (seulement autour des cases changées).</summary>
        private void CommitChanges()
        {
            if (_changed.Count == 0) return;
            Version++;
            // Bord mis à jour seulement pour les cases changées et leurs voisines.
            foreach (int2 c in _changed)
            {
                UpdateEdge(c.x, c.y);
                UpdateEdge(c.x + 1, c.y);
                UpdateEdge(c.x - 1, c.y);
                UpdateEdge(c.x, c.y + 1);
                UpdateEdge(c.x, c.y - 1);
            }
        }

        private readonly List<int2> _changed = new List<int2>();

        private void UpdateEdge(int i, int j)
        {
            bool edge = IsPainted(i, j) && (!IsPainted(i + 1, j) || !IsPainted(i - 1, j) || !IsPainted(i, j + 1) || !IsPainted(i, j - 1));
            if (edge) _edgeCells.Add(Key(i, j)); else _edgeCells.Remove(Key(i, j));
        }

        /// <summary>
        /// Trait de from (déjà tamponné au frame précédent) à to : tampons rapprochés, sans trou
        /// même si la souris va vite.
        /// </summary>
        public void Stroke(float2 from, float2 to, float radius, bool erase, bool square = false, float angleDegrees = 0f)
        {
            float length = math.distance(from, to);
            if (length < 0.5f * Cell) return;
            int steps = math.max(1, (int)math.ceil(length / math.max(radius * 0.25f, Cell)));
            for (int k = 1; k <= steps; k++)
            {
                Stamp(math.lerp(from, to, (float)k / steps), radius, erase, square, angleDegrees);
            }
        }

        /// <summary>
        /// Segments (xz) du bord brut des cases peintes, pour l'aperçu pendant la peinture : côtés
        /// alignés fusionnés (au plus MaxRun cases), à partir des seules cases du bord.
        /// </summary>
        public List<(float2 a, float2 b)> RawBoundary()
        {
            // Côtés exposés par ligne de la grille : horizontaux (ligne z, case x), verticaux (ligne x, case z).
            var horizontal = new Dictionary<int, List<int>>();
            var vertical = new Dictionary<int, List<int>>();
            void Add(Dictionary<int, List<int>> lines, int line, int at)
            {
                if (!lines.TryGetValue(line, out var list)) lines[line] = list = new List<int>();
                list.Add(at);
            }
            foreach (long k in _edgeCells)
            {
                int2 c = FromKey(k);
                if (!IsPainted(c.x, c.y - 1)) Add(horizontal, 2 * c.y, c.x);
                if (!IsPainted(c.x, c.y + 1)) Add(horizontal, 2 * (c.y + 1) + 1, c.x);
                if (!IsPainted(c.x - 1, c.y)) Add(vertical, 2 * c.x, c.y);
                if (!IsPainted(c.x + 1, c.y)) Add(vertical, 2 * (c.x + 1) + 1, c.y);
            }
            var result = new List<(float2, float2)>();
            void Merge(Dictionary<int, List<int>> lines, bool isHorizontal)
            {
                foreach (var pair in lines)
                {
                    // Clé = 2 × ligne (+1 selon le côté, pour ne pas fusionner deux bords opposés).
                    int line = pair.Key >> 1;
                    List<int> at = pair.Value;
                    at.Sort();
                    int start = 0;
                    for (int n = 1; n <= at.Count; n++)
                    {
                        if (n < at.Count && at[n] == at[n - 1] + 1 && n - start < MaxRun) continue;
                        float from = at[start] * Cell, to = (at[n - 1] + 1) * Cell, fixedAt = line * Cell;
                        result.Add(isHorizontal ? (new float2(from, fixedAt), new float2(to, fixedAt)) : (new float2(fixedAt, from), new float2(fixedAt, to)));
                        start = n;
                    }
                }
            }
            Merge(horizontal, true);
            Merge(vertical, false);
            return result;
        }

        /// <summary>
        /// Remplissage de l'aperçu : bandes horizontales (xz, au centre des rangées de cases) des cases
        /// peintes, d'au plus maxCells cases, lues directement dans les tuiles (sans table de hachage).
        /// Dessinées en lignes projetées de largeur Cell, elles couvrent exactement la zone peinte.
        /// rowStep > 1 (grande zone) : une rangée sur rowStep, à dessiner en largeur rowStep × Cell.
        /// </summary>
        public List<(float2 a, float2 b)> FillRuns(int maxCells = 16, int rowStep = 1)
        {
            var result = new List<(float2, float2)>();
            rowStep = math.max(1, rowStep);
            foreach (var pair in _tiles)
            {
                int2 origin = FromKey(pair.Key) * TileSize;
                bool[] tile = pair.Value;
                for (int row = 0; row < TileSize; row++)
                {
                    if (((origin.y + row) % rowStep + rowStep) % rowStep != 0) continue;
                    int start = -1;
                    for (int col = 0; col <= TileSize; col++)
                    {
                        bool painted = col < TileSize && tile[(row << TileBits) | col];
                        if (painted && start < 0)
                        {
                            start = col;
                        }
                        if (start >= 0 && (!painted || col - start >= maxCells))
                        {
                            float z = (origin.y + row + 0.5f * rowStep) * Cell;
                            result.Add((new float2((origin.x + start) * Cell, z), new float2((origin.x + col) * Cell, z)));
                            start = painted ? col : -1;
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Contour lissé de la plus grande tache peinte (trous bouchés), y = 0 ; vide si rien
        /// d'exploitable. Le contour n'a jamais de côtés qui se croisent (lissage réduit sinon).
        /// </summary>
        public List<float3> Outline()
        {
            var empty = new List<float3>();
            if (_count == 0) return empty;

            // Grille locale avec une case de marge.
            var painted = new List<int2>(PaintedCells());
            int2 min = new int2(int.MaxValue), max = new int2(int.MinValue);
            foreach (int2 c in painted)
            {
                min = math.min(min, c);
                max = math.max(max, c);
            }
            min -= 2;
            max += 2;
            int nx = max.x - min.x + 1, nz = max.y - min.y + 1;
            var grid = new bool[nx * nz];
            foreach (int2 c in painted)
            {
                int2 q = c - min;
                grid[q.y * nx + q.x] = true;
            }

            // Plus grande tache (4-voisinage).
            var label = new int[nx * nz];
            int best = 0, bestSize = 0, next = 0;
            var stack = new List<int>();
            for (int s = 0; s < grid.Length; s++)
            {
                if (!grid[s] || label[s] != 0) continue;
                next++;
                int size = 0;
                stack.Add(s);
                label[s] = next;
                while (stack.Count > 0)
                {
                    int q = stack[stack.Count - 1];
                    stack.RemoveAt(stack.Count - 1);
                    size++;
                    int qx = q % nx, qz = q / nx;
                    foreach (int n in new[] { qx > 0 ? q - 1 : -1, qx < nx - 1 ? q + 1 : -1, qz > 0 ? q - nx : -1, qz < nz - 1 ? q + nx : -1 })
                    {
                        if (n >= 0 && grid[n] && label[n] == 0)
                        {
                            label[n] = next;
                            stack.Add(n);
                        }
                    }
                }
                if (size > bestSize)
                {
                    bestSize = size;
                    best = next;
                }
            }
            for (int s = 0; s < grid.Length; s++) grid[s] = label[s] == best;

            // Pincements (deux cases qui ne se touchent que par un coin) : bouchés, le bord y
            // repasserait par le même point.
            for (bool changed = true; changed;)
            {
                changed = false;
                for (int z = 0; z + 1 < nz; z++)
                {
                    for (int x = 0; x + 1 < nx; x++)
                    {
                        int a = z * nx + x, b = a + 1, c = a + nx, d = c + 1;
                        if (grid[a] && grid[d] && !grid[b] && !grid[c]) { grid[b] = true; changed = true; }
                        else if (grid[b] && grid[c] && !grid[a] && !grid[d]) { grid[a] = true; changed = true; }
                    }
                }
            }

            // Trous bouchés : tout ce que l'extérieur n'atteint pas est dedans.
            var outside = new bool[nx * nz];
            stack.Add(0);
            outside[0] = true;
            while (stack.Count > 0)
            {
                int q = stack[stack.Count - 1];
                stack.RemoveAt(stack.Count - 1);
                int qx = q % nx, qz = q / nx;
                foreach (int n in new[] { qx > 0 ? q - 1 : -1, qx < nx - 1 ? q + 1 : -1, qz > 0 ? q - nx : -1, qz < nz - 1 ? q + nx : -1 })
                {
                    if (n >= 0 && !grid[n] && !outside[n])
                    {
                        outside[n] = true;
                        stack.Add(n);
                    }
                }
            }
            for (int s = 0; s < grid.Length; s++) grid[s] = !outside[s];

            // Bord : arêtes orientées des cases (intérieur à gauche), enchaînées en boucle. Aux
            // points de pincement (deux cases qui ne se touchent que par un coin), on tourne à
            // gauche pour rester sur la même tache.
            bool In(int x, int z) => x >= 0 && z >= 0 && x < nx && z < nz && grid[z * nx + x];
            var outgoing = new Dictionary<long, List<int2>>();
            void Add(int2 a, int2 b)
            {
                long key = Key(a.x, a.y);
                if (!outgoing.TryGetValue(key, out var list)) outgoing[key] = list = new List<int2>();
                list.Add(b);
            }
            int2 start = default;
            bool found = false;
            for (int z = 0; z < nz; z++)
            {
                for (int x = 0; x < nx; x++)
                {
                    if (!In(x, z)) continue;
                    if (!In(x, z - 1)) Add(new int2(x, z), new int2(x + 1, z));
                    if (!In(x + 1, z)) Add(new int2(x + 1, z), new int2(x + 1, z + 1));
                    if (!In(x, z + 1)) Add(new int2(x + 1, z + 1), new int2(x, z + 1));
                    if (!In(x - 1, z)) Add(new int2(x, z + 1), new int2(x, z));
                    if (!found)
                    {
                        found = true;
                        start = new int2(x, z);
                    }
                }
            }
            if (!found) return empty;
            var loop = new List<float2>();
            int2 cur = start, dir = new int2(1, 0);
            for (int guard = 0; guard < 4 * nx * nz; guard++)
            {
                loop.Add(cur);
                List<int2> options = outgoing[Key(cur.x, cur.y)];
                int2 chosen = options[0];
                if (options.Count > 1)
                {
                    // Tourner à gauche en priorité (repère x→droite, z→bas de la grille : gauche = (d.y, -d.x)).
                    int2 left = new int2(dir.y, -dir.x);
                    foreach (int2 o in options)
                    {
                        if (math.all(o - cur == left)) chosen = o;
                    }
                }
                options.Remove(chosen);
                dir = chosen - cur;
                cur = chosen;
                if (math.all(cur == start)) break;
            }
            // Coins de la grille seulement (points alignés retirés), puis passage en mètres.
            var corners = new List<float2>();
            for (int i = 0; i < loop.Count; i++)
            {
                float2 a = loop[(i + loop.Count - 1) % loop.Count], b = loop[i], c = loop[(i + 1) % loop.Count];
                float2 d1 = b - a, d2 = c - b;
                if (d1.x * d2.y - d1.y * d2.x != 0f) corners.Add((b + (float2)min) * Cell);
            }
            if (corners.Count < 4) return empty;

            // Rééchantillonnage régulier puis lissage (moyenne glissante) : les marches de la grille
            // disparaissent ; lissage réduit si le contour finit par se croiser (col étroit).
            List<float2> even = Resample(corners, Cell);
            for (int passes = 6; passes >= 0; passes -= 2)
            {
                List<float2> smooth = SmoothLoop(even, passes, 3);
                List<float2> simple = Simplify(smooth, 0.6f);
                var ring = new List<float3>(simple.Count);
                foreach (float2 p in simple) ring.Add(new float3(p.x, 0f, p.y));
                if (FreeAreaPerimeter.IsSimple(ring)) return ring;
            }
            return empty;
        }

        private static List<float2> Resample(List<float2> loop, float step)
        {
            var result = new List<float2>();
            int n = loop.Count;
            float carry = 0f;
            for (int i = 0; i < n; i++)
            {
                float2 a = loop[i], b = loop[(i + 1) % n];
                float length = math.distance(a, b);
                float t = carry;
                while (t < length)
                {
                    result.Add(math.lerp(a, b, t / length));
                    t += step;
                }
                carry = t - length;
            }
            return result;
        }

        private static List<float2> SmoothLoop(List<float2> loop, int passes, int half)
        {
            var result = new List<float2>(loop);
            int n = result.Count;
            for (int pass = 0; pass < passes; pass++)
            {
                var next = new List<float2>(n);
                for (int k = 0; k < n; k++)
                {
                    float2 sum = float2.zero;
                    for (int d = -half; d <= half; d++) sum += result[((k + d) % n + n) % n];
                    next.Add(sum / (2 * half + 1));
                }
                result = next;
            }
            return result;
        }

        /// <summary>Douglas-Peucker sur une boucle : retire les points à moins de tolerance m de la corde.</summary>
        private static List<float2> Simplify(List<float2> loop, float tolerance)
        {
            int n = loop.Count;
            var keep = new bool[n];
            // Deux points opposés comme ancres de la boucle.
            int far = 0;
            for (int i = 1; i < n; i++) if (math.distancesq(loop[i], loop[0]) > math.distancesq(loop[far], loop[0])) far = i;
            keep[0] = keep[far] = true;
            void Run(int from, int to)
            {
                // Indices de from à to (sens croissant, en boucle).
                int count = (to - from + n) % n;
                if (count < 2) return;
                float2 a = loop[from], b = loop[to], ab = b - a;
                float lengthSq = math.max(math.lengthsq(ab), 1e-6f);
                int worst = -1;
                float worstDistance = tolerance;
                for (int s = 1; s < count; s++)
                {
                    int i = (from + s) % n;
                    float t = math.saturate(math.dot(loop[i] - a, ab) / lengthSq);
                    float d = math.distance(loop[i], a + t * ab);
                    if (d > worstDistance)
                    {
                        worstDistance = d;
                        worst = i;
                    }
                }
                if (worst < 0) return;
                keep[worst] = true;
                Run(from, worst);
                Run(worst, to);
            }
            Run(0, far);
            Run(far, 0);
            var result = new List<float2>();
            for (int i = 0; i < n; i++) if (keep[i]) result.Add(loop[i]);
            return result;
        }
    }
}
