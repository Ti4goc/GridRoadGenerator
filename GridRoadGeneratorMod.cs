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

        /// <summary>Langues déjà enregistrées (voir RegisterLocalizations, appelé plusieurs fois).</summary>
        // Créé dans RegisterLocalizations et non par un initialiseur de champ : le jeu instancie
        // le mod sans exécuter les initialiseurs (vécu : NullReferenceException ici au
        // chargement, mod déchargé aussitôt, panneau qui n'ouvre plus).
        private System.Collections.Generic.HashSet<string> _registeredLocales;
        private bool _localeListenerAttached;

        /// <summary>
        /// Enregistre les langues officielles du jeu, plus les langues communautaires dès qu'un mod
        /// de langue (I18N Everywhere, packs de langue) les ajoute au jeu. L'anglais sert de
        /// fallback natif pour toute locale absente.
        ///
        /// Ces mods ajoutent leurs locales APRÈS le chargement de ce mod (log : "pt-PT non
        /// disponible" alors que le pack était installé) ; or AddSource sur une locale encore
        /// inconnue ne fait rien, et AddLocale ne rattrape pas les sources ajoutées avant. D'où
        /// l'écoute de onSupportedLocalesChanged : chaque nouvelle locale est enregistrée dès
        /// qu'elle apparaît.
        /// </summary>
        private void RegisterLocalizations()
        {
            var localizationManager = GameManager.instance.localizationManager;
            if (_registeredLocales == null)
            {
                _registeredLocales = new System.Collections.Generic.HashSet<string>();
            }
            if (!_localeListenerAttached)
            {
                localizationManager.onSupportedLocalesChanged += RegisterLocalizations;
                _localeListenerAttached = true;
            }

            foreach (var pair in Translations.All)
            {
                string localeCode = pair.Key;
                if (_registeredLocales.Contains(localeCode) || !localizationManager.SupportsLocale(localeCode))
                {
                    continue;
                }
                // Une langue en échec ne doit ni empêcher les autres ni remonter jusqu'au mod de
                // langue qui a déclenché onSupportedLocalesChanged.
                try
                {
                    localizationManager.AddSource(localeCode,
                        new LocaleSource(Translations.Build(Settings, pair.Value)));
                    _registeredLocales.Add(localeCode);
                    Log.Info($"Localisation enregistrée : {localeCode}");
                }
                catch (System.Exception e)
                {
                    _registeredLocales.Add(localeCode);
                    Log.Error(e, $"Échec de l'enregistrement de la langue {localeCode}.");
                }
            }
        }

        public void OnDispose()
        {
            Log.Info("GridRoadGenerator déchargé.");
            var localizationManager = GameManager.instance?.localizationManager;
            if (localizationManager != null && _localeListenerAttached)
            {
                localizationManager.onSupportedLocalesChanged -= RegisterLocalizations;
                _localeListenerAttached = false;
            }
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
