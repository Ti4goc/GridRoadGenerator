// RETOUR EN ARRIÈRE (2ᵉ fois) au panneau custom (LegacyGridPanel) — décision explicite de
// l'utilisateur après un 2ᵉ essai du panneau natif (NativeGridPanel) : les 3 causes de la 1ʳᵉ
// régression (2026-07-26) avaient bien été corrigées (badges "coming soon" retirés, icônes
// Geometria distinctes, section Avenue complète, sélecteur de prefab par réseau ajouté), mais
// DEUX PROBLÈMES NOUVEAUX sont apparus à l'usage réel et n'ont PAS pu être résolus malgré
// plusieurs rondes de correctifs :
//  - Lenteur persistante à toute interaction (ouvrir une section, déplacer le panneau, bouger
//    un slider) — cause isolée aux widgets d'ÉDITEUR détournés pour un usage en jeu
//    (VC.FloatSliderField/IntSliderField, remplacés par un slider custom léger — voir
//    NativeSlider dans gridPanelNative.tsx) puis à l'écriture disque par tick de slider
//    (ApplyAndSave, voir MarkSettingsDirty/GridRoadUISystem.cs) — deux vraies causes corrigées,
//    mais la lenteur restait perçue par l'utilisateur, cause finale non identifiée.
//  - Espacement du panneau : margin/gap CSS ignorés par le système natif Panel/InfoSection
//    (confirmé par une div "canari" à hauteur/fond forcés, voir Spacer dans gridPanelNative.tsx)
//    — calibré à l'aide de ce canari, mais le résultat final ("horrível") n'a pas convaincu.
// gridPanelNative.tsx GARDE tout ce travail (canari, NativeSlider, Spacer, NativeNetworkPrefabRow)
// dans le dépôt pour référence/reprise ultérieure éventuelle, mais n'est plus rendu.
//
// Enveloppé dans RenderErrorBoundary : LegacyGridPanel a plusieurs centaines de lignes de JSX
// conditionnel (Padrão/Redes/Cul-de-sac) et n'a pas tourné en jeu depuis un moment — sans
// filet, une exception de rendu ici plante tout l'arbre cohtml (vécu : React error #130, voir
// errorBoundary.tsx) et se traduit par "le panneau n'ouvre pas" sans trace dans les logs C# du
// jeu. Le repli affiche donc le message d'erreur lui-même, en superposition fixe bien visible.
import React from "react";
import { LegacyGridPanel } from "./gridPanel";
import { RenderErrorBoundary } from "./errorBoundary";

const errorFallback = (error: unknown) => (
    <div
        style={{
            position: "fixed",
            top: "12px",
            left: "12px",
            maxWidth: "480px",
            padding: "10px 14px",
            backgroundColor: "#5a1414ee",
            border: "2px solid #ff4d4d",
            borderRadius: "8px",
            color: "white",
            fontSize: "13px",
            lineHeight: "1.4em",
            zIndex: 9999,
            pointerEvents: "none",
        }}>
        <strong>GridRoadGenerator — erro ao abrir o painel:</strong>
        <div>{error instanceof Error ? `${error.name}: ${error.message}` : String(error)}</div>
    </div>
);

export const GridPanel = () => (
    <RenderErrorBoundary fallback={errorFallback}>
        <LegacyGridPanel />
    </RenderErrorBoundary>
);
