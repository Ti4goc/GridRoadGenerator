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
    {
        // Toggle natif (widget de l'éditeur, utilisé en jeu par NetworkTools pour
        // ses options booléennes) : mode culs-de-sac, quinconce.
        path: "game-ui/editor/widgets/fields/toggle-field.tsx",
        components: ["ToggleField"],
    },
    {
        // Liste déroulante native (widget de l'éditeur) : taille et style du
        // cercle de retournement (remplace les rangées de boutons collés).
        path: "game-ui/editor/widgets/fields/dropdown-field.tsx",
        components: ["DropdownField"],
    },
    {
        // Rangée/section du panneau d'info sélection natif (ex. fiche bâtiment) —
        // PAS exportés par cs2/ui malgré leur présence dans les types (vérifié :
        // ni le mod Move It ni CS2-NetworkTools ne les lisent depuis cs2/ui, tous
        // deux passent par ce même chemin de registre direct). Utilisés par
        // gridPanelNative.tsx pour un contenu visuellement identique aux fiches
        // natives (ex. panneau d'info d'un bâtiment).
        path: "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
        components: ["InfoSection"],
    },
    {
        path: "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
        components: ["InfoRow"],
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
    {
        path: "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
        name: "infoRow",
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
