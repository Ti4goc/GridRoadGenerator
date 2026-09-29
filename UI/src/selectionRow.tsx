// Barre permanente "Seleção" : mode de sélection (périmètre existant / tracer / peindre) et nombre de
// points actuellement sélectionnés pour le périmètre. Même patron que viewSelection.tsx/
// roadSelection.tsx (composant non repliable, toujours visible en haut du panneau, son propre
// module scss minimal).
import React from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { TIP_TEXT, Tip } from "./tips";
import { brushSquare$, selectionMode$, setBrushSquare, setSelectionMode } from "./bindings";
import styles from "./selectionRow.module.scss";
import selectionExisting from "./selectionExisting.svg";
import selectionDraw from "./selectionDraw.svg";
import selectionPaint from "./selectionPaint.svg";
import brushRound from "./brushRound.svg";
import brushSquare from "./brushSquare.svg";

/// Barre du haut du panneau (toujours visible) : les trois types de zone (icône + nom, description
/// en infobulle). Le compteur (nœuds, points ou hectares) est dans le résumé, au-dessus de Générer.
export const AreaModeBar = () => {
    const { translate } = useLocalization();
    const selectionMode = useValue(selectionMode$);
    const modes: [number, string, string, string][] = [
        [0, selectionExisting, "SelectionExisting", "Existing perimeter"],
        [1, selectionDraw, "SelectionFreeArea", "Draw area"],
        [2, selectionPaint, "SelectionBrush", "Paint area"],
    ];
    return (
        <div className={styles.areaBar}>
            {modes.map(([mode, icon, key, fallback]) => (
                <Tip
                    key={mode}
                    title={translate(`GridRoadGenerator.UI.${key}`, fallback) ?? fallback}
                    description={translate(`GridRoadGenerator.UI.Tip.${key}`, TIP_TEXT[key]) ?? TIP_TEXT[key]}>
                    <button
                        type="button"
                        className={selectionMode === mode ? `${styles.areaButton} ${styles.areaButtonActive}` : styles.areaButton}
                        onClick={() => setSelectionMode(mode)}>
                        <img src={icon} className={styles.areaButtonIcon} />
                        <span className={styles.areaButtonLabel}>{translate(`GridRoadGenerator.UI.${key}`, fallback)}</span>
                    </button>
                </Tip>
            ))}
        </div>
    );
};

/// Compteur de la sélection pour le résumé (au-dessus de Générer) : icône du type de zone et nombre de
/// nœuds / points, ou d'hectares peints ; rouge si le périmètre ne donne rien.
export const SelectionCount = ({ count, invalid }: { count: number; invalid: boolean }) => {
    const { translate } = useLocalization();
    const selectionMode = useValue(selectionMode$);
    const [countKey, countFallback] = COUNT_LABEL[selectionMode] ?? COUNT_LABEL[0];
    const icon = selectionMode === 2 ? selectionPaint : selectionMode === 1 ? selectionDraw : selectionExisting;
    return (
        <Tip
            title={translate(`GridRoadGenerator.UI.${countKey}`, countFallback) ?? countFallback}
            description={translate(`GridRoadGenerator.UI.Tip.${countKey}`, TIP_TEXT[countKey]) ?? TIP_TEXT[countKey]}>
            <span className={invalid ? `${styles.selectionCount} ${styles.invalid}` : styles.selectionCount}>
                <img src={icon} className={styles.selectionCountIcon} />
                {count}
                {selectionMode === 2 ? " ha" : ""}
            </span>
        </Tip>
    );
};

/// Forme du pinceau (mode Pintar área) : deux boutons-icônes, rond ou carré.
export const BrushShapeRow = () => {
    const { translate } = useLocalization();
    const square = useValue(brushSquare$);
    const shapeButton = (isSquare: boolean, icon: string, key: string, fallback: string) => (
        <button
            type="button"
            className={square === isSquare ? `${styles.shapeButton} ${styles.shapeButtonActive}` : styles.shapeButton}
            onClick={() => setBrushSquare(isSquare)}>
            <Tip title={translate(`GridRoadGenerator.UI.${key}`, fallback)} description={translate("GridRoadGenerator.UI.Tip.BrushShape", TIP_TEXT.BrushShape) ?? TIP_TEXT.BrushShape}>
                <img src={icon} className={styles.shapeButtonIcon} />
            </Tip>
        </button>
    );
    return (
        <div className={styles.shapeRow}>
            <Tip
                title={translate("GridRoadGenerator.UI.BrushShape", "Brush shape")}
                description={translate("GridRoadGenerator.UI.Tip.BrushShape", TIP_TEXT.BrushShape) ?? TIP_TEXT.BrushShape}>
                <span className={styles.shapeLabel}>{translate("GridRoadGenerator.UI.BrushShape", "Brush shape")}</span>
            </Tip>
            <div className={styles.shapeButtons}>
                {shapeButton(false, brushRound, "BrushRound", "Round")}
                {shapeButton(true, brushSquare, "BrushSquare", "Square")}
            </div>
        </div>
    );
};

type SelectionRowProps = {
    nodeCount: number;
    perimeterInvalid: boolean;
    /// Réglages propres au mode (taille du pinceau), entre le sélecteur et le compteur.
    children?: React.ReactNode;
};

/// 0 = routes existantes, 1 = zone libre (points cliqués), 2 = pinceau — voir SET_SELECTION_MODE.
const COUNT_LABEL: Record<number, [string, string]> = {
    0: ["NodesSelected", "Selected nodes"],
    1: ["AreaPoints", "Area points"],
    2: ["PaintedArea", "Painted area (ha)"],
};

export const SelectionRow = ({ nodeCount, perimeterInvalid, children }: SelectionRowProps) => {
    const { translate } = useLocalization();
    const selectionMode = useValue(selectionMode$);

    const modeButton = (mode: number, icon: string, key: string, fallback: string) => (
        <button
            type="button"
            className={selectionMode === mode ? `${styles.modeButton} ${styles.modeButtonActive}` : styles.modeButton}
            onClick={() => setSelectionMode(mode)}>
            <Tip
                title={translate(`GridRoadGenerator.UI.${key}`, fallback)}
                description={translate(`GridRoadGenerator.UI.Tip.${key}`, TIP_TEXT[key]) ?? TIP_TEXT[key]}>
                <div className={styles.modeButtonContent}>
                    <img src={icon} className={styles.modeButtonIcon} />
                    <span className={styles.modeButtonLabel}>{translate(`GridRoadGenerator.UI.${key}`, fallback)}</span>
                </div>
            </Tip>
        </button>
    );

    const [countKey, countFallback] = COUNT_LABEL[selectionMode] ?? COUNT_LABEL[0];

    return (
        <div className={styles.selectionRow}>
            <div className={styles.modeButtons}>
                {modeButton(0, selectionExisting, "SelectionExisting", "Existing perimeter")}
                {modeButton(1, selectionDraw, "SelectionFreeArea", "Draw area")}
                {modeButton(2, selectionPaint, "SelectionBrush", "Paint area")}
            </div>
            {children}
            <div className={styles.countRow}>
                <Tip
                    title={translate(`GridRoadGenerator.UI.${countKey}`, countFallback)}
                    description={translate(`GridRoadGenerator.UI.Tip.${countKey}`, TIP_TEXT[countKey]) ?? TIP_TEXT[countKey]}>
                    <span className={styles.selectionLabel}>
                        {translate(`GridRoadGenerator.UI.${countKey}`, countFallback)}
                    </span>
                </Tip>
                {/* Sans le fond du champ numérique natif (numberField) — demande utilisateur. */}
                <div className={perimeterInvalid ? `${styles.nodeCount} ${styles.invalid}` : styles.nodeCount}>
                    {nodeCount}
                </div>
            </div>
        </div>
    );
};
