namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Comment l'angle de la grille (AngleOffsetDegrees) est déterminé. Chantier
    /// exploratoire : FollowTerrain calcule UN seul angle pour tout le périmètre
    /// sélectionné (pas une orientation continue par bloc — voir
    /// GridRoadToolSystem.ComputeTerrainFollowAngle pour le détail et les raisons
    /// de cette simplification volontaire).
    /// </summary>
    public enum OrientationMode
    {
        /// <summary>Angle manuel (AngleOffsetDegrees), comportement historique. Choix par défaut.</summary>
        FixedAngle = 0,
        /// <summary>Angle recalculé à chaque génération pour suivre les courbes de niveau du terrain.</summary>
        FollowTerrain = 1
    }
}
