using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace GridRoadGenerator.Core
{
    /// <summary>
    /// Mode "Concêntrico" (retour utilisateur : "um modo que copia a forma exterior dentro, em que
    /// possamos escolher o número de camadas") : N anneaux de routes qui reprennent la forme du
    /// périmètre choisi, reliés entre eux et au périmètre par des rayons courts.
    ///
    /// Pourquoi pas un offset arête par arête (ancien mode Adaptativo, retiré) : décaler chaque
    /// arête puis recoller les coins (jonction miter) s'auto-intersecte dès qu'un coin est
    /// concave, qu'une partie étroite se referme ou que la forme se sépare en deux — chaque cas
    /// demandait son propre garde-fou, et il en restait toujours un. Ici chaque anneau est la
    /// courbe de niveau "distance au périmètre = k × espacement" d'un champ de distance calculé
    /// sur une grille (marching squares) : c'est par construction le vrai offset intérieur, qui
    /// ne peut ni se croiser lui-même ni sortir du périmètre, garde des coins nets sur les coins
    /// convexes, s'arrondit dans les coins concaves, et se sépare/disparaît naturellement quand
    /// la forme devient trop étroite.
    ///
    /// L'espacement n'est pas un réglage : il est calculé pour que les N anneaux remplissent la
    /// forme jusqu'à son centre (profondeur maximale / (N + 1)), en respectant MinLayerSpacing.
    /// </summary>
    public static class ConcentricGenerator
    {
        public const int MinLayers = 1;
        public const int MaxLayersLimit = 10;
        public const int MinConnections = 2;
        public const int MaxConnections = 12;

        /// <summary>
        /// Écart minimal (m) entre deux anneaux voisins, mesuré à l'endroit le plus serré (les
        /// anneaux se resserrent dans les parties étroites de la forme) — une bande plus étroite
        /// ne serait plus constructible. Détermine le nombre maximal d'anneaux (voir MaxLayers).
        /// </summary>
        public const float MinLayerSpacing = 40f;

        /// <summary>Tailles de cellule (m) du champ de distance : fine sur une petite zone, plafonnée sur une grande pour rester rapide.</summary>
        private const float MinCellSize = 1.5f;
        private const float MaxCellSize = 8f;
        private const int TargetCellsPerAxis = 220;

        // Cache : GenerateLoopGrid est appelé à chaque frame par le croquis (overlay) et par
        // la validation — le champ de distance ne dépend que du périmètre, le résultat final
        // du périmètre + nombre d'anneaux + nombre de rayons.
        private static float3[] s_CachedPerimeter;
        private static int s_CachedLayers;
        private static int s_CachedConnections;
        private static List<RoadSegmentDef> s_CachedResult;
        /// <summary>Nombre de circuits de chaque anneau (du plus extérieur au plus intérieur) lors de la dernière génération non mise en cache — diagnostic pour les tests.</summary>
        internal static int[] LastRingLoopCounts;
        /// <summary>Circuits de chaque anneau (du plus extérieur au plus intérieur) lors de la dernière génération non mise en cache — diagnostic pour les tests.</summary>
        internal static List<List<List<float2>>> LastRings;

        private static float3[] s_CachedFieldPerimeter;
        private static DistanceField s_CachedField;

        public static List<RoadSegmentDef> Generate(IReadOnlyList<float3> selectedNodePositions, int layers, int connections)
        {
            if (selectedNodePositions == null || selectedNodePositions.Count < 2)
            {
                return new List<RoadSegmentDef>();
            }
            layers = math.clamp(layers, MinLayers, MaxLayersLimit);
            connections = math.clamp(connections, MinConnections, MaxConnections);

            if (s_CachedResult != null && s_CachedLayers == layers && s_CachedConnections == connections
                && SamePositions(s_CachedPerimeter, selectedNodePositions))
            {
                return new List<RoadSegmentDef>(s_CachedResult);
            }

            List<RoadSegmentDef> result = GenerateUncached(selectedNodePositions, layers, connections);
            s_CachedPerimeter = new float3[selectedNodePositions.Count];
            for (int i = 0; i < selectedNodePositions.Count; i++)
            {
                s_CachedPerimeter[i] = selectedNodePositions[i];
            }
            s_CachedLayers = layers;
            s_CachedConnections = connections;
            s_CachedResult = result;
            return new List<RoadSegmentDef>(result);
        }

        private static bool SamePositions(float3[] cached, IReadOnlyList<float3> positions)
        {
            if (cached == null || cached.Length != positions.Count)
            {
                return false;
            }
            for (int i = 0; i < cached.Length; i++)
            {
                if (!math.all(cached[i] == positions[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static List<RoadSegmentDef> GenerateUncached(IReadOnlyList<float3> nodes, int layers, int connections)
        {
            var segments = new List<RoadSegmentDef>();
            if (!TryGetField(nodes, out DistanceField field, out List<float2> polygon, out float y))
            {
                return segments;
            }

            // Jamais plus d'anneaux que la forme n'en permet (voir MaxLayersFor) — le panneau
            // bloque déjà le slider à cette limite, ceci couvre une valeur sauvegardée plus haute.
            int layerCount = math.min(layers, MaxLayersFor(field, polygon));
            if (layerCount < 1)
            {
                return segments;
            }
            var corners = new HashSet<(float, float)>();
            List<List<List<float2>>> levels = BuildLevels(field, polygon, layerCount, corners, out float spacing);
            if (levels == null)
            {
                return segments;
            }

            LastRings = levels.GetRange(1, levels.Count - 1);
            LastRingLoopCounts = new int[levels.Count - 1];
            for (int k = 1; k < levels.Count; k++)
            {
                LastRingLoopCounts[k - 1] = levels[k].Count;
            }

            EmitRadialChains(segments, levels, connections, spacing, y);

            for (int k = 1; k < levels.Count; k++)
            {
                foreach (List<float2> loop in levels[k])
                {
                    EmitSmoothLoop(segments, loop, corners, y);
                }
            }
            return segments;
        }

        /// <summary>
        /// Nombre maximal d'anneaux pour ce périmètre (retour utilisateur : "é preciso haver um
        /// limite de anéis dependendo do tamanho do perímetro, e no painel simplesmente
        /// bloqueia além do limite") : le plus grand N pour lequel, même à l'endroit le plus
        /// serré, deux anneaux voisins restent à au moins MinLayerSpacing l'un de l'autre. 0 si
        /// la forme est trop petite pour un seul anneau. Mis en cache avec le champ de distance.
        /// </summary>
        public static int MaxLayers(IReadOnlyList<float3> selectedNodePositions)
        {
            if (selectedNodePositions == null || selectedNodePositions.Count < 2)
            {
                return MaxLayersLimit;
            }
            return TryGetField(selectedNodePositions, out DistanceField field, out List<float2> polygon, out float _)
                ? MaxLayersFor(field, polygon)
                : 0;
        }

        private static int MaxLayersFor(DistanceField field, List<float2> polygon)
        {
            if (field.MaxLayersCache >= 0)
            {
                return field.MaxLayersCache;
            }
            // Borne haute : l'espacement MOYEN doit déjà atteindre MinLayerSpacing.
            int upper = math.min(MaxLayersLimit, (int)math.floor(field.RingDepth / MinLayerSpacing) - 1);
            int result = 0;
            for (int layerCount = upper; layerCount >= 1; layerCount--)
            {
                List<List<List<float2>>> levels = BuildLevels(field, polygon, layerCount, new HashSet<(float, float)>(), out float _);
                if (levels != null && levels.Count - 1 == layerCount && MinGapBetweenLevels(levels) >= MinLayerSpacing)
                {
                    result = layerCount;
                    break;
                }
            }
            field.MaxLayersCache = result;
            return result;
        }

        /// <summary>Champ de distance du périmètre (en cache tant que le périmètre ne change pas) et polygone nettoyé.</summary>
        private static bool TryGetField(IReadOnlyList<float3> nodes, out DistanceField field, out List<float2> polygon, out float y)
        {
            field = null;
            y = 0f;
            polygon = new List<float2>(nodes.Count);
            foreach (float3 p in nodes)
            {
                y += p.y;
                polygon.Add(p.xz);
            }
            y /= nodes.Count;

            if (polygon.Count == 2)
            {
                float2 mn = math.min(polygon[0], polygon[1]);
                float2 mx = math.max(polygon[0], polygon[1]);
                polygon = new List<float2> { mn, new float2(mx.x, mn.y), mx, new float2(mn.x, mx.y) };
            }
            polygon = CleanPolygon(polygon);
            if (polygon.Count < 3 || math.abs(SignedArea(polygon)) < 1f)
            {
                return false;
            }

            // Le champ de distance (la partie coûteuse) ne dépend que du périmètre : gardé en cache
            // séparément, pour que glisser Camadas/Ligações ne le recalcule jamais (retour
            // utilisateur : "lentidão quando se tenta mudar parâmetros").
            if (s_CachedField != null && SamePositions(s_CachedFieldPerimeter, nodes))
            {
                field = s_CachedField;
            }
            else
            {
                field = DistanceField.Build(polygon);
                s_CachedField = field;
                s_CachedFieldPerimeter = new float3[nodes.Count];
                for (int i = 0; i < nodes.Count; i++)
                {
                    s_CachedFieldPerimeter[i] = nodes[i];
                }
            }
            return field.MaxDepth > 0f;
        }

        /// <summary>
        /// Anneaux = courbes de niveau d'un champ lisse qui vaut 0 sur le périmètre et 1 sur
        /// l'"épine" de la forme (voir DistanceField.RingField) : chaque anneau fait le tour de
        /// toute la forme sans se couper ni finir en pointe, plus serré dans les parties
        /// étroites, plus large dans les parties larges. Espacement MOYEN = profondeur / (N+1).
        /// levels[0] = le périmètre lui-même. null si on n'obtient pas les N anneaux demandés.
        /// </summary>
        private static List<List<List<float2>>> BuildLevels(DistanceField field, List<float2> polygon, int layerCount,
            HashSet<(float, float)> corners, out float spacing)
        {
            spacing = field.RingDepth / (layerCount + 1);
            var levels = new List<List<List<float2>>> { new List<List<float2>> { polygon } };
            for (int k = 1; k <= layerCount; k++)
            {
                float level = field.LevelForDepth(k * spacing);
                var loops = new List<List<float2>>();
                foreach (List<float2> raw in field.ExtractContours(field.RingField, level))
                {
                    List<float2> loop = SmoothAndResample(raw, field, corners, spacing);
                    if (loop.Count < 3 || math.abs(SignedArea(loop)) < MinLayerSpacing * MinLayerSpacing)
                    {
                        continue; // lambeau trop petit pour porter une zone constructible
                    }
                    loops.Add(loop);
                }
                if (loops.Count == 0)
                {
                    return null;
                }
                levels.Add(loops);
            }
            return levels;
        }

        /// <summary>Plus petit écart entre un anneau et le niveau juste à l'extérieur (le périmètre pour le premier).</summary>
        private static float MinGapBetweenLevels(List<List<List<float2>>> levels)
        {
            float min = float.MaxValue;
            for (int k = 1; k < levels.Count; k++)
            {
                foreach (List<float2> loop in levels[k])
                {
                    for (int i = 0; i < loop.Count; i++)
                    {
                        if (NearestOnLevel(levels[k - 1], loop[i], out int _, out float2 q))
                        {
                            min = math.min(min, math.distance(loop[i], q));
                        }
                    }
                }
            }
            return min;
        }

        /// <summary>Virage minimal (degrés) pour qu'un point soit un coin — et seulement là où le périmètre a lui-même un coin net (voir DistanceField.SharpWeightAt).</summary>
        private const float CornerTurnDegrees = 30f;

        /// <summary>Marge (degrés) entre le virage d'un coin d'anneau et celui du coin de périmètre dont il hérite.</summary>
        private const float CornerTurnSlackDegrees = 20f;

        /// <summary>Distance visée (m) entre deux sommets d'un anneau courbe après rééchantillonnage.</summary>
        private const float RingSampleStep = 25f;

        /// <summary>
        /// Transforme la courbe de niveau brute (un point par cellule de grille, en escalier) en
        /// anneau fluide : repère les VRAIS coins (fort virage ET dans le sillage d'un coin net du
        /// périmètre), lisse tout le reste, puis rééchantillonne à pas régulier entre deux coins.
        /// Remplace l'ancien Douglas-Peucker, qui laissait des sommets espacés irrégulièrement :
        /// sur une courbe serrée, l'angle entre deux cordes dépassait le seuil de coin et
        /// l'anneau restait cassé à cet endroit ("falta fluidez das curvas"). Les coins retenus
        /// sont ajoutés à `corners` (positions exactes des sommets retournés).
        /// </summary>
        private static List<float2> SmoothAndResample(List<float2> raw, DistanceField field, HashSet<(float, float)> corners, float spacing)
        {
            int n = raw.Count;
            if (n < 6)
            {
                return new List<float2>(raw);
            }

            // Virage mesuré sur une fenêtre de quelques mètres (les points bruts sont trop
            // serrés et en escalier pour un angle point à point).
            int window = math.max(2, (int)math.round(6f / field.CellSize));
            window = math.min(window, n / 4);
            var turn = new float[n];
            for (int i = 0; i < n; i++)
            {
                float2 dIn = math.normalizesafe(raw[i] - raw[(i - window + n) % n]);
                float2 dOut = math.normalizesafe(raw[(i + window) % n] - raw[i]);
                turn[i] = math.degrees(math.acos(math.clamp(math.dot(dIn, dOut), -1f, 1f)));
            }
            var isCorner = new bool[n];
            int cornerCount = 0;
            for (int i = 0; i < n; i++)
            {
                // Un coin hérité tourne autant que le coin du périmètre dont il vient (un carré
                // donne des anneaux carrés). Retour utilisateur (bicos persistants, perimètre réel
                // rejoué en test) : la zone d'influence d'un coin OBTUS du périmètre (deux rues qui
                // se croisent à 150°) couvre une large bande jusqu'au fond de la forme, et des
                // pointes d'anneau à 110-128° qui s'y trouvaient étaient gardées comme "coins",
                // donc jamais arrondies (LimitCurvature laisse les coins fixes).
                if (turn[i] < CornerTurnDegrees || field.SharpWeightAt(raw[i]) < 0.5f
                    || turn[i] > field.SharpTurnAt(raw[i]) + CornerTurnSlackDegrees)
                {
                    continue;
                }
                bool localMax = true;
                for (int o = -window; o <= window && localMax; o++)
                {
                    int j = (i + o + n) % n;
                    if (o != 0 && (turn[j] > turn[i] || (turn[j] == turn[i] && isCorner[j])))
                    {
                        localMax = false;
                    }
                }
                if (localMax)
                {
                    isCorner[i] = true;
                    cornerCount++;
                }
            }

            // Lissage laplacien (coins fixés) : efface l'escalier du marching squares.
            var points = new List<float2>(raw);
            var next = new float2[n];
            for (int pass = 0; pass < 8; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    next[i] = isCorner[i]
                        ? points[i]
                        : 0.25f * points[(i - 1 + n) % n] + 0.5f * points[i] + 0.25f * points[(i + 1) % n];
                }
                for (int i = 0; i < n; i++)
                {
                    points[i] = next[i];
                }
            }

            // Rééchantillonnage à pas régulier, par tronçons entre deux coins (ou tout l'anneau).
            int startIndex = 0;
            if (cornerCount > 0)
            {
                while (!isCorner[startIndex])
                {
                    startIndex++;
                }
            }
            var result = new List<float2>();
            int runStart = startIndex;
            int visited = 0;
            while (visited < n)
            {
                // Tronçon [runStart .. prochain coin (ou retour au départ)].
                var run = new List<float2> { points[runStart] };
                int idx = runStart;
                do
                {
                    idx = (idx + 1) % n;
                    run.Add(points[idx]);
                    visited++;
                }
                while (idx != startIndex && !isCorner[idx]);

                float runLength = 0f;
                for (int i = 0; i + 1 < run.Count; i++)
                {
                    runLength += math.distance(run[i], run[i + 1]);
                }
                int intervals = math.max(1, (int)math.ceil(runLength / RingSampleStep));
                if (cornerCount == 0)
                {
                    intervals = math.max(intervals, 8);
                }
                float step = runLength / intervals;
                result.Add(run[0]);
                if (isCorner[runStart])
                {
                    corners.Add((run[0].x, run[0].y));
                }
                float target = step;
                float walked = 0f;
                for (int i = 0; i + 1 < run.Count && result.Count < 100000; i++)
                {
                    float edge = math.distance(run[i], run[i + 1]);
                    while (walked + edge >= target - 1e-3f && target < runLength - step * 0.5f)
                    {
                        float t = edge > 1e-6f ? (target - walked) / edge : 0f;
                        result.Add(math.lerp(run[i], run[i + 1], t));
                        target += step;
                    }
                    walked += edge;
                }
                runStart = idx;
                if (idx == startIndex)
                {
                    break;
                }
            }

            LimitCurvature(result, corners, spacing * MinRingRadiusFactor);

            // Retire les sommets quasi alignés (côtés droits) : moins de nœuds inutiles.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = result.Count - 1; i >= 0 && result.Count > 4; i--)
                {
                    float2 p = result[i];
                    if (corners.Contains((p.x, p.y)))
                    {
                        continue;
                    }
                    float2 a = result[(i - 1 + result.Count) % result.Count];
                    float2 b = result[(i + 1) % result.Count];
                    float2 d1 = math.normalizesafe(p - a);
                    float2 d2 = math.normalizesafe(b - p);
                    if (math.dot(d1, d2) > math.cos(math.radians(0.5f)) && math.distance(a, b) < 400f)
                    {
                        result.RemoveAt(i);
                    }
                }
            }
            return result;
        }

        /// <summary>Rayon de courbure minimal d'un anneau (hors vrais coins), en fraction de l'espacement moyen entre anneaux.</summary>
        private const float MinRingRadiusFactor = 0.5f;

        /// <summary>
        /// Arrondit les pointes qui restent sur un anneau (retour utilisateur : "ainda existem
        /// alguns bicos em outras formas complexas") : quand le périmètre tourne en large courbe
        /// (ex. une rue en U) dans une forme plus large que le rayon de cette courbe, un anneau
        /// plus profond que ce rayon se referme en pointe — le flou de ComputeRingField n'arrondit
        /// que sur ~¼ de la profondeur, et la fraction de demi-largeur n'aide que là où la forme
        /// elle-même s'amincit. Lissage laplacien local, répété tant qu'un sommet tourne plus
        /// serré que `minRadius` (cercle passant par ses deux voisins) : la pointe recule vers
        /// l'intérieur de sa courbe (loin du périmètre) jusqu'à devenir un arc de ce rayon. Les
        /// vrais coins (hérités d'un coin net du périmètre) restent fixes : un carré reste carré.
        /// </summary>
        internal static void LimitCurvature(List<float2> loop, HashSet<(float, float)> corners, float minRadius)
        {
            int n = loop.Count;
            if (n < 5 || minRadius <= 0f)
            {
                return;
            }
            var fixedPoint = new bool[n];
            for (int i = 0; i < n; i++)
            {
                fixedPoint[i] = corners.Contains((loop[i].x, loop[i].y));
            }
            float maxCurvature = 1f / minRadius;
            var move = new bool[n];
            var next = new float2[n];
            for (int pass = 0; pass < 400; pass++)
            {
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    move[i] = false;
                }
                for (int i = 0; i < n; i++)
                {
                    if (fixedPoint[i])
                    {
                        continue;
                    }
                    float2 a = loop[(i - 1 + n) % n];
                    float2 p = loop[i];
                    float2 b = loop[(i + 1) % n];
                    float2 dIn = math.normalizesafe(p - a);
                    float2 dOut = math.normalizesafe(b - p);
                    float cos = math.dot(dIn, dOut);
                    float sin = math.abs(dIn.x * dOut.y - dIn.y * dOut.x);
                    float chord = math.distance(a, b);
                    // Courbure du cercle par a, p, b : 2 sin(virage) / |ab| ; au-delà de 90° de
                    // virage (cos < 0) c'est de toute façon une pointe.
                    bool tooSharp = cos < 0f || (chord > 1e-4f && 2f * sin / chord > maxCurvature);
                    if (tooSharp)
                    {
                        move[(i - 1 + n) % n] = true;
                        move[i] = true;
                        move[(i + 1) % n] = true;
                        any = true;
                    }
                }
                if (!any)
                {
                    return;
                }
                for (int i = 0; i < n; i++)
                {
                    next[i] = move[i] && !fixedPoint[i]
                        ? 0.5f * loop[i] + 0.25f * (loop[(i - 1 + n) % n] + loop[(i + 1) % n])
                        : loop[i];
                }
                for (int i = 0; i < n; i++)
                {
                    loop[i] = next[i];
                }
            }
        }

        /// <summary>
        /// Anneau en vraies courbes (RoadSegmentDef.IsArc, NetUtils.FitCurve côté ECS) : tangente
        /// en chaque sommet = moyenne des deux tronçons adjacents (Catmull-Rom), sauf aux vrais
        /// coins (hérités d'un coin net du périmètre) où chaque côté garde sa propre direction.
        /// Sans ça, un anneau courbe se voit en facettes droites cassées à chaque sommet.
        /// </summary>
        private static void EmitSmoothLoop(List<RoadSegmentDef> segments, List<float2> loop, HashSet<(float, float)> corners, float y)
        {
            int n = loop.Count;
            var incoming = new float2[n];
            var outgoing = new float2[n];
            for (int i = 0; i < n; i++)
            {
                float2 dIn = math.normalizesafe(loop[i] - loop[(i - 1 + n) % n]);
                float2 dOut = math.normalizesafe(loop[(i + 1) % n] - loop[i]);
                // Seuls les vrais coins (hérités d'un coin net du périmètre, voir
                // SmoothAndResample) gardent une cassure ; tout le reste est lissé, quel que soit
                // l'angle entre deux tronçons (retour utilisateur : "falta fluidez das curvas").
                if (corners.Contains((loop[i].x, loop[i].y)))
                {
                    incoming[i] = dIn;
                    outgoing[i] = dOut;
                }
                else
                {
                    float2 t = math.normalizesafe(dIn + dOut, dOut);
                    incoming[i] = t;
                    outgoing[i] = t;
                }
            }
            // Sommets gardés (retour utilisateur : "quando o mod gera as curvas, cria demasiados
            // nós" — un nœud tous les ~25 m, pas de rééchantillonnage) : les vrais coins, les
            // raccords des rayons (déjà émis, voir EmitRadialChains) et, entre deux, seulement
            // assez de sommets pour que chaque tronçon reste un arc de cercle fidèle (voir
            // CanSpanAsArc) — NetUtils.FitCurve côté ECS refait la courbe à partir des tangentes.
            var joints = new HashSet<(float, float)>();
            foreach (RoadSegmentDef s in segments)
            {
                joints.Add((s.Start.x, s.Start.z));
                joints.Add((s.End.x, s.End.z));
            }
            var keep = new bool[n];
            int first = -1;
            for (int i = 0; i < n; i++)
            {
                keep[i] = corners.Contains((loop[i].x, loop[i].y)) || joints.Contains((loop[i].x, loop[i].y));
                if (keep[i] && first < 0)
                {
                    first = i;
                }
            }
            if (first < 0)
            {
                first = 0;
                keep[0] = true;
            }
            var kept = new List<int> { first };
            int current = first;
            int walked = 0;
            while (walked < n)
            {
                int end = (current + 1) % n;
                int steps = 1;
                // Étend le tronçon tant que le sommet suivant n'est pas imposé et que l'arc tient.
                while (!keep[end] && steps + 1 <= n - walked
                    && CanSpanAsArc(loop, current, (end + 1) % n, outgoing[current], incoming[(end + 1) % n]))
                {
                    end = (end + 1) % n;
                    steps++;
                }
                walked += steps;
                current = end;
                if (current == first)
                {
                    break;
                }
                kept.Add(current);
            }

            // Pas de tronçon plus court que MinJunctionSegmentLength collé à un carrefour (raccord
            // de rayon ou coin) : le jeu le refuse ("Objetos sobrepostos", vécu sur le périmètre
            // réel). Un sommet ordinaire trop près d'un carrefour est retiré ; l'arc va alors
            // directement du carrefour au sommet suivant.
            bool removedStub = true;
            while (removedStub && kept.Count > 3)
            {
                removedStub = false;
                for (int k = 0; k < kept.Count && kept.Count > 3; k++)
                {
                    int vertex = kept[k];
                    if (keep[vertex])
                    {
                        continue;
                    }
                    int previous = kept[(k - 1 + kept.Count) % kept.Count];
                    int next = kept[(k + 1) % kept.Count];
                    bool nearPrevious = keep[previous] && math.distance(loop[previous], loop[vertex]) < MinJunctionSegmentLength;
                    bool nearNext = keep[next] && math.distance(loop[next], loop[vertex]) < MinJunctionSegmentLength;
                    if (nearPrevious || nearNext)
                    {
                        kept.RemoveAt(k);
                        removedStub = true;
                        k--;
                    }
                }
            }

            for (int k = 0; k < kept.Count; k++)
            {
                int i = kept[k];
                int j = kept[(k + 1) % kept.Count];
                float2 a = loop[i];
                float2 b = loop[j];
                if (math.distance(a, b) < 0.01f)
                {
                    continue;
                }
                float2 chord = math.normalizesafe(b - a);
                float2 ta = outgoing[i];
                float2 tb = incoming[j];
                var start = new float3(a.x, y, a.y);
                var end = new float3(b.x, y, b.y);
                // Tronçon quasi droit : ligne droite ordinaire.
                if (math.dot(ta, chord) > 0.9995f && math.dot(tb, chord) > 0.9995f)
                {
                    segments.Add(new RoadSegmentDef(start, end, isHorizontal: false));
                }
                else
                {
                    segments.Add(RoadSegmentDef.Arc(start, end, new float3(ta.x, 0f, ta.y), new float3(tb.x, 0f, tb.y)));
                }
            }
        }

        /// <summary>
        /// Longueur minimale (m) d'un tronçon d'anneau qui touche un carrefour. D'abord la largeur
        /// d'une route (16 m) ; vécu en jeu (log [Diag colisão]) : des tronçons de 16-20 m collés à
        /// un carrefour faisaient partie de chaque refus — le carrefour du jeu occupe davantage.
        /// </summary>
        private const float MinJunctionSegmentLength = 35f;

        /// <summary>Écart maximal (m) entre l'anneau lissé et l'arc qui remplace plusieurs de ses sommets.</summary>
        private const float ArcFitTolerance = 2f;
        /// <summary>Corde maximale (m) d'un tronçon courbe d'anneau.</summary>
        private const float MaxArcChord = 300f;
        /// <summary>Virage maximal (degrés) d'un seul tronçon courbe : au-delà, FitCurve s'écarte du cercle.</summary>
        private const float MaxArcTurnDegrees = 90f;

        /// <summary>
        /// Vrai si les sommets de loop[from] à loop[to] (sens de parcours) tiennent sur UN arc de
        /// cercle partant de loop[from] avec la tangente `startTangent` et arrivant sur loop[to]
        /// avec (à 6° près) la tangente `endTangent` : alors un seul tronçon courbe suffit.
        /// </summary>
        private static bool CanSpanAsArc(List<float2> loop, int from, int to, float2 startTangent, float2 endTangent)
        {
            int n = loop.Count;
            float2 a = loop[from];
            float2 b = loop[to];
            float2 ab = b - a;
            float chordLength = math.length(ab);
            if (chordLength < 0.01f || chordLength > MaxArcChord)
            {
                return false;
            }
            float2 chord = ab / chordLength;
            // Tangente d'arrivée d'un arc de cercle : symétrique de celle de départ par rapport à la corde.
            float2 arcEnd = 2f * math.dot(startTangent, chord) * chord - startTangent;
            if (math.dot(startTangent, chord) <= 0f
                || math.dot(arcEnd, endTangent) < math.cos(math.radians(6f))
                || math.dot(startTangent, arcEnd) < math.cos(math.radians(MaxArcTurnDegrees)))
            {
                return false;
            }
            float2 normal = new float2(-startTangent.y, startTangent.x);
            float side = math.dot(ab, normal);
            bool straight = math.abs(side) < 1e-3f * chordLength;
            float radius = straight ? 0f : chordLength * chordLength / (2f * side);
            float2 centre = a + normal * radius;
            for (int i = (from + 1) % n; i != to; i = (i + 1) % n)
            {
                float2 p = loop[i];
                float error = straight
                    ? math.abs(math.dot(p - a, new float2(-chord.y, chord.x)))
                    : math.abs(math.distance(p, centre) - math.abs(radius));
                if (error > ArcFitTolerance || math.dot(p - a, chord) < -ArcFitTolerance || math.dot(p - b, chord) > ArcFitTolerance)
                {
                    return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Rayons
        // ------------------------------------------------------------------

        /// <summary>
        /// Rayons en chaînes continues du périmètre vers le centre (retour utilisateur : "as
        /// conexões aparecem muito próximas umas das outras em vez de aparecerem em vários
        /// cantos do perímetro" — les chaînes partaient de l'anneau le plus INTÉRIEUR, petit et
        /// étiré sur une forme allongée, et se retrouvaient toutes groupées au milieu). On place
        /// `connections` points répartis également sur le premier anneau (celui qui suit le
        /// périmètre), on relie chacun au périmètre (point le plus proche = perpendiculaire), puis
        /// on le projette sur l'anneau suivant vers l'intérieur, et ainsi de suite. Deux chaînes
        /// qui convergent (vers la pointe d'un anneau intérieur) aboutissent au même nœud. Les
        /// points d'arrivée sont INSÉRÉS comme sommets des anneaux, pour que rayon et anneau
        /// partagent exactement le même nœud ; ceux qui arrivent sur le périmètre (levels[0]) s'y
        /// raccordent côté ECS (MakeCoursePos, règle 2). Un anneau qu'aucune chaîne n'atteint
        /// (branche de forme séparée) reçoit ses propres points, reliés vers l'extérieur.
        /// </summary>
        private static void EmitRadialChains(List<RoadSegmentDef> segments, List<List<List<float2>>> levels,
            int connections, float spacing, float y)
        {
            s_CumulativeLengths?.Clear();
            try
            {
                EmitRadialChainsCore(segments, levels, connections, spacing, y);
            }
            finally
            {
                s_CumulativeLengths?.Clear();
            }
        }

        private static void EmitRadialChainsCore(List<RoadSegmentDef> segments, List<List<List<float2>>> levels,
            int connections, float spacing, float y)
        {
            float mergeDistance = spacing * 0.8f;
            // L'écart réel entre anneaux varie autour de `spacing` (plus large dans les parties
            // larges de la forme, plus serré dans les étroites).
            float maxSpokeLength = spacing * 4f;

            var chainPoints = new List<List<List<float2>>>();
            for (int k = 0; k < levels.Count; k++)
            {
                var perLoop = new List<List<float2>>();
                foreach (List<float2> _ in levels[k])
                {
                    perLoop.Add(new List<float2>());
                }
                chainPoints.Add(perLoop);
            }

            // Vrai si le segment droit a -> b croise un anneau de `level` ou un rayon déjà posé, hors
            // de ses propres extrémités.
            bool CrossesSomething(float2 a, float2 b, List<List<float2>> level)
            {
                foreach (List<float2> loop in level)
                {
                    for (int i = 0; i < loop.Count; i++)
                    {
                        if (SegmentsCrossAwayFromEnds(a, b, loop[i], loop[(i + 1) % loop.Count]))
                        {
                            return true;
                        }
                    }
                }
                foreach (RoadSegmentDef spoke in segments)
                {
                    if (SegmentsCrossAwayFromEnds(a, b, spoke.Start.xz, spoke.End.xz))
                    {
                        return true;
                    }
                }
                return false;
            }

            // Cherche sur `target` le raccord d'un rayon partant de p : le point le plus proche, puis
            // de proche en proche le long de l'anneau (jusqu'à 1,5 × l'espacement), le premier dont
            // le rayon est assez court, arrive à angle ouvert aux deux bouts et ne croise rien.
            // `avoid` (facultatif) : nœuds déjà posés sur `target`, dont le raccord doit rester
            // écarté d'au moins la moitié de l'espacement, et jamais à moins de
            // MinJunctionSegmentLength (sinon un tronçon trop court entre deux carrefours).
            // `targetLevel` >= 0 (rayon vers le centre) : parmi les raccords valables, préfère le plus
            // proche d'où la suite de la chaîne, simulée sur les anneaux tels qu'ils sont à cet
            // instant, atteint encore l'anneau le plus intérieur (retours utilisateur : "a ligação
            // não vai até ao centro", puis "a estrada não é contínua"). Sinon, le premier valable.
            bool FindSpokeTarget(float2 p, List<float2> source, List<float2> target, float2 nearest, List<float2> avoid, int targetLevel, out float2 result)
            {
                if (targetLevel >= 0 && targetLevel + 1 < levels.Count
                    && FindSpokeTargetCore(p, source, target, nearest, avoid, targetLevel, out result))
                {
                    return true;
                }
                return FindSpokeTargetCore(p, source, target, nearest, avoid, -1, out result);
            }

            bool FindSpokeTargetCore(float2 p, List<float2> source, List<float2> target, float2 nearest, List<float2> avoid, int targetLevel, out float2 result)
            {
                result = nearest;
                // Avec anticipation : si aucun raccord ne mène jusqu'au centre, garder celui qui
                // mène le plus loin plutôt que le premier venu (qui menait parfois à une impasse).
                bool hasPartial = false;
                int bestDepth = -1;
                // Coût borné (la génération se refait à chaque réglage du panneau) : au plus
                // MaxSimulatedCandidates simulations par choix, une seule par sommet d'arrivée.
                int simulations = 0;
                var simulated = new HashSet<(float, float)>();
                bool hasFirstValid = false;
                float2 firstValid = nearest;
                float2 bestPartial = nearest;
                float along = ArcPosition(target, nearest);
                float maxShift = SpokeSearchSpacings * spacing;
                for (float shift = 0f; shift <= maxShift; shift += SpokeSearchStep)
                {
                    foreach (float sign in shift == 0f ? new[] { 1f } : new[] { -1f, 1f })
                    {
                        float2 candidate = SnappedJoint(target, PointAtArc(target, along + sign * shift));
                        float length = math.distance(p, candidate);
                        if (length > maxSpokeLength || length < 1f)
                        {
                            continue;
                        }
                        if (avoid != null && avoid.Exists(node => math.distance(node, candidate) < math.max(0.5f * spacing, MinJunctionSegmentLength + 5f)))
                        {
                            continue;
                        }
                        if (!MeetsRingAtOpenAngle(target, candidate, p - candidate) || !MeetsRingAtOpenAngle(source, p, candidate - p))
                        {
                            continue;
                        }
                        if (shift > 0f && (CrossesSomething(p, candidate, new List<List<float2>> { source, target })))
                        {
                            continue;
                        }
                        if (targetLevel >= 0)
                        {
                            if (!hasFirstValid)
                            {
                                hasFirstValid = true;
                                firstValid = candidate;
                            }
                            if (!simulated.Add((candidate.x, candidate.y)))
                            {
                                continue;
                            }
                            if (simulations++ >= MaxSimulatedCandidates)
                            {
                                result = hasPartial ? bestPartial : firstValid;
                                return true;
                            }
                            int depth = SimulatedChainDepth(candidate, target, targetLevel);
                            if (depth < levels.Count - 1 - targetLevel)
                            {
                                if (depth > bestDepth)
                                {
                                    bestDepth = depth;
                                    bestPartial = candidate;
                                    hasPartial = true;
                                }
                                continue;
                            }
                        }
                        result = candidate;
                        return true;
                    }
                }
                if (hasPartial)
                {
                    result = bestPartial;
                    return true;
                }
                return false;
            }

            // Vrai si un rayon partant de p (sur `loop`, niveau k) trouve un raccord valable sur le
            // niveau `to` (voir FindSpokeTarget), sans rien insérer.
            bool CanReach(float2 p, List<float2> loop, int to)
            {
                return NearestOnLevel(levels[to], p, out int targetLoop, out float2 nearest)
                    && FindSpokeTargetCore(p, loop, levels[to][targetLoop], nearest, null, -1, out float2 _);
            }

            // Nombre d'anneaux qu'une chaîne partant de p (sur `loop`, niveau k) traverserait vers le
            // centre, en suivant la même règle que la construction (voir Project), sans rien insérer.
            int SimulatedChainDepth(float2 p, List<float2> loop, int k)
            {
                float2 current = p;
                List<float2> currentLoop = loop;
                int depth = 0;
                for (int level = k + 1; level < levels.Count; level++)
                {
                    if (!NearestOnLevel(levels[level], current, out int targetLoop, out float2 nearest)
                        || !FindSpokeTargetCore(current, currentLoop, levels[level][targetLoop], nearest, null, -1, out float2 next))
                    {
                        break;
                    }
                    // Mêmes règles que Project pour une chaîne déjà posée à proximité : la rejoindre
                    // (elle va déjà jusqu'au centre) si l'angle est ouvert, sinon s'en écarter.
                    List<float2> targetPoints = chainPoints[level][targetLoop];
                    float2 joined = next;
                    bool nearChain = targetPoints.Exists(existing =>
                    {
                        joined = existing;
                        return math.distance(existing, next) < mergeDistance;
                    });
                    if (nearChain)
                    {
                        if (JoinsNodeAtOpenAngle(joined, current, levels[level][targetLoop]))
                        {
                            return levels.Count - 1 - k;
                        }
                        if (!FindSpokeTargetCore(current, currentLoop, levels[level][targetLoop], nearest, targetPoints, -1, out next))
                        {
                            break;
                        }
                    }
                    current = next;
                    currentLoop = levels[level][targetLoop];
                    depth++;
                }
                return depth;
            }

            // Point de départ d'une chaîne : le point réparti `seed`, ou le plus proche le long de
            // l'anneau (jusqu'à `maxShift`) d'où la chaîne peut rejoindre l'extérieur ET, d'un seul
            // tenant, l'anneau le plus intérieur. Retours utilisateur : "a ligação não vai até ao
            // centro" (chaîne partie de l'axe d'un lobe, bloquée par les dobras des anneaux), puis
            // "a estrada não é contínua" quand un rayon bloqué repartait d'un autre point de
            // l'anneau — d'où le choix du départ en amont, sur toute la chaîne simulée.
            float2 ViableSeed(List<float2> loop, float2 seed, int k, float maxShift)
            {
                float along = ArcPosition(loop, seed);
                int fullDepth = levels.Count - 1 - k;
                float2 best = seed;
                int bestDepth = -1;
                int simulations = 0;
                var simulated = new HashSet<(float, float)>();
                for (float shift = 0f; shift <= maxShift && simulations < 2 * MaxSimulatedCandidates; shift += SpokeSearchStep)
                {
                    foreach (float sign in shift == 0f ? new[] { 1f } : new[] { -1f, 1f })
                    {
                        float2 candidate = SnappedJoint(loop, PointAtArc(loop, along + sign * shift));
                        if (!simulated.Add((candidate.x, candidate.y)) || !CanReach(candidate, loop, k - 1))
                        {
                            continue;
                        }
                        simulations++;
                        int depth = SimulatedChainDepth(candidate, loop, k);
                        if (depth >= fullDepth)
                        {
                            return candidate;
                        }
                        if (depth > bestDepth)
                        {
                            bestDepth = depth;
                            best = candidate;
                        }
                    }
                }
                return best;
            }

            // Vrai si un rayon p -> node ferait un angle ouvert avec l'anneau en node et avec chaque
            // rayon déjà raccordé en node (voir MinSpokeRingAngleDegrees).
            bool JoinsNodeAtOpenAngle(float2 node, float2 p, List<float2> ring)
            {
                float2 incoming = p - node;
                if (!MeetsRingAtOpenAngle(ring, node, incoming))
                {
                    return false;
                }
                float2 direction = math.normalizesafe(incoming);
                float cosLimit = math.cos(math.radians(MinSpokeRingAngleDegrees));
                foreach (RoadSegmentDef spoke in segments)
                {
                    float2 other;
                    if (math.all(spoke.Start.xz == node))
                    {
                        other = spoke.End.xz;
                    }
                    else if (math.all(spoke.End.xz == node))
                    {
                        other = spoke.Start.xz;
                    }
                    else
                    {
                        continue;
                    }
                    if (math.dot(math.normalizesafe(other - node), direction) > cosLimit)
                    {
                        return false;
                    }
                }
                return true;
            }

            void AddSpoke(float2 a, float2 b)
            {
                segments.Add(new RoadSegmentDef(new float3(a.x, y, a.y), new float3(b.x, y, b.y), isHorizontal: false));
            }

            // Projette p (sur un anneau du niveau `from`) vers le niveau `to` (to = from ± 1) ;
            // retourne le nœud d'arrivée (fusionné ou inséré), ou null si trop loin.
            // `sourceLoop` : l'anneau (niveau `to` ± 1) sur lequel se trouve p — le rayon doit aussi en
            // partir à angle ouvert (voir MeetsRingAtOpenAngle).
            float2? Project(float2 p, List<float2> sourceLoop, int to, bool inward)
            {
                if (!NearestOnLevel(levels[to], p, out int targetLoop, out float2 nearest))
                {
                    return null;
                }
                // Point le plus proche d'abord ; s'il tombe dans la dobra d'un anneau (angle fermé,
                // voir MeetsRingAtOpenAngle), le rayon glisse le long de l'anneau cible jusqu'au
                // premier point où il arrive à angle ouvert des deux côtés sans rien croiser.
                // Retour utilisateur : s'arrêter là laissait des rayons qui n'allaient pas jusqu'au
                // centre ("a ligação não vai até ao centro") — le rayon est seulement un peu en
                // biais dans ces zones.
                int lookaheadLevel = inward ? to : -1;
                if (!FindSpokeTarget(p, sourceLoop, levels[to][targetLoop], nearest, null, lookaheadLevel, out float2 q))
                {
                    return null;
                }
                if (to == 0)
                {
                    return q; // périmètre = route existante, raccordée côté ECS
                }
                List<float2> targetPoints = chainPoints[to][targetLoop];
                foreach (float2 existing in targetPoints)
                {
                    if (math.distance(existing, q) < mergeDistance)
                    {
                        // Deux chaînes qui convergent se rejoignent au même nœud — seulement si le
                        // nouveau rayon y arrive à angle ouvert, avec l'anneau ET avec les rayons
                        // déjà raccordés là (vécu sur le périmètre réel : deux rayons à 23° au même
                        // nœud). Sinon, plutôt que de s'arrêter (retour utilisateur : "a ligação não
                        // vai até ao centro"), la chaîne cherche son propre raccord, à l'écart des
                        // nœuds déjà posés sur cet anneau, et continue vers le centre.
                        if (JoinsNodeAtOpenAngle(existing, p, levels[to][targetLoop]))
                        {
                            return existing;
                        }
                        if (!FindSpokeTarget(p, sourceLoop, levels[to][targetLoop], nearest, targetPoints, lookaheadLevel, out q))
                        {
                            return null;
                        }
                        break;
                    }
                }
                q = InsertOnLoop(levels[to][targetLoop], q, snapDistance: JointSnapDistance);
                targetPoints.Add(q);
                return q;
            }

            // Nœuds de chaîne déjà prolongés vers le centre.
            var extended = new HashSet<(float, float)>();

            // Prolonge la chaîne qui passe par p (sur `loop`, niveau k) jusqu'au centre, une chaîne
            // entière à la fois : chaque raccord est choisi en connaissant les chaînes déjà posées
            // (voir FindSpokeTarget et sa simulation de la suite). Construites anneau par anneau,
            // une chaîne posée ensuite invalidait le chemin prévu d'une autre, qui s'arrêtait.
            void ExtendChain(float2 p, List<float2> loop, int k)
            {
                if (!extended.Add((p.x, p.y)))
                {
                    return;
                }
                float2 current = p;
                List<float2> currentLoop = loop;
                for (int level = k + 1; level < levels.Count; level++)
                {
                    float2? inner = Project(current, currentLoop, level, inward: true);
                    if (!inner.HasValue)
                    {
                        break;
                    }
                    AddSpoke(current, inner.Value);
                    if (!extended.Add((inner.Value.x, inner.Value.y)))
                    {
                        break; // a rejoint une chaîne déjà prolongée jusqu'au centre
                    }
                    NearestOnLevel(levels[level], inner.Value, out int nextLoop, out float2 _);
                    current = inner.Value;
                    currentLoop = levels[level][nextLoop];
                }
            }

            for (int k = 1; k < levels.Count; k++)
            {
                for (int li = 0; li < levels[k].Count; li++)
                {
                    List<float2> loop = levels[k][li];
                    List<float2> points = chainPoints[k][li];
                    if (points.Count == 0)
                    {
                        // Anneau qu'aucune chaîne n'atteint (le premier, ou une branche isolée) : ses
                        // propres points de départ, reliés vers l'extérieur (au périmètre pour le
                        // premier anneau). Chacun est choisi APRÈS que les chaînes précédentes ont été
                        // posées jusqu'au centre (voir ViableSeed), puis sa chaîne est posée à son tour.
                        float length = LoopLength(loop);
                        int count = math.clamp((int)math.floor(length / (1.5f * spacing)), 1, connections);
                        float maxSeedShift = 0.5f * length / count;
                        foreach (float2 seed in SeedPoints(loop, count, spacing))
                        {
                            float2 start = InsertOnLoop(loop, ViableSeed(loop, seed, k, maxSeedShift), snapDistance: JointSnapDistance);
                            if (points.Exists(existing => math.distance(existing, start) < 1e-3f))
                            {
                                continue;
                            }
                            points.Add(start);
                            float2? outer = Project(start, loop, k - 1, inward: false);
                            if (outer.HasValue)
                            {
                                AddSpoke(outer.Value, start);
                            }
                            if (k + 1 < levels.Count)
                            {
                                ExtendChain(start, loop, k);
                            }
                        }
                    }

                    // Chaînes arrivées de l'extérieur sur cet anneau et pas encore prolongées.
                    if (k + 1 < levels.Count)
                    {
                        foreach (float2 p in points.ToArray())
                        {
                            ExtendChain(p, loop, k);
                        }
                    }
                }
            }
        }

        /// <summary>Pas (m) de la recherche d'un raccord de rayon le long de l'anneau cible (voir FindSpokeTarget).</summary>
        private const float SpokeSearchStep = 8f;

        /// <summary>Nombre maximal de raccords candidats dont la suite de chaîne est simulée par choix (voir FindSpokeTarget) : borne le temps de génération.</summary>
        private const int MaxSimulatedCandidates = 200;

        /// <summary>Portée de cette recherche, de part et d'autre du point le plus proche, en nombre d'espacements entre anneaux.</summary>
        private const float SpokeSearchSpacings = 3f;

        /// <summary>Abscisse curviligne (m) du point de `loop` le plus proche de `q`.</summary>
        private static float ArcPosition(List<float2> loop, float2 q)
        {
            float bestDistance = float.MaxValue;
            float along = 0f;
            float walked = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                float2 a = loop[i];
                float2 b = loop[(i + 1) % loop.Count];
                float2 closest = ClosestOnSegment(a, b, q);
                float d = math.distancesq(closest, q);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    along = walked + math.distance(a, closest);
                }
                walked += math.distance(a, b);
            }
            return along;
        }

        /// <summary>Position que prendra le raccord en `point` une fois inséré (même règle qu'InsertOnLoop : sommet existant à moins de JointSnapDistance).</summary>
        private static float2 SnappedJoint(List<float2> loop, float2 point)
        {
            int bestEdge = 0;
            float bestDistance = float.MaxValue;
            float2 bestPoint = point;
            for (int i = 0; i < loop.Count; i++)
            {
                float2 projected = ClosestOnSegment(loop[i], loop[(i + 1) % loop.Count], point);
                float d = math.distancesq(projected, point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestEdge = i;
                    bestPoint = projected;
                }
            }
            float2 a = loop[bestEdge];
            float2 b = loop[(bestEdge + 1) % loop.Count];
            float toA = math.distance(a, bestPoint);
            float toB = math.distance(b, bestPoint);
            if (math.min(toA, toB) < JointSnapDistance)
            {
                return toA <= toB ? a : b;
            }
            return bestPoint;
        }

        /// <summary>Vrai si [a1, a2] et [b1, b2] se croisent franchement, à plus de 1 m de leurs extrémités.</summary>
        private static bool SegmentsCrossAwayFromEnds(float2 a1, float2 a2, float2 b1, float2 b2)
        {
            float2 r = a2 - a1;
            float2 s2 = b2 - b1;
            float denominator = r.x * s2.y - r.y * s2.x;
            if (math.abs(denominator) < 1e-6f)
            {
                return false;
            }
            float2 w = b1 - a1;
            float t = (w.x * s2.y - w.y * s2.x) / denominator;
            float u = (w.x * r.y - w.y * r.x) / denominator;
            float marginA = 1f / math.max(math.length(r), 1e-3f);
            float marginB = 1f / math.max(math.length(s2), 1e-3f);
            return t > marginA && t < 1f - marginA && u > marginB && u < 1f - marginB;
        }

        /// <summary>Angle minimal (degrés) entre un rayon et chacune des deux branches de l'anneau qu'il rejoint.</summary>
        private const float MinSpokeRingAngleDegrees = 50f;

        /// <summary>Distances (m) le long de l'anneau, de part et d'autre du raccord, auxquelles cet angle est mesuré.</summary>
        private static readonly float[] SpokeAngleProbeReaches = { 10f, 20f, 35f, 50f };

        /// <summary>
        /// Vrai si un rayon arrivant sur `loop` au point `q`, en direction de `towardOther`, y fait un
        /// angle ouvert avec les DEUX branches de l'anneau. Retour utilisateur (capture en jeu,
        /// "Objetos sobrepostos") : sur un lobe, les deux rayons suivaient l'axe du lobe et passaient
        /// par la pointe de chaque anneau, qui y fait une dobra ; le rayon entrait dans la dobra à
        /// ~44° de chaque branche et les bords des routes se chevauchaient près du carrefour.
        /// </summary>
        private static bool MeetsRingAtOpenAngle(List<float2> loop, float2 q, float2 towardOther)
        {
            float2 direction = math.normalizesafe(towardOther);
            if (loop.Count < 3 || math.lengthsq(direction) < 1e-6f)
            {
                return true;
            }
            // Position de q le long de l'anneau (abscisse curviligne).
            float bestDistance = float.MaxValue;
            float along = 0f;
            float walked = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                float2 a = loop[i];
                float2 b = loop[(i + 1) % loop.Count];
                float2 closest = ClosestOnSegment(a, b, q);
                float d = math.distancesq(closest, q);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    along = walked + math.distance(a, closest);
                }
                walked += math.distance(a, b);
            }
            float cosLimit = math.cos(math.radians(MinSpokeRingAngleDegrees));
            // Plusieurs distances le long de chaque branche : un anneau peut partir à angle ouvert
            // puis se recourber vers le rayon (vécu en jeu, log [Diag colisão] : 54° au nœud mais
            // 35° à 40 m, routes côte à côte et refus du jeu).
            foreach (float reach in SpokeAngleProbeReaches)
            {
                foreach (float sign in new[] { -1f, 1f })
                {
                    float2 branch = math.normalizesafe(PointAtArc(loop, along + sign * reach) - q);
                    if (math.dot(branch, direction) > cosLimit)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// `count` points répartis également le long de l'anneau, en choisissant le décalage de
        /// départ qui les éloigne le plus des coins marqués — un rayon parti d'un coin partirait
        /// en diagonale vers l'un des deux côtés extérieurs.
        /// </summary>
        private static List<float2> SeedPoints(List<float2> loop, int count, float spacing)
        {
            float length = LoopLength(loop);
            var corners = new List<float>();
            float arc = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                float2 prev = loop[(i - 1 + loop.Count) % loop.Count];
                float2 curr = loop[i];
                float2 next = loop[(i + 1) % loop.Count];
                float2 d1 = math.normalizesafe(curr - prev);
                float2 d2 = math.normalizesafe(next - curr);
                if (math.dot(d1, d2) < math.cos(math.radians(35f)))
                {
                    corners.Add(arc);
                }
                arc += math.distance(curr, next);
            }

            float step = length / count;
            float bestOffset = step * 0.5f;
            float bestScore = float.MinValue;
            const int candidates = 24;
            for (int c = 0; c < candidates; c++)
            {
                float offset = step * c / candidates;
                float score = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    float s = offset + i * step;
                    foreach (float corner in corners)
                    {
                        float d = math.abs(s - corner);
                        d = math.min(d, length - d);
                        score = math.min(score, d);
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    bestOffset = offset;
                }
            }

            var result = new List<float2>(count);
            for (int i = 0; i < count; i++)
            {
                result.Add(PointAtArc(loop, bestOffset + i * step));
            }
            return result;
        }

        /// <summary>
        /// Longueurs cumulées par anneau (cumulative[i] = abscisse du sommet i, dernier élément =
        /// longueur totale), recalculées dès que l'anneau gagne un sommet (voir InsertOnLoop, seule
        /// modification pendant la pose des rayons). PointAtArc est appelé des centaines de milliers
        /// de fois par la simulation des chaînes (voir FindSpokeTarget) : le parcours complet de
        /// l'anneau à chaque appel rendait la génération lente (~2 s avec 12 rayons).
        /// Par thread : les tests peuvent générer en parallèle.
        /// </summary>
        [ThreadStatic]
        private static Dictionary<List<float2>, float[]> s_CumulativeLengths;

        private static float[] CumulativeLengths(List<float2> loop)
        {
            if (s_CumulativeLengths == null)
            {
                s_CumulativeLengths = new Dictionary<List<float2>, float[]>();
            }
            if (s_CumulativeLengths.TryGetValue(loop, out float[] cached) && cached.Length == loop.Count + 1)
            {
                return cached;
            }
            var cumulative = new float[loop.Count + 1];
            for (int i = 0; i < loop.Count; i++)
            {
                cumulative[i + 1] = cumulative[i] + math.distance(loop[i], loop[(i + 1) % loop.Count]);
            }
            s_CumulativeLengths[loop] = cumulative;
            return cumulative;
        }

        private static float2 PointAtArc(List<float2> loop, float s)
        {
            float[] cumulative = CumulativeLengths(loop);
            float length = cumulative[loop.Count];
            if (length <= 0f)
            {
                return loop[0];
            }
            s = ((s % length) + length) % length;
            // Dernier sommet i tel que cumulative[i] <= s (recherche dichotomique).
            int lo = 0;
            int hi = loop.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (cumulative[mid] <= s)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            float2 a = loop[lo];
            float2 b = loop[(lo + 1) % loop.Count];
            float edge = cumulative[lo + 1] - cumulative[lo];
            return edge > 0f ? math.lerp(a, b, (s - cumulative[lo]) / edge) : a;
        }

        /// <summary>
        /// Distance (m) sous laquelle un rayon se raccorde à un sommet existant de l'anneau au lieu
        /// d'en insérer un nouveau à côté. Retour utilisateur (capture en jeu, "Objetos
        /// sobrepostos" sur le périmètre réel) : avec 3 m, des raccords tombaient à 3-15 m d'un
        /// sommet voisin et laissaient des tronçons minuscules collés au carrefour, que le jeu
        /// refuse. Les sommets d'un anneau sont espacés d'environ RingSampleStep (25 m) : un
        /// rayon se décale au plus d'une douzaine de mètres, invisible à l'échelle d'un anneau.
        /// </summary>
        private const float JointSnapDistance = 15f;

        /// <summary>
        /// Insère `point` (supposé sur l'anneau) comme sommet, sur l'arête la plus proche.
        /// À moins de `snapDistance` d'un sommet existant, réutilise ce sommet. Retourne la
        /// position EXACTE du sommet, à réutiliser telle quelle comme extrémité du rayon.
        /// </summary>
        private static float2 InsertOnLoop(List<float2> loop, float2 point, float snapDistance)
        {
            int bestEdge = 0;
            float bestDistance = float.MaxValue;
            float2 bestPoint = point;
            for (int i = 0; i < loop.Count; i++)
            {
                float2 projected = ClosestOnSegment(loop[i], loop[(i + 1) % loop.Count], point);
                float d = math.distancesq(projected, point);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestEdge = i;
                    bestPoint = projected;
                }
            }
            float2 a = loop[bestEdge];
            float2 b = loop[(bestEdge + 1) % loop.Count];
            // Le plus proche des deux bouts (et non le premier sous le seuil) : un point qui EST le
            // sommet b était sinon « ramené » sur a, voisin à moins de snapDistance — le rayon
            // partait alors d'un autre sommet que celui choisi (et simulé) par FindSpokeTarget.
            float toA = math.distance(a, bestPoint);
            float toB = math.distance(b, bestPoint);
            if (math.min(toA, toB) < snapDistance)
            {
                return toA <= toB ? a : b;
            }
            loop.Insert(bestEdge + 1, bestPoint);
            return bestPoint;
        }

        private static bool NearestOnLevel(List<List<float2>> loops, float2 point, out int loopIndex, out float2 nearest)
        {
            loopIndex = -1;
            nearest = point;
            float best = float.MaxValue;
            for (int li = 0; li < loops.Count; li++)
            {
                List<float2> loop = loops[li];
                for (int i = 0; i < loop.Count; i++)
                {
                    float2 q = ClosestOnSegment(loop[i], loop[(i + 1) % loop.Count], point);
                    float d = math.distancesq(q, point);
                    if (d < best)
                    {
                        best = d;
                        nearest = q;
                        loopIndex = li;
                    }
                }
            }
            return loopIndex >= 0;
        }

        // ------------------------------------------------------------------
        // Géométrie
        // ------------------------------------------------------------------

        private static float2 ClosestOnSegment(float2 a, float2 b, float2 p)
        {
            float2 ab = b - a;
            float lenSq = math.lengthsq(ab);
            if (lenSq < 1e-8f)
            {
                return a;
            }
            float t = math.clamp(math.dot(p - a, ab) / lenSq, 0f, 1f);
            return a + t * ab;
        }

        private static float LoopLength(List<float2> loop)
        {
            float length = 0f;
            for (int i = 0; i < loop.Count; i++)
            {
                length += math.distance(loop[i], loop[(i + 1) % loop.Count]);
            }
            return length;
        }

        private static float SignedArea(List<float2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                float2 a = polygon[i];
                float2 b = polygon[(i + 1) % polygon.Count];
                area += a.x * b.y - b.x * a.y;
            }
            return area * 0.5f;
        }

        private static List<float2> CleanPolygon(List<float2> pts)
        {
            var result = new List<float2>(pts.Count);
            foreach (float2 p in pts)
            {
                if (result.Count == 0 || math.distance(result[result.Count - 1], p) > 0.01f)
                {
                    result.Add(p);
                }
            }
            if (result.Count > 1 && math.distance(result[0], result[result.Count - 1]) <= 0.01f)
            {
                result.RemoveAt(result.Count - 1);
            }

            // Allers-retours du contour : voir GridGenerator.RemoveBacktracks. Déjà retirés par
            // GridRoadToolSystem.BuildCurveAwarePerimeterPositions ; refait ici pour tout appelant
            // direct (tests, autre source de périmètre). Vécu : un virage de 179° sur 5 m passait
            // pour un coin net ultra-aigu dont la zone d'influence (ComputeSharpCornerWeights)
            // couvrait toute la forme — toutes les pointes d'anneau y étaient gardées comme
            // "coins", jamais arrondies.
            GridGenerator.RemoveBacktracks(result);
            return result;
        }

        /// <summary>Douglas-Peucker sur une polyligne fermée (coupée en deux au point le plus éloigné du premier).</summary>
        internal static List<float2> SimplifyClosed(List<float2> loop, float tolerance)
        {
            if (loop.Count < 4)
            {
                return new List<float2>(loop);
            }
            int far = 0;
            float farDistance = -1f;
            for (int i = 1; i < loop.Count; i++)
            {
                float d = math.distancesq(loop[0], loop[i]);
                if (d > farDistance)
                {
                    farDistance = d;
                    far = i;
                }
            }
            var keep = new bool[loop.Count];
            keep[0] = true;
            keep[far] = true;
            SimplifyRange(loop, 0, far, tolerance, keep);
            SimplifyRange(loop, far, loop.Count, tolerance, keep);
            var result = new List<float2>();
            for (int i = 0; i < loop.Count; i++)
            {
                if (keep[i])
                {
                    result.Add(loop[i]);
                }
            }
            return result;
        }

        /// <summary>Douglas-Peucker entre les indices first et last (last peut valoir loop.Count = retour au sommet 0).</summary>
        private static void SimplifyRange(List<float2> loop, int first, int last, float tolerance, bool[] keep)
        {
            if (last - first < 2)
            {
                return;
            }
            float2 a = loop[first];
            float2 b = loop[last % loop.Count];
            int index = -1;
            float maxDistance = tolerance;
            for (int i = first + 1; i < last; i++)
            {
                float d = math.distance(ClosestOnSegment(a, b, loop[i]), loop[i]);
                if (d > maxDistance)
                {
                    maxDistance = d;
                    index = i;
                }
            }
            if (index < 0)
            {
                return;
            }
            keep[index] = true;
            SimplifyRange(loop, first, index, tolerance, keep);
            SimplifyRange(loop, index, last, tolerance, keep);
        }

        // ------------------------------------------------------------------
        // Champ de distance + marching squares
        // ------------------------------------------------------------------

        private sealed class DistanceField
        {
            public float CellSize;
            public float MaxDepth;
            /// <summary>Voir ComputeConnectedDepth.</summary>
            public float ConnectedDepth;
            /// <summary>Voir ConcentricGenerator.MaxLayersFor ; -1 = pas encore calculé.</summary>
            public int MaxLayersCache = -1;
            private float2 _origin;
            private int _nx;
            private int _nz;
            private float[] _values; // distance signée au périmètre : > 0 à l'intérieur
            private float[] _sharpWeight; // voir ComputeSharpCornerWeights ; null = aucun coin net
            private float[] _sharpTurn; // virage (degrés) du coin de périmètre qui domine _sharpWeight

            public static DistanceField Build(List<float2> polygon)
            {
                float2 mn = polygon[0];
                float2 mx = polygon[0];
                foreach (float2 p in polygon)
                {
                    mn = math.min(mn, p);
                    mx = math.max(mx, p);
                }
                float span = math.cmax(mx - mn);
                float cell = math.clamp(span / TargetCellsPerAxis, MinCellSize, MaxCellSize);

                var field = new DistanceField { CellSize = cell };
                // Une rangée de marge tout autour (valeurs négatives) : tout contour est fermé.
                field._origin = mn - cell;
                field._nx = (int)math.ceil((mx.x - mn.x) / cell) + 3;
                field._nz = (int)math.ceil((mx.y - mn.y) / cell) + 3;
                field._values = new float[field._nx * field._nz];

                int n = polygon.Count;
                var edgeA = new float2[n];
                var edgeAB = new float2[n];
                var edgeInvLenSq = new float[n];
                for (int i = 0; i < n; i++)
                {
                    edgeA[i] = polygon[i];
                    edgeAB[i] = polygon[(i + 1) % n] - polygon[i];
                    float lenSq = math.lengthsq(edgeAB[i]);
                    edgeInvLenSq[i] = lenSq > 1e-8f ? 1f / lenSq : 0f;
                }

                // Index spatial des arêtes par baldes : pour un point donné on visite les baldes
                // en anneaux de plus en plus larges et on s'arrête dès que la meilleure distance
                // trouvée est plus petite que la distance minimale possible à l'anneau suivant —
                // au lieu de tester chaque arête pour chaque point (1,1 s sur un rond-point
                // densifié à 525 points, contre quelques dizaines de ms ainsi).
                float bucketSize = math.max(cell * 8f, span / 32f);
                int bx = (int)math.ceil((field._nx * cell) / bucketSize) + 1;
                int bz = (int)math.ceil((field._nz * cell) / bucketSize) + 1;
                var buckets = new List<int>[bx * bz];
                for (int e = 0; e < n; e++)
                {
                    float2 lo = math.min(edgeA[e], edgeA[e] + edgeAB[e]) - field._origin;
                    float2 hi = math.max(edgeA[e], edgeA[e] + edgeAB[e]) - field._origin;
                    int x0 = math.clamp((int)math.floor(lo.x / bucketSize), 0, bx - 1);
                    int x1 = math.clamp((int)math.floor(hi.x / bucketSize), 0, bx - 1);
                    int z0 = math.clamp((int)math.floor(lo.y / bucketSize), 0, bz - 1);
                    int z1 = math.clamp((int)math.floor(hi.y / bucketSize), 0, bz - 1);
                    for (int zz = z0; zz <= z1; zz++)
                    {
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            ref List<int> list = ref buckets[zz * bx + xx];
                            if (list == null)
                            {
                                list = new List<int>(4);
                            }
                            list.Add(e);
                        }
                    }
                }
                var visitedStamp = new int[n];
                int stamp = 0;

                var crossings = new List<float>();
                float maxDepth = 0f;
                for (int j = 0; j < field._nz; j++)
                {
                    // Intérieur/extérieur par balayage de la rangée (pair-impair, même règle que
                    // GridGenerator.PointInPolygon) : abscisses où la rangée coupe le polygone.
                    float rowY = field._origin.y + j * cell;
                    crossings.Clear();
                    for (int e = 0; e < n; e++)
                    {
                        float2 a = edgeA[e];
                        float2 b = a + edgeAB[e];
                        if ((a.y > rowY) != (b.y > rowY))
                        {
                            crossings.Add((b.x - a.x) * (rowY - a.y) / (b.y - a.y) + a.x);
                        }
                    }
                    crossings.Sort();
                    int crossingIndex = 0;

                    for (int i = 0; i < field._nx; i++)
                    {
                        float2 p = field._origin + new float2(i, j) * cell;
                        while (crossingIndex < crossings.Count && crossings[crossingIndex] <= p.x)
                        {
                            crossingIndex++;
                        }
                        bool inside = (crossingIndex & 1) == 1;

                        stamp++;
                        float best = float.MaxValue;
                        int pbx = math.clamp((int)math.floor((p.x - field._origin.x) / bucketSize), 0, bx - 1);
                        int pbz = math.clamp((int)math.floor((p.y - field._origin.y) / bucketSize), 0, bz - 1);
                        int maxRing = math.max(bx, bz);
                        for (int ring = 0; ring <= maxRing; ring++)
                        {
                            for (int zz = pbz - ring; zz <= pbz + ring; zz++)
                            {
                                if (zz < 0 || zz >= bz)
                                {
                                    continue;
                                }
                                bool edgeRow = zz == pbz - ring || zz == pbz + ring;
                                int step = edgeRow ? 1 : 2 * ring;
                                for (int xx = pbx - ring; xx <= pbx + ring; xx += math.max(step, 1))
                                {
                                    if (xx < 0 || xx >= bx)
                                    {
                                        continue;
                                    }
                                    List<int> list = buckets[zz * bx + xx];
                                    if (list == null)
                                    {
                                        continue;
                                    }
                                    foreach (int e in list)
                                    {
                                        if (visitedStamp[e] == stamp)
                                        {
                                            continue;
                                        }
                                        visitedStamp[e] = stamp;
                                        float2 a = edgeA[e];
                                        float2 ab = edgeAB[e];
                                        float t = math.clamp(math.dot(p - a, ab) * edgeInvLenSq[e], 0f, 1f);
                                        float distSq = math.lengthsq(p - (a + t * ab));
                                        if (distSq < best)
                                        {
                                            best = distSq;
                                        }
                                    }
                                }
                            }
                            // Tout balde de l'anneau suivant est à au moins ring × bucketSize.
                            float reach = ring * bucketSize;
                            if (best <= reach * reach)
                            {
                                break;
                            }
                        }

                        float value = math.sqrt(best);
                        if (!inside)
                        {
                            value = -value;
                        }
                        field._values[j * field._nx + i] = value;
                        maxDepth = math.max(maxDepth, value);
                    }
                }
                field.MaxDepth = maxDepth;
                field.ComputeSharpCornerWeights(polygon);
                field.ConnectedDepth = field.ComputeConnectedDepth();
                field.ComputeRingField();
                return field;
            }

            /// <summary>
            /// Plus grande profondeur jusqu'à laquelle la région {distance ≥ profondeur} reste d'un
            /// seul tenant — au-delà, une "taille" étroite de la forme sépare deux lobes et les
            /// anneaux se couperaient en deux. Arbre de fusion : on ajoute les points de grille du
            /// plus profond au moins profond (union-find), et on retient la profondeur de la
            /// DERNIÈRE fusion entre deux parties toutes deux assez grandes pour porter un anneau
            /// (les petites bosses du champ, qui fusionnent aussitôt, sont ignorées).
            /// </summary>
            private float ComputeConnectedDepth()
            {
                var order = new List<int>();
                for (int idx = 0; idx < _values.Length; idx++)
                {
                    if (_values[idx] > 0f)
                    {
                        order.Add(idx);
                    }
                }
                order.Sort((a, b) => _values[b].CompareTo(_values[a]));

                var parent = new int[_values.Length];
                var size = new int[_values.Length];
                for (int idx = 0; idx < parent.Length; idx++)
                {
                    parent[idx] = -1; // pas encore ajouté
                }
                int Find(int x)
                {
                    while (parent[x] != x)
                    {
                        parent[x] = parent[parent[x]];
                        x = parent[x];
                    }
                    return x;
                }

                int significantCells = math.max(4, (int)(math.PI * MinLayerSpacing * MinLayerSpacing / (CellSize * CellSize)));
                float connectedDepth = MaxDepth;
                foreach (int idx in order)
                {
                    parent[idx] = idx;
                    size[idx] = 1;
                    int i = idx % _nx;
                    int j = idx / _nx;
                    for (int nIdx = 0; nIdx < 4; nIdx++)
                    {
                        int ni = i + (nIdx == 0 ? 1 : nIdx == 1 ? -1 : 0);
                        int nj = j + (nIdx == 2 ? 1 : nIdx == 3 ? -1 : 0);
                        if (ni < 0 || nj < 0 || ni >= _nx || nj >= _nz)
                        {
                            continue;
                        }
                        int neighbor = nj * _nx + ni;
                        if (parent[neighbor] < 0)
                        {
                            continue;
                        }
                        int ra = Find(idx);
                        int rb = Find(neighbor);
                        if (ra == rb)
                        {
                            continue;
                        }
                        if (size[ra] >= significantCells && size[rb] >= significantCells)
                        {
                            connectedDepth = _values[idx];
                        }
                        if (size[ra] < size[rb])
                        {
                            (ra, rb) = (rb, ra);
                        }
                        parent[rb] = ra;
                        size[ra] += size[rb];
                    }
                }
                return connectedDepth;
            }

            /// <summary>Poids de coin net (voir ComputeSharpCornerWeights) au point de grille le plus proche.</summary>
            public float SharpWeightAt(float2 p)
            {
                if (_sharpWeight == null)
                {
                    return 0f;
                }
                int i = math.clamp((int)math.round((p.x - _origin.x) / CellSize), 0, _nx - 1);
                int j = math.clamp((int)math.round((p.y - _origin.y) / CellSize), 0, _nz - 1);
                return _sharpWeight[j * _nx + i];
            }

            /// <summary>Virage (degrés) du coin net du périmètre dont dépend ce point (voir SharpWeightAt) ; 0 sans coin.</summary>
            public float SharpTurnAt(float2 p)
            {
                if (_sharpTurn == null)
                {
                    return 0f;
                }
                int i = math.clamp((int)math.round((p.x - _origin.x) / CellSize), 0, _nx - 1);
                int j = math.clamp((int)math.round((p.y - _origin.y) / CellSize), 0, _nz - 1);
                return _sharpTurn[j * _nx + i];
            }

            /// <summary>
            /// Poids 0..1 par point de grille : 1 dans le "sillage" d'un coin NET convexe du
            /// périmètre (sa bissectrice vers l'intérieur), 0 ailleurs. Voir ComputeRingField :
            /// là où le poids vaut 1, l'anneau garde le coin net hérité du périmètre (un carré
            /// reste un carré) ; ailleurs, les pointes qui n'apparaissent qu'en profondeur (bout
            /// d'une forme arrondie) sont arrondies.
            /// </summary>
            private void ComputeSharpCornerWeights(List<float2> polygon)
            {
                int n = polygon.Count;
                float windingSign = math.sign(SignedArea(polygon));
                float cornerCos = math.cos(math.radians(CornerTurnDegrees));
                var corners = new List<(float2 position, float sinHalfAngle, float turnDegrees)>();
                for (int i = 0; i < n; i++)
                {
                    float2 prev = polygon[(i - 1 + n) % n];
                    float2 curr = polygon[i];
                    float2 next = polygon[(i + 1) % n];
                    float2 dIn = math.normalizesafe(curr - prev);
                    float2 dOut = math.normalizesafe(next - curr);
                    float cross = dIn.x * dOut.y - dIn.y * dOut.x;
                    if (math.dot(dIn, dOut) >= cornerCos || cross * windingSign <= 0f)
                    {
                        continue; // pas un coin net, ou coin concave (arrondi naturellement)
                    }
                    // Angle intérieur = π - angle de virage ; demi-angle pour la bissectrice.
                    float turn = math.acos(math.clamp(math.dot(dIn, dOut), -1f, 1f));
                    float halfInterior = 0.5f * (math.PI - turn);
                    corners.Add((curr, math.max(math.sin(halfInterior), 0.05f), math.degrees(turn)));
                }
                if (corners.Count == 0)
                {
                    return;
                }

                _sharpWeight = new float[_values.Length];
                _sharpTurn = new float[_values.Length];
                for (int j = 0; j < _nz; j++)
                {
                    for (int i = 0; i < _nx; i++)
                    {
                        int idx = j * _nx + i;
                        float depth = _values[idx];
                        if (depth <= 0f)
                        {
                            continue;
                        }
                        float2 p = Position(i, j);
                        float weight = 0f;
                        float turnOfBest = 0f;
                        foreach ((float2 c, float sinHalf, float cornerTurn) in corners)
                        {
                            // Sur la bissectrice d'un coin, distance au coin = profondeur / sin(demi-angle).
                            float q = math.distance(p, c) * sinHalf / depth;
                            float w = 1f - math.smoothstep(1.1f, 1.5f, q);
                            if (w > weight || (w == weight && w > 0f && cornerTurn > turnOfBest))
                            {
                                weight = w;
                                turnOfBest = cornerTurn;
                            }
                        }
                        _sharpWeight[idx] = weight;
                        _sharpTurn[idx] = turnOfBest;
                    }
                }
            }

            /// <summary>Profondeur moyenne (m) de l'épine : l'anneau le plus profond possible (voir LevelForDepth).</summary>
            public float RingDepth;

            /// <summary>
            /// Champ dont les courbes de niveau sont les anneaux : 0 sur le périmètre, 1 sur
            /// l'épine (voir ComputeRingField), mélangé près des coins nets du périmètre avec la
            /// distance exacte (voir ComputeSharpCornerWeights) pour qu'un carré garde des coins nets.
            /// </summary>
            public float[] RingField;

            // Table niveau -> profondeur moyenne des points de ce niveau (croissante), pour
            // choisir les niveaux qui donnent un espacement moyen régulier.
            private float[] _levelDepth;
            private const int LevelBins = 256;

            /// <summary>Niveau du champ lisse dont la profondeur moyenne vaut `depth` (m).</summary>
            public float LevelForDepth(float depth)
            {
                for (int b = 1; b < LevelBins; b++)
                {
                    if (_levelDepth[b] >= depth)
                    {
                        float d0 = _levelDepth[b - 1];
                        float d1 = _levelDepth[b];
                        float t = d1 > d0 ? (depth - d0) / (d1 - d0) : 0f;
                        return (b - 1 + math.saturate(t)) / (LevelBins - 1);
                    }
                }
                return 1f;
            }

            /// <summary>
            /// Champ des anneaux G = F / H : F = distance exacte au périmètre, H =
            /// demi-largeur locale de la forme = profondeur du point de l'"épine" qu'on atteint en
            /// suivant la plus forte montée du champ de distance. L'épine est la crête du champ,
            /// gardée seulement là où elle est au moins aussi profonde que la taille la plus
            /// étroite (0,97 × ConnectedDepth) : d'un seul tenant à travers toute la forme, sans
            /// les branches de crête qui partent vers les coins et les bouts de lobes. Chaque
            /// courbe de niveau est donc à une FRACTION fixe de la demi-largeur locale — elle fait
            /// le tour de toute la forme sans se couper ni s'arrêter en pointe là où la forme
            /// s'amincit (elle s'y resserre), et reste parallèle aux côtés droits (tous les points
            /// d'un côté droit montent au même endroit de l'épine, H y est constant). Les plis du
            /// champ de distance donnent des anneaux en pointe ("bicos") : ils sont arrondis après
            /// coup, anneau par anneau (LimitCurvature), pas par un flou du champ qui arrondissait
            /// toute la forme.
            /// </summary>
            private void ComputeRingField()
            {
                int count = _values.Length;
                float spineLevel = 0.97f * math.min(ConnectedDepth, MaxDepth);
                var spine = new bool[count];
                int spineCount = 0;
                int maxIdx = 0;
                for (int j = 0; j < _nz; j++)
                {
                    for (int i = 0; i < _nx; i++)
                    {
                        int idx = j * _nx + i;
                        if (_values[idx] > _values[maxIdx])
                        {
                            maxIdx = idx;
                        }
                        if (_values[idx] >= spineLevel && IsRidge(i, j))
                        {
                            spine[idx] = true;
                            spineCount++;
                        }
                    }
                }
                if (spineCount == 0)
                {
                    spine[maxIdx] = true;
                }
                // Demi-largeur lissée (flou limité à l'intérieur de la forme) : brute, elle saute
                // d'une valeur à l'autre là où deux parties de largeurs différentes se rejoignent
                // (ex. un passage étroit qui débouche sur un lobe), et les anneaux se tassaient
                // contre cette ligne de saut (écart de quelques mètres seulement).
                float[] halfWidth = MaskedBlur(HalfWidthByAscent(spine), math.max(2f, 0.5f * ConnectedDepth / CellSize));

                // Distance EXACTE, sans flou (retour utilisateur : "está a criar formas cada vez
                // mais circulares em vez de guardar a forma original"). Un flou gaussien du champ
                // de distance (σ = ¼ de la profondeur, ~150 m sur une grande forme) arrondissait
                // les pointes, mais aussi toute la forme : les anneaux intérieurs tendaient vers
                // des cercles. Il ne s'était jamais vu en jeu que par accident — un faux coin
                // (aller-retour du périmètre, voir CleanPolygon) forçait le poids de coin à 1
                // partout, donc la distance exacte. Les pointes sont désormais arrondies
                // localement, anneau par anneau (LimitCurvature), sans toucher au reste.
                var ring = new float[count];
                for (int idx = 0; idx < count; idx++)
                {
                    if (_values[idx] <= 0f)
                    {
                        ring[idx] = -1f;
                        continue;
                    }
                    ring[idx] = math.saturate(_values[idx] / math.max(halfWidth[idx], CellSize));
                }
                RingField = ring;

                // Profondeur moyenne par niveau (table croissante), et profondeur de l'épine.
                var sum = new double[LevelBins];
                var hits = new int[LevelBins];
                for (int idx = 0; idx < count; idx++)
                {
                    if (_values[idx] <= 0f)
                    {
                        continue;
                    }
                    int bin = math.clamp((int)math.round(math.saturate(ring[idx]) * (LevelBins - 1)), 0, LevelBins - 1);
                    sum[bin] += _values[idx];
                    hits[bin]++;
                }
                _levelDepth = new float[LevelBins];
                float previous = 0f;
                for (int b = 0; b < LevelBins; b++)
                {
                    float mean = b == 0 ? 0f : hits[b] > 0 ? (float)(sum[b] / hits[b]) : previous;
                    _levelDepth[b] = math.max(mean, previous);
                    previous = _levelDepth[b];
                }
                RingDepth = _levelDepth[LevelBins - 1];
            }

            /// <summary>
            /// Flou gaussien restreint à l'intérieur de la forme (valeur > 0) : moyenne pondérée
            /// des seuls points intérieurs, pour que l'extérieur (0) ne tire pas les valeurs vers
            /// le bas près du bord.
            /// </summary>
            private float[] MaskedBlur(float[] source, float sigmaCells)
            {
                int count = source.Length;
                var weighted = new float[count];
                var mask = new float[count];
                for (int idx = 0; idx < count; idx++)
                {
                    if (_values[idx] > 0f)
                    {
                        weighted[idx] = source[idx];
                        mask[idx] = 1f;
                    }
                }
                float[] blurredValue = GaussianBlur(weighted, sigmaCells);
                float[] blurredMask = GaussianBlur(mask, sigmaCells);
                var result = new float[count];
                for (int idx = 0; idx < count; idx++)
                {
                    result[idx] = blurredMask[idx] > 1e-4f ? blurredValue[idx] / blurredMask[idx] : source[idx];
                }
                return result;
            }

            /// <summary>Flou gaussien séparable (écart-type en cellules), bords étendus par la valeur du bord.</summary>
            private float[] GaussianBlur(float[] source, float sigmaCells)
            {
                int radius = math.max(1, (int)math.ceil(3f * sigmaCells));
                var kernel = new float[2 * radius + 1];
                float total = 0f;
                for (int k = -radius; k <= radius; k++)
                {
                    float value = math.exp(-0.5f * k * k / (sigmaCells * sigmaCells));
                    kernel[k + radius] = value;
                    total += value;
                }
                for (int k = 0; k < kernel.Length; k++)
                {
                    kernel[k] /= total;
                }

                var temp = new float[source.Length];
                for (int j = 0; j < _nz; j++)
                {
                    for (int i = 0; i < _nx; i++)
                    {
                        float acc = 0f;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int ii = math.clamp(i + k, 0, _nx - 1);
                            acc += kernel[k + radius] * source[j * _nx + ii];
                        }
                        temp[j * _nx + i] = acc;
                    }
                }
                var result = new float[source.Length];
                for (int j = 0; j < _nz; j++)
                {
                    for (int i = 0; i < _nx; i++)
                    {
                        float acc = 0f;
                        for (int k = -radius; k <= radius; k++)
                        {
                            int jj = math.clamp(j + k, 0, _nz - 1);
                            acc += kernel[k + radius] * temp[jj * _nx + i];
                        }
                        result[j * _nx + i] = acc;
                    }
                }
                return result;
            }

            /// <summary>
            /// Demi-largeur locale (m) de chaque point : profondeur du point d'épine atteint en
            /// suivant la plus forte montée du champ de distance (calculé en une passe, du plus
            /// profond au moins profond). Un point qui monte jusqu'à un maximum local hors de
            /// l'épine (petite bosse) prend la profondeur de ce maximum.
            /// </summary>
            private float[] HalfWidthByAscent(bool[] spine)
            {
                int count = _values.Length;
                var result = new float[count];
                var order = new List<int>(count);
                for (int idx = 0; idx < count; idx++)
                {
                    if (_values[idx] > 0f)
                    {
                        order.Add(idx);
                    }
                }
                order.Sort((a, b) => _values[b].CompareTo(_values[a]));
                foreach (int idx in order)
                {
                    if (spine[idx])
                    {
                        result[idx] = _values[idx];
                        continue;
                    }
                    int i = idx % _nx;
                    int j = idx / _nx;
                    float best = _values[idx];
                    int bestIdx = -1;
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        for (int di = -1; di <= 1; di++)
                        {
                            int ni = i + di;
                            int nj = j + dj;
                            if ((di == 0 && dj == 0) || ni < 0 || nj < 0 || ni >= _nx || nj >= _nz)
                            {
                                continue;
                            }
                            int n = nj * _nx + ni;
                            // Pente (et non valeur brute) : un voisin en diagonale est plus loin.
                            float slope = (_values[n] - _values[idx]) / ((di != 0 && dj != 0) ? 1.4142f : 1f);
                            if (_values[n] > _values[idx] && (bestIdx < 0 || slope > best))
                            {
                                best = slope;
                                bestIdx = n;
                            }
                        }
                    }
                    result[idx] = bestIdx >= 0 ? result[bestIdx] : _values[idx];
                }
                return result;
            }

            /// <summary>Point de crête : maximum local du champ de distance dans au moins une direction.</summary>
            private bool IsRidge(int i, int j)
            {
                float v = _values[j * _nx + i];
                float tolerance = CellSize * 0.01f;
                for (int d = 0; d < 4; d++)
                {
                    int di = d == 1 ? 0 : 1;
                    int dj = d == 0 ? 0 : d == 3 ? -1 : 1;
                    int i1 = i + di;
                    int j1 = j + dj;
                    int i2 = i - di;
                    int j2 = j - dj;
                    if (i1 < 0 || j1 < 0 || i2 < 0 || j2 < 0 || i1 >= _nx || i2 >= _nx || j1 >= _nz || j2 >= _nz)
                    {
                        continue;
                    }
                    if (v + tolerance >= _values[j1 * _nx + i1] && v + tolerance >= _values[j2 * _nx + i2])
                    {
                        return true;
                    }
                }
                return false;
            }

            private float2 Position(int i, int j) => _origin + new float2(i, j) * CellSize;

            /// <summary>Courbes de niveau fermées "valeur = level" (marching squares, ambiguïtés résolues par la valeur au centre).</summary>
            public List<List<float2>> ExtractContours(float[] values, float level)
            {
                float Value(int i, int j) => values[j * _nx + i];

                // Clé d'un point de contour = l'arête de grille qu'il coupe : (i, j, 0) arête
                // horizontale (i,j)-(i+1,j), (i, j, 1) arête verticale (i,j)-(i,j+1).
                var neighbors = new Dictionary<(int, int, int), List<(int, int, int)>>();
                var points = new Dictionary<(int, int, int), float2>();

                void Link((int, int, int) k1, (int, int, int) k2)
                {
                    AddNeighbor(neighbors, k1, k2);
                    AddNeighbor(neighbors, k2, k1);
                    EnsurePoint(points, values, k1, level);
                    EnsurePoint(points, values, k2, level);
                }

                for (int j = 0; j + 1 < _nz; j++)
                {
                    for (int i = 0; i + 1 < _nx; i++)
                    {
                        float va = Value(i, j);
                        float vb = Value(i + 1, j);
                        float vc = Value(i + 1, j + 1);
                        float vd = Value(i, j + 1);
                        int code = (va > level ? 1 : 0) | (vb > level ? 2 : 0) | (vc > level ? 4 : 0) | (vd > level ? 8 : 0);
                        if (code == 0 || code == 15)
                        {
                            continue;
                        }
                        var bottom = (i, j, 0);
                        var top = (i, j + 1, 0);
                        var left = (i, j, 1);
                        var right = (i + 1, j, 1);
                        bool centerInside = (va + vb + vc + vd) * 0.25f > level;
                        switch (code)
                        {
                            case 1: case 14: Link(left, bottom); break;
                            case 2: case 13: Link(bottom, right); break;
                            case 3: case 12: Link(left, right); break;
                            case 4: case 11: Link(right, top); break;
                            case 6: case 9: Link(bottom, top); break;
                            case 7: case 8: Link(left, top); break;
                            case 5:
                                if (centerInside) { Link(bottom, right); Link(top, left); }
                                else { Link(left, bottom); Link(right, top); }
                                break;
                            case 10:
                                if (centerInside) { Link(left, bottom); Link(right, top); }
                                else { Link(bottom, right); Link(top, left); }
                                break;
                        }
                    }
                }

                var loops = new List<List<float2>>();
                var visited = new HashSet<(int, int, int)>();
                foreach (var start in neighbors.Keys)
                {
                    if (visited.Contains(start))
                    {
                        continue;
                    }
                    var loop = new List<float2>();
                    var previous = start;
                    var current = start;
                    while (true)
                    {
                        visited.Add(current);
                        loop.Add(points[current]);
                        List<(int, int, int)> next = neighbors[current];
                        var candidate = next[0].Equals(previous) && next.Count > 1 ? next[1] : next[0];
                        if (candidate.Equals(start) || visited.Contains(candidate))
                        {
                            break;
                        }
                        previous = current;
                        current = candidate;
                    }
                    if (loop.Count >= 3)
                    {
                        loops.Add(loop);
                    }
                }
                return loops;
            }

            private static void AddNeighbor(Dictionary<(int, int, int), List<(int, int, int)>> neighbors, (int, int, int) key, (int, int, int) other)
            {
                if (!neighbors.TryGetValue(key, out List<(int, int, int)> list))
                {
                    list = new List<(int, int, int)>(2);
                    neighbors[key] = list;
                }
                list.Add(other);
            }

            private void EnsurePoint(Dictionary<(int, int, int), float2> points, float[] values, (int, int, int) key, float level)
            {
                if (points.ContainsKey(key))
                {
                    return;
                }
                (int i, int j, int dir) = key;
                int i2 = dir == 0 ? i + 1 : i;
                int j2 = dir == 0 ? j : j + 1;
                float v1 = values[j * _nx + i];
                float v2 = values[j2 * _nx + i2];
                float t = math.abs(v2 - v1) > 1e-6f ? math.saturate((level - v1) / (v2 - v1)) : 0.5f;
                points[key] = math.lerp(Position(i, j), Position(i2, j2), t);
            }
        }
    }
}
