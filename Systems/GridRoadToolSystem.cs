using System.Collections.Generic;
using Colossal.Entities;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Outil personnalisé : le joueur clique sur des nœuds de route existants pour définir
    /// le rectangle (typiquement 4 coins), puis valide pour générer la grille interne.
    ///
    /// ATTENTION : les appels de création réseau (NetCourse / NetToolSystem) ci-dessous sont
    /// un squelette. Le nom exact des méthodes/composants pour instancier des segments de route
    /// via du code (plutôt que via l'input souris classique du NetTool) dépend de la version du
    /// jeu / du SDK. Vérifie contre la doc à jour ou le Discord des moddeurs CS2 avant de builder.
    /// Deux approches possibles :
    ///   A) Piloter NetToolSystem par code (poser des ControlPoint successifs et appeler sa
    ///      logique de placement, comme si le joueur cliquait).
    ///   B) Créer directement les entités Edge/NetCourse via l'EntityManager + un job de
    ///      finalisation réseau (plus bas niveau, plus fragile face aux mises à jour du jeu).
    /// Ce squelette utilise l'approche A, plus robuste dans le temps.
    /// </summary>
    public partial class GridRoadToolSystem : ToolBaseSystem
    {
        public override string toolID => "GridRoadGenerator.GridTool";

        private readonly List<Entity> _selectedNodes = new List<Entity>();
        private readonly List<float3> _selectedPositions = new List<float3>();

        private ProxyAction _selectNodeAction;
        private ProxyAction _confirmGridAction;
        private ProxyAction _cancelAction;

        private PrefabBase _roadPrefab; // prefab de route choisi (par défaut ou sélectionné par le joueur)
        private GridRoadGeneratorSettings _settings;

        protected override void OnCreate()
        {
            base.OnCreate();

            _settings = Mod.Instance.Settings;

            // Bindings clavier/souris. Les noms d'actions doivent être déclarés dans les Input Actions
            // du mod (voir GridRoadGeneratorMod.cs). Clic gauche = sélectionner un nœud sous le curseur,
            // Entrée = valider et générer, Échap = annuler la sélection en cours.
            _selectNodeAction = InputManager.instance.FindAction("GridRoadGenerator", "SelectNode");
            _confirmGridAction = InputManager.instance.FindAction("GridRoadGenerator", "ConfirmGrid");
            _cancelAction = InputManager.instance.FindAction("GridRoadGenerator", "Cancel");
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // On ne veut détecter que les nœuds de réseau routier existants.
            m_ToolRaycastSystem.typeMask = TypeMask.Net;
            m_ToolRaycastSystem.netLayerMask = Layer.Road;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements; // permet de cibler les nœuds précisément
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            _selectNodeAction.shouldBeEnabled = true;
            _confirmGridAction.shouldBeEnabled = true;
            _cancelAction.shouldBeEnabled = true;
        }

        protected override void OnStopRunning()
        {
            _selectNodeAction.shouldBeEnabled = false;
            _confirmGridAction.shouldBeEnabled = false;
            _cancelAction.shouldBeEnabled = false;
            base.OnStopRunning();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_cancelAction.WasPressedThisFrame())
            {
                _selectedNodes.Clear();
                _selectedPositions.Clear();
                return inputDeps;
            }

            if (_selectNodeAction.WasPressedThisFrame())
            {
                TrySelectNodeUnderCursor();
            }

            if (_confirmGridAction.WasPressedThisFrame() && _selectedNodes.Count >= 2)
            {
                GenerateAndPlaceGrid();
                _selectedNodes.Clear();
                _selectedPositions.Clear();
            }

            return inputDeps;
        }

        private void TrySelectNodeUnderCursor()
        {
            if (GetRaycastResult(out Entity hitEntity, out RaycastHit hit))
            {
                if (EntityManager.HasComponent<Node>(hitEntity))
                {
                    if (!_selectedNodes.Contains(hitEntity))
                    {
                        _selectedNodes.Add(hitEntity);
                        _selectedPositions.Add(hit.m_HitPosition);
                        // TODO: feedback visuel (surbrillance du nœud sélectionné),
                        // typiquement via un composant Highlighted ajouté à hitEntity.
                    }
                }
            }
        }

        private void GenerateAndPlaceGrid()
        {
            var parameters = _settings.ToGridParameters();
            List<RoadSegmentDef> segments = GridGenerator.GenerateGrid(_selectedPositions, parameters);

            foreach (var segment in segments)
            {
                PlaceRoadSegment(segment);
            }
        }

        /// <summary>
        /// Pose un segment de route entre Start et End en utilisant le prefab de route configuré.
        /// SQUELETTE : à adapter avec l'API réelle de création de NetCourse / appel à NetToolSystem
        /// en mode "programmatique" (sans passer par l'UI). Recherche "NetCourse", "CreateDefinition"
        /// et "NetToolSystem.GetAvailableSnapMask" dans le SDK pour la mécanique exacte de snapping
        /// aux nœuds déjà existants (pour bien raccorder la grille au périmètre sélectionné).
        /// </summary>
        private void PlaceRoadSegment(RoadSegmentDef segment)
        {
            // 1. Reprojeter Start/End sur la hauteur réelle du terrain à cette position (TerrainSystem).
            // 2. Construire un NetCourse (ou équivalent) avec le prefab _roadPrefab.
            // 3. Soumettre la définition au système de construction du jeu (comme le ferait NetToolSystem
            //    lors d'un clic-glisser manuel), pour bénéficier automatiquement du snapping, de la
            //    validation de collision, et de la génération correcte des intersections avec les
            //    routes du périmètre déjà sélectionnées.

            Debug.Log($"[GridRoadGenerator] TODO placement route: {segment.Start} -> {segment.End} " +
                      $"({(segment.IsHorizontal ? "horizontale" : "verticale")})");
        }
    }
}
