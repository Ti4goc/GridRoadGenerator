// Structure de panneau et intégration des composants vanilla adaptées de
// CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React, { useRef, useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button } from "cs2/ui";
import styles from "./gridPanel.module.scss";
import { PrefabPicker } from "./prefabPicker";
import { VC, VF, VT } from "./vanilla";
import {
    anarchyAvailable$,
    anarchyEnabled$,
    angleOffset$,
    canApply$,
    clearSelection,
    columns$,
    culDeSacDepth$,
    culDeSacMode$,
    culDeSacRatio$,
    generateGrid,
    mode$,
    nodeCount$,
    perimeterInvalid$,
    roadPrefabIcon$,
    roadPrefabName$,
    rows$,
    setAngleOffset,
    setColumns,
    setCulDeSacDepth,
    setCulDeSacMode,
    setCulDeSacRatio,
    setMode,
    setRows,
    setSpacing,
    setStaggered,
    spacing$,
    staggered$,
    toggleAnarchy,
    toolActive$,
} from "bindings";

const MODE_FIT = 0;
const MODE_FIXED = 1;

// ------------------------------------------------------------------
// Panneau déplaçable : drag par la barre de titre, position persistée
// (localStorage cohtml : survit aux ouvertures et aux sessions de jeu).
// Le Panel draggable de cs2/ui n'expose pas la position finale, d'où
// cette implémentation manuelle, pattern courant des mods CS2.
// ------------------------------------------------------------------

const PANEL_POSITION_KEY = "GridRoadGenerator.panelPosition";
const DEFAULT_POSITION = { x: 12, y: 220 };
/// Marge (px) de panneau qui doit toujours rester visible à l'écran.
const MIN_VISIBLE = 60;

type PanelPosition = { x: number; y: number };

const clampToScreen = (x: number, y: number, panelWidth: number): PanelPosition => ({
    x: Math.min(Math.max(x, MIN_VISIBLE - panelWidth), window.innerWidth - MIN_VISIBLE),
    y: Math.min(Math.max(y, 0), window.innerHeight - MIN_VISIBLE),
});

const loadPanelPosition = (): PanelPosition => {
    try {
        const raw = localStorage.getItem(PANEL_POSITION_KEY);
        if (raw) {
            const pos = JSON.parse(raw);
            if (typeof pos.x === "number" && typeof pos.y === "number") {
                // Reclampe au chargement (la résolution a pu changer entre deux sessions).
                return clampToScreen(Math.max(pos.x, 0), pos.y, 0);
            }
        }
    } catch {
        // localStorage indisponible ou contenu corrompu : position par défaut.
    }
    return DEFAULT_POSITION;
};

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
    const angleOffset = useValue(angleOffset$);
    const culDeSacMode = useValue(culDeSacMode$);
    const culDeSacDepth = useValue(culDeSacDepth$);
    const staggered = useValue(staggered$);
    const culDeSacRatio = useValue(culDeSacRatio$);
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const anarchyAvailable = useValue(anarchyAvailable$);
    const anarchyEnabled = useValue(anarchyEnabled$);
    const [pickerOpen, setPickerOpen] = useState(false);
    const [panelPosition, setPanelPosition] = useState<PanelPosition>(loadPanelPosition);
    const panelRef = useRef<HTMLDivElement>(null);

    if (!toolActive) {
        return null;
    }

    const startDrag = (event: React.MouseEvent) => {
        const rect = panelRef.current?.getBoundingClientRect();
        if (!rect) {
            return;
        }
        const offsetX = event.clientX - rect.left;
        const offsetY = event.clientY - rect.top;

        const positionFrom = (ev: MouseEvent) =>
            clampToScreen(ev.clientX - offsetX, ev.clientY - offsetY, rect.width);
        const onMove = (ev: MouseEvent) => setPanelPosition(positionFrom(ev));
        const onUp = (ev: MouseEvent) => {
            window.removeEventListener("mousemove", onMove);
            window.removeEventListener("mouseup", onUp);
            const final = positionFrom(ev);
            setPanelPosition(final);
            try {
                localStorage.setItem(PANEL_POSITION_KEY, JSON.stringify(final));
            } catch {
                // Pas de persistance possible : la position reste valable pour la session.
            }
        };
        window.addEventListener("mousemove", onMove);
        window.addEventListener("mouseup", onUp);
    };

    const fitMode = mode === MODE_FIT;
    // Nom localisé du prefab, comme le fait le jeu (fallback : nom brut).
    const roadDisplayName = roadPrefabName
        ? (translate(`Assets.NAME[${roadPrefabName}]`, roadPrefabName) ?? roadPrefabName)
        : "—";

    return (
        <div
            ref={panelRef}
            className={styles.panel}
            style={{ left: `${panelPosition.x}px`, top: `${panelPosition.y}px` }}>
            <div className={styles.header} onMouseDown={startDrag}>
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
                <div className={styles.vanillaRow}>
                    <div className={styles.vanillaField}>
                        <VC.FloatSliderField
                            label={translate("GridRoadGenerator.UI.Angle", "Angle")}
                            value={angleOffset}
                            min={-90}
                            max={90}
                            fractionDigits={0}
                            disabled={false}
                            onChange={(value: number) => setAngleOffset(value)}
                        />
                        <span className={styles.unitLabel}>°</span>
                    </div>
                </div>

                {/* Culs-de-sac : quartier pavillonnaire (collectrices traversantes,
                    résidentielles en impasse). Sliders et toggle Quinconce grisés
                    tant que le mode est désactivé. */}
                <div className={styles.vanillaRow}>
                    <VC.Section
                        focusKey={VF.FOCUS_DISABLED}
                        title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}>
                        <VC.ToggleField
                            value={culDeSacMode}
                            disabled={false}
                            onChange={(value: boolean) => setCulDeSacMode(value)}
                        />
                    </VC.Section>
                </div>
                <div className={styles.vanillaRow}>
                    <div className={styles.vanillaField}>
                        <VC.FloatSliderField
                            label={translate("GridRoadGenerator.UI.CulDeSacDepth", "Depth")}
                            value={culDeSacDepth}
                            min={50}
                            max={90}
                            fractionDigits={0}
                            disabled={!culDeSacMode}
                            onChange={(value: number) => setCulDeSacDepth(value)}
                        />
                        <span className={styles.unitLabel}>%</span>
                    </div>
                </div>
                <div className={styles.vanillaRow}>
                    <div className={styles.vanillaField}>
                        <VC.FloatSliderField
                            label={translate("GridRoadGenerator.UI.CulDeSacRatio", "Frequency")}
                            value={culDeSacRatio}
                            min={0}
                            max={100}
                            fractionDigits={0}
                            disabled={!culDeSacMode}
                            onChange={(value: number) => setCulDeSacRatio(value)}
                        />
                        <span className={styles.unitLabel}>%</span>
                    </div>
                </div>
                <div className={styles.vanillaRow}>
                    <VC.Section
                        focusKey={VF.FOCUS_DISABLED}
                        title={translate("GridRoadGenerator.UI.Staggered", "Staggered")}>
                        <VC.ToggleField
                            value={staggered}
                            disabled={!culDeSacMode}
                            onChange={(value: boolean) => setStaggered(value)}
                        />
                    </VC.Section>
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

                {/* Anarchy (mod tiers optionnel) : rangée visible seulement s'il est
                    chargé ; état et toggle passent par les bindings d'Anarchy lui-même,
                    donc synchronisés avec son bouton toolbar et son raccourci. */}
                {anarchyAvailable && (
                    <div className={styles.vanillaRow}>
                        <VC.Section focusKey={VF.FOCUS_DISABLED} title="Anarchy">
                            <VC.ToolButton
                                src="coui://uil/Standard/Anarchy.svg"
                                selected={anarchyEnabled}
                                multiSelect={false}
                                disabled={false}
                                focusKey={VF.FOCUS_DISABLED}
                                tooltip={translate("GridRoadGenerator.UI.AnarchyTooltip", "Toggle Anarchy")}
                                onSelect={toggleAnarchy}
                                className={VT.toolButton.button}
                            />
                        </VC.Section>
                    </div>
                )}

                {/* Actions : bouton primaire natif + bouton secondaire natif. */}
                <div className={styles.actions}>
                    <Button
                        variant="primary"
                        className={styles.applyButton}
                        disabled={!canApply}
                        onSelect={generateGrid}>
                        {translate("GridRoadGenerator.UI.Generate", "Generate")}
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
