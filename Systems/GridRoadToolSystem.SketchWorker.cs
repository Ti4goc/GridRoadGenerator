using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using GridRoadGenerator.Core;
using Unity.Mathematics;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Grilles calculées sur un thread de fond (retour utilisateur, mesures en jeu : Misto 300-370 ms,
    /// Orgânico 55-165 ms figés dans une seule image au relâchement d'un curseur ou au changement de
    /// motif). Un seul calcul à la fois ; la dernière demande attend son tour ; les derniers résultats
    /// sont gardés (celui du drag sert tel quel au relâchement). Tant qu'une grille n'est pas prête, le
    /// croquis précédent reste affiché et Générer attend.
    /// </summary>
    public partial class GridRoadToolSystem
    {
        private const int SketchResultCacheSize = 8;
        // road : route de périmètre d'une zone libre (calculée en fond elle aussi, 20-50 ms mesurés en jeu), sinon null.
        private readonly List<(object key, List<RoadSegmentDef> segments, RoundaboutInfo roundabout, List<RoadSegmentDef> road)> _sketchResults =
            new List<(object, List<RoadSegmentDef>, RoundaboutInfo, List<RoadSegmentDef>)>();
        private Task<(List<RoadSegmentDef> segments, RoundaboutInfo roundabout, List<RoadSegmentDef> road, double ms, string error)> _sketchJob;
        private object _sketchJobKey;
        private (object key, List<float3> positions, GridParameters parameters, bool loop, bool road)? _sketchWanted;

        /// <summary>Vrai si la grille des réglages actuels est encore en calcul (croquis précédent affiché).</summary>
        public bool SketchPending { get; private set; }

        /// <summary>Clé d'une grille : périmètre (nombre de points, somme), motif Loop ou non, réglages (sans le relief).</summary>
        public static object SketchKey(List<float3> positions, GridParameters parameters, bool loop)
        {
            GridParameters keyParameters = parameters;
            keyParameters.HeightAt = null;
            float3 sum = float3.zero;
            foreach (float3 p in positions) sum += p;
            return (positions.Count, sum, loop, keyParameters);
        }

        /// <summary>Grille déjà calculée pour cette clé.</summary>
        public bool TryReadySketch(object key, out List<RoadSegmentDef> segments, out RoundaboutInfo roundabout)
            => TryReadySketch(key, out segments, out roundabout, out _);

        /// <summary>Grille déjà calculée pour cette clé, avec sa route de périmètre (zone libre) ou null.</summary>
        public bool TryReadySketch(object key, out List<RoadSegmentDef> segments, out RoundaboutInfo roundabout, out List<RoadSegmentDef> road)
        {
            for (int i = 0; i < _sketchResults.Count; i++)
            {
                if (_sketchResults[i].key.Equals(key))
                {
                    var entry = _sketchResults[i];
                    _sketchResults.RemoveAt(i);
                    _sketchResults.Insert(0, entry);
                    segments = entry.segments;
                    roundabout = entry.roundabout;
                    road = entry.road;
                    return true;
                }
            }
            segments = null;
            roundabout = default;
            road = null;
            return false;
        }

        /// <summary>Demande la grille de cette clé (rien si elle est prête ou déjà en calcul).</summary>
        public void RequestSketch(object key, List<float3> positions, GridParameters parameters, bool loop)
        {
            if (TryReadySketch(key, out _, out _) || (_sketchJob != null && key.Equals(_sketchJobKey))) return;
            _sketchWanted = (key, new List<float3>(positions), parameters, loop, _settings.FreeAreaMode && positions.Count >= 3);
            PumpSketchWorker();
        }

        /// <summary>Récupère un calcul terminé et lance la demande en attente. Appelé à chaque image.</summary>
        private void PumpSketchWorker()
        {
            if (_sketchJob != null && _sketchJob.IsCompleted)
            {
                (List<RoadSegmentDef> segments, RoundaboutInfo roundabout, List<RoadSegmentDef> road, double ms, string error) result = _sketchJob.Status == TaskStatus.RanToCompletion
                    ? _sketchJob.Result
                    : (new List<RoadSegmentDef>(), default(RoundaboutInfo), null, 0d, _sketchJob.Exception?.GetBaseException().Message);
                if (result.error != null)
                {
                    Mod.Log.Warn($"Grille impossible pour ce périmètre et ces réglages : {result.error}");
                }
                _sketchResults.Insert(0, (_sketchJobKey, result.segments, result.roundabout, result.road));
                if (_sketchResults.Count > SketchResultCacheSize) _sketchResults.RemoveAt(_sketchResults.Count - 1);
                _sketchJob = null;
                _sketchJobKey = null;
            }
            if (_sketchJob == null && _sketchWanted.HasValue)
            {
                var wanted = _sketchWanted.Value;
                _sketchWanted = null;
                if (TryReadySketch(wanted.key, out _, out _)) return;
                GridParameters parameters = wanted.parameters;
                parameters.HeightAt = HeightSnapshot(wanted.positions);
                List<float3> positions = wanted.positions;
                bool loop = wanted.loop, withRoad = wanted.road;
                _sketchJobKey = wanted.key;
                _sketchJob = Task.Run(() =>
                {
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        RoundaboutInfo roundabout = default;
                        List<RoadSegmentDef> segments = loop
                            ? GridGenerator.GenerateLoopGrid(positions, parameters)
                            : GridGenerator.GenerateGrid(positions, parameters, out _, out roundabout);
                        List<RoadSegmentDef> road = withRoad && segments.Count > 0 ? FreeAreaPerimeter.PerimeterRoad(positions, segments) : null;
                        return (segments, roundabout, road, sw.Elapsed.TotalMilliseconds, (string)null);
                    }
                    catch (Exception e)
                    {
                        return (new List<RoadSegmentDef>(), default(RoundaboutInfo), (List<RoadSegmentDef>)null, sw.Elapsed.TotalMilliseconds, e.Message);
                    }
                });
            }
        }
    }
}
