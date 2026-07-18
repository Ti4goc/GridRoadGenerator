namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Style visuel du cercle de retournement posé en bout de chaque impasse
    /// (CulDeSacMode). La taille, elle, n'est jamais choisie ici : elle est
    /// déduite automatiquement de la largeur du réseau utilisé (voir
    /// GridRoadToolSystem.TryResolveCulDeSacCapPrefab).
    /// </summary>
    public enum CulDeSacCapStyle
    {
        /// <summary>Variante entièrement asphaltée (prefabs "...01").</summary>
        Asphalt = 0,
        /// <summary>Variante avec îlot central engazonné (prefabs "...02"). Choix par défaut.</summary>
        Grass = 1,
        /// <summary>Variante avec îlot planté d'arbres/arbustes (prefabs "...03").</summary>
        Trees = 2
    }
}
