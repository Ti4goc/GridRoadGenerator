using System.Diagnostics;
using System.Collections.Generic;
using Colossal.Collections;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using GridRoadGenerator.Core;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Mode "Área livre" : au lieu de choisir des nœuds de routes existantes, le joueur clique des
    /// points sur le terrain (comme l'outil des bairros). La zone se referme en cliquant près du
    /// premier point ou par un double-clic ; clic droit retire le dernier point (ou rouvre la
    /// zone), Échap efface tout. Le contour arrondi (FreeAreaPerimeter.Smooth) sert de périmètre
    /// aux générateurs, et une route de périmètre y est posée (FreeAreaPerimeter.PerimeterRoad).
    /// </summary>
    public partial class GridRoadToolSystem
    {
        private readonly List<float3> _freePoints = new List<float3>();
        /// <summary>Contour arrondi, hauteurs du terrain ; vide tant que la zone n'est pas refermée.</summary>
        private readonly List<float3> _freeRing = new List<float3>();
        private float _lastFreeClickTime = -1f;

        // Mode "Pincel" (variante de la zone libre, FreeAreaBrush) : zone peinte bouton maintenu,
        // contour recalculé au relâchement (pas à chaque frame : les caches des générateurs
        // repartiraient de zéro en continu).
        private readonly BrushMask _brush = new BrushMask();
        private float2? _lastBrushPoint;
        private bool _brushPainting;
        private int _brushBoundaryVersion = -1;
        private readonly List<(float3 a, float3 b)> _brushBoundary = new List<(float3, float3)>();

        /// <summary>Vrai en mode "Pincel" (zone libre peinte).</summary>
        public bool BrushMode => _settings.FreeAreaMode && _settings.FreeAreaBrush;
        /// <summary>Rayon (m) du pinceau.</summary>
        public float BrushRadius => 0.5f * math.clamp(_settings.BrushDiameter > 0f ? _settings.BrushDiameter : BrushMask.DefaultDiameter,
            BrushMask.MinDiameter, BrushMask.MaxDiameter);
        /// <summary>Pinceau carré (sinon rond).</summary>
        public bool BrushSquare => _settings.BrushSquare;
        /// <summary>Rotation (degrés) du pinceau carré.</summary>
        public float BrushAngle => _settings.BrushAngle;
        /// <summary>Degrés de rotation par pixel de souris (Shift maintenu).</summary>
        private const float BrushRotateSpeed = 0.35f;
        /// <summary>Point figé pendant la rotation (le pinceau tourne sur place, comme ceux du jeu).</summary>
        private float3? _rotatePivot;
        /// <summary>Vrai pendant un trait de pinceau (bouton maintenu).</summary>
        public bool BrushPainting => _brushPainting;

        private int _brushFillVersion = -1;
        private readonly List<(float3 a, float3 b)> _brushFill = new List<(float3, float3)>();
        /// <summary>Largeur (m) des bandes de BrushFill : une case, ou plus sur une grande zone.</summary>
        public float BrushFillWidth { get; private set; } = BrushMask.Cell;
        /// <summary>Nombre de bandes de remplissage visé par frame (retour utilisateur : jeu lent sur une très grande zone).</summary>
        public const int FillLineBudget = 1500;
        /// <summary>
        /// Pendant un trait : remplissage et bord de la zone peinte recalculés au plus tous les
        /// PaintRefreshSeconds (retour utilisateur : "enquanto pinto" le jeu est lent) ; le cercle du
        /// pinceau, lui, suit la souris à chaque frame.
        /// </summary>
        private const float PaintRefreshSeconds = 0.1f;
        private float _brushFillTime, _brushBoundaryTime;

        private bool PaintRefreshDue(float last) => !_brushPainting || UnityEngine.Time.realtimeSinceStartup - last >= PaintRefreshSeconds;

        /// <summary>Bandes de remplissage de la zone peinte, hauteurs du terrain — aperçu pendant la peinture.</summary>
        public IReadOnlyList<(float3 a, float3 b)> BrushFill
        {
            get
            {
                if (_brushFillVersion != _brush.Version && PaintRefreshDue(_brushFillTime))
                {
                    _brushFillTime = UnityEngine.Time.realtimeSinceStartup;
                    _brushFillVersion = _brush.Version;
                    _brushFill.Clear();
                    // Grande zone : bandes plus larges et plus longues (au plus une tuile, 64 cases),
                    // pour rester sous FillLineBudget lignes dessinées à chaque frame.
                    int rowStep = 1;
                    while (rowStep < 16 && _brush.Area / (rowStep * BrushMask.Cell * math.min(16 * rowStep, 64) * BrushMask.Cell) > FillLineBudget)
                    {
                        rowStep++;
                    }
                    BrushFillWidth = rowStep * BrushMask.Cell;
                    TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
                    foreach ((float2 a, float2 b) in _brush.FillRuns(math.min(16 * rowStep, 64), rowStep))
                    {
                        float3 a3 = new float3(a.x, 0f, a.y), b3 = new float3(b.x, 0f, b.y);
                        a3.y = TerrainUtils.SampleHeight(ref heightData, a3);
                        b3.y = TerrainUtils.SampleHeight(ref heightData, b3);
                        _brushFill.Add((a3, b3));
                    }
                }
                return _brushFill;
            }
        }

        /// <summary>Bord brut de la zone peinte (cases), hauteurs du terrain — aperçu pendant la peinture.</summary>
        public IReadOnlyList<(float3 a, float3 b)> BrushBoundary
        {
            get
            {
                if (_brushBoundaryVersion != _brush.Version && PaintRefreshDue(_brushBoundaryTime))
                {
                    _brushBoundaryTime = UnityEngine.Time.realtimeSinceStartup;
                    _brushBoundaryVersion = _brush.Version;
                    _brushBoundary.Clear();
                    TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
                    foreach ((float2 a, float2 b) in _brush.RawBoundary())
                    {
                        float3 a3 = new float3(a.x, 0f, a.y), b3 = new float3(b.x, 0f, b.y);
                        a3.y = TerrainUtils.SampleHeight(ref heightData, a3);
                        b3.y = TerrainUtils.SampleHeight(ref heightData, b3);
                        _brushBoundary.Add((a3, b3));
                    }
                }
                return _brushBoundary;
            }
        }

        /// <summary>Vrai en mode "Área livre" (sélecteur du panneau).</summary>
        public bool FreeAreaMode => _settings.FreeAreaMode;
        /// <summary>Points cliqués, dans l'ordre (lus par le rendu overlay).</summary>
        public IReadOnlyList<float3> FreeAreaPoints => _freePoints;
        /// <summary>Contour arrondi de la zone refermée (vide sinon).</summary>
        public IReadOnlyList<float3> FreeAreaRing => _freeRing;
        public bool FreeAreaClosed => _freeRing.Count > 0;
        /// <summary>Point du terrain sous le curseur (null hors terrain).</summary>
        public float3? FreeAreaCursor { get; private set; }
        /// <summary>Point cliqué en cours de déplacement (zone refermée, "Desenhar área"), sinon -1.</summary>
        private int _dragPoint = -1;
        /// <summary>Distance (m) au point sous laquelle un clic sur une zone refermée saisit ce point.</summary>
        private const float GrabDistance = 14f;
        /// <summary>Vrai pendant le déplacement d'un point de la zone refermée.</summary>
        public bool FreeAreaDragging => _dragPoint >= 0;
        /// <summary>Vrai si le curseur est sur un point déplaçable (zone refermée).</summary>
        public bool FreeAreaCanGrab { get; private set; }

        /// <summary>Vrai si la dernière tentative de fermeture a échoué (côtés qui se croisent ou zone trop petite).</summary>
        public bool FreeAreaInvalid { get; private set; }

        /// <summary>Positions qui décrivent la sélection active (nœuds ou contour de la zone libre) — signature des caches.</summary>
        private List<float3> ActivePositions => _settings.FreeAreaMode ? _freeRing : _selectedPositions;

        /// <summary>Vrai si la sélection active décrit un périmètre (au moins deux nœuds, ou une zone libre refermée).</summary>
        public bool HasPerimeter => ActivePositions.Count >= 2;

        /// <summary>Points de la sélection active (nœuds, ou points cliqués en zone libre) — affiché dans le panneau.</summary>
        public int SelectionCount => BrushMode ? (int)math.round(_brush.Area / 10000f)
            : _settings.FreeAreaMode ? _freePoints.Count : _selectedNodes.Count;

        /// <summary>Vrai si quelque chose est sélectionné/peint (Échap efface d'abord la sélection).</summary>
        private bool HasFreeAreaSelection => _freePoints.Count > 0 || !_brush.IsEmpty;

        // ------------------------------------------------------------------
        // Obstacles de la zone libre : eau et bâtiments
        // ------------------------------------------------------------------

        /// <summary>Profondeur d'eau (m) au-delà de laquelle un tronçon est considéré dans l'eau.</summary>
        private const float ObstacleWaterDepth = 0.3f;
        /// <summary>Marge (m) autour de l'emprise d'un bâtiment.</summary>
        private const float ObstacleBuildingMargin = 4f;
        /// <summary>Marge (m) autour d'un pylône : demi-largeur de route + pas d'échantillonnage.</summary>
        private const float ObstacleUtilityMargin = 8f;

        /// <summary>Emprises des bâtiments (centre, axes, demi-côtés) de la zone, recalculées quand le contour change.</summary>
        private readonly List<(float2 centre, float2 axisX, float2 axisZ, float2 half)> _buildings = new List<(float2, float2, float2, float2)>();
        private int _buildingsRingCount = -1;
        private float3 _buildingsRingFirst;

        /// <summary>Game.Objects.SearchSystem : arbre spatial des objets statiques (bâtiments).</summary>
        private Game.Objects.SearchSystem m_ObjectSearchSystem;
        private WaterSystem m_WaterSystem;

        /// <summary>
        /// Zone libre (retour : "evitar água e edifícios") : retire les tronçons — rues et route de
        /// périmètre — qui passent dans l'eau ou sur un bâtiment. Même filtre pour l'aperçu et la pose.
        /// </summary>
        public void RemoveObstacleSegments(List<RoadSegmentDef> segments)
        {
            if (!_settings.FreeAreaMode || _freeRing.Count < 3 || segments.Count == 0)
            {
                return;
            }
            m_ObjectSearchSystem ??= World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            m_WaterSystem ??= World.GetOrCreateSystemManaged<WaterSystem>();
            RefreshBuildings();
            WaterSurfaceData<SurfaceWater> water = m_WaterSystem.GetSurfaceData(out JobHandle waterDeps);
            waterDeps.Complete();
            segments.RemoveAll(segment =>
            {
                // Décision mémorisée par tronçon (le croquis redemande la même chose à chaque frame).
                (int, int, int, int) key = SegmentKey(segment);
                if (_obstacleDecisions.TryGetValue(key, out bool known))
                {
                    return known;
                }
                bool blocked = Blocked(segment, ref water);
                _obstacleDecisions[key] = blocked;
                return blocked;
            });
        }

        private readonly Dictionary<(int, int, int, int), bool> _obstacleDecisions = new Dictionary<(int, int, int, int), bool>();

        private bool Blocked(RoadSegmentDef segment, ref WaterSurfaceData<SurfaceWater> water)
        {
            {
                Bezier4x3 curve = segment.IsArc
                    ? NetUtils.FitCurve(segment.Start, segment.StartTangent, segment.EndTangent, segment.End)
                    : NetUtils.StraightCurve(segment.Start, segment.End);
                int samples = math.max(2, (int)math.ceil(math.distance(segment.Start.xz, segment.End.xz) / 8f));
                for (int k = 0; k <= samples; k++)
                {
                    float3 p = MathUtils.Position(curve, (float)k / samples);
                    if (WaterUtils.SampleDepth(ref water, p) > ObstacleWaterDepth || InsideBuilding(p.xz))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        private bool InsideBuilding(float2 p)
        {
            foreach ((float2 centre, float2 axisX, float2 axisZ, float2 half) in _buildings)
            {
                float2 d = p - centre;
                if (math.abs(math.dot(d, axisX)) <= half.x && math.abs(math.dot(d, axisZ)) <= half.y)
                {
                    return true;
                }
            }
            return false;
        }

        private void RefreshBuildings()
        {
            if (_buildingsRingCount == _freeRing.Count && _buildingsRingFirst.Equals(_freeRing[0]))
            {
                return;
            }
            _buildingsRingCount = _freeRing.Count;
            _buildingsRingFirst = _freeRing[0];
            _buildings.Clear();
            _obstacleDecisions.Clear();
            float3 min = _freeRing[0], max = _freeRing[0];
            foreach (float3 p in _freeRing)
            {
                min = math.min(min, p);
                max = math.max(max, p);
            }
            NativeQuadTree<Entity, QuadTreeBoundsXZ> tree = m_ObjectSearchSystem.GetStaticSearchTree(readOnly: true, out JobHandle deps);
            deps.Complete();
            var iterator = new InteriorEdgeSearchIterator { m_QueryBounds = new Bounds3(min - 50f, max + 50f), m_Candidates = new List<Entity>() };
            tree.Iterate(ref iterator);
            foreach (Entity candidate in iterator.m_Candidates)
            {
                // Bâtiments et objets d'utilité publique (pylônes électriques — log [Diag colisão] :
                // OverlapExisting sur des routes traversant un PowerLinePylon).
                bool utility = EntityManager.HasComponent<Game.Objects.UtilityObject>(candidate);
                if (!(utility || EntityManager.HasComponent<Game.Buildings.Building>(candidate))
                    || EntityManager.HasComponent<Game.Common.Deleted>(candidate)
                    || !EntityManager.TryGetComponent(candidate, out Game.Objects.Transform transform)
                    || !EntityManager.TryGetComponent(candidate, out PrefabRef prefabRef)
                    || !EntityManager.TryGetComponent(prefabRef.m_Prefab, out ObjectGeometryData geometry))
                {
                    continue;
                }
                float3 axisX = math.mul(transform.m_Rotation, new float3(1f, 0f, 0f));
                float3 axisZ = math.mul(transform.m_Rotation, new float3(0f, 0f, 1f));
                float3 localCentre = MathUtils.Center(geometry.m_Bounds);
                float3 size = MathUtils.Size(geometry.m_Bounds);
                float3 worldCentre = transform.m_Position + math.mul(transform.m_Rotation, localCentre);
                _buildings.Add((worldCentre.xz, math.normalizesafe(axisX.xz), math.normalizesafe(axisZ.xz),
                    new float2(size.x, size.z) * 0.5f + (utility ? ObstacleUtilityMargin : ObstacleBuildingMargin)));
            }
        }

        /// <summary>Change de mode de sélection depuis le panneau : l'ancienne sélection n'a plus de sens.</summary>
        public void OnSelectionModeChanged()
        {
            ResetState();
        }

        private void ClearFreeArea()
        {
            _buildingsRingCount = -1;
            _freePoints.Clear();
            _freeRing.Clear();
            FreeAreaCursor = null;
            FreeAreaInvalid = false;
            _lastFreeClickTime = -1f;
            _dragPoint = -1;
            FreeAreaCanGrab = false;
            _brush.Clear();
            _brushPainting = false;
            _lastBrushPoint = null;
        }

        /// <summary>Clics sur le terrain en mode zone libre (remplace le survol/la sélection des nœuds).</summary>
        private void HandleFreeAreaInput()
        {
            FreeAreaCursor = GetRaycastResult(out Entity _, out RaycastHit hit) ? hit.m_HitPosition : (float3?)null;
            if (BrushMode)
            {
                HandleBrushInput();
                return;
            }

            // Zone refermée : saisir un point et le déplacer ; le contour est recalculé au relâchement.
            FreeAreaCanGrab = false;
            if (FreeAreaClosed || _dragPoint >= 0)
            {
                if (_dragPoint >= 0)
                {
                    if (FreeAreaCursor.HasValue)
                    {
                        _freePoints[_dragPoint] = FreeAreaCursor.Value;
                    }
                    if (!applyAction.IsPressed())
                    {
                        _dragPoint = -1;
                        TryCloseFreeArea();
                        if (FreeAreaInvalid)
                        {
                            // Forme impossible (côtés croisés) : la zone reste ouverte, à corriger.
                            _freeRing.Clear();
                        }
                    }
                    return;
                }
                int nearest = NearestPoint(FreeAreaCursor);
                FreeAreaCanGrab = nearest >= 0;
                if (applyAction.WasPressedThisFrame() && nearest >= 0)
                {
                    _dragPoint = nearest;
                    FreeAreaInvalid = false;
                    _freeRing.Clear();
                    return;
                }
            }

            if (applyAction.WasPressedThisFrame() && FreeAreaCursor.HasValue && !FreeAreaClosed)
            {
                float3 p = FreeAreaCursor.Value;
                float now = UnityEngine.Time.unscaledTime;
                bool isDoubleClick = now - _lastFreeClickTime <= DoubleClickWindow;
                _lastFreeClickTime = now;
                FreeAreaInvalid = false;
                if (_freePoints.Count >= 3
                    && (isDoubleClick || math.distance(p.xz, _freePoints[0].xz) < FreeAreaPerimeter.CloseDistance))
                {
                    TryCloseFreeArea();
                }
                else if (_freePoints.Count == 0
                    || math.distance(p.xz, _freePoints[_freePoints.Count - 1].xz) >= FreeAreaPerimeter.MinPointDistance)
                {
                    _freePoints.Add(p);
                }
            }

            if (secondaryApplyAction.WasPressedThisFrame())
            {
                FreeAreaInvalid = false;
                if (FreeAreaClosed)
                {
                    _freeRing.Clear(); // rouvre la zone : on peut encore ajouter/retirer des points
                }
                else if (_freePoints.Count > 0)
                {
                    _freePoints.RemoveAt(_freePoints.Count - 1);
                }
            }
        }

        /// <summary>Pincel : bouton gauche maintenu peint, bouton droit maintenu efface ; contour au relâchement.</summary>
        private void HandleBrushInput()
        {
            // Shift + souris : tourne le pinceau carré sur place (comme les pinceaux du jeu), sans peindre.
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            UnityEngine.InputSystem.Mouse mouse = UnityEngine.InputSystem.Mouse.current;
            if (BrushSquare && keyboard != null && mouse != null && keyboard.shiftKey.isPressed)
            {
                _rotatePivot ??= FreeAreaCursor;
                FreeAreaCursor = _rotatePivot;
                float delta = mouse.delta.ReadValue().x * BrushRotateSpeed;
                if (math.abs(delta) > 1e-3f)
                {
                    float angle = (_settings.BrushAngle + delta) % 90f;
                    _settings.BrushAngle = angle < 0f ? angle + 90f : angle;
                }
                _lastBrushPoint = null;
                return;
            }
            _rotatePivot = null;
            bool paint = applyAction.IsPressed();
            bool erase = !paint && secondaryApplyAction.IsPressed();
            if ((paint || erase) && FreeAreaCursor.HasValue)
            {
                float2 p = FreeAreaCursor.Value.xz;
                if (_lastBrushPoint.HasValue)
                {
                    _brush.Stroke(_lastBrushPoint.Value, p, BrushRadius, erase, BrushSquare, BrushAngle);
                }
                else
                {
                    _brush.Stamp(p, BrushRadius, erase, BrushSquare, BrushAngle);
                }
                _lastBrushPoint = p;
                _brushPainting = true;
                FreeAreaInvalid = false;
                _freeRing.Clear(); // pas d'aperçu de la grille pendant le trait
                return;
            }
            _lastBrushPoint = null;
            if (_brushPainting)
            {
                _brushPainting = false;
                RebuildBrushRing();
            }
        }

        private void RebuildBrushRing()
        {
            _freeRing.Clear();
            if (_brush.IsEmpty)
            {
                return;
            }
            List<float3> outline = _brush.Outline();
            if (outline.Count == 0)
            {
                FreeAreaInvalid = true;
                Mod.Log.Info("Zone peinte inexploitable (trop petite ou contour impossible à lisser).");
                return;
            }
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            foreach (float3 p in outline)
            {
                _freeRing.Add(new float3(p.x, TerrainUtils.SampleHeight(ref heightData, p), p.z));
            }
        }

        private int NearestPoint(float3? cursor)
        {
            if (!cursor.HasValue) return -1;
            int best = -1;
            float bestDistance = GrabDistance;
            for (int i = 0; i < _freePoints.Count; i++)
            {
                float d = math.distance(_freePoints[i].xz, cursor.Value.xz);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }
            return best;
        }

        private void TryCloseFreeArea()
        {
            if (!FreeAreaPerimeter.IsSimple(_freePoints))
            {
                FreeAreaInvalid = true;
                Mod.Log.Info("Zone libre refusée : côtés qui se croisent ou zone trop petite.");
                return;
            }
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            _freeRing.Clear();
            foreach (float3 p in FreeAreaPerimeter.Smooth(_freePoints))
            {
                _freeRing.Add(new float3(p.x, TerrainUtils.SampleHeight(ref heightData, p), p.z));
            }
        }
    }
}
