using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using GridRoadGenerator.Localization;
using GridRoadGenerator.Settings;
using GridRoadGenerator.Systems;
using UnityEngine.InputSystem;

namespace GridRoadGenerator
{
    public class Mod : IMod
    {
        public static Mod Instance { get; private set; }
        public static readonly ILog Log = LogManager.GetLogger("GridRoadGenerator").SetShowsErrorsInUI(true);

        public GridRoadGeneratorSettings Settings { get; private set; }

        /// <summary>Version affichée dans la section "Sobre" (About) des Options > Mods.</summary>
        public string Version => GetType().Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        private GridRoadToolSystem _toolSystem;
        private ProxyAction _toggleToolAction;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            Log.Info("GridRoadGenerator chargé.");

            Settings = new GridRoadGeneratorSettings(this);
            Settings.RegisterInOptionsUI();
            AssetDatabase.global.LoadSettings(nameof(GridRoadGenerator), Settings, new GridRoadGeneratorSettings(this));
            // Enregistre les actions d'input déclarées par les propriétés ProxyBinding des
            // settings (ToggleTool, ConfirmGrid) — à faire avant tout GetAction().
            Settings.RegisterKeyBindings();

            RegisterLocalizations();

            updateSystem.UpdateAt<GridRoadToolSystem>(SystemUpdatePhase.ToolUpdate);
            updateSystem.UpdateAt<GridRoadUISystem>(SystemUpdatePhase.UIUpdate);
            updateSystem.UpdateAt<GridRoadTooltipSystem>(SystemUpdatePhase.UITooltip);
            updateSystem.UpdateAt<GridRoadOverlaySystem>(SystemUpdatePhase.Rendering);
            _toolSystem = updateSystem.World.GetOrCreateSystemManaged<GridRoadToolSystem>();

            // Raccourci global (Ctrl+G par défaut) : active/désactive l'outil.
            _toggleToolAction = Settings.GetAction(GridRoadGeneratorSettings.ActionToggleTool);
            _toggleToolAction.shouldBeEnabled = true;
            _toggleToolAction.onInteraction += OnToggleToolAction;
        }

        private void OnToggleToolAction(ProxyAction action, InputActionPhase phase)
        {
            if (phase != InputActionPhase.Performed)
            {
                return;
            }
            try
            {
                // Uniquement en partie (pas dans le menu principal / l'éditeur).
                if ((GameManager.instance.gameMode & GameMode.Game) != 0)
                {
                    _toolSystem?.ToggleTool();
                }
            }
            catch (System.Exception e)
            {
                Log.Error(e, "Impossible d'activer l'outil de grille.");
            }
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
            if (_toggleToolAction != null)
            {
                _toggleToolAction.onInteraction -= OnToggleToolAction;
                _toggleToolAction.shouldBeEnabled = false;
                _toggleToolAction = null;
            }
            if (Settings != null)
            {
                Settings.UnregisterInOptionsUI();
                Settings = null;
            }
        }
    }
}
