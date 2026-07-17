// Structure de panneau et intégration des composants vanilla adaptées de
// CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React, { useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button } from "cs2/ui";
import styles from "./gridPanel.module.scss";
import { PrefabPicker } from "./prefabPicker";
import { VC, VF, VT } from "./vanilla";
import {
    canApply$,
    clearSelection,
    columns$,
    generateGrid,
    mode$,
    nodeCount$,
    perimeterInvalid$,
    roadPrefabIcon$,
    roadPrefabName$,
    rows$,
    setColumns,
    setMode,
    setRows,
    setSpacing,
    spacing$,
    toolActive$,
} from "bindings";

const MODE_FIT = 0;
const MODE_FIXED = 1;

export const GridPanel = () => {
    const { translate } = useLocalization();
    const toolActive = useValue(toolActive$);
    const nodeCount = useValue(nodeCount$);
    const canApply = useValue(canApply$);
    const perimeterInvalid = useValue(perimeterInvalid$);
    const mode = useValue(mode$);
    const columns = useValue(columns$);
    const rows = useValue(rows$);
    const spacing = useValue(spacing$);
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const [pickerOpen, setPickerOpen] = useState(false);

    if (!toolActive) {
        return null;
    }

    const fitMode = mode === MODE_FIT;
    // Nom localisé du prefab, comme le fait le jeu (fallback : nom brut).
    const roadDisplayName = roadPrefabName
        ? (translate(`Assets.NAME[${roadPrefabName}]`, roadPrefabName) ?? roadPrefabName)
        : "—";

    return (
        <div className={styles.panel}>
            <div className={styles.header}>
                {translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}
            </div>

            <div className={styles.content}>
                {/* Mode : boutons d'outil natifs, état sélectionné violet vanilla. */}
                <div className={styles.vanillaRow}>
                    <VC.Section
                        focusKey={VF.FOCUS_DISABLED}
                        title={translate("GridRoadGenerator.UI.Mode", "Mode")}>
                        <VC.ToolButton
                            src="Media/Tools/Snap Options/ZoneGrid.svg"
                            selected={fitMode}
                            multiSelect={false}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                            onSelect={() => setMode(MODE_FIT)}
                            className={VT.toolButton.button}
                        />
                        <VC.ToolButton
                            src="Media/Glyphs/Length.svg"
                            selected={!fitMode}
                            multiSelect={false}
                            disabled={false}
                            focusKey={VF.FOCUS_DISABLED}
                            tooltip={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")}
                            onSelect={() => setMode(MODE_FIXED)}
                            className={VT.toolButton.button}
                        />
                    </VC.Section>
                </div>

                {/* Colonnes / Lignes / Espacement : sliders natifs avec champ éditable. */}
                <div className={styles.vanillaRow}>
                    <div className={styles.vanillaField}>
                        <VC.IntSliderField
                            label={translate("GridRoadGenerator.UI.Columns", "Columns")}
                            value={columns}
                            min={1}
                            max={12}
                            disabled={!fitMode}
                            onChange={(value: number) => setColumns(Math.round(value))}
                        />
                    </div>
                </div>
                <div className={styles.vanillaRow}>
                    <div className={styles.vanillaField}>
                        <VC.IntSliderField
                            label={translate("GridRoadGenerator.UI.Rows", "Rows")}
                            value={rows}
                            min={1}
                            max={12}
                            disabled={!fitMode}
                            onChange={(value: number) => setRows(Math.round(value))}
                        />
                    </div>
                </div>
                <div className={styles.vanillaRow}>
                    <div className={styles.vanillaField}>
                        <VC.FloatSliderField
                            label={translate("GridRoadGenerator.UI.SpacingShort", "Spacing")}
                            value={spacing}
                            min={10}
                            max={300}
                            fractionDigits={0}
                            disabled={fitMode}
                            onChange={(value: number) => setSpacing(value)}
                        />
                        <span className={styles.unitLabel}>m</span>
                    </div>
                </div>

                {/* Compteur de nœuds : rangée native label / valeur. */}
                <div className={styles.vanillaRow}>
                    <VC.Section
                        focusKey={VF.FOCUS_DISABLED}
                        title={translate("GridRoadGenerator.UI.NodesSelected", "Selected nodes")}>
                        <div
                            className={
                                perimeterInvalid
                                    ? `${VT.mouseToolOptions.numberField} ${styles.invalid}`
                                    : VT.mouseToolOptions.numberField
                            }>
                            {nodeCount}
                        </div>
                    </VC.Section>
                </div>

                {/* Réseau utilisé pour la grille : clic = ouvre le sélecteur. */}
                <div className={styles.vanillaRow}>
                    <VC.Section
                        focusKey={VF.FOCUS_DISABLED}
                        title={translate("GridRoadGenerator.UI.RoadPrefab", "Road")}>
                        <button
                            className={styles.prefabRow}
                            onClick={() => setPickerOpen((open) => !open)}>
                            {roadPrefabIcon && <img src={roadPrefabIcon} className={styles.prefabIcon} />}
                            <span className={styles.prefabName}>{roadDisplayName}</span>
                            <span className={styles.prefabChevron}>›</span>
                        </button>
                    </VC.Section>
                </div>

                {/* Actions : bouton primaire natif + bouton secondaire natif. */}
                <div className={styles.actions}>
                    <Button
                        variant="primary"
                        className={styles.applyButton}
                        disabled={!canApply}
                        onSelect={generateGrid}>
                        {translate("GridRoadGenerator.UI.Generate", "Generate grid")}
                    </Button>
                    <Button variant="flat" className={styles.clearButton} onSelect={clearSelection}>
                        {translate("GridRoadGenerator.UI.ClearAll", "Clear all")}
                    </Button>
                </div>
            </div>

            {pickerOpen && <PrefabPicker onClose={() => setPickerOpen(false)} />}
        </div>
    );
};
