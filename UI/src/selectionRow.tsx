// Barre permanente "Seleção" : nombre de nœuds actuellement sélectionnés pour le
// périmètre. Même patron que viewSelection.tsx/roadSelection.tsx (composant non
// repliable, toujours visible en haut du panneau, son propre module scss minimal) —
// remplace l'ancienne section repliable "Selection", déplacée ici à la demande de
// l'utilisateur (au même niveau que "Vista"/"Estrada", pas enterrée dans une section
// qu'il faut déplier).
import React from "react";
import { useLocalization } from "cs2/l10n";
import { VT } from "./vanilla";
import styles from "./selectionRow.module.scss";

type SelectionRowProps = {
    nodeCount: number;
    perimeterInvalid: boolean;
};

export const SelectionRow = ({ nodeCount, perimeterInvalid }: SelectionRowProps) => {
    const { translate } = useLocalization();

    return (
        <div className={styles.selectionRow}>
            <span className={styles.selectionLabel}>
                {translate("GridRoadGenerator.UI.NodesSelected", "Selected nodes")}
            </span>
            <div
                className={
                    perimeterInvalid
                        ? `${VT.mouseToolOptions.numberField} ${styles.invalid}`
                        : VT.mouseToolOptions.numberField
                }>
                {nodeCount}
            </div>
        </div>
    );
};
