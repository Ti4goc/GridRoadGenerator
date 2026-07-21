// Barre permanente de sélection du réseau, au même niveau que "Vista" (voir
// viewSelection.tsx — même patron : composant non repliable, toujours visible en haut
// du panneau, son propre module scss). Remplace l'ancienne rangée "Route" de la section
// "Selection" : deux réseaux possibles, principal (collectrices/anneaux) et secondaire
// (impasses/rayons — voir RoadSegmentDef.IsCulDeSacEnd/IsRadial côté C#), ce dernier replié
// derrière un bouton "+" tant qu'aucun n'a été choisi explicitement.
import React, { useState } from "react";
import { createPortal } from "react-dom";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { PrefabPicker } from "./prefabPicker";
import styles from "./roadSelection.module.scss";
import {
    avenueRoadPrefabAuto$,
    avenueRoadPrefabIcon$,
    avenueRoadPrefabName$,
    roadPrefabIcon$,
    roadPrefabName$,
    secondaryRoadPrefabAuto$,
    secondaryRoadPrefabIcon$,
    secondaryRoadPrefabName$,
} from "bindings";

type PickerSlot = "primary" | "secondary" | "avenue" | null;

type RoadSelectionProps = {
    /// Nœud DOM frère de .panel, à l'intérieur du wrapper positionné (voir
    /// gridPanel.tsx/gridPanelNative.tsx, panelRef) — cible du portail pour que
    /// PrefabPicker s'affiche hors de la zone défilante/overflow:hidden du panneau
    /// (bug corrigé : le picker était rendu ici même, coupé/mal positionné). Null
    /// tant que le ref n'est pas encore attaché (premier rendu) : picker non rendu
    /// ce cas-là, pas d'effet visible puisque pickerOpen part toujours à null.
    portalContainer: HTMLElement | null;
};

export const RoadSelection = ({ portalContainer }: RoadSelectionProps) => {
    const { translate } = useLocalization();
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const secondaryRoadPrefabName = useValue(secondaryRoadPrefabName$);
    const secondaryRoadPrefabIcon = useValue(secondaryRoadPrefabIcon$);
    const secondaryRoadPrefabAuto = useValue(secondaryRoadPrefabAuto$);
    const avenueRoadPrefabName = useValue(avenueRoadPrefabName$);
    const avenueRoadPrefabIcon = useValue(avenueRoadPrefabIcon$);
    const avenueRoadPrefabAuto = useValue(avenueRoadPrefabAuto$);
    const [pickerOpen, setPickerOpen] = useState<PickerSlot>(null);
    // Repliées par défaut ; déjà dépliées si un réseau secondaire/avenue a été choisi
    // explicitement lors d'une session précédente (persisté côté C#, donc plus vraiment
    // "auto" au chargement).
    const [secondaryExpanded, setSecondaryExpanded] = useState(false);
    const showSecondary = secondaryExpanded || !secondaryRoadPrefabAuto;
    const [avenueExpanded, setAvenueExpanded] = useState(false);
    const showAvenue = avenueExpanded || !avenueRoadPrefabAuto;

    const displayName = (name: string) => (name ? (translate(`Assets.NAME[${name}]`, name) ?? name) : "—");
    const secondaryLabel = translate("GridRoadGenerator.UI.SecondaryRoadPrefab", "Secondary road") ?? "Secondary road";
    const avenueLabel = translate("GridRoadGenerator.UI.AvenueRoadPrefab", "Avenue road") ?? "Avenue road";

    return (
        <div className={styles.roadRow}>
            <span className={styles.roadLabel}>{translate("GridRoadGenerator.UI.RoadPrefab", "Road")}</span>
            <div className={styles.roadFields}>
                <button className={styles.prefabButton} onClick={() => setPickerOpen("primary")}>
                    {roadPrefabIcon && <img src={roadPrefabIcon} className={styles.prefabIcon} />}
                    <span className={styles.prefabName}>{displayName(roadPrefabName)}</span>
                    <span className={styles.prefabChevron}>›</span>
                </button>
                {showSecondary ? (
                    <button
                        className={styles.prefabButton}
                        title={secondaryLabel}
                        onClick={() => setPickerOpen("secondary")}>
                        {secondaryRoadPrefabIcon && <img src={secondaryRoadPrefabIcon} className={styles.prefabIcon} />}
                        <span className={styles.prefabName}>{displayName(secondaryRoadPrefabName)}</span>
                        <span className={styles.prefabChevron}>›</span>
                    </button>
                ) : (
                    <button
                        className={styles.addSecondaryButton}
                        title={secondaryLabel}
                        onClick={() => setSecondaryExpanded(true)}>
                        +
                    </button>
                )}
                {showAvenue ? (
                    <button
                        className={styles.prefabButton}
                        title={avenueLabel}
                        onClick={() => setPickerOpen("avenue")}>
                        {avenueRoadPrefabIcon && <img src={avenueRoadPrefabIcon} className={styles.prefabIcon} />}
                        <span className={styles.prefabName}>{displayName(avenueRoadPrefabName)}</span>
                        <span className={styles.prefabChevron}>›</span>
                    </button>
                ) : (
                    <button
                        className={styles.addSecondaryButton}
                        title={avenueLabel}
                        onClick={() => setAvenueExpanded(true)}>
                        +
                    </button>
                )}
            </div>
            {pickerOpen &&
                portalContainer &&
                createPortal(
                    <PrefabPicker slot={pickerOpen} onClose={() => setPickerOpen(null)} />,
                    portalContainer,
                )}
        </div>
    );
};
