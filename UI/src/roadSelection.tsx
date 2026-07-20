// Barre permanente de sélection du réseau, au même niveau que "Vista" (voir
// viewSelection.tsx — même patron : composant non repliable, toujours visible en haut
// du panneau, son propre module scss). Remplace l'ancienne rangée "Route" de la section
// "Selection" : deux réseaux possibles, principal (collectrices/anneaux) et secondaire
// (impasses/rayons — voir RoadSegmentDef.IsCulDeSacEnd/IsRadial côté C#), ce dernier replié
// derrière un bouton "+" tant qu'aucun n'a été choisi explicitement.
import React, { useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { PrefabPicker } from "./prefabPicker";
import styles from "./roadSelection.module.scss";
import {
    roadPrefabIcon$,
    roadPrefabName$,
    secondaryRoadPrefabAuto$,
    secondaryRoadPrefabIcon$,
    secondaryRoadPrefabName$,
} from "bindings";

type PickerSlot = "primary" | "secondary" | null;

export const RoadSelection = () => {
    const { translate } = useLocalization();
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const secondaryRoadPrefabName = useValue(secondaryRoadPrefabName$);
    const secondaryRoadPrefabIcon = useValue(secondaryRoadPrefabIcon$);
    const secondaryRoadPrefabAuto = useValue(secondaryRoadPrefabAuto$);
    const [pickerOpen, setPickerOpen] = useState<PickerSlot>(null);
    // Repliée par défaut ; déjà dépliée si un réseau secondaire a été choisi explicitement
    // lors d'une session précédente (persisté côté C#, donc plus vraiment "auto" au chargement).
    const [secondaryExpanded, setSecondaryExpanded] = useState(false);
    const showSecondary = secondaryExpanded || !secondaryRoadPrefabAuto;

    const displayName = (name: string) => (name ? (translate(`Assets.NAME[${name}]`, name) ?? name) : "—");
    const secondaryLabel = translate("GridRoadGenerator.UI.SecondaryRoadPrefab", "Secondary road") ?? "Secondary road";

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
            </div>
            {pickerOpen && <PrefabPicker slot={pickerOpen} onClose={() => setPickerOpen(null)} />}
        </div>
    );
};
