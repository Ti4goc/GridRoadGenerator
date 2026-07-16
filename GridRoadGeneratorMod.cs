using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using GridRoadGenerator.Localization;
using GridRoadGenerator.Settings;
using GridRoadGenerator.Systems;

namespace GridRoadGenerator
{
    public class Mod : IMod
    {
        public static Mod Instance { get; private set; }
        public static readonly ILog Log = LogManager.GetLogger("GridRoadGenerator").SetShowsErrorsInUI(true);

        public GridRoadGeneratorSettings Settings { get; private set; }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            Log.Info("GridRoadGenerator chargé.");

            Settings = new GridRoadGeneratorSettings(this);
            Settings.RegisterInOptionsUI();
            AssetDatabase.global.LoadSettings(nameof(GridRoadGenerator), Settings, new GridRoadGeneratorSettings(this));

            RegisterLocalizations();

            updateSystem.UpdateAt<GridRoadToolSystem>(SystemUpdatePhase.ToolUpdate);
        }

        /// <summary>
        /// Enregistre les 16 langues : 12 officielles + 4 communautaires (pt-PT, uk-UA, th-TH, vi-VN).
        /// Les langues communautaires ne sont enregistrées que si la locale existe dans le jeu,
        /// c'est-à-dire si le joueur a installé un mod de langue (ex. I18n EveryWhere).
        /// L'anglais sert de fallback natif pour toute locale absente.
        /// </summary>
        private void RegisterLocalizations()
        {
            var localizationManager = GameManager.instance.localizationManager;

            foreach (var pair in Translations.All)
            {
                string localeCode = pair.Key;

                if (localizationManager.SupportsLocale(localeCode))
                {
                    localizationManager.AddSource(localeCode,
                        new LocaleSource(Translations.Build(Settings, pair.Value)));
                    Log.Info($"Localisation enregistrée : {localeCode}");
                }
                else
                {
                    Log.Info($"Locale {localeCode} non disponible dans le jeu (mod de langue requis), ignorée.");
                }
            }
        }

        public void OnDispose()
        {
            Log.Info("GridRoadGenerator déchargé.");
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
