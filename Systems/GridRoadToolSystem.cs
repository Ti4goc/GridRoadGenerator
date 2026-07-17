// Patterns de sélection (survol/surbrillance/résolution de nœud) adaptés de
// CS2-NetworkTools (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
using System;
using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Outil personnalisé : le joueur survole un nœud de route (surbrillance), clique pour
    /// l'ajouter au périmètre (polygone dans l'ordre de clic), puis valide (Entrée ou bouton
    /// du panneau) pour générer la grille interne.
    ///
    /// Placement : pipeline natif de construction réseau, comme NetToolSystem. À chaque frame
    /// de prévisualisation, les définitions (CreationDefinition + NetCourse + Updated) sont
    /// détruites puis recréées via le ToolOutputBarrier avec applyMode = Clear — la cadence
    /// exacte du NetTool vanilla, ce qui garantit qu'aucune entité Temp ne s'empile. À la
    /// validation, applyMode = Apply concrétise les Temp de la frame précédente.
    /// </summary>
    public partial class GridRoadToolSystem : ToolBaseSystem
    {
        /// <summary>Distance max (m) entre le point survolé sur une arête et un nœud pour le sélectionner.</summary>
        private const float MaxSelectDistance = 16f;
        /// <summary>Distance (m) sous laquelle une extrémité de segment est raccordée à un nœud sélectionné.</summary>
        private const float NodeSnapDistance = 4f;
        /// <summary>Distance (m) sous laquelle une extrémité de segment est raccordée à une route du périmètre.</summary>
        private const float EdgeSnapDistance = 4f;
        /// <summary>Fenêtre (s) entre deux clics sur le même nœud pour détecter un double-clic.</summary>
        private const float DoubleClickWindow = 0.35f;
        /// <summary>Garde-fou : nombre max de nœuds visités par la recherche de chemin entre deux clics.</summary>
        private const int MaxPathfindNodes = 2000;
        /// <summary>Garde-fou : nombre max de nœuds du contour détecté au double-clic.</summary>
        private const int MaxPerimeterNodes = 50;

        public override string toolID => "Grid Road Tool";

        private readonly List<Entity> _selectedNodes = new List<Entity>();
        private readonly List<float3> _selectedPositions = new List<float3>();
        private Entity _hoveredNode = Entity.Null;
        private bool _applyRequested;
        private bool _clearRequested;
        private bool _invalidLogged;
        /// <summary>Empêche le spam du log d'omission de nœuds trop proches (MinNodeDistance) : un avis par sélection.</summary>
        private bool _omittedNodesLogged;
        private Entity _lastClickedNode = Entity.Null;
        private float _lastClickTime = -1f;
        private readonly List<Entity> _pathScratch = new List<Entity>();

        /// <summary>Vrai si le dernier double-clic n'a pas trouvé de contour fermé (affiché en tooltip).</summary>
        public bool PerimeterDetectionFailed { get; private set; }

        /// <summary>Nombre de nœuds actuellement sélectionnés (lu par l'UI et les tooltips).</summary>
        public int NodeCount => _selectedNodes.Count;
        /// <summary>Nœuds sélectionnés, dans l'ordre de clic (lus par le rendu overlay).</summary>
        public IReadOnlyList<Entity> SelectedNodes => _selectedNodes;
        /// <summary>Positions des nœuds sélectionnés, dans l'ordre de clic (lues par le rendu overlay).</summary>
        public IReadOnlyList<float3> SelectedPositions => _selectedPositions;
        /// <summary>Nœud actuellement survolé et sélectionnable (Entity.Null sinon).</summary>
        public Entity HoveredNode => _hoveredNode;
        /// <summary>Vrai si une grille prévisualisée existe (définitions créées à la dernière frame).</summary>
        public bool HasPreview { get; private set; }
        /// <summary>Vrai si le périmètre sélectionné ne produit aucune grille (polygone dégénéré).</summary>
        public bool PerimeterInvalid { get; private set; }
        /// <summary>Vrai si la grille prévisualisée peut être construite (pas d'erreur de placement).</summary>
        public bool CanApply { get; private set; }

        private ProxyAction _confirmAction;
        private GridRoadGeneratorSettings _settings;

        private NetToolSystem m_NetToolSystem;
        private TerrainSystem m_TerrainSystem;
        private ToolOutputBarrier m_ToolOutputBarrier;
        private EntityQuery m_DefinitionQuery;

        private PrefabBase _fallbackPrefab;
        private bool _fallbackSearched;
        private PrefabBase _overridePrefab;
        private bool _overrideResolved;

        /// <summary>Arête du périmètre (route existante entre deux nœuds sélectionnés consécutifs).</summary>
        private struct PerimeterEdge
        {
            public Entity m_Entity;
            public Bezier4x3 m_Curve;
        }

        protected override void OnCreate()
        {
            base.OnCreate();

            m_NetToolSystem = World.GetOrCreateSystemManaged<NetToolSystem>();
            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_ToolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            m_DefinitionQuery = GetDefinitionQuery();

            _settings = Mod.Instance.Settings;
            _confirmAction = _settings.GetAction(GridRoadGeneratorSettings.ActionConfirmGrid);
        }

        public override PrefabBase GetPrefab() => GetRoadPrefab();

        /// <summary>L'outil ne s'active que par son raccourci ou le panneau, jamais via un prefab.</summary>
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // Réseaux routiers uniquement, nœuds ciblables via SubElements.
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            ResetState();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            cancelAction.shouldBeEnabled = true;
            if (_confirmAction != null)
            {
                _confirmAction.shouldBeEnabled = true;
            }
        }

        protected override void OnStopRunning()
        {
            ResetState();
            if (_confirmAction != null)
            {
                _confirmAction.shouldBeEnabled = false;
            }
            // Purge les définitions restantes pour ne pas laisser d'aperçu fantôme derrière soi.
            Dependency = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, Dependency);
            base.OnStopRunning();
        }

        /// <summary>Active/désactive l'outil (raccourci clavier global ou panneau UI).</summary>
        public void ToggleTool()
        {
            m_ToolSystem.activeTool = m_ToolSystem.activeTool == this ? (ToolBaseSystem)m_DefaultToolSystem : this;
        }

        /// <summary>Demande la construction de la grille prévisualisée (bouton "Générer" du panneau).</summary>
        public void RequestApply() => _applyRequested = true;

        /// <summary>Demande l'annulation de toute la sélection (bouton "Tout annuler" du panneau).</summary>
        public void RequestClear() => _clearRequested = true;

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            // Cadence vanilla du NetTool : par défaut on repart de zéro chaque frame
            // (Temp détruits + régénérés depuis les définitions recréées ci-dessous).
            applyMode = ApplyMode.Clear;

            try
            {
                // Échap : annule la sélection en cours (1er appui) ; sans sélection,
                // désactive l'outil — le panneau se ferme et le bouton toolbar se
                // relâche via TOOL_ACTIVE (même cadence que les outils vanilla).
                // Le bouton "Tout annuler" du panneau ne fait que vider la sélection.
                if (_clearRequested || cancelAction.WasPressedThisFrame())
                {
                    bool escapePressed = !_clearRequested;
                    _clearRequested = false;
                    bool hadSelection = _selectedNodes.Count > 0;
                    ResetState();
                    if (escapePressed && !hadSelection)
                    {
                        m_ToolSystem.activeTool = m_DefaultToolSystem;
                    }
                    return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                }

                // Entrée ou bouton "Générer" : concrétise l'aperçu de la frame précédente.
                bool confirm = _applyRequested || (_confirmAction != null && _confirmAction.WasPressedThisFrame());
                _applyRequested = false;
                if (confirm && HasPreview)
                {
                    if (GetAllowApply() && !m_DefinitionQuery.IsEmptyIgnoreFilter)
                    {
                        applyMode = ApplyMode.Apply;
                        ResetState();
                        return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                    }
                    Mod.Log.Warn("Grille refusée : l'aperçu contient des erreurs de placement (collisions, pente...). Ajuste le périmètre ou l'espacement.");
                }

                // Survol : surbrillance du nœud sous le curseur.
                Entity hovered = ResolveHoveredNode();
                if (hovered != _hoveredNode)
                {
                    if (!_selectedNodes.Contains(_hoveredNode))
                    {
                        SetHighlight(_hoveredNode, false);
                    }
                    SetHighlight(hovered, true);
                    _hoveredNode = hovered;
                }

                // Clic gauche : sélection/désélection du nœud survolé. Un double-clic sur
                // le premier nœud sélectionné tente de détecter le contour fermé complet.
                if (applyAction.WasPressedThisFrame() && _hoveredNode != Entity.Null)
                {
                    float now = UnityEngine.Time.unscaledTime;
                    bool isDoubleClick = _hoveredNode == _lastClickedNode && (now - _lastClickTime) <= DoubleClickWindow;
                    _lastClickedNode = _hoveredNode;
                    _lastClickTime = now;
                    PerimeterDetectionFailed = false;

                    if (isDoubleClick && _selectedNodes.Count > 0 && _hoveredNode == _selectedNodes[0])
                    {
                        TryAutoSelectPerimeter();
                    }
                    else
                    {
                        ToggleNodeSelection(_hoveredNode);
                    }
                }

                // Clic droit : retire le dernier nœud sélectionné.
                if (secondaryApplyAction.WasPressedThisFrame() && _selectedNodes.Count > 0)
                {
                    Entity last = _selectedNodes[_selectedNodes.Count - 1];
                    _selectedNodes.RemoveAt(_selectedNodes.Count - 1);
                    _selectedPositions.RemoveAt(_selectedPositions.Count - 1);
                    if (last != _hoveredNode)
                    {
                        SetHighlight(last, false);
                    }
                }

                // Reconstruction de l'aperçu (chaque frame, comme le NetTool — pas d'empilement).
                inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                HasPreview = false;
                PerimeterInvalid = false;
                if (_selectedPositions.Count >= 2)
                {
                    int created = CreateGridDefinitions();
                    HasPreview = created > 0;
                    PerimeterInvalid = created == 0;
                }
                if (PerimeterInvalid && !_invalidLogged)
                {
                    _invalidLogged = true;
                    Mod.Log.Info("Périmètre dégénéré (aire trop petite ou points alignés) : aucune grille générée.");
                }
                else if (!PerimeterInvalid)
                {
                    _invalidLogged = false;
                }
                CanApply = HasPreview && GetAllowApply();
            }
            catch (Exception e)
            {
                // Jamais de crash du jeu : on log, on nettoie, et l'outil reste utilisable.
                Mod.Log.Error(e, "Erreur dans GridRoadToolSystem, sélection annulée.");
                ResetState();
            }

            return inputDeps;
        }

        // ------------------------------------------------------------------
        // Survol et sélection des nœuds
        // ------------------------------------------------------------------

        /// <summary>
        /// Résout le nœud sous le curseur, à la manière de NetworkTools : si le raycast touche
        /// directement un nœud on le prend ; s'il touche une arête, on prend le nœud d'extrémité
        /// le plus proche du point survolé sur la courbe (hit.m_Position), dans la limite de
        /// MaxSelectDistance. La position stockée vient toujours du composant Node, jamais du
        /// point d'impact du rayon.
        /// </summary>
        private Entity ResolveHoveredNode()
        {
            if (!GetRaycastResult(out Entity entity, out RaycastHit hit))
            {
                return Entity.Null;
            }

            if (EntityManager.HasComponent<Game.Net.Node>(entity))
            {
                return entity;
            }
            if (hit.m_HitEntity != entity && EntityManager.HasComponent<Game.Net.Node>(hit.m_HitEntity))
            {
                return hit.m_HitEntity;
            }
            if (EntityManager.TryGetComponent(entity, out Edge edge)
                && EntityManager.TryGetComponent(edge.m_Start, out Game.Net.Node startNode)
                && EntityManager.TryGetComponent(edge.m_End, out Game.Net.Node endNode))
            {
                float distToStart = math.distance(hit.m_Position, startNode.m_Position);
                float distToEnd = math.distance(hit.m_Position, endNode.m_Position);
                Entity closest = distToStart <= distToEnd ? edge.m_Start : edge.m_End;
                if (math.min(distToStart, distToEnd) < MaxSelectDistance)
                {
                    return closest;
                }
            }
            return Entity.Null;
        }

        private void ToggleNodeSelection(Entity node)
        {
            if (!EntityManager.TryGetComponent(node, out Game.Net.Node nodeData))
            {
                return;
            }

            int index = _selectedNodes.IndexOf(node);
            if (index >= 0)
            {
                _selectedNodes.RemoveAt(index);
                _selectedPositions.RemoveAt(index);
                if (node != _hoveredNode)
                {
                    SetHighlight(node, false);
                }
                return;
            }

            // S'il existe un chemin sur le réseau EXISTANT entre le dernier nœud
            // sélectionné et celui-ci, insère les nœuds intermédiaires dans l'ordre
            // (comme si le joueur avait cliqué dessus un par un). Réseaux déconnectés :
            // comportement direct conservé (ajoute simplement le nœud cliqué).
            if (_selectedNodes.Count > 0
                && NetworkGraphAlgorithms.FindShortestPath(new EcsNetworkGraph(EntityManager),
                    _selectedNodes[_selectedNodes.Count - 1], node, _pathScratch, MaxPathfindNodes))
            {
                for (int i = 1; i < _pathScratch.Count; i++)
                {
                    Entity pathNode = _pathScratch[i];
                    if (_selectedNodes.Contains(pathNode)
                        || !EntityManager.TryGetComponent(pathNode, out Game.Net.Node pathNodeData))
                    {
                        continue;
                    }
                    _selectedNodes.Add(pathNode);
                    _selectedPositions.Add(pathNodeData.m_Position);
                    SetHighlight(pathNode, true);
                }
                return;
            }

            _selectedNodes.Add(node);
            // Position exacte du nœud (composant Node), pas le point d'impact du raycast.
            _selectedPositions.Add(nodeData.m_Position);
            SetHighlight(node, true);
        }

        /// <summary>
        /// Double-clic sur le premier nœud sélectionné : tente de détecter le contour
        /// fermé du réseau à partir de ce point (suivi de la face, "hug the right
        /// wall") et remplace toute la sélection par les nœuds du contour dans
        /// l'ordre. Échec propre (aucun crash, aucun blocage) si le point de départ
        /// n'appartient à aucune boucle fermée : la sélection en cours est conservée
        /// et PerimeterDetectionFailed est levé pour le tooltip contextuel.
        /// </summary>
        private void TryAutoSelectPerimeter()
        {
            var loop = new List<Entity>();
            if (!NetworkGraphAlgorithms.TraceBoundary(new EcsNetworkGraph(EntityManager),
                _selectedNodes[0], loop, MaxPerimeterNodes))
            {
                PerimeterDetectionFailed = true;
                Mod.Log.Info("Périmètre non détecté depuis ce nœud : continue la sélection manuellement.");
                return;
            }

            foreach (Entity node in _selectedNodes)
            {
                if (node != loop[0])
                {
                    SetHighlight(node, false);
                }
            }
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            foreach (Entity node in loop)
            {
                if (!EntityManager.TryGetComponent(node, out Game.Net.Node nodeData))
                {
                    continue;
                }
                _selectedNodes.Add(node);
                _selectedPositions.Add(nodeData.m_Position);
                SetHighlight(node, true);
            }
        }

        /// <summary>
        /// Adaptateur ECS de l'abstraction de graphe pure (Core/NetworkGraph.cs) : lit
        /// les arêtes sortantes d'un nœud via ConnectedEdge/Edge/Curve, en coût
        /// (longueur réelle de l'arête) et direction (dans le plan XZ, pour le suivi
        /// de contour). Une nouvelle liste à chaque appel — pas d'état partagé.
        /// </summary>
        private readonly struct EcsNetworkGraph : INetworkGraph<Entity>
        {
            private readonly EntityManager _entityManager;

            public EcsNetworkGraph(EntityManager entityManager) => _entityManager = entityManager;

            public IReadOnlyList<GraphEdge<Entity>> GetNeighbors(Entity node)
            {
                var result = new List<GraphEdge<Entity>>();
                if (!_entityManager.TryGetComponent(node, out Game.Net.Node nodeData)
                    || !_entityManager.TryGetBuffer(node, true, out DynamicBuffer<ConnectedEdge> connectedEdges))
                {
                    return result;
                }

                for (int i = 0; i < connectedEdges.Length; i++)
                {
                    Entity edgeEntity = connectedEdges[i].m_Edge;
                    if (!_entityManager.TryGetComponent(edgeEntity, out Edge edge))
                    {
                        continue;
                    }
                    Entity neighbor = edge.m_Start == node ? edge.m_End : edge.m_Start;
                    if (neighbor == node || !_entityManager.TryGetComponent(neighbor, out Game.Net.Node neighborData))
                    {
                        continue; // arête dégénérée ou voisin invalide, ignorée
                    }

                    float cost = _entityManager.TryGetComponent(edgeEntity, out Curve curve) ? curve.m_Length : 0f;

                    float2 delta = neighborData.m_Position.xz - nodeData.m_Position.xz;
                    float length = math.length(delta);
                    float2 direction = length > 1e-4f ? delta / length : new float2(1f, 0f);

                    result.Add(new GraphEdge<Entity>(neighbor, cost, direction));
                }
                return result;
            }
        }

        private void ResetState()
        {
            foreach (Entity node in _selectedNodes)
            {
                SetHighlight(node, false);
            }
            SetHighlight(_hoveredNode, false);
            _hoveredNode = Entity.Null;
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            _applyRequested = false;
            HasPreview = false;
            PerimeterInvalid = false;
            CanApply = false;
            _invalidLogged = false;
            _omittedNodesLogged = false;
            PerimeterDetectionFailed = false;
        }

        private void SetHighlight(Entity entity, bool highlighted)
        {
            if (entity == Entity.Null || !EntityManager.Exists(entity))
            {
                return;
            }
            if (highlighted)
            {
                if (!EntityManager.HasComponent<Highlighted>(entity))
                {
                    EntityManager.AddComponent<Highlighted>(entity);
                }
            }
            else if (EntityManager.HasComponent<Highlighted>(entity))
            {
                EntityManager.RemoveComponent<Highlighted>(entity);
            }
            if (!EntityManager.HasComponent<BatchesUpdated>(entity))
            {
                EntityManager.AddComponent<BatchesUpdated>(entity);
            }
        }

        // ------------------------------------------------------------------
        // Prefab de route
        // ------------------------------------------------------------------

        /// <summary>Vrai si aucun réseau n'a été choisi explicitement (suivre l'outil route natif).</summary>
        public bool RoadPrefabIsAuto => string.IsNullOrEmpty(_settings.RoadPrefabName);

        /// <summary>
        /// Fixe le réseau utilisé pour la grille (choisi dans le sélecteur du panneau)
        /// et le mémorise dans les settings. null = revenir au mode auto (suivre le NetTool).
        /// </summary>
        public void SetRoadPrefab(PrefabBase prefab)
        {
            _overridePrefab = prefab;
            _overrideResolved = true;
            _settings.RoadPrefabName = prefab != null ? $"{prefab.GetType().Name}:{prefab.name}" : string.Empty;
            _settings.ApplyAndSave();
        }

        /// <summary>
        /// Prefab utilisé pour la grille, par priorité : le réseau choisi explicitement
        /// dans le sélecteur (mémorisé entre les sessions), sinon celui de l'outil route
        /// natif s'il s'agit d'une route, sinon la petite route deux voies par défaut.
        /// </summary>
        private PrefabBase GetRoadPrefab()
        {
            if (!_overrideResolved)
            {
                _overrideResolved = true;
                _overridePrefab = ResolveSavedPrefab(_settings.RoadPrefabName);
            }
            if (_overridePrefab != null)
            {
                return _overridePrefab;
            }

            PrefabBase current = m_NetToolSystem.GetPrefab();
            if (current is RoadPrefab)
            {
                return current;
            }

            if (!_fallbackSearched)
            {
                _fallbackSearched = true;
                if (!m_PrefabSystem.TryGetPrefab(new PrefabID(nameof(RoadPrefab), "Small Road"), out _fallbackPrefab))
                {
                    Mod.Log.Warn("Prefab par défaut \"Small Road\" introuvable ; sélectionne une route dans l'outil route natif avant de valider une grille.");
                }
            }
            return _fallbackPrefab;
        }

        /// <summary>
        /// Résout le réseau mémorisé "TypePrefab:Nom" (ex. "RoadPrefab:Small Road").
        /// Retourne null (mode auto) si la chaîne est vide ou le prefab introuvable
        /// (ex. mod d'assets désinstallé) — jamais d'erreur bloquante.
        /// </summary>
        private PrefabBase ResolveSavedPrefab(string saved)
        {
            if (string.IsNullOrEmpty(saved))
            {
                return null;
            }
            int colon = saved.IndexOf(':');
            if (colon <= 0 || colon >= saved.Length - 1)
            {
                return null;
            }
            string typeName = saved.Substring(0, colon);
            string prefabName = saved.Substring(colon + 1);
            if (m_PrefabSystem.TryGetPrefab(new PrefabID(typeName, prefabName), out PrefabBase prefab))
            {
                return prefab;
            }
            Mod.Log.Info($"Réseau mémorisé introuvable ({saved}), retour au mode auto.");
            return null;
        }

        // ------------------------------------------------------------------
        // Création des définitions réseau (aperçu fantôme + pose réelle)
        // ------------------------------------------------------------------

        /// <summary>
        /// Génère la grille (logique pure de Core) et crée pour chaque segment une entité de
        /// définition CreationDefinition + NetCourse, comme NetToolSystem lors d'un tracé
        /// manuel. Le jeu en dérive les Temp (aperçu), découpe les croisements
        /// (CourseSplitSystem) et crée les intersections avec le périmètre.
        /// Retourne le nombre de segments créés (0 = polygone dégénéré ou trop petit).
        /// </summary>
        private int CreateGridDefinitions()
        {
            PrefabBase roadPrefab = GetRoadPrefab();
            if (roadPrefab == null)
            {
                return 0;
            }

            List<RoadSegmentDef> segments;
            try
            {
                segments = GridGenerator.GenerateGrid(_selectedPositions, _settings.ToGridParameters(), out int omittedNodeCount);
                if (omittedNodeCount > 0 && !_omittedNodesLogged)
                {
                    _omittedNodesLogged = true;
                    Mod.Log.Info($"{omittedNodeCount} croisement(s) omis (nœuds trop proches, < {GridGenerator.MinNodeDistance} m).");
                }
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Génération de grille impossible : {e.Message}");
                return 0;
            }
            if (segments.Count == 0)
            {
                return 0;
            }

            Entity prefabEntity = m_PrefabSystem.GetEntity(roadPrefab);
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            List<PerimeterEdge> perimeter = BuildPerimeterEdges();
            EntityCommandBuffer commandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            Unity.Mathematics.Random random = RandomSeed.Next().GetRandom(0);
            int created = 0;

            foreach (RoadSegmentDef segment in segments)
            {
                CoursePos start = MakeCoursePos(segment.Start, ref heightData, perimeter);
                CoursePos end = MakeCoursePos(segment.End, ref heightData, perimeter);

                // Après snapping les deux extrémités peuvent s'être rejointes : segment inutile.
                if (math.distance(start.m_Position.xz, end.m_Position.xz) < GridGenerator.MinSegmentLength)
                {
                    continue;
                }

                NetCourse course = default;
                course.m_Curve = NetUtils.StraightCurve(start.m_Position, end.m_Position);
                course.m_Length = MathUtils.Length(course.m_Curve);
                course.m_FixedIndex = -1;

                start.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(course.m_Curve));
                end.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(course.m_Curve));
                start.m_CourseDelta = 0f;
                end.m_CourseDelta = 1f;
                start.m_Flags |= CoursePosFlags.IsFirst | CoursePosFlags.IsLeft | CoursePosFlags.IsRight;
                end.m_Flags |= CoursePosFlags.IsLast | CoursePosFlags.IsLeft | CoursePosFlags.IsRight;
                course.m_StartPosition = start;
                course.m_EndPosition = end;

                Entity definition = commandBuffer.CreateEntity();
                commandBuffer.AddComponent(definition, new CreationDefinition
                {
                    m_Prefab = prefabEntity,
                    m_RandomSeed = random.NextInt()
                });
                commandBuffer.AddComponent(definition, default(Updated));
                commandBuffer.AddComponent(definition, course);
                created++;
            }
            return created;
        }

        /// <summary>
        /// Construit une extrémité de course, raccordée au périmètre sélectionné :
        ///  - sur un nœud sélectionné s'il est assez proche (la grille rejoint le coin) ;
        ///  - sinon sur la route existante entre deux nœuds consécutifs (split de l'arête,
        ///    comme quand le joueur termine un tracé au milieu d'une route) ;
        ///  - sinon extrémité libre, reprojetée sur la hauteur du terrain.
        /// </summary>
        private CoursePos MakeCoursePos(float3 position, ref TerrainHeightData heightData, List<PerimeterEdge> perimeter)
        {
            CoursePos coursePos = default;
            coursePos.m_ParentMesh = -1;

            // 1) Raccord sur un nœud sélectionné.
            for (int i = 0; i < _selectedPositions.Count; i++)
            {
                if (math.distance(position.xz, _selectedPositions[i].xz) < NodeSnapDistance
                    && EntityManager.Exists(_selectedNodes[i]))
                {
                    coursePos.m_Entity = _selectedNodes[i];
                    coursePos.m_Position = _selectedPositions[i];
                    return coursePos;
                }
            }

            // 2) Raccord sur une route du périmètre (split de l'arête au point d'arrivée).
            foreach (PerimeterEdge edge in perimeter)
            {
                float distance = MathUtils.Distance(edge.m_Curve.xz, position.xz, out float t);
                if (distance >= EdgeSnapDistance)
                {
                    continue;
                }
                if (EntityManager.TryGetComponent(edge.m_Entity, out Edge edgeData))
                {
                    if (t <= 0.01f)
                    {
                        coursePos.m_Entity = edgeData.m_Start;
                        coursePos.m_SplitPosition = 0f;
                    }
                    else if (t >= 0.99f)
                    {
                        coursePos.m_Entity = edgeData.m_End;
                        coursePos.m_SplitPosition = 1f;
                    }
                    else
                    {
                        coursePos.m_Entity = edge.m_Entity;
                        coursePos.m_SplitPosition = t;
                    }
                }
                coursePos.m_Position = MathUtils.Position(edge.m_Curve, t);
                return coursePos;
            }

            // 3) Extrémité libre (ex. périmètre virtuel du mode 2 nœuds) : hauteur du terrain.
            position.y = TerrainUtils.SampleHeight(ref heightData, position);
            coursePos.m_Position = position;
            return coursePos;
        }

        /// <summary>
        /// Retrouve les routes existantes reliant les nœuds sélectionnés consécutifs
        /// (dans l'ordre de clic, en refermant le polygone), pour y raccorder la grille.
        /// </summary>
        private List<PerimeterEdge> BuildPerimeterEdges()
        {
            var result = new List<PerimeterEdge>();
            int count = _selectedNodes.Count;
            if (count < 2)
            {
                return result;
            }

            int pairCount = count == 2 ? 1 : count;
            for (int i = 0; i < pairCount; i++)
            {
                Entity nodeA = _selectedNodes[i];
                Entity nodeB = _selectedNodes[(i + 1) % count];
                if (!EntityManager.TryGetBuffer(nodeA, true, out DynamicBuffer<ConnectedEdge> connectedEdges))
                {
                    continue;
                }
                for (int j = 0; j < connectedEdges.Length; j++)
                {
                    Entity edgeEntity = connectedEdges[j].m_Edge;
                    if (!EntityManager.TryGetComponent(edgeEntity, out Edge edge))
                    {
                        continue;
                    }
                    bool connects = (edge.m_Start == nodeA && edge.m_End == nodeB)
                                 || (edge.m_Start == nodeB && edge.m_End == nodeA);
                    if (connects && EntityManager.TryGetComponent(edgeEntity, out Curve curve))
                    {
                        result.Add(new PerimeterEdge { m_Entity = edgeEntity, m_Curve = curve.m_Bezier });
                        break;
                    }
                }
            }
            return result;
        }
    }
}
