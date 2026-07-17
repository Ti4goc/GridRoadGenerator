// Structure de panneau adaptée de CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import classNames from "./gridPanel.module.scss";
import {
    canApply$,
    clearSelection,
    columns$,
    generateGrid,
    mode$,
    nodeCount$,
    perimeterInvalid$,
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

interface StepperRowProps {
    label: string | null;
    value: number;
    display?: string;
    disabled?: boolean;
    onDecrease: () => void;
    onIncrease: () => void;
}

const StepperRow = ({ label, value, display, disabled, onDecrease, onIncrease }: StepperRowProps) => (
    <div className={disabled ? `${classNames.row} ${classNames.rowDisabled}` : classNames.row}>
        <div className={classNames.label}>{label}</div>
        <div className={classNames.stepButton} onClick={onDecrease}>
            -
        </div>
        <div className={classNames.value}>{display ?? value}</div>
        <div className={classNames.stepButton} onClick={onIncrease}>
            +
        </div>
    </div>
);

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

    if (!toolActive) {
        return null;
    }

    const fitMode = mode === MODE_FIT;

    return (
        <div className={classNames.panel}>
            <div className={classNames.title}>{translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}</div>

            <div className={classNames.row}>
                <div
                    className={
                        fitMode ? `${classNames.modeButton} ${classNames.modeButtonActive}` : classNames.modeButton
                    }
                    onClick={() => setMode(MODE_FIT)}>
                    {translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                </div>
                <div
                    className={
                        !fitMode ? `${classNames.modeButton} ${classNames.modeButtonActive}` : classNames.modeButton
                    }
                    onClick={() => setMode(MODE_FIXED)}>
                    {translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")}
                </div>
            </div>

            <StepperRow
                label={translate("GridRoadGenerator.UI.Columns", "Columns")}
                value={columns}
                disabled={!fitMode}
                onDecrease={() => setColumns(Math.max(1, columns - 1))}
                onIncrease={() => setColumns(Math.min(12, columns + 1))}
            />
            <StepperRow
                label={translate("GridRoadGenerator.UI.Rows", "Rows")}
                value={rows}
                disabled={!fitMode}
                onDecrease={() => setRows(Math.max(1, rows - 1))}
                onIncrease={() => setRows(Math.min(12, rows + 1))}
            />
            <StepperRow
                label={translate("GridRoadGenerator.UI.Spacing", "Spacing (m)")}
                value={spacing}
                display={`${Math.round(spacing)} m`}
                disabled={fitMode}
                onDecrease={() => setSpacing(Math.max(10, spacing - 5))}
                onIncrease={() => setSpacing(Math.min(300, spacing + 5))}
            />

            <div
                className={
                    perimeterInvalid ? `${classNames.nodeCount} ${classNames.invalid}` : classNames.nodeCount
                }>
                {translate("GridRoadGenerator.UI.NodesSelected", "Selected nodes")}: {nodeCount}
            </div>

            <div
                className={
                    canApply
                        ? classNames.generateButton
                        : `${classNames.generateButton} ${classNames.generateButtonDisabled}`
                }
                onClick={generateGrid}>
                {translate("GridRoadGenerator.UI.Generate", "Generate grid")}
            </div>
            <div className={classNames.clearButton} onClick={clearSelection}>
                {translate("GridRoadGenerator.UI.ClearAll", "Clear all")}
            </div>
        </div>
    );
};
