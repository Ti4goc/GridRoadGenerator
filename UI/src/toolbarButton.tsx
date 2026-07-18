// Bouton d'activation dans la barre d'icônes en haut à gauche, pattern repris du
// GameInjection de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { toggleTool, toolActive$ } from "bindings";
import { RenderErrorBoundary } from "./errorBoundary";
import gridIcon from "./gridIcon.svg";
import { SafeButton, SafeTooltip } from "./safeButton";

/// Bouton flottant permanent : clic = équivalent exact de Ctrl+G (TOGGLE_TOOL) ;
/// l'état enfoncé suit TOOL_ACTIVE, donc reste synchronisé quand l'outil est
/// activé/désactivé par le raccourci clavier ou Échap.
///
/// Toujours monté dès le chargement du mod (contrairement au panneau, qui ne se
/// rend que si l'outil est actif) : c'est le seul élément d'UI sans aucun repli,
/// donc enveloppé dans RenderErrorBoundary avec un fallback null — une erreur ici
/// ne doit jamais faire disparaître le reste de l'UI du jeu.
const ToolbarButtonContent = () => {
    const { translate } = useLocalization();
    const toolActive = useValue(toolActive$);

    return (
        <SafeTooltip
            tooltip={translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}
            delayTime={0}
            direction="down">
            <SafeButton variant="floating" src={gridIcon} selected={toolActive} onSelect={toggleTool} />
        </SafeTooltip>
    );
};

export const GridToolbarButton = () => (
    <RenderErrorBoundary fallback={null}>
        <ToolbarButtonContent />
    </RenderErrorBoundary>
);
