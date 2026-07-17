// Pattern d'enregistrement adapté de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import { ModRegistrar } from "cs2/modding";
import { GridPanel } from "./gridPanel";

const register: ModRegistrar = (moduleRegistry) => {
    // Le panneau se rend lui-même invisible tant que l'outil n'est pas actif.
    moduleRegistry.append("Game", GridPanel);
};

export default register;
