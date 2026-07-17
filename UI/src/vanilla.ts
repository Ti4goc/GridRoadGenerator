// Résolution des composants UI VANILLA du jeu depuis le registre de modules cohtml,
// pattern repris de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
//
// Les composants sont résolus une seule fois au moment du register() du mod :
//  - VC : composants React vanilla (Section, ToolButton, sliders de l'éditeur…)
//  - VT : thèmes SCSS vanilla (classes CSS du jeu, ex. VT.toolButton.button)
//  - VF : clés de focus vanilla (FOCUS_DISABLED pour neutraliser le focus manette)
import { ModuleRegistry } from "cs2/modding";

type ModulePath = { path: string; components: string[] };
type ThemePath = { path: string; name: string };

const modulePaths: ModulePath[] = [
    {
        // Rangée native des options d'outil : label à gauche, contrôles à droite.
        path: "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx",
        components: ["Section"],
    },
    {
        // Bouton d'outil natif, avec l'état sélectionné violet vanilla.
        path: "game-ui/game/components/tool-options/tool-button/tool-button.tsx",
        components: ["ToolButton"],
    },
    {
        // Sliders natifs avec champ de valeur éditable (widgets de l'éditeur,
        // utilisés en jeu par NetworkTools pour ses paramètres "Raio 113 m").
        path: "game-ui/editor/widgets/fields/number-slider-field.tsx",
        components: ["IntSliderField", "FloatSliderField"],
    },
    {
        // Zone scrollable native (liste du sélecteur de réseau).
        path: "game-ui/common/scrolling/scrollable.tsx",
        components: ["Scrollable"],
    },
];

const themePaths: ThemePath[] = [
    {
        path: "game-ui/game/components/tool-options/tool-button/tool-button.module.scss",
        name: "toolButton",
    },
    {
        path: "game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.module.scss",
        name: "mouseToolOptions",
    },
];

export const VC: Record<string, any> = {};
export const VT: Record<string, Record<string, string>> = {};
export const VF: { FOCUS_DISABLED?: unknown; FOCUS_AUTO?: unknown } = {};

export const initializeVanilla = (moduleRegistry: ModuleRegistry) => {
    modulePaths.forEach(({ path, components }) => {
        const module = moduleRegistry.registry.get(path);
        components.forEach((component) => (VC[component] = module?.[component]));
    });
    themePaths.forEach(({ path, name }) => {
        VT[name] = moduleRegistry.registry.get(path)?.classes ?? {};
    });

    const focusKey = moduleRegistry.registry.get("game-ui/common/focus/focus-key.ts");
    VF.FOCUS_DISABLED = focusKey?.FOCUS_DISABLED;
    VF.FOCUS_AUTO = focusKey?.FOCUS_AUTO;
};
