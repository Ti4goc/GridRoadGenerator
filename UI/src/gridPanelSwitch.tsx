// Choisit entre le panneau natif InfoView (Panel/InfoRow/InfoSection de
// cs2/ui) et le panneau custom éprouvé, avec deux garde-fous cumulés — le
// crash précédent (React error #130 : "Element type is invalid... got:
// undefined") venait d'un composant cs2/ui absent au RUNTIME malgré sa
// présence dans les types, qui a cassé tout l'arbre cohtml, pas seulement
// notre panneau :
//
//  1. Garde statique : si Panel/InfoRow/InfoSection ne sont pas résolus
//     (`!= null`), on ne tente même pas de les rendre — c'est exactement
//     la condition qui a provoqué le crash la dernière fois.
//  2. Filet de secours : même si le garde statique passe, le rendu du
//     panneau natif est enveloppé dans un RenderErrorBoundary — toute
//     autre erreur de rendu (prop inattendue, etc.) reste confinée à
//     notre panneau et bascule sur le panneau custom, sans jamais
//     remonter jusqu'à casser le reste de l'UI du jeu.
import React from "react";
import { Panel, InfoRow, InfoSection } from "cs2/ui";
import { RenderErrorBoundary } from "./errorBoundary";
import { LegacyGridPanel } from "./gridPanel";
import { NativeGridPanel } from "./gridPanelNative";

const HAS_NATIVE_INFOVIEW = Panel != null && InfoRow != null && InfoSection != null;

if (!HAS_NATIVE_INFOVIEW) {
    // eslint-disable-next-line no-console
    console.warn(
        "[GridRoadGenerator] Panel/InfoRow/InfoSection indisponibles au runtime cs2/ui : panneau custom utilisé à la place.",
    );
}

export const GridPanel = () =>
    HAS_NATIVE_INFOVIEW ? (
        <RenderErrorBoundary fallback={<LegacyGridPanel />}>
            <NativeGridPanel />
        </RenderErrorBoundary>
    ) : (
        <LegacyGridPanel />
    );
