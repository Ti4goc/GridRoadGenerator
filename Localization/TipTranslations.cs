using System.Collections.Generic;

namespace GridRoadGenerator.Localization
{
    /// <summary>
    /// Descriptions des infobulles du panneau (clés "GridRoadGenerator.UI.Tip.&lt;clé&gt;", lues par
    /// UI/src/tips.tsx). Une ligne par clé, dans l'ordre de <see cref="Keys"/> ; l'anglais est aussi
    /// le texte de repli côté UI (TIP_TEXT) quand une langue n'a pas la clé.
    /// Réparties par familles de langues dans les fichiers TipTranslations.*.cs.
    /// </summary>
    public static partial class TipTranslations
    {
        public const string KeyPrefix = "GridRoadGenerator.UI.Tip.";

        /// <summary>Clés, dans l'ordre des tableaux de chaque langue.</summary>
        public static readonly string[] Keys =
        {
            "PatternGrid", "PatternLoop", "PatternSuperblock", "PatternConcentric", "PatternRadial",
            "ResetDefaults", "Anarchy", "ModeFit", "ModeFixed", "Columns", "Rows", "Spacing",
            "SuperblockZone", "ConcentricLayers", "ConcentricConnections", "RadialLayers", "RadialAvenues",
            "RadialRoundabout", "CollectorSpacing", "LoopCulDeSacRatio", "Angle", "FollowTerrain",
            "CulDeSacMode", "CulDeSacDepth", "CulDeSacAxis", "CulDeSacRatio", "Staggered",
            "CulDeSacCapSize", "CulDeSacCapStyle", "AvenueColumn", "AvenueColumnIndex", "AvenueRow",
            "AvenueRowIndex", "NetworkTabs", "NetworkPrefab", "UpgradeMiddleTrees", "UpgradeMiddleGrass",
            "UpgradeSideTrees", "UpgradeSideGrass", "UpgradeWideSidewalk", "UpgradeBikeLane",
            "ViewUnderground", "ViewZoneGrid", "ViewInvisibleNetworks", "NodesSelected", "Generate",
        };

        // Construit à la première lecture (pas d'initialiseur de champ statique lourd : même
        // précaution que _registeredLocales dans GridRoadGeneratorMod).
        private static Dictionary<string, string[]> s_All;

        /// <summary>Textes par code de langue (même codes que Translations.All).</summary>
        public static Dictionary<string, string[]> All
        {
            get
            {
                if (s_All == null)
                {
                    var all = new Dictionary<string, string[]>();
                    AddWestern(all);
                    AddIberianAndRegional(all);
                    AddNorthernAndCentral(all);
                    AddEasternAndAsian(all);
                    s_All = all;
                }
                return s_All;
            }
        }

        /// <summary>
        /// Ajoute les descriptions de `locale` à `entries` (source de localisation de cette langue).
        /// Langue absente, ou tableau de mauvaise longueur : rien n'est ajouté, l'UI retombe sur
        /// l'anglais.
        /// </summary>
        public static void AddTo(Dictionary<string, string> entries, string locale)
        {
            if (!All.TryGetValue(locale, out string[] texts) || texts.Length != Keys.Length)
            {
                return;
            }
            for (int i = 0; i < Keys.Length; i++)
            {
                entries[KeyPrefix + Keys[i]] = texts[i];
            }
        }
    }
}
