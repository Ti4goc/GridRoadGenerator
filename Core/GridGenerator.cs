using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace GridRoadGenerator.Core
{
    /// <summary>
    /// Représente un segment de route à créer : un point de départ et un point d'arrivée.
    /// Les coordonnées sont en espace monde (X, Z ; Y = hauteur du terrain, gérée ailleurs).
    /// </summary>
    public struct RoadSegmentDef
    {
        public float3 Start;
        public float3 End;
        public bool IsHorizontal; // "horizontal" = parallèle à l'axe principal de la grille
        /// <summary>
        /// Vrai si End est le bout libre d'une impasse (CulDeSacMode) : l'appelant ECS
        /// y pose un objet de cercle de retournement une fois le segment créé.
        /// </summary>
        public bool IsCulDeSacEnd;

        public RoadSegmentDef(float3 start, float3 end, bool isHorizontal, bool isCulDeSacEnd = false)
        {
            Start = start;
            End = end;
            IsHorizontal = isHorizontal;
            IsCulDeSacEnd = isCulDeSacEnd;
        }
    }

    public enum SpacingMode
    {
        /// <summary>Répartit rows/columns de façon égale pour remplir toute l'emprise.</summary>
        FitToArea,
        /// <summary>Espacement fixe (en mètres) ; le nombre de routes est calculé automatiquement.</summary>
        FixedSpacing
    }

    public struct GridParameters
    {
        public SpacingMode Mode;
        public int Rows;
        public int Columns;
        public float SpacingMeters;
        /// <summary>Rotation additionnelle (degrés, -90..+90) de la grille par rapport à l'arête la plus longue du polygone.</summary>
        public float AngleOffsetDegrees;

        /// <summary>
        /// Mode quartier pavillonnaire : les lignes perpendiculaires à l'axe principal
        /// (les "colonnes") deviennent des impasses au lieu de collectrices traversantes.
        /// Les lignes parallèles à l'axe principal (les "rangées") restent inchangées.
        /// </summary>
        public bool CulDeSacMode;
        /// <summary>Profondeur de l'impasse (0.5–0.9), fraction de la longueur réelle (clippée) du tronçon entre deux collectrices.</summary>
        public float CulDeSacDepth;
        /// <summary>Alterne l'origine des impasses entre la collectrice du haut et celle du bas d'un tronçon à l'autre.</summary>
        public bool Staggered;
        /// <summary>Fréquence des impasses (0–100 %) : motif déterministe "une sur N", pas aléatoire.</summary>
        public float CulDeSacRatio;

        /// <summary>
        /// Amplitude (m, 0–15) du décalage pseudo-aléatoire déterministe appliqué à la
        /// position de chaque ligne INTERNE (les "colonnes", axisIsU — voir CulDeSacMode
        /// ci-dessus) avant clipping. Les "rangées" (collectrices) et le périmètre lui-même
        /// ne sont jamais concernés. 0 = désactivé, identique à avant l'existence du jitter.
        /// </summary>
        public float JitterAmount;
        /// <summary>Graine du jitter : mêmes paramètres + même seed = même résultat, jamais aléatoire d'une génération à l'autre.</summary>
        public int JitterSeed;

        public static GridParameters Default => new GridParameters
        {
            Mode = SpacingMode.FitToArea,
            Rows = 3,
            Columns = 3,
            SpacingMeters = 60f,
            AngleOffsetDegrees = 0f,
            CulDeSacMode = false,
            CulDeSacDepth = 0.75f,
            Staggered = true,
            CulDeSacRatio = 100f,
            JitterAmount = 0f,
            JitterSeed = 0
        };
    }

    /// <summary>
    /// Génère une grille de routes à l'intérieur d'un périmètre QUELCONQUE (polygone
    /// convexe, concave, tourné, irrégulier) défini par les nœuds sélectionnés dans
    /// leur ordre de clic.
    ///
    /// Algorithme :
    ///  1. Le polygone est fermé à partir des points dans l'ordre de sélection.
    ///  2. Un repère local (u, v) est construit, orienté sur l'arête la plus longue
    ///     du polygone : la grille suit ainsi la "rue principale" du périmètre,
    ///     pas les axes du monde. AngleOffsetDegrees ajoute une rotation
    ///     supplémentaire à cette orientation (u et v tournent ensemble).
    ///  3. Des lignes de grille u = const et v = const sont générées dans la
    ///     bounding box locale, selon le mode d'espacement.
    ///  4. Chaque ligne est découpée aux frontières du polygone par la règle
    ///     pair-impair (even-odd) : les intersections avec les arêtes sont triées
    ///     le long de la ligne et appariées deux à deux. Un polygone concave
    ///     produit donc naturellement plusieurs sous-segments par ligne.
    ///  5. Les croisements internes entre lignes u et lignes v sont pré-calculés,
    ///     et chaque ligne est émise en sous-segments entre croisements consécutifs.
    ///     Le point monde d'un croisement est calculé UNE seule fois et partagé
    ///     (mêmes floats) par les extrémités des sous-segments des deux lignes,
    ///     pour que le jeu fusionne ces extrémités en un seul nœud d'intersection.
    ///  6. Les segments trop courts pour être une route sont éliminés.
    ///
    /// Cas particulier : avec exactement 2 nœuds, ils sont traités comme les coins
    /// opposés d'un rectangle aligné sur les axes (comportement simple et prévisible).
    /// </summary>
    public static class GridGenerator
    {
        /// <summary>Longueur minimale d'un segment généré, en mètres (en dessous, pas une route viable).</summary>
        public const float MinSegmentLength = 8f;

        /// <summary>
        /// Distance minimale (m) entre deux nœuds générés distincts (croisements entre
        /// eux). En dessous, le croisement le plus tardif (ordre de balayage colonnes
        /// puis rangées) est purement omis plutôt que fusionné ou décalé — un nœud
        /// omis proprement vaut mieux qu'une intersection dégénérée en jeu.
        /// </summary>
        public const float MinNodeDistance = 8f;

        private const float Epsilon = 1e-4f;

        /// <summary>
        /// Tolérance (m) pour rattacher un croisement à un intervalle clippé : un croisement
        /// à moins de cette distance d'une extrémité d'intervalle est fusionné avec elle
        /// (jonction en T au bord du polygone) au lieu de créer un micro-segment.
        /// </summary>
        private const float JoinTolerance = 0.05f;

        /// <summary>Ligne de grille clippée : sa position sur l'axe transverse et ses intervalles intérieurs.</summary>
        private struct GridLine
        {
            public float Position;
            /// <summary>Intervalles [x, y] le long de la ligne, triés, intérieurs au polygone.</summary>
            public List<float2> Intervals;
        }

        /// <summary>Surcharge pratique quand le diagnostic d'omission n'est pas nécessaire (ex. tests existants).</summary>
        public static List<RoadSegmentDef> GenerateGrid(IReadOnlyList<float3> selectedNodePositions, GridParameters parameters)
            => GenerateGrid(selectedNodePositions, parameters, out _);

        /// <summary>
        /// Génère la grille. omittedNodeCount compte les croisements omis pour cause de
        /// proximité excessive avec un autre nœud généré (MinNodeDistance) — 0 si aucun.
        /// </summary>
        public static List<RoadSegmentDef> GenerateGrid(IReadOnlyList<float3> selectedNodePositions,
            GridParameters parameters, out int omittedNodeCount)
        {
            omittedNodeCount = 0;
            if (selectedNodePositions == null || selectedNodePositions.Count < 2)
                throw new ArgumentException("Il faut au moins 2 nœuds sélectionnés.");

            // Hauteur moyenne ; sera reprojetée sur le terrain à la pose.
            float y = 0f;
            foreach (var p in selectedNodePositions) y += p.y;
            y /= selectedNodePositions.Count;

            // Projection 2D (X, Z)
            var pts = new List<float2>(selectedNodePositions.Count);
            foreach (var p in selectedNodePositions) pts.Add(new float2(p.x, p.z));

            // 2 nœuds : rectangle aligné sur les axes, coins opposés.
            if (pts.Count == 2)
            {
                float2 mn = math.min(pts[0], pts[1]);
                float2 mx = math.max(pts[0], pts[1]);
                pts = new List<float2> { mn, new float2(mx.x, mn.y), mx, new float2(mn.x, mx.y) };
            }

            var polygon = RemoveConsecutiveDuplicates(pts);
            if (polygon.Count < 3 || math.abs(SignedArea(polygon)) < 1f)
                return new List<RoadSegmentDef>(); // polygone dégénéré (points alignés/confondus)

            // Repère local orienté sur l'arête la plus longue, plus l'angle réglable.
            (float2 origin, float2 uDir, float2 vDir) = BuildLocalFrame(polygon, parameters.AngleOffsetDegrees);

            // Polygone en coordonnées locales + bounding box locale.
            var local = new List<float2>(polygon.Count);
            float2 lmin = new float2(float.MaxValue), lmax = new float2(float.MinValue);
            foreach (var p in polygon)
            {
                var lp = new float2(math.dot(p - origin, uDir), math.dot(p - origin, vDir));
                local.Add(lp);
                lmin = math.min(lmin, lp);
                lmax = math.max(lmax, lp);
            }

            // Positions des lignes de grille dans le repère local.
            List<float> uPositions, vPositions;
            if (parameters.Mode == SpacingMode.FitToArea)
            {
                uPositions = DistributeEvenly(lmin.x, lmax.x, parameters.Columns);
                vPositions = DistributeEvenly(lmin.y, lmax.y, parameters.Rows);
            }
            else
            {
                uPositions = DistributeFixed(lmin.x, lmax.x, parameters.SpacingMeters);
                vPositions = DistributeFixed(lmin.y, lmax.y, parameters.SpacingMeters);
            }

            // Jitter : uniquement les lignes internes (colonnes, u), jamais les
            // collectrices (rangées, v) ni bien sûr le périmètre lui-même. Appliqué
            // sur les POSITIONS des lignes, en amont du clipping/calcul des croisements
            // (BuildSubSegments) — ceux-ci ne supposent aucun ordre entre les lignes,
            // donc rester correct après jitter est automatique, pas une propriété à
            // maintenir séparément.
            if (parameters.JitterAmount > 0f)
            {
                ApplyJitter(uPositions, parameters.JitterSeed, parameters.JitterAmount, lmin.x, lmax.x);
            }

            // Clipping de chaque ligne au polygone (intervalles intérieurs).
            var uLines = new List<GridLine>(uPositions.Count);
            foreach (var u in uPositions)
                uLines.Add(new GridLine { Position = u, Intervals = ClipLineToPolygon(local, axisIsU: true, position: u) });
            var vLines = new List<GridLine>(vPositions.Count);
            foreach (var v in vPositions)
                vLines.Add(new GridLine { Position = v, Intervals = ClipLineToPolygon(local, axisIsU: false, position: v) });

            return BuildSubSegments(uLines, vLines, origin, uDir, vDir, y, parameters, out omittedNodeCount);
        }

        // ------------------------------------------------------------------
        // Pré-découpage aux croisements internes
        // ------------------------------------------------------------------

        /// <summary>
        /// Calcule les croisements entre lignes u et lignes v (en coordonnées locales, la
        /// ligne u = x croise la ligne v = z au point (x, z), retenu seulement s'il est dans
        /// les intervalles clippés DES DEUX lignes), puis émet chaque ligne en sous-segments
        /// entre croisements consécutifs. Le float3 monde de chaque croisement est calculé
        /// une seule fois et réutilisé tel quel par les deux lignes : les extrémités qui se
        /// rejoignent sont bit-à-bit identiques, condition de la fusion en un seul nœud.
        ///
        /// Un nouveau croisement à moins de MinNodeDistance d'un croisement DÉJÀ accepté
        /// (nécessairement sur une paire de lignes différente : deux croisements sur la
        /// même paire seraient le même point) est purement omis — ni fusionné, ni décalé —
        /// pour éviter une intersection dégénérée en jeu. Balayage colonnes puis rangées,
        /// donc déterministe : c'est toujours le croisement le plus tardif qui cède.
        /// </summary>
        private static List<RoadSegmentDef> BuildSubSegments(List<GridLine> uLines, List<GridLine> vLines,
            float2 origin, float2 uDir, float2 vDir, float y, GridParameters parameters, out int omittedNodeCount)
        {
            var uSplits = new List<(float t, float3 world)>[uLines.Count];
            var vSplits = new List<(float t, float3 world)>[vLines.Count];
            for (int i = 0; i < uLines.Count; i++) uSplits[i] = new List<(float, float3)>();
            for (int i = 0; i < vLines.Count; i++) vSplits[i] = new List<(float, float3)>();

            var acceptedWorldPoints = new List<float3>();
            omittedNodeCount = 0;

            for (int iu = 0; iu < uLines.Count; iu++)
            {
                for (int iv = 0; iv < vLines.Count; iv++)
                {
                    float u = uLines[iu].Position;
                    float v = vLines[iv].Position;
                    if (!ContainsPosition(uLines[iu].Intervals, v) || !ContainsPosition(vLines[iv].Intervals, u))
                        continue;

                    float3 world = ToWorld(origin, uDir, vDir, u, v, y);

                    bool tooClose = false;
                    foreach (float3 accepted in acceptedWorldPoints)
                    {
                        if (math.distance(accepted.xz, world.xz) < MinNodeDistance)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose)
                    {
                        omittedNodeCount++;
                        continue; // omission propre : ni fusion, ni décalage
                    }

                    acceptedWorldPoints.Add(world);
                    uSplits[iu].Add((v, world));
                    vSplits[iv].Add((u, world));
                }
            }

            var segments = new List<RoadSegmentDef>();
            // Culs-de-sac : EmitLine n'applique le mode que pour axisIsU=true (les
            // "colonnes", perpendiculaires à l'axe principal). Les collectrices
            // (v-lines) restent toujours des traversées complètes.
            for (int i = 0; i < uLines.Count; i++)
                EmitLine(segments, uLines[i], uSplits[i], axisIsU: true, origin, uDir, vDir, y, parameters, ref omittedNodeCount);
            for (int i = 0; i < vLines.Count; i++)
                EmitLine(segments, vLines[i], vSplits[i], axisIsU: false, origin, uDir, vDir, y, parameters, ref omittedNodeCount);
            return segments;
        }

        /// <summary>Vrai si t appartient à l'un des intervalles (tolérance JoinTolerance aux bords).</summary>
        private static bool ContainsPosition(List<float2> intervals, float t)
        {
            foreach (var interval in intervals)
            {
                if (t >= interval.x - JoinTolerance && t <= interval.y + JoinTolerance)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Émet les sous-segments d'une ligne : pour chaque intervalle clippé, la chaîne
        /// [extrémité, croisements internes triés, extrémité] est parcourue par paires
        /// consécutives. Un croisement à moins de JoinTolerance d'une extrémité de
        /// l'intervalle remplace cette extrémité (jonction en T : le point monde partagé
        /// du croisement est réutilisé). Les tronçons &lt; MinSegmentLength sont éliminés.
        ///
        /// Mode culs-de-sac (axisIsU=true uniquement, parameters.CulDeSacMode) : chaque
        /// tronçon entre deux points consécutifs de la chaîne ("bloc") est, selon un motif
        /// déterministe "une fois sur N" (CulDeSacRatio), soit laissé traversant comme
        /// d'habitude, soit remplacé par une impasse partant d'une seule extrémité du bloc
        /// (alternée haut/bas si Staggered) et s'arrêtant à CulDeSacDepth de sa longueur.
        /// Un bloc dont l'extrémité de départ choisie n'est PAS une vraie collectrice (pur
        /// point de clipping au bord du polygone, jamais une entrée de `splits`) est
        /// supprimé plutôt que de créer une impasse suspendue dans le vide.
        /// </summary>
        private static void EmitLine(List<RoadSegmentDef> segments, GridLine line,
            List<(float t, float3 world)> splits, bool axisIsU, float2 origin, float2 uDir, float2 vDir, float y,
            GridParameters parameters, ref int omittedNodeCount)
        {
            splits.Sort((a, b) => a.t.CompareTo(b.t));
            bool culDeSac = axisIsU && parameters.CulDeSacMode;
            int blockIndex = 0;

            foreach (var interval in line.Intervals)
            {
                float tA = interval.x;
                float tB = interval.y;
                float3 worldA = InterpolateAlongLine(line, axisIsU, tA, origin, uDir, vDir, y);
                float3 worldB = InterpolateAlongLine(line, axisIsU, tB, origin, uDir, vDir, y);

                // isCollector : vrai seulement si ce point de chaîne provient d'un
                // croisement réel (splits), faux pour une pure extrémité de clipping
                // au bord du polygone (aucune route existante à cet endroit).
                var chain = new List<(float t, float3 world, bool isCollector)> { (tA, worldA, false) };
                bool lastIsCollector = false;
                foreach (var split in splits)
                {
                    if (split.t < tA - JoinTolerance || split.t > tB + JoinTolerance)
                        continue; // croisement d'un autre intervalle de la même ligne
                    if (split.t <= tA + JoinTolerance)
                        chain[0] = (tA, split.world, true);
                    else if (split.t >= tB - JoinTolerance)
                    {
                        worldB = split.world;
                        lastIsCollector = true;
                    }
                    else
                        chain.Add((split.t, split.world, true));
                }
                chain.Add((tB, worldB, lastIsCollector));

                for (int i = 0; i + 1 < chain.Count; i++)
                {
                    if (chain[i + 1].t - chain[i].t < MinSegmentLength)
                        continue; // bloc trop court

                    if (!culDeSac)
                    {
                        segments.Add(new RoadSegmentDef(chain[i].world, chain[i + 1].world, isHorizontal: !axisIsU));
                        continue;
                    }

                    EmitCulDeSacBlock(segments, line, chain[i], chain[i + 1], blockIndex, parameters,
                        origin, uDir, vDir, y, ref omittedNodeCount);
                    blockIndex++;
                }
            }
        }

        /// <summary>
        /// Un seul bloc en mode culs-de-sac : décide (motif déterministe CulDeSacRatio)
        /// s'il reste traversant ou devient une impasse, choisit l'extrémité de départ
        /// (alternance Staggered), et émet l'impasse si son départ est une vraie
        /// collectrice — sinon le bloc est simplement omis. Une impasse dont le bout
        /// libre tombe à moins de MinNodeDistance de la collectrice visée (profondeur
        /// proche de 1 sur un bloc court) est elle aussi omise : ce serait un nœud
        /// quasiment confondu avec une intersection déjà existante.
        /// </summary>
        private static void EmitCulDeSacBlock(List<RoadSegmentDef> segments, GridLine line,
            (float t, float3 world, bool isCollector) a, (float t, float3 world, bool isCollector) b,
            int blockIndex, GridParameters parameters, float2 origin, float2 uDir, float2 vDir, float y,
            ref int omittedNodeCount)
        {
            if (!IsCulDeSacBlock(blockIndex, parameters.CulDeSacRatio))
            {
                // Hors motif : bloc traversant normal, comme sans le mode.
                segments.Add(new RoadSegmentDef(a.world, b.world, isHorizontal: false));
                return;
            }

            // Alternance haut/bas : les blocs pairs partent de a, les impairs de b.
            bool startFromA = !parameters.Staggered || blockIndex % 2 == 0;
            var start = startFromA ? a : b;
            var end = startFromA ? b : a;

            if (!start.isCollector)
            {
                return; // départ hors polygone/collectrice réelle : impasse supprimée
            }

            float depth = math.clamp(parameters.CulDeSacDepth, 0.5f, 0.9f);
            float stubT = start.t + (end.t - start.t) * depth;
            if (math.abs(stubT - start.t) < MinSegmentLength)
            {
                return; // impasse trop courte pour être une route viable
            }

            float3 stubWorld = InterpolateAlongLine(line, axisIsU: true, stubT, origin, uDir, vDir, y);
            if (math.distance(stubWorld.xz, end.world.xz) < MinNodeDistance)
            {
                omittedNodeCount++;
                return; // le bout de l'impasse serait quasi confondu avec la collectrice visée
            }

            segments.Add(new RoadSegmentDef(start.world, stubWorld, isHorizontal: false, isCulDeSacEnd: true));
        }

        /// <summary>Motif déterministe "une fois sur N" : N = round(100/ratio), jamais aléatoire.</summary>
        private static bool IsCulDeSacBlock(int blockIndex, float ratioPercent)
        {
            if (ratioPercent <= 0f) return false;
            if (ratioPercent >= 100f) return true;
            int n = math.max(1, (int)math.round(100f / ratioPercent));
            return blockIndex % n == 0;
        }

        /// <summary>Point monde à la coordonnée t le long d'une ligne de grille (u=const si axisIsU, sinon v=const).</summary>
        private static float3 InterpolateAlongLine(GridLine line, bool axisIsU, float t,
            float2 origin, float2 uDir, float2 vDir, float y)
        {
            return axisIsU
                ? ToWorld(origin, uDir, vDir, line.Position, t, y)
                : ToWorld(origin, uDir, vDir, t, line.Position, y);
        }

        /// <summary>Convertit un point local (u, v) en point monde (X, y, Z).</summary>
        private static float3 ToWorld(float2 origin, float2 uDir, float2 vDir, float u, float v, float y)
        {
            float2 world = origin + u * uDir + v * vDir;
            return new float3(world.x, y, world.y);
        }

        // ------------------------------------------------------------------
        // Repère local
        // ------------------------------------------------------------------

        /// <summary>
        /// Construit le repère (origine, axe u, axe v) : u suit l'arête la plus
        /// longue du polygone puis subit une rotation additionnelle d'angleOffsetDegrees
        /// (sens trigonométrique), v lui reste perpendiculaire.
        /// </summary>
        private static (float2 origin, float2 uDir, float2 vDir) BuildLocalFrame(List<float2> polygon, float angleOffsetDegrees)
        {
            int bestIndex = 0;
            float bestLengthSq = -1f;

            for (int i = 0; i < polygon.Count; i++)
            {
                float2 a = polygon[i];
                float2 b = polygon[(i + 1) % polygon.Count];
                float lenSq = math.lengthsq(b - a);
                if (lenSq > bestLengthSq)
                {
                    bestLengthSq = lenSq;
                    bestIndex = i;
                }
            }

            float2 origin2 = polygon[bestIndex];
            float2 uDir = math.normalize(polygon[(bestIndex + 1) % polygon.Count] - origin2);

            if (angleOffsetDegrees != 0f)
            {
                float radians = math.radians(angleOffsetDegrees);
                float cosA = math.cos(radians);
                float sinA = math.sin(radians);
                uDir = new float2(uDir.x * cosA - uDir.y * sinA, uDir.x * sinA + uDir.y * cosA);
            }

            float2 vDir = new float2(-uDir.y, uDir.x); // perpendiculaire (rotation +90°)
            return (origin2, uDir, vDir);
        }

        // ------------------------------------------------------------------
        // Clipping pair-impair d'une ligne de grille dans le polygone
        // ------------------------------------------------------------------

        /// <summary>
        /// Découpe la droite (u = position) ou (v = position) aux frontières du
        /// polygone (en coordonnées locales). Retourne les intervalles INTÉRIEURS
        /// au polygone : chaque float2 est [début, fin] le long de la droite
        /// (coordonnée v si axisIsU, u sinon), triés croissants.
        /// </summary>
        private static List<float2> ClipLineToPolygon(List<float2> local, bool axisIsU, float position)
        {
            // Intersections de la droite avec chaque arête, exprimées par la
            // coordonnée le long de la droite (t = v si axisIsU, sinon t = u).
            var crossings = new List<float>();

            for (int i = 0; i < local.Count; i++)
            {
                float2 p1 = local[i];
                float2 p2 = local[(i + 1) % local.Count];

                // Coordonnée "transverse" (celle qui doit franchir 'position')
                float c1 = axisIsU ? p1.x : p1.y;
                float c2 = axisIsU ? p2.x : p2.y;
                // Coordonnée "le long de la droite"
                float t1 = axisIsU ? p1.y : p1.x;
                float t2 = axisIsU ? p2.y : p2.x;

                // Règle demi-ouverte [c1, c2) : chaque sommet n'est compté qu'une
                // fois, ce qui garantit un nombre pair d'intersections (robustesse
                // du ray-casting quand la droite passe pile sur un sommet).
                bool crosses = (c1 <= position && position < c2) || (c2 <= position && position < c1);
                if (!crosses) continue;

                float denom = c2 - c1;
                if (math.abs(denom) < Epsilon) continue; // arête parallèle à la droite

                float f = (position - c1) / denom;
                crossings.Add(t1 + f * (t2 - t1));
            }

            crossings.Sort();

            var result = new List<float2>();
            // Appariement pair-impair : [entrée, sortie], [entrée, sortie], ...
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                float tA = crossings[i];
                float tB = crossings[i + 1];
                if (tB - tA < MinSegmentLength) continue; // tronçon trop court

                result.Add(new float2(tA, tB));
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Utilitaires
        // ------------------------------------------------------------------

        private static List<float2> RemoveConsecutiveDuplicates(List<float2> pts)
        {
            var result = new List<float2>();
            foreach (var p in pts)
            {
                if (result.Count == 0 || math.distance(result[result.Count - 1], p) > Epsilon)
                    result.Add(p);
            }
            if (result.Count > 1 && math.distance(result[0], result[result.Count - 1]) < Epsilon)
                result.RemoveAt(result.Count - 1);
            return result;
        }

        /// <summary>Aire signée (shoelace). Sert à détecter les polygones dégénérés.</summary>
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

        private static List<float> DistributeEvenly(float min, float max, int count)
        {
            var result = new List<float>();
            if (count <= 0) return result;
            float step = (max - min) / (count + 1);
            for (int i = 1; i <= count; i++)
                result.Add(min + step * i);
            return result;
        }

        private static List<float> DistributeFixed(float min, float max, float spacing)
        {
            var result = new List<float>();
            if (spacing <= 0.5f) return result;
            float pos = min + spacing;
            while (pos < max - 0.5f)
            {
                result.Add(pos);
                pos += spacing;
            }
            return result;
        }

        /// <summary>
        /// Décale chaque position d'une quantité pseudo-aléatoire déterministe dans
        /// [-amountMeters, +amountMeters] (Unity.Mathematics.Random, seedé une fois puis
        /// consommé dans l'ordre de la liste — reproductible pour un même seed), avant
        /// de clamper au rectangle englobant local pour ne pas éjecter une ligne hors du
        /// polygone.
        /// </summary>
        private static void ApplyJitter(List<float> positions, int seed, float amountMeters, float min, float max)
        {
            // Random exige une seed non nulle ; combinée à une constante impaire pour
            // éviter le cas seed=0 sans jamais changer le résultat pour seed!=0.
            var random = new Unity.Mathematics.Random(((uint)seed ^ 0x9E3779B9u) | 1u);
            for (int i = 0; i < positions.Count; i++)
            {
                float delta = random.NextFloat(-amountMeters, amountMeters);
                positions[i] = math.clamp(positions[i] + delta, min, max);
            }
        }

        // ------------------------------------------------------------------
        // Échantillonnage de courbe (périmètre courbe, ex. rond-point)
        // ------------------------------------------------------------------

        /// <summary>Distance cible (m) entre deux points échantillonnés le long d'une arête courbe.</summary>
        public const float CurveSampleSpacing = 5f;

        /// <summary>Garde-fou : nombre max de points insérés pour une seule arête (courbe très longue).</summary>
        public const int MaxCurveSamplesPerEdge = 24;

        /// <summary>
        /// Échantillonne des points STRICTEMENT INTÉRIEURS (t dans ]0, 1[) le long d'une
        /// courbe de Bézier cubique (a = départ, b/c = points de contrôle, d = arrivée),
        /// pour approximer une arête EXISTANTE courbe (rond-point, virage...) dans le
        /// polygone du périmètre — un polygone construit uniquement à partir des nœuds
        /// coupe tout droit à travers la courbe (corde), ce qui peut faire déborder la
        /// grille générée sur la route courbe elle-même. Les extrémités (a et d) ne sont
        /// PAS incluses : ce sont déjà les positions des nœuds sélectionnés côté appelant.
        /// Aucune dépendance à Colossal.Mathematics ici (Bezier4x3 est décomposée en 4
        /// float3 par l'appelant) : Core reste testable sans le SDK du jeu.
        /// </summary>
        public static List<float3> SampleCurve(float3 a, float3 b, float3 c, float3 d)
        {
            var result = new List<float3>();
            float length = CubicBezierLength(a, b, c, d);
            int samples = math.clamp((int)math.ceil(length / CurveSampleSpacing), 0, MaxCurveSamplesPerEdge);
            for (int i = 1; i <= samples; i++)
            {
                float t = (float)i / (samples + 1);
                result.Add(CubicBezierPosition(a, b, c, d, t));
            }
            return result;
        }

        private static float3 CubicBezierPosition(float3 a, float3 b, float3 c, float3 d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }

        /// <summary>Longueur approchée par ligne brisée sur 16 segments — suffisant pour choisir un nombre d'échantillons.</summary>
        private static float CubicBezierLength(float3 a, float3 b, float3 c, float3 d)
        {
            const int segments = 16;
            float length = 0f;
            float3 prev = a;
            for (int i = 1; i <= segments; i++)
            {
                float3 point = CubicBezierPosition(a, b, c, d, (float)i / segments);
                length += math.distance(prev, point);
                prev = point;
            }
            return length;
        }
    }
}
