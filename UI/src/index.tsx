// Pattern d'enregistrement adapté de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import { ModRegistrar } from "cs2/modding";
import { GridPanel } from "./gridPanel";
import { GridToolbarButton } from "./toolbarButton";
import { initializeVanilla } from "./vanilla";

const register: ModRegistrar = (moduleRegistry) => {
    // Résout les composants/thèmes vanilla du jeu avant tout rendu du panneau.
    initializeVanilla(moduleRegistry);
    // Le panneau se rend lui-même invisible tant que l'outil n'est pas actif.
    moduleRegistry.append("Game", GridPanel);
    // Bouton d'activation permanent dans la barre d'icônes en haut à gauche.
    moduleRegistry.append("GameTopLeft", GridToolbarButton);
};

export default register;
