namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Taille du cercle de retournement posé en bout de chaque impasse (CulDeSacMode).
    /// Combinée au style (CulDeSacCapStyle) pour désigner le prefab exact
    /// "CulDeSac&lt;Taille&gt;&lt;Style&gt;" (ex. "CulDeSacSmall01").
    /// </summary>
    public enum CulDeSacCapSize
    {
        /// <summary>Déduite automatiquement de la largeur du réseau utilisé (comportement historique).</summary>
        Auto = 0,
        Small = 1,
        Medium = 2,
        Large = 3,
        /// <summary>Sans variante en style Asphalt : repli automatique sur Large (voir TryResolveCulDeSacCapPrefab).</summary>
        XL = 4
    }
}
