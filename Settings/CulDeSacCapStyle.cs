namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Style visuel du cercle de retournement posé en bout de chaque impasse
    /// (CulDeSacMode). Combiné à CulDeSacCapSize (voir ce fichier) pour désigner
    /// le prefab exact (voir GridRoadToolSystem.TryResolveCulDeSacCapPrefab).
    /// </summary>
    public enum CulDeSacCapStyle
    {
        /// <summary>Variante entièrement asphaltée (prefabs "...01"). Choix par défaut.</summary>
        Asphalt = 0,
        /// <summary>Variante avec îlot central engazonné (prefabs "...02").</summary>
        Grass = 1,
        /// <summary>Variante avec îlot planté d'arbres/arbustes (prefabs "...03").</summary>
        Trees = 2
    }
}
