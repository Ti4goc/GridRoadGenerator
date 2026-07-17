// Bouton d'activation dans la barre d'icônes en haut à gauche, pattern repris du
// GameInjection de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button, Tooltip } from "cs2/ui";
import { toggleTool, toolActive$ } from "bindings";
import gridIcon from "./gridIcon.svg";

/// Bouton flottant permanent : clic = équivalent exact de Ctrl+G (TOGGLE_TOOL) ;
/// l'état enfoncé suit TOOL_ACTIVE, donc reste synchronisé quand l'outil est
/// activé/désactivé par le raccourci clavier ou Échap.
export const GridToolbarButton = () => {
    const { translate } = useLocalization();
    const toolActive = useValue(toolActive$);

    return (
        <Tooltip
            tooltip={translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}
            delayTime={0}
            direction="down">
            <Button variant="floating" src={gridIcon} selected={toolActive} onSelect={toggleTool} />
        </Tooltip>
    );
};
