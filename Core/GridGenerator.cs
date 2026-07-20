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
        /// <summary>
        /// Vrai pour une connexion radiale (mode Adaptativo, voir EmitRadialConnections) — jamais
        /// pour un anneau (EmitRingSegments). Comme IsCulDeSacEnd, sert à l'appelant ECS à
        /// distinguer un tronçon "traversant" (collectrice/anneau) d'un tronçon "local/terminal"
        /// (impasse/rayon), par exemple pour y appliquer un réseau secondaire différent.
        /// </summary>
        public bool IsRadial;

        public RoadSegmentDef(float3 start, float3 end, bool isHorizontal, bool isCulDeSacEnd = false, bool isRadial = false)
        {
            Start = start;
            End = end;
            IsHorizontal = isHorizontal;
            IsCulDeSacEnd = isCulDeSacEnd;
            IsRadial = isRadial;
        }
    }

    public enum SpacingMode
    {
        /// <summary>Répartit rows/columns de façon égale pour remplir toute l'emprise.</summary>
        FitToArea,
        /// <summary>Espacement fixe (en mètres) ; le nombre de routes est calculé automatiquement.</summary>
        FixedSpacing
    }

    /// <summary>Quel(s) axe(s) de la grille peuvent devenir des impasses en mode CulDeSacMode.</summary>
    public enum CulDeSacAxis
    {
        /// <summary>Seules les colonnes (axisIsU), perpendiculaires à l'axe principal — comportement historique.</summary>
        Columns = 0,
        /// <summary>Seules les rangées (axisIsV), parallèles à l'axe principal.</summary>
        Rows = 1,
        /// <summary>Les deux axes, chacun selon son propre motif/tirage indépendant.</summary>
        Both = 2
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
        /// Mode quartier pavillonnaire : certaines lignes deviennent des impasses au lieu
        /// de collectrices traversantes, selon CulDeSacAxis.
        /// </summary>
        public bool CulDeSacMode;
        /// <summary>Quel(s) axe(s) peuvent devenir des impasses (voir CulDeSacMode).</summary>
        public CulDeSacAxis CulDeSacAxis;
        /// <summary>Profondeur de l'impasse (0.5–0.9), fraction de la longueur réelle (clippée) du tronçon entre deux collectrices.</summary>
        public float CulDeSacDepth;
        /// <summary>Alterne l'origine des impasses entre la collectrice du haut et celle du bas d'un tronçon à l'autre.</summary>
        public bool Staggered;
        /// <summary>Fréquence des impasses (0–100 %) : motif déterministe "une sur N".</summary>
        public float CulDeSacRatio;

        /// <summary>
        /// Mode Adaptativo (voir GridGenerator.GenerateAdaptiveGrid) : nombre de connexions
        /// radiales reliant les anneaux entre eux (0 = aucune, anneaux isolés).
        /// </summary>
        public int RadialConnections;
        /// <summary>
        /// Mode Adaptativo : coins arrondis (un arc de rayon SpacingMeters à chaque sommet net)
        /// au lieu de la jonction en pointe (miter) par défaut — voir RoundCorners.
        /// </summary>
        public bool AdaptiveRoundedCorners;

        public static GridParameters Default => new GridParameters
        {
            Mode = SpacingMode.FitToArea,
            Rows = 3,
            Columns = 3,
            SpacingMeters = 60f,
            AngleOffsetDegrees = 0f,
            CulDeSacMode = false,
            CulDeSacAxis = CulDeSacAxis.Columns,
            CulDeSacDepth = 0.75f,
            Staggered = true,
            CulDeSacRatio = 100f,
            RadialConnections = 8,
            AdaptiveRoundedCorners = false
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
            // Culs-de-sac : EmitLine décide, ligne par ligne, si le mode s'applique via
            // AppliesToAxis(parameters.CulDeSacAxis, axisIsU) — colonnes, rangées, ou les deux.
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
        /// Mode culs-de-sac (parameters.CulDeSacMode, sur l'axe ou les deux axes désignés par
        /// CulDeSacAxis) : chaque tronçon entre deux points consécutifs de la chaîne ("bloc")
        /// est, selon CulDeSacRatio (motif déterministe "une fois sur N"), soit laissé
        /// traversant comme d'habitude, soit remplacé par une impasse partant d'une seule
        /// extrémité du bloc (alternée haut/bas si Staggered) et s'arrêtant à CulDeSacDepth de
        /// sa longueur. Un bloc dont
        /// l'extrémité de départ choisie n'est PAS une vraie collectrice (pur point de
        /// clipping au bord du polygone, jamais une entrée de `splits`) est supprimé plutôt
        /// que de créer une impasse suspendue dans le vide.
        /// </summary>
        private static void EmitLine(List<RoadSegmentDef> segments, GridLine line,
            List<(float t, float3 world)> splits, bool axisIsU, float2 origin, float2 uDir, float2 vDir, float y,
            GridParameters parameters, ref int omittedNodeCount)
        {
            splits.Sort((a, b) => a.t.CompareTo(b.t));
            bool culDeSac = parameters.CulDeSacMode && AppliesToAxis(parameters.CulDeSacAxis, axisIsU);
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

                    EmitCulDeSacBlock(segments, line, chain[i], chain[i + 1], blockIndex, axisIsU, parameters,
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
            int blockIndex, bool axisIsU, GridParameters parameters, float2 origin, float2 uDir, float2 vDir, float y,
            ref int omittedNodeCount)
        {
            if (!IsCulDeSacBlock(blockIndex, parameters.CulDeSacRatio))
            {
                // Hors motif : bloc traversant normal, comme sans le mode.
                segments.Add(new RoadSegmentDef(a.world, b.world, isHorizontal: !axisIsU));
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

            float3 stubWorld = InterpolateAlongLine(line, axisIsU, stubT, origin, uDir, vDir, y);
            if (math.distance(stubWorld.xz, end.world.xz) < MinNodeDistance)
            {
                omittedNodeCount++;
                return; // le bout de l'impasse serait quasi confondu avec la collectrice visée
            }

            segments.Add(new RoadSegmentDef(start.world, stubWorld, isHorizontal: !axisIsU, isCulDeSacEnd: true));
        }

        /// <summary>Vrai si CulDeSacAxis autorise ce mode sur une ligne de cet axisIsU.</summary>
        private static bool AppliesToAxis(CulDeSacAxis axis, bool axisIsU) => axis switch
        {
            CulDeSacAxis.Columns => axisIsU,
            CulDeSacAxis.Rows => !axisIsU,
            CulDeSacAxis.Both => true,
            _ => axisIsU
        };

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

        // ------------------------------------------------------------------
        // Mode adaptativo : anneaux concentriques par offset de polygone
        // ------------------------------------------------------------------

        /// <summary>Nombre max d'anneaux générés : garde-fou anti-boucle sur un polygone pathologique.</summary>
        public const int MaxAdaptiveRings = 200;

        /// <summary>
        /// Un point offset dont la distance au sommet d'origine dépasse MiterLimit × distance
        /// d'offset est clampé à cette distance au lieu d'être laissé filer — voir OffsetVertex.
        /// </summary>
        private const float MiterLimit = 4f;

        /// <summary>
        /// Génère le mode "Adaptativo" : des routes concentriques obtenues en décalant le
        /// polygone du périmètre vers l'intérieur par pas de parameters.SpacingMeters (anneaux,
        /// comme des courbes de niveau), plus quelques connexions radiales reliant les anneaux
        /// entre eux (parameters.RadialConnections). Épouse n'importe quelle forme de périmètre
        /// (convexe, en L, courbe) sans qu'aucun segment ne semble arbitraire : c'est la forme du
        /// polygone qui crée l'irrégularité, pas du hasard. Voir OffsetPolygonInward pour
        /// l'algorithme d'offset et sa gestion (volontairement simplifiée) des coins concaves.
        ///
        /// Si parameters.CulDeSacMode est actif, certains rayons s'arrêtent avant le dernier
        /// anneau au lieu de le traverser complètement, en impasse — voir EmitRadialConnections
        /// (réutilise CulDeSacRatio/CulDeSacDepth, comme en mode classique). Les anneaux eux-mêmes
        /// restent toujours traversants ; CulDeSacAxis/Staggered ne s'appliquent pas ici (concepts
        /// propres à la grille de lignes droites, sans équivalent pour des anneaux/rayons).
        /// </summary>
        public static List<RoadSegmentDef> GenerateAdaptiveGrid(IReadOnlyList<float3> selectedNodePositions, GridParameters parameters)
        {
            if (selectedNodePositions == null || selectedNodePositions.Count < 2 || parameters.SpacingMeters <= 0.5f)
                return new List<RoadSegmentDef>();

            float y = 0f;
            foreach (var p in selectedNodePositions) y += p.y;
            y /= selectedNodePositions.Count;

            var pts = new List<float2>(selectedNodePositions.Count);
            foreach (var p in selectedNodePositions) pts.Add(new float2(p.x, p.z));

            // 2 nœuds : rectangle aligné sur les axes, coins opposés (même convention que GenerateGrid).
            if (pts.Count == 2)
            {
                float2 mn = math.min(pts[0], pts[1]);
                float2 mx = math.max(pts[0], pts[1]);
                pts = new List<float2> { mn, new float2(mx.x, mn.y), mx, new float2(mn.x, mx.y) };
            }

            var polygon = RemoveConsecutiveDuplicates(pts);
            if (polygon.Count < 3 || math.abs(SignedArea(polygon)) < 1f)
                return new List<RoadSegmentDef>();

            var segments = new List<RoadSegmentDef>();
            var rings = new List<List<float2>> { polygon };

            List<float2> current = polygon;
            for (int ring = 0; ring < MaxAdaptiveRings; ring++)
            {
                List<float2> next = OffsetPolygonInward(current, parameters.SpacingMeters, parameters.AdaptiveRoundedCorners);
                if (next == null)
                    break; // dégénéré : aire trop petite, retournée, ou auto-intersectante

                EmitRingSegments(segments, next, y);
                rings.Add(next);
                current = next;
            }

            if (parameters.RadialConnections > 0 && rings.Count > 1)
            {
                EmitRadialConnections(segments, rings, parameters.RadialConnections, y,
                    parameters.CulDeSacMode, parameters.CulDeSacRatio, parameters.CulDeSacDepth);
            }

            return segments;
        }

        /// <summary>Émet les arêtes d'un anneau comme des RoadSegmentDef (boucle fermée, segments trop courts éliminés).</summary>
        private static void EmitRingSegments(List<RoadSegmentDef> segments, List<float2> ring, float y)
        {
            for (int i = 0; i < ring.Count; i++)
            {
                float2 a = ring[i];
                float2 b = ring[(i + 1) % ring.Count];
                if (math.distance(a, b) < MinSegmentLength) continue;
                segments.Add(new RoadSegmentDef(new float3(a.x, y, a.y), new float3(b.x, y, b.y), isHorizontal: false));
            }
        }

        /// <summary>
        /// Décale chaque arête du polygone vers l'intérieur de distance mètres, puis reconstruit
        /// chaque sommet par intersection des deux droites-support adjacentes décalées ("jonction
        /// en pointe"/miter join) — un seul point de sortie par sommet d'entrée valide.
        ///
        /// Avant ce calcul, ResamplePolygon ré-échantillonne les arêtes plus courtes que distance :
        /// un polygone densifié (périmètre courbe échantillonné par SampleCurve, par exemple) a
        /// des arêtes bien plus courtes que la distance d'offset typique, et le miter join PAR
        /// SOMMET ne peut pas rester géométriquement cohérent sur une arête plus courte que le
        /// décalage lui-même (les deux droites-support adjacentes se croisent hors de l'arête
        /// d'origine) — sans ce ré-échantillonnage préalable, un anneau entier serait rejeté à
        /// tort dès qu'une seule arête est trop courte. Conséquence : le nombre de sommets peut
        /// varier d'un anneau à l'autre (voir EmitRadialConnections, qui ne suppose pas une
        /// correspondance d'index fixe entre anneaux).
        ///
        /// Coins concaves ou presque plats ("le point délicat") : l'intersection des deux droites
        /// peut filer très loin du sommet d'origine (droites quasi parallèles, ou virage en
        /// pointe très aiguë). Plutôt qu'un vrai bevel (qui ajouterait un sommet), le point est
        /// CLAMPÉ à MiterLimit × distance du sommet d'origine, dans la même direction — un petit
        /// méplat au lieu d'une pointe qui s'échappe à l'infini. Voir OffsetVertex pour le détail.
        ///
        /// Retourne null si le résultat est dégénéré : aire signée qui a changé de signe (le
        /// polygone s'est "retourné", signe que l'offset a dépassé la largeur locale de la
        /// forme), aire trop petite, ou auto-intersection entre arêtes non adjacentes détectée
        /// (O(n²), n est petit — quelques dizaines de sommets au plus). Pas de tentative de
        /// scinder la forme en sous-polygones (vrai straight-skeleton, hors scope volontairement :
        /// un algorithme simple suffit ici) : sur une forme très concave (ex. un U étroit), les
        /// anneaux s'arrêtent simplement plus tôt qu'un algorithme complet ne le ferait.
        /// </summary>
        internal static List<float2> OffsetPolygonInward(List<float2> rawPolygon, float distance, bool roundedCorners = false)
        {
            List<float2> polygon = ResamplePolygon(rawPolygon, distance);
            int n = polygon.Count;
            if (n < 3) return null;

            float originalArea = SignedArea(polygon);
            if (math.abs(originalArea) < Epsilon) return null;
            float windingSign = math.sign(originalArea);

            var result = new List<float2>(n);
            var clamped = new bool[n];
            for (int i = 0; i < n; i++)
            {
                float2 prev = polygon[(i - 1 + n) % n];
                float2 curr = polygon[i];
                float2 next = polygon[(i + 1) % n];
                result.Add(OffsetVertex(prev, curr, next, distance, windingSign, out clamped[i]));
            }

            // Un coin dont le décalage a "mangé" plus que la largeur locale disponible se
            // retrouve, avec son voisin, à pointer dans le sens INVERSE du tracé d'origine (les
            // deux corrections de sommets se sont croisées) : détectable coin à coin, sans
            // attendre que ça déforme la forme globale. C'est le cas typique d'une forme convexe
            // simple sur-érodée (offset > la moitié de sa largeur locale) — l'aire signée globale
            // et l'auto-intersection (ci-dessous) ne le détectent pas forcément à elles seules
            // (vérifié empiriquement : le résultat peut rester un polygone simple, juste
            // géométriquement faux). Comparaison coin à COIN (DetectCorners), jamais sommet à
            // sommet : un coin net (miter) s'avance légitimement plus loin le long de l'arête que
            // son voisin immédiat ré-échantillonné (ResamplePolygon), ce qui créerait un faux
            // positif si on comparait chaque petite arête individuellement — seul l'écart NET
            // entre deux vrais coins consécutifs (en ignorant les points intermédiaires) indique
            // une véritable inversion. Exemptés : les coins clampés (miter limit) — une vraie
            // cuspide (ex. courbe très serrée) retourne légitimement l'arête locale, c'est
            // justement ce que le clamp absorbe du mieux possible plutôt que de rejeter tout
            // l'anneau à cause d'elle.
            bool[] isCorner = DetectCorners(polygon, out int cornerCount);
            if (cornerCount > 0)
            {
                int firstCorner = 0;
                while (!isCorner[firstCorner]) firstCorner++;
                int ci = firstCorner;
                do
                {
                    int cj = (ci + 1) % n;
                    while (!isCorner[cj]) cj = (cj + 1) % n;
                    if (!clamped[ci] && !clamped[cj])
                    {
                        float2 originalEdge = polygon[cj] - polygon[ci];
                        float2 newEdge = result[cj] - result[ci];
                        if (math.dot(originalEdge, newEdge) <= 0f)
                            return null;
                    }
                    ci = cj;
                } while (ci != firstCorner);
            }

            float resultArea = SignedArea(result);
            // Anneau plus petit qu'un carré de ~2×MinSegmentLength de côté : plus de sens comme route.
            float minArea = 4f * MinSegmentLength * MinSegmentLength;
            if (math.sign(resultArea) != windingSign || math.abs(resultArea) < minArea)
                return null;

            if (HasSelfIntersection(result, clamped))
                return null;

            if (!roundedCorners)
                return result;

            // Cantos arredondados : remplace chaque coin net (miter) par un petit arc de rayon
            // distance centré sur le sommet D'ORIGINE — le round join classique du offsetting de
            // polygone. Purement cosmétique, appliqué seulement APRÈS validation : la forme a déjà
            // été acceptée sur sa version miter ci-dessus, jamais recalculée sur la version arrondie.
            return RoundCorners(polygon, result, distance, windingSign, isCorner);
        }

        /// <summary>
        /// Remplace, dans un anneau déjà validé (miter), chaque coin marqué isCorner par un arc
        /// de rayon distance centré sur le sommet D'ORIGINE (polygon[i], PAS son point offset
        /// miter) — le round join classique de l'offsetting de polygone : au lieu d'une pointe
        /// nette, le contour suit un petit arc de cercle entre la direction d'arrivée et la
        /// direction de départ de ce coin. Les points non marqués comme coins (sur un tronçon
        /// déjà "lisse") sont recopiés tels quels. Le nombre de sommets du résultat change
        /// (plusieurs points par coin arrondi) — sans conséquence : EmitRadialConnections relie
        /// déjà les anneaux par point le plus proche, pas par correspondance d'index.
        /// </summary>
        private static List<float2> RoundCorners(List<float2> polygon, List<float2> miterResult, float distance, float windingSign, bool[] isCorner)
        {
            int n = polygon.Count;
            var result = new List<float2>(n);
            for (int i = 0; i < n; i++)
            {
                if (!isCorner[i])
                {
                    result.Add(miterResult[i]);
                    continue;
                }

                float2 dirIn = polygon[i] - polygon[(i - 1 + n) % n];
                float2 dirOut = polygon[(i + 1) % n] - polygon[i];
                if (math.lengthsq(dirIn) < Epsilon || math.lengthsq(dirOut) < Epsilon)
                {
                    result.Add(miterResult[i]);
                    continue;
                }
                dirIn = math.normalize(dirIn);
                dirOut = math.normalize(dirOut);
                float2 normalIn = InwardNormal(dirIn, windingSign);
                float2 normalOut = InwardNormal(dirOut, windingSign);

                float fromAngle = math.atan2(normalIn.y, normalIn.x);
                float toAngle = math.atan2(normalOut.y, normalOut.x);
                float delta = toAngle - fromAngle;
                while (delta > math.PI) delta -= 2f * math.PI;
                while (delta < -math.PI) delta += 2f * math.PI;

                // Échantillons tous les ~20°, plafonnés : une cuspide très serrée (delta proche
                // de π) ne doit pas produire un nombre déraisonnable de points.
                int samples = math.clamp((int)math.round(math.abs(delta) / math.radians(20f)), 1, RoundCornerMaxSamples);
                for (int s = 0; s <= samples; s++)
                {
                    float t = (float)s / samples;
                    float angle = fromAngle + delta * t;
                    result.Add(polygon[i] + new float2(math.cos(angle), math.sin(angle)) * distance);
                }
            }
            return result;
        }

        /// <summary>Nombre max de points insérés par coin arrondi (RoundCorners) — voir aussi MiterLimit pour le cas non arrondi.</summary>
        private const int RoundCornerMaxSamples = 6;

        /// <summary>Virage (degrés, 0-180) à partir duquel un sommet est considéré comme un "vrai" coin par ResamplePolygon/RoundCorners.</summary>
        private const float CornerAngleThresholdDegrees = 20f;

        /// <summary>Vrai pour chaque sommet dont le virage dépasse CornerAngleThresholdDegrees (ou dégénéré) ; realCornerCount = nombre de vrais.</summary>
        private static bool[] DetectCorners(List<float2> polygon, out int realCornerCount)
        {
            int n = polygon.Count;
            var isRealCorner = new bool[n];
            realCornerCount = 0;
            for (int i = 0; i < n; i++)
            {
                float2 dirIn = polygon[i] - polygon[(i - 1 + n) % n];
                float2 dirOut = polygon[(i + 1) % n] - polygon[i];
                float lenIn = math.length(dirIn);
                float lenOut = math.length(dirOut);
                bool corner;
                if (lenIn < Epsilon || lenOut < Epsilon)
                {
                    corner = true; // sommet dupliqué/dégénéré : à traiter comme un coin plutôt que d'y toucher
                }
                else
                {
                    float cosAngle = math.clamp(math.dot(dirIn / lenIn, dirOut / lenOut), -1f, 1f);
                    corner = math.degrees(math.acos(cosAngle)) > CornerAngleThresholdDegrees;
                }
                isRealCorner[i] = corner;
                if (corner) realCornerCount++;
            }
            return isRealCorner;
        }

        /// <summary>
        /// Ré-échantillonne les tronçons "denses" (arêtes courtes, typiquement l'échantillonnage
        /// de SampleCurve le long d'un périmètre courbe) à espacement régulier le long de l'arc,
        /// EN CONSERVANT tel quel chaque sommet "réel" — un virage de plus de
        /// CornerAngleThresholdDegrees, ex. les coins d'un carré ou d'un L. Remplace une ancienne
        /// version qui fusionnait itérativement l'arête la plus courte : correcte pour un coin
        /// isolé, mais elle pouvait faire dériver un polygone densément échantillonné (un cercle
        /// approximant un giratoire, par exemple) vers une forme dégénérée bien avant d'avoir
        /// réduit son nombre de sommets à quelque chose de raisonnable, en collapsant vers un
        /// côté plutôt qu'en réduisant uniformément — voir GridGeneratorAdaptiveGridTests pour un
        /// cas concret. Ici, chaque "tronçon" entre deux coins réels consécutifs est ré-échantillonné
        /// indépendamment par interpolation le long de son propre tracé (jamais par fusion de
        /// sommets voisins), donc il reste représentatif de la forme d'origine quel que soit le
        /// nombre de points retirés.
        ///
        /// Si aucun sommet ne dépasse le seuil (polygone déjà lisse, ex. un cercle sans coin net),
        /// tout le polygone est traité comme un seul tronçon ancré arbitrairement au premier sommet.
        /// </summary>
        private static List<float2> ResamplePolygon(List<float2> polygon, float targetSpacing)
        {
            int n = polygon.Count;
            if (n <= 3) return polygon;

            bool[] isRealCorner = DetectCorners(polygon, out int realCornerCount);
            // Un polygone entièrement lisse (ex. un cercle sans coin net) est traité comme un
            // seul tronçon bouclé, ancré arbitrairement au premier sommet — cette ancre n'étant
            // PAS un vrai coin, elle ne crée aucun risque de dépassement de type miter, donc pas
            // besoin de la marge de dégagement (CornerClearanceFactor) réservée pour un vrai coin :
            // sans ça, un petit périmètre lisse perdrait le plus gros de sa longueur utile en
            // marge des DEUX côtés d'un tronçon qui n'a en réalité aucune extrémité à protéger.
            bool applyCornerMargin = realCornerCount > 0;
            if (realCornerCount == 0)
            {
                isRealCorner[0] = true;
            }

            int firstAnchor = 0;
            while (!isRealCorner[firstAnchor]) firstAnchor++;

            var result = new List<float2>();
            int current = firstAnchor;
            do
            {
                int next = (current + 1) % n;
                while (!isRealCorner[next]) next = (next + 1) % n;
                AppendResampledRun(result, polygon, current, next, targetSpacing, applyCornerMargin);
                current = next;
            } while (current != firstAnchor);

            return result.Count >= 3 ? result : polygon;
        }

        /// <summary>
        /// Marge (× la distance d'offset du prochain OffsetPolygonInward) laissée libre de tout
        /// point ré-échantillonné à chaque extrémité d'un tronçon (voir AppendResampledRun) : le
        /// point offset d'un coin net (miter join) avance légitimement le long de l'arête d'une
        /// distance de cet ordre de grandeur (exactement 1× à 90°, davantage pour un coin plus
        /// aigu) — sans cette marge, un point ré-échantillonné juste à côté du coin se ferait
        /// "dépasser" par le coin lui-même une fois décalé, créant un repli local (auto-
        /// intersection ou inversion de sens détectés à tort comme une vraie dégénérescence).
        /// </summary>
        private const float CornerClearanceFactor = 2f;

        /// <summary>
        /// Ajoute à result le sommet startIdx (conservé exactement) puis, si le tronçon
        /// startIdx→...→endIdx (dans l'ordre du polygone, avec retour au début possible) est
        /// assez long, quelques points intermédiaires interpolés à intervalle régulier le long
        /// de son tracé réel, en laissant CornerClearanceFactor × targetSpacing d'espace libre à
        /// chaque extrémité (voir CornerClearanceFactor) — endIdx lui-même n'est PAS ajouté ici
        /// (il sera le startIdx du tronçon suivant, ou déjà l'ancre de départ si la boucle se referme).
        /// </summary>
        private static void AppendResampledRun(List<float2> result, List<float2> polygon, int startIdx, int endIdx, float targetSpacing, bool applyCornerMargin)
        {
            int n = polygon.Count;
            // do/while, jamais for(;i!=endIdx;) : quand startIdx==endIdx (UN SEUL coin détecté,
            // ou aucun — voir ResamplePolygon, tout le polygone est alors un unique tronçon
            // "bouclé" ancré arbitrairement) le tour complet doit quand même être parcouru une
            // fois, pas zéro — un for(;i!=endIdx;) démarrerait avec la condition déjà fausse.
            var runPoints = new List<float2> { polygon[startIdx] };
            int i = startIdx;
            do
            {
                i = (i + 1) % n;
                runPoints.Add(polygon[i]);
            } while (i != endIdx);

            var cumulative = new float[runPoints.Count];
            for (int k = 1; k < runPoints.Count; k++)
            {
                cumulative[k] = cumulative[k - 1] + math.distance(runPoints[k - 1], runPoints[k]);
            }
            float runLength = cumulative[cumulative.Length - 1];

            result.Add(polygon[startIdx]);
            if (runLength < Epsilon) return;

            float margin = applyCornerMargin ? CornerClearanceFactor * targetSpacing : 0f;
            float usable = runLength - 2f * margin;
            if (usable < targetSpacing) return; // tronçon trop court pour insérer un point en toute sécurité

            int steps = math.max(1, (int)math.round(usable / targetSpacing));
            int segIndex = 0;
            for (int s = 1; s <= steps; s++)
            {
                float targetDist = margin + usable * s / (steps + 1);
                while (segIndex < runPoints.Count - 2 && cumulative[segIndex + 1] < targetDist)
                    segIndex++;
                float segStart = cumulative[segIndex];
                float segEnd = cumulative[segIndex + 1];
                float t = segEnd > segStart ? (targetDist - segStart) / (segEnd - segStart) : 0f;
                result.Add(math.lerp(runPoints[segIndex], runPoints[segIndex + 1], t));
            }
        }

        /// <summary>
        /// Point offset d'un sommet unique (voir OffsetPolygonInward) : intersection des deux
        /// droites-support (arête prev→curr et arête curr→next), chacune décalée vers l'intérieur
        /// de distance le long de sa normale. windingSign vient de SignedArea(polygon d'origine) :
        /// détermine quel côté de chaque arête est "l'intérieur", indépendamment du sens de
        /// parcours du polygone. clamped ressort vrai si le miter limit a dû s'appliquer (voir
        /// OffsetPolygonInward : les arêtes adjacentes à un sommet clampé sont exemptées de la
        /// vérification "arête retournée", un vrai rebroussement — ex. cuspide d'une courbe très
        /// serrée — étant justement le cas que le clamp existe pour absorber du mieux possible).
        /// </summary>
        private static float2 OffsetVertex(float2 prev, float2 curr, float2 next, float distance, float windingSign, out bool clamped)
        {
            clamped = false;
            float2 dirIn = curr - prev;
            float2 dirOut = next - curr;
            float lenIn = math.length(dirIn);
            float lenOut = math.length(dirOut);
            if (lenIn < Epsilon) return curr + InwardNormal(dirOut, windingSign) * distance;
            if (lenOut < Epsilon) return curr + InwardNormal(dirIn, windingSign) * distance;
            dirIn /= lenIn;
            dirOut /= lenOut;

            float2 normalIn = InwardNormal(dirIn, windingSign);
            float2 normalOut = InwardNormal(dirOut, windingSign);

            // Droite offset "in"  : passe par (prev + normalIn*distance), direction dirIn.
            // Droite offset "out" : passe par (curr + normalOut*distance), direction dirOut.
            float2 p1 = prev + normalIn * distance;
            float2 p2 = curr + normalOut * distance;

            float2 miter;
            float cross = dirIn.x * dirOut.y - dirIn.y * dirOut.x;
            if (math.abs(cross) < 1e-4f)
            {
                // Droites quasi parallèles (sommet presque aligné, très courant après
                // densification des arêtes courbes — voir SampleCurve) : pas d'intersection
                // fiable, la moyenne des deux offsets simples est la meilleure approximation locale.
                miter = curr + (normalIn + normalOut) * 0.5f * distance;
            }
            else
            {
                // Intersection des deux droites p1 + t*dirIn = p2 + s*dirOut.
                float2 diff = p2 - p1;
                float t = (diff.x * dirOut.y - diff.y * dirOut.x) / cross;
                miter = p1 + dirIn * t;
            }

            float miterDist = math.distance(miter, curr);
            float maxDist = MiterLimit * distance;
            if (miterDist > maxDist)
            {
                // Clamp le long de la même direction plutôt que d'ajouter un point de bevel :
                // garde un seul sommet de sortie par sommet d'entrée (voir OffsetPolygonInward).
                float2 dir = miterDist > Epsilon ? (miter - curr) / miterDist : (normalIn + normalOut);
                if (math.lengthsq(dir) > Epsilon) dir = math.normalize(dir);
                miter = curr + dir * maxDist;
                clamped = true;
            }
            return miter;
        }

        /// <summary>Normale unitaire d'une direction d'arête, orientée vers l'intérieur du polygone selon windingSign.</summary>
        private static float2 InwardNormal(float2 edgeDir, float windingSign)
        {
            float2 n = new float2(-edgeDir.y, edgeDir.x);
            return windingSign >= 0f ? n : -n;
        }

        /// <summary>
        /// Détection O(n²) d'auto-intersection entre arêtes NON adjacentes du polygone (les
        /// paires d'arêtes adjacentes partagent un sommet par construction, donc exclues). n est
        /// petit (quelques dizaines de sommets au plus pour un périmètre réaliste), le coût est
        /// négligeable ici — voir OffsetPolygonInward pour pourquoi ce test suffit (pas de
        /// tentative de scission topologique).
        /// </summary>
        private static bool HasSelfIntersection(List<float2> polygon, bool[] clamped = null)
        {
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                int i2 = (i + 1) % n;
                // Un sommet clampé (miter limit — voir OffsetVertex) est un "meilleur effort" sur
                // une cuspide très serrée : le petit repli local que ça peut créer près de lui
                // n'indique pas un anneau réellement cassé, seulement l'approximation du clamp.
                // Voir aussi le check "arête retournée" dans OffsetPolygonInward, même principe.
                if (clamped != null && (clamped[i] || clamped[i2])) continue;
                float2 a1 = polygon[i];
                float2 a2 = polygon[i2];
                for (int j = i + 1; j < n; j++)
                {
                    bool adjacent = j == i + 1 || (i == 0 && j == n - 1);
                    if (adjacent) continue;
                    int j2 = (j + 1) % n;
                    if (clamped != null && (clamped[j] || clamped[j2])) continue;

                    if (SegmentsIntersect(a1, a2, polygon[j], polygon[j2]))
                        return true;
                }
            }
            return false;
        }

        /// <summary>Test d'intersection stricte de deux segments (orientation-based) : ignore les contacts en bout de segment.</summary>
        private static bool SegmentsIntersect(float2 a1, float2 a2, float2 b1, float2 b2)
        {
            float d1 = Cross(b2 - b1, a1 - b1);
            float d2 = Cross(b2 - b1, a2 - b1);
            float d3 = Cross(a2 - a1, b1 - a1);
            float d4 = Cross(a2 - a1, b2 - a1);
            return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
        }

        private static float Cross(float2 a, float2 b) => a.x * b.y - a.y * b.x;

        /// <summary>
        /// Relie les anneaux entre eux par quelques rayons traversants, pour que le résultat ne
        /// soit pas juste des boucles isolées sans connexion. count sommets du périmètre
        /// d'origine (rings[0]) sont choisis comme points de départ, espacés le plus
        /// uniformément possible par LONGUEUR D'ARC (pas par index brut : les arêtes courbes
        /// déjà densifiées par SampleCurve auraient sinon une part disproportionnée des rayons).
        ///
        /// Chaque rayon avance ensuite anneau par anneau en rejoignant, à chaque étape, le
        /// sommet le PLUS PROCHE de l'anneau suivant (pas le même index) : SimplifyShortEdges
        /// (voir OffsetPolygonInward) peut changer le nombre de sommets d'un anneau à l'autre,
        /// donc la correspondance d'index n'est plus garantie — "point le plus proche" reste
        /// valide quelle que soit la cardinalité de chaque anneau.
        /// </summary>
        /// <summary>
        /// culDeSacMode actif : certains rayons (motif déterministe culDeSacRatio, même fonction
        /// IsCulDeSacBlock qu'en mode classique — un rayon = un "bloc") s'arrêtent avant le
        /// dernier anneau plutôt que de le traverser, en impasse (IsCulDeSacEnd, culDeSacDepth
        /// fraction du nombre total d'anneaux traversés) — le même cercle de retournement que
        /// pour les impasses de la grille classique s'y pose ensuite côté GridRoadToolSystem, qui
        /// ne distingue pas l'origine du segment. Les anneaux eux-mêmes restent toujours complets.
        /// </summary>
        private static void EmitRadialConnections(List<RoadSegmentDef> segments, List<List<float2>> rings, int count, float y,
            bool culDeSacMode, float culDeSacRatio, float culDeSacDepth)
        {
            List<float2> perimeter = rings[0];
            int n = perimeter.Count;
            if (n == 0) return;

            var cumulative = new float[n + 1];
            for (int i = 0; i < n; i++)
                cumulative[i + 1] = cumulative[i] + math.distance(perimeter[i], perimeter[(i + 1) % n]);
            float totalLength = cumulative[n];
            if (totalLength < Epsilon) return;

            var chosenIndices = new List<int>();
            var seen = new HashSet<int>();
            int actualCount = math.min(count, n);
            for (int k = 0; k < actualCount; k++)
            {
                float target = totalLength * k / actualCount;
                int bestIndex = 0;
                float bestDist = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float d = math.abs(cumulative[i] - target);
                    if (d < bestDist) { bestDist = d; bestIndex = i; }
                }
                if (seen.Add(bestIndex)) chosenIndices.Add(bestIndex);
            }

            int radialBlockIndex = 0;
            foreach (int index in chosenIndices)
            {
                bool isCulDeSac = culDeSacMode && IsCulDeSacBlock(radialBlockIndex, culDeSacRatio);
                int stopAtRing = isCulDeSac
                    ? math.max(1, (int)math.round((rings.Count - 1) * math.clamp(culDeSacDepth, 0.5f, 0.9f)))
                    : rings.Count - 1;

                float2 current = perimeter[index];
                for (int r = 1; r <= stopAtRing; r++)
                {
                    float2 nearest = NearestPoint(rings[r], current);
                    bool isEnd = isCulDeSac && r == stopAtRing;
                    if (math.distance(current, nearest) >= MinSegmentLength)
                    {
                        segments.Add(new RoadSegmentDef(new float3(current.x, y, current.y), new float3(nearest.x, y, nearest.y),
                            isHorizontal: false, isCulDeSacEnd: isEnd, isRadial: true));
                    }
                    current = nearest;
                }
                radialBlockIndex++;
            }
        }

        /// <summary>Sommet de ring le plus proche de target (recherche linéaire, ring reste petit).</summary>
        private static float2 NearestPoint(List<float2> ring, float2 target)
        {
            float2 best = ring[0];
            float bestDist = math.distance(ring[0], target);
            for (int i = 1; i < ring.Count; i++)
            {
                float d = math.distance(ring[i], target);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = ring[i];
                }
            }
            return best;
        }
    }
}
