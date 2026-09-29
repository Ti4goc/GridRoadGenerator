using System.Collections.Generic;
using Colossal.Entities;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Game.Zones;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Zonage automatique (retour : "pintar o zoneamento ao longo das ruas geradas") : après Générer,
    /// la zone choisie dans le panneau est posée le long des nouvelles routes par la même mécanique
    /// que l'outil de zonage du jeu (voir UpdateZoning) — jamais sur une case déjà zonée.
    /// </summary>
    public partial class GridRoadToolSystem
    {
        /// <summary>Frames entre la pose des routes et l'application du zonage (les blocs apparaissent entre-temps).</summary>
        private const int ZoningFrames = 30;
        /// <summary>Dernières frames : définitions Zoning créées, puis appliquées à la toute dernière.</summary>
        private const int ZoningDefinitionFrames = 8;
        /// <summary>Profondeur (m) zonée de part et d'autre de la route (blocs de 6 cases de 8 m + demi-chaussée).</summary>
        private const float ZoningDepth = 60f;
        /// <summary>Longueur (m) des bandes Marquee le long d'une route courbe.</summary>
        private const float ZoningPieceLength = 24f;

        private EntityQuery m_ZonePrefabQuery;
        private List<Bezier4x3> _zoningCurves;
        private Entity _zoningPrefab;
        private int _zoningFramesLeft;

        private List<(string name, string icon, string color)> _zoneOptions;
        private float _zoneOptionsTime = float.NegativeInfinity;

        /// <summary>
        /// Zones proposées dans le panneau : (nom du prefab, icône), toutes (mods compris — retour
        /// utilisateur : filtrer sur le groupe du menu retirait les zones de mods), dans l'ordre du
        /// menu du jeu (groupe puis priorité), les zones sans groupe à la fin. Retour utilisateur :
        /// les zones ajoutées par des mods s'affichaient en cases vides alors que le jeu montre leur
        /// image — l'icône (ImageSystem.GetThumbnail, comme la barre d'outils du jeu) n'était lue
        /// qu'une fois, avant que l'image du mod soit prête. Relue toutes les 2 s.
        /// </summary>
        public List<(string name, string icon, string color)> ZoneOptions()
        {
            float now = UnityEngine.Time.realtimeSinceStartup;
            if (_zoneOptions != null && _zoneOptions.Count > 0 && now - _zoneOptionsTime < 2f) return _zoneOptions;
            _zoneOptionsTime = now;
            var sortable = new List<(int group, int item, string name, string icon, string color)>();
            if (m_ZonePrefabQuery == default)
            {
                m_ZonePrefabQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneData>(), ComponentType.ReadOnly<PrefabData>());
            }
            using (NativeArray<Entity> entities = m_ZonePrefabQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in entities)
                {
                    if (!EntityManager.TryGetComponent(entity, out ZoneData data) || data.m_AreaType == AreaType.None) continue;
                    if (!m_PrefabSystem.TryGetPrefab(entity, out PrefabBase prefab) || prefab == null) continue;
                    // Zones de mods sans groupe du menu (UIObjectData) : gardées, en fin de liste.
                    int group = int.MaxValue, item = int.MaxValue;
                    if (EntityManager.TryGetComponent(entity, out UIObjectData ui))
                    {
                        item = ui.m_Priority;
                        if (EntityManager.TryGetComponent(ui.m_Group, out UIObjectData groupData)) group = groupData.m_Priority;
                    }
                    // Couleur de la zone (celle de la grille de zonage) : socle de secours dans le panneau
                    // quand l'image ne se charge pas (voir zoningRow.tsx).
                    UnityEngine.Color color = prefab is ZonePrefab zonePrefab ? zonePrefab.m_Color : UnityEngine.Color.gray;
                    sortable.Add((group, item, prefab.name, Game.UI.ImageSystem.GetThumbnail(prefab) ?? string.Empty,
                        "#" + UnityEngine.ColorUtility.ToHtmlStringRGB(color)));
                }
            }
            sortable.Sort((a, b) => a.group != b.group ? a.group.CompareTo(b.group)
                : a.item != b.item ? a.item.CompareTo(b.item) : string.CompareOrdinal(a.name, b.name));
            var result = new List<(string, string, string)>(sortable.Count);
            foreach (var zone in sortable) result.Add((zone.name, zone.icon, zone.color));
            _zoneOptions = result;

            return result;
        }

        /// <summary>Entité du prefab de zone choisi (nom), ou faux si aucun / introuvable.</summary>
        private bool TryResolveZonePrefab(string name, out Entity zonePrefab)
        {
            zonePrefab = Entity.Null;
            if (string.IsNullOrEmpty(name)) return false;
            if (m_ZonePrefabQuery == default)
            {
                m_ZonePrefabQuery = GetEntityQuery(ComponentType.ReadOnly<ZoneData>(), ComponentType.ReadOnly<PrefabData>());
            }
            using (NativeArray<Entity> entities = m_ZonePrefabQuery.ToEntityArray(Allocator.Temp))
            {
                foreach (Entity entity in entities)
                {
                    if (m_PrefabSystem.TryGetPrefab(entity, out PrefabBase prefab) && prefab != null && prefab.name == name)
                    {
                        zonePrefab = entity;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Au moment d'appliquer : mémorise les routes posées et démarre le zonage si une zone est choisie.</summary>
        private void StartZoning()
        {
            if (!TryResolveZonePrefab(_settings.ZoningPrefabName, out _zoningPrefab) || _lastCreatedCurves.Count == 0)
            {
                return;
            }
            _zoningCurves = new List<Bezier4x3>(_lastCreatedCurves.Count);
            foreach ((var _, Bezier4x3 curve) in _lastCreatedCurves)
            {
                _zoningCurves.Add(curve);
            }
            _zoningFramesLeft = ZoningFrames;
        }

        /// <summary>
        /// Zonage par la mécanique de l'outil de zonage du jeu (retour utilisateur : écrire directement
        /// les cases des blocs "parece não funcionar" — le jeu ne le reprenait pas) : des définitions
        /// Zoning (bande "Marquee" de part et d'autre de chaque route posée, sans Overwrite : les
        /// cases déjà zonées restent telles quelles) sont créées pendant quelques frames, le temps que
        /// le jeu prépare ses blocs temporaires, puis appliquées comme par l'outil de zonage.
        /// Vrai si cette frame est consacrée au zonage (le reste de l'outil attend).
        /// </summary>
        private bool UpdateZoning(ref JobHandle inputDeps)
        {
            if (_zoningFramesLeft <= 0 || _zoningCurves == null)
            {
                return false;
            }
            _zoningFramesLeft--;
            if (_zoningFramesLeft > ZoningDefinitionFrames)
            {
                return false; // les routes et leurs blocs de zonage n'existent pas encore
            }
            inputDeps = DestroyDefinitions(m_DefinitionQuery, m_ToolOutputBarrier, inputDeps);
            if (_zoningFramesLeft == 0)
            {
                // Définitions de la frame précédente devenues blocs temporaires : on applique.
                applyMode = ApplyMode.Apply;
                _zoningCurves = null;
                Mod.Log.Info("Zonage automatique appliqué le long des nouvelles routes.");
                return true;
            }
            EntityCommandBuffer commandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            foreach (Bezier4x3 curve in _zoningCurves)
            {
                float length = MathUtils.Length(curve.xz);
                int pieces = math.max(1, (int)math.ceil(length / ZoningPieceLength));
                for (int k = 0; k < pieces; k++)
                {
                    float3 a = MathUtils.Position(curve, (float)k / pieces);
                    float3 b = MathUtils.Position(curve, (float)(k + 1) / pieces);
                    float2 along = math.normalizesafe(b.xz - a.xz);
                    float3 side = new float3(-along.y, 0f, along.x) * ZoningDepth;
                    Entity definition = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent(definition, new CreationDefinition { m_Prefab = _zoningPrefab });
                    commandBuffer.AddComponent(definition, new Zoning
                    {
                        m_Position = new Quad3(a - side, b - side, b + side, a + side),
                        m_Flags = ZoningFlags.Zone | ZoningFlags.Marquee,
                    });
                    commandBuffer.AddComponent(definition, default(Updated));
                }
            }
            applyMode = ApplyMode.Clear;
            return true;
        }
    }
}
