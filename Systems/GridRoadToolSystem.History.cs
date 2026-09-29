using System;
using System.Collections.Generic;
using System.Reflection;
using GridRoadGenerator.Core;
using GridRoadGenerator.Settings;
using Unity.Entities;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Annuler / refaire (Ctrl+Z, Ctrl+Y ou Ctrl+Maj+Z) dans l'outil : sélection de nœuds, points et
    /// contour de la zone libre, zone peinte et réglages du panneau, dans un seul historique. Un geste
    /// continu (trait de pinceau, point déplacé) compte pour un seul pas : l'état stable d'avant le
    /// geste n'est empilé qu'une fois celui-ci terminé.
    /// </summary>
    public partial class GridRoadToolSystem
    {
        private const int MaxHistory = 40;

        private sealed class Snapshot
        {
            public List<Entity> Nodes;
            public List<float3> Positions;
            public List<float3> FreePoints;
            public List<float3> FreeRing;
            public object Brush;
            public Dictionary<PropertyInfo, object> Settings;
        }

        private readonly List<Snapshot> _undo = new List<Snapshot>();
        private readonly List<Snapshot> _redo = new List<Snapshot>();
        private Snapshot _stable;
        private (int, float3, int, float3, int, int, int) _stableSignature;
        private int _settingsVersion;

        /// <summary>Vrai s'il y a quelque chose à annuler / refaire (boutons du panneau).</summary>
        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Demandes venues du panneau (boutons), traitées à la frame suivante.</summary>
        private bool _undoRequested, _redoRequested;
        public void RequestUndo() => _undoRequested = true;
        public void RequestRedo() => _redoRequested = true;

        private static PropertyInfo[] s_UndoableSettings;

        /// <summary>
        /// Réglages du panneau repris par l'historique : propriétés simples de GridRoadGeneratorSettings,
        /// sauf mode de sélection (qui efface la sélection), vues, réseaux choisis et options du menu.
        /// </summary>
        private static PropertyInfo[] UndoableSettings()
        {
            if (s_UndoableSettings != null) return s_UndoableSettings;
            var excluded = new HashSet<string>
            {
                nameof(GridRoadGeneratorSettings.FreeAreaMode), nameof(GridRoadGeneratorSettings.FreeAreaBrush),
                nameof(GridRoadGeneratorSettings.SelectedViews), nameof(GridRoadGeneratorSettings.AutoResolveCollisions),
                nameof(GridRoadGeneratorSettings.RoadPrefabName), nameof(GridRoadGeneratorSettings.SecondaryRoadPrefabName),
                nameof(GridRoadGeneratorSettings.AvenueRoadPrefabName), nameof(GridRoadGeneratorSettings.RoundaboutRoadPrefabName),
                nameof(GridRoadGeneratorSettings.PathPrefabName),
            };
            var list = new List<PropertyInfo>();
            foreach (PropertyInfo property in typeof(GridRoadGeneratorSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Type type = property.PropertyType;
                if (!property.CanRead || !property.CanWrite || excluded.Contains(property.Name)) continue;
                if (type.IsPrimitive || type.IsEnum || type == typeof(string))
                {
                    list.Add(property);
                }
            }
            return s_UndoableSettings = list.ToArray();
        }

        private Snapshot Capture()
        {
            var settings = new Dictionary<PropertyInfo, object>();
            foreach (PropertyInfo property in UndoableSettings())
            {
                settings[property] = property.GetValue(_settings);
            }
            return new Snapshot
            {
                Nodes = new List<Entity>(_selectedNodes),
                Positions = new List<float3>(_selectedPositions),
                FreePoints = new List<float3>(_freePoints),
                FreeRing = new List<float3>(_freeRing),
                Brush = _brush.Save(),
                Settings = settings,
            };
        }

        private (int, float3, int, float3, int, int, int) Signature()
        {
            float3 nodeSum = float3.zero, pointSum = float3.zero;
            foreach (float3 p in _selectedPositions) nodeSum += p;
            foreach (float3 p in _freePoints) pointSum += p;
            return (_selectedNodes.Count, nodeSum, _freePoints.Count, pointSum, _freeRing.Count, _brush.Version, _settingsVersion);
        }

        /// <summary>
        /// Appelé à chaque frame après la saisie : empile l'état stable précédent dès qu'un changement
        /// est terminé, puis traite Ctrl+Z / Ctrl+Y et les boutons du panneau.
        /// </summary>
        private void UpdateHistory()
        {
            UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
            bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;
            bool undo = _undoRequested || (ctrl && keyboard.zKey.wasPressedThisFrame && !keyboard.shiftKey.isPressed);
            bool redo = _redoRequested || (ctrl && (keyboard.yKey.wasPressedThisFrame || (keyboard.zKey.wasPressedThisFrame && keyboard.shiftKey.isPressed)));
            _undoRequested = _redoRequested = false;

            bool midGesture = _brushPainting || _dragPoint >= 0;
            if (_stable == null)
            {
                _stable = Capture();
                _stableSignature = Signature();
            }
            else if (!midGesture)
            {
                var signature = Signature();
                if (!signature.Equals(_stableSignature))
                {
                    Push(_undo, _stable);
                    _redo.Clear();
                    _stable = Capture();
                    _stableSignature = signature;
                }
            }

            if (midGesture) return;
            if (undo && _undo.Count > 0)
            {
                Push(_redo, _stable);
                Restore(Pop(_undo));
            }
            else if (redo && _redo.Count > 0)
            {
                Push(_undo, _stable);
                Restore(Pop(_redo));
            }
        }

        private static void Push(List<Snapshot> stack, Snapshot snapshot)
        {
            stack.Add(snapshot);
            if (stack.Count > MaxHistory) stack.RemoveAt(0);
        }

        private static Snapshot Pop(List<Snapshot> stack)
        {
            Snapshot last = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return last;
        }

        private void Restore(Snapshot snapshot)
        {
            foreach (Entity node in _selectedNodes) SetHighlight(node, false);
            _selectedNodes.Clear();
            _selectedPositions.Clear();
            for (int i = 0; i < snapshot.Nodes.Count; i++)
            {
                if (!EntityManager.Exists(snapshot.Nodes[i])) continue; // nœud démoli entre-temps
                _selectedNodes.Add(snapshot.Nodes[i]);
                _selectedPositions.Add(snapshot.Positions[i]);
                SetHighlight(snapshot.Nodes[i], true);
            }
            _freePoints.Clear();
            _freePoints.AddRange(snapshot.FreePoints);
            _freeRing.Clear();
            _freeRing.AddRange(snapshot.FreeRing);
            _brush.Restore(snapshot.Brush);
            foreach (KeyValuePair<PropertyInfo, object> pair in snapshot.Settings)
            {
                pair.Key.SetValue(_settings, pair.Value);
            }
            FreeAreaInvalid = false;
            _previewDirty = true;
            _stable = snapshot;
            _stableSignature = Signature();
        }

        /// <summary>Vide l'historique (outil refermé, grille construite).</summary>
        private void ClearHistory()
        {
            _undo.Clear();
            _redo.Clear();
            _stable = null;
        }
    }
}
