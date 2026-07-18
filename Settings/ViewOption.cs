namespace GridRoadGenerator.Settings
{
    /// <summary>
    /// Vue (affichage) pendant que l'outil est actif : réseaux souterrains, grille de
    /// zonage, réseaux normalement invisibles (chemins piétons implicites, etc.).
    /// Bitmask combinable, pattern repris de CS2-NetworkTools (c) Luca Rager,
    /// licence MIT — https://github.com/lucarager/CS2-NetworkTools
    /// </summary>
    [System.Flags]
    public enum ViewOption
    {
        None = 0,
        Underground = 1 << 0,
        ZoneGrid = 1 << 1,
        InvisibleNetworks = 1 << 2,
        All = Underground | ZoneGrid | InvisibleNetworks
    }
}
