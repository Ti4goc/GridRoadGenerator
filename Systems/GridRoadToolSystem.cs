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
    /// Outil personnalisé : le joueur clique sur des nœuds de route existants pour définir
    /// le périmètre (polygone quelconque, dans l'ordre de clic), puis valide (Entrée) pour
    /// générer la grille interne.
    ///
    /// Placement : l'outil pilote le pipeline natif de construction réseau, exactement comme
    /// NetToolSystem. À chaque changement de sélection il (re)crée des entités de définition
    /// (CreationDefinition + NetCourse + Updated) via le ToolOutputBarrier ; le jeu en dérive
    /// des entités Temp qui servent d'aperçu fantôme (avec validation de collision native,
    /// gestion des croisements par CourseSplitSystem, etc.). À la validation, applyMode =
    /// ApplyMode.Apply concrétise les Temp de la frame précédente — même mécanique qu'un
    /// clic-glisser du joueur avec l'outil route.
    /// </summary>
    public partial class GridRoadToolSystem : ToolBaseSystem
    {
        /// <summary>Distance (m) sous laquelle une extrémité de segment est raccordée à un nœud sélectionné.</summary>
        private const float NodeSnapDistance = 4f;
        /// <summary>Distance (m) sous laquelle une extrémité de segment est raccordée à une route du périmètre.</summary>
        private const float EdgeSnapDistance = 4f;

        public override string toolID => "Grid Road Tool";

        private readonly List<Entity> _selectedNodes = new List<Entity>();
        private readonly List<float3> _selectedPositions = new List<float3>();
        private bool _selectionDirty;

        private ProxyAction _confirmAction;
        private GridRoadGeneratorSettings _settings;

        private NetToolSystem m_NetToolSystem;
        private TerrainSystem m_TerrainSystem;
        private ToolOutputBarrier m_ToolOutputBarrier;
        private EntityQuery m_DefinitionQuery;

        private PrefabBase _fallbackPrefab;
        private bool _fallbackSearched;

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
            // Action déclarée par la propriété ProxyBinding correspondante des settings
            // (enregistrée par Settings.RegisterKeyBindings() dans Mod.OnLoad).
            _confirmAction = _settings.GetAction(GridRoadGeneratorSettings.ActionConfirmGrid);
        }

        /// <summary>Le prefab affiché par l'UI pour cet outil : celui qui sera réellement posé.</summary>
        public override PrefabBase GetPrefab() => GetRoadPrefab();

        /// <summary>
        /// L'outil ne s'active pas via la sélection d'un prefab dans la barre d'outils
        /// (uniquement via son raccourci) : toujours false pour ne pas intercepter le NetTool.
        /// </summary>
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // On ne veut détecter que les réseaux routiers existants (nœuds inclus via SubElements).
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements;
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            ClearSelection();
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
            ClearSelection();
            if (_confirmAction != null)
            {
                _confirmAction.shouldBeEnabled = false;
            }
            // Purge les définitions restantes pour ne pas laisser d'aperçu fantôme derrière soi.
            Dependency = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, Dependency);
            base.OnStopRunning();
        }

        /// <summary>Active/désactive l'outil (appelé par le raccourci clavier global du mod).</summary>
        public void ToggleTool()
        {
            if (m_ToolSystem.activeTool == this)
            {
                m_ToolSystem.activeTool = m_DefaultToolSystem;
            }
            else
            {
                m_ToolSystem.activeTool = this;
            }
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            applyMode = ApplyMode.None;

            try
            {
                // Échap : annule la sélection en cours et efface l'aperçu.
                if (cancelAction.WasPressedThisFrame())
                {
                    ClearSelection();
                    applyMode = ApplyMode.Clear;
                    return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                }

                // Entrée : concrétise l'aperçu fantôme de la frame précédente.
                // (!_selectionDirty : l'aperçu doit être à jour avec la sélection.)
                if (!_selectionDirty && _confirmAction != null && _confirmAction.WasPressedThisFrame()
                    && _selectedPositions.Count >= 2)
                {
                    if (GetAllowApply() && !m_DefinitionQuery.IsEmptyIgnoreFilter)
                    {
                        applyMode = ApplyMode.Apply;
                        ClearSelection();
                        return DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                    }
                    Mod.Log.Warn("Grille refusée : l'aperçu contient des erreurs de placement (collisions, pente...). Ajuste le périmètre ou l'espacement.");
                    return inputDeps;
                }

                // Clic gauche : sélectionne/désélectionne le nœud sous le curseur.
                if (applyAction.WasPressedThisFrame())
                {
                    TryToggleNodeUnderCursor();
                }

                // Clic droit : retire le dernier nœud sélectionné.
                if (secondaryApplyAction.WasPressedThisFrame() && _selectedNodes.Count > 0)
                {
                    SetHighlight(_selectedNodes[_selectedNodes.Count - 1], false);
                    _selectedNodes.RemoveAt(_selectedNodes.Count - 1);
                    _selectedPositions.RemoveAt(_selectedPositions.Count - 1);
                    _selectionDirty = true;
                }

                // Sélection modifiée : on reconstruit l'aperçu (définitions -> Temp fantômes).
                if (_selectionDirty)
                {
                    _selectionDirty = false;
                    applyMode = ApplyMode.Clear;
                    inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
                    if (_selectedPositions.Count >= 2)
                    {
                        CreateGridDefinitions();
                    }
                }
            }
            catch (Exception e)
            {
                // Jamais de crash du jeu : on log, on nettoie, et l'outil reste utilisable.
                Mod.Log.Error(e, "Erreur dans GridRoadToolSystem, sélection annulée.");
                ClearSelection();
                applyMode = ApplyMode.Clear;
            }

            return inputDeps;
        }

        // ------------------------------------------------------------------
        // Sélection des nœuds
        // ------------------------------------------------------------------

        private void TryToggleNodeUnderCursor()
        {
            if (!GetRaycastResult(out Entity entity, out RaycastHit hit))
            {
                return;
            }

            Entity node = Entity.Null;
            if (EntityManager.HasComponent<Game.Net.Node>(entity))
            {
                node = entity;
            }
            else if (hit.m_HitEntity != entity && EntityManager.HasComponent<Game.Net.Node>(hit.m_HitEntity))
            {
                node = hit.m_HitEntity;
            }
            else if (EntityManager.TryGetComponent(entity, out Edge edge))
            {
                // Clic sur une arête : on prend le nœud d'extrémité le plus proche du curseur.
                if (EntityManager.TryGetComponent(edge.m_Start, out Game.Net.Node startNode)
                    && EntityManager.TryGetComponent(edge.m_End, out Game.Net.Node endNode))
                {
                    node = math.distancesq(hit.m_HitPosition.xz, startNode.m_Position.xz)
                        <= math.distancesq(hit.m_HitPosition.xz, endNode.m_Position.xz)
                        ? edge.m_Start : edge.m_End;
                }
            }

            if (node == Entity.Null || !EntityManager.TryGetComponent(node, out Game.Net.Node nodeData))
            {
                return;
            }

            int index = _selectedNodes.IndexOf(node);
            if (index >= 0)
            {
                // Re-clic sur un nœud déjà sélectionné : désélection.
                SetHighlight(node, false);
                _selectedNodes.RemoveAt(index);
                _selectedPositions.RemoveAt(index);
            }
            else
            {
                _selectedNodes.Add(node);
                _selectedPositions.Add(nodeData.m_Position);
                SetHighlight(node, true);
            }
            _selectionDirty = true;
        }

        private void ClearSelection()
        {
            foreach (Entity node in _selectedNodes)
            {
                SetHighlight(node, false);
            }
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            _selectionDirty = false;
        }

        private void SetHighlight(Entity entity, bool highlighted)
        {
            if (!EntityManager.Exists(entity))
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

        /// <summary>
        /// Prefab utilisé pour la grille : celui actuellement sélectionné dans l'outil route
        /// natif du joueur s'il s'agit d'une route, sinon la petite route deux voies par défaut.
        /// </summary>
        private PrefabBase GetRoadPrefab()
        {
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

        // ------------------------------------------------------------------
        // Création des définitions réseau (aperçu fantôme + pose réelle)
        // ------------------------------------------------------------------

        /// <summary>
        /// Génère la grille (logique pure de Core) et crée pour chaque segment une entité de
        /// définition CreationDefinition + NetCourse, comme NetToolSystem le fait lors d'un
        /// tracé manuel. Le jeu transforme ces définitions en entités Temp (aperçu), découpe
        /// les croisements (CourseSplitSystem) et crée les intersections avec le périmètre.
        /// </summary>
        private void CreateGridDefinitions()
        {
            PrefabBase roadPrefab = GetRoadPrefab();
            if (roadPrefab == null)
            {
                Mod.Log.Warn("Aucun prefab de route disponible : grille non générée.");
                return;
            }

            List<RoadSegmentDef> segments;
            try
            {
                segments = GridGenerator.GenerateGrid(_selectedPositions, _settings.ToGridParameters());
            }
            catch (Exception e)
            {
                Mod.Log.Warn($"Génération de grille impossible : {e.Message}");
                return;
            }
            if (segments.Count == 0)
            {
                return;
            }

            Entity prefabEntity = m_PrefabSystem.GetEntity(roadPrefab);
            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            List<PerimeterEdge> perimeter = BuildPerimeterEdges();
            EntityCommandBuffer commandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            Unity.Mathematics.Random random = RandomSeed.Next().GetRandom(0);

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
            }
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
                    // Extrémité d'arête : on raccorde directement au nœud correspondant.
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
