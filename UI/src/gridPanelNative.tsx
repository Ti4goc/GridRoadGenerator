// Panneau sur le chrome InfoView natif (Panel/InfoSection/InfoRow de cs2/ui,
// la même famille de composants que les panneaux Fire & Rescue / Transportation),
// contenu et interactions reprises du panneau custom éprouvé (gridPanel.tsx),
// lui-même structuré sur le modèle de CS2-NetworkTools (c) Luca Rager,
// licence MIT — https://github.com/lucarager/CS2-NetworkTools
//
// N'est rendu que si gridPanelSwitch.tsx a confirmé que Panel/InfoRow/
// InfoSection existent réellement au runtime (pas seulement dans les types)
// ET l'a enveloppé dans un RenderErrorBoundary : si ce fichier plante quand
// même pour une autre raison, seul le panneau custom prend le relais, jamais
// le reste de l'UI du jeu.
import React, { useRef, useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { Button, InfoRow, InfoSection, Panel } from "cs2/ui";
import styles from "./gridPanelNative.module.scss";
import gridIcon from "./gridIcon.svg";
import { PrefabPicker } from "./prefabPicker";
import { VC, VF, VT } from "./vanilla";
import {
    anarchyAvailable$,
    anarchyEnabled$,
    angleOffset$,
    canApply$,
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
    toggleTool,
    toolActive$,
} from "bindings";

const MODE_FIT = 0;
const MODE_FIXED = 1;

// ------------------------------------------------------------------
// Position du panneau : draggable de cs2/ui n'accepte qu'une position
// INITIALE (DraggablePanelProps.initialPosition), sans callback de
// position finale — impossible d'y persister le déplacement. On garde
// donc le drag manuel (déjà vérifié en jeu) sur un wrapper positionné
// en absolu autour du vrai composant Panel natif.
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
                return clampToScreen(Math.max(pos.x, 0), pos.y, 0);
            }
        }
    } catch {
        // localStorage indisponible ou contenu corrompu : position par défaut.
    }
    return DEFAULT_POSITION;
};

export const NativeGridPanel = () => {
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
    // Replié : garde l'outil actif (seule la fermeture via le X le désactive).
    const [collapsed, setCollapsed] = useState(false);
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
    const roadDisplayName = roadPrefabName
        ? (translate(`Assets.NAME[${roadPrefabName}]`, roadPrefabName) ?? roadPrefabName)
        : "—";
    const title = (translate("GridRoadGenerator.UI.Title", "Grid Road Generator") ?? "").toUpperCase();

    // En-tête composé à la main (icône + titre + chevron replier), passé au
    // slot `header` du Panel natif — le X de fermeture est rendu par Panel
    // lui-même (PanelTitleBarTheme.closeButton) via onClose, pas recréé ici.
    // Le chevron replier/déplier n'a PAS d'équivalent Panel tout fait (pas de
    // prop "collapsed" native) : composé ici avec l'icône vanilla de flèche,
    // état géré côté panneau (garde l'outil actif, seul le X le désactive).
    const header = (
        <div className={styles.header} onMouseDown={startDrag}>
            <img src={gridIcon} className={styles.headerIcon} />
            <span className={styles.headerTitle}>{title}</span>
            <button
                className={styles.collapseButton}
                onClick={(event) => {
                    event.stopPropagation();
                    setCollapsed((value) => !value);
                }}
                onMouseDown={(event) => event.stopPropagation()}>
                <img
                    src="Media/Glyphs/ThickStrokeArrowDown.svg"
                    className={collapsed ? styles.collapseIconCollapsed : styles.collapseIcon}
                />
            </button>
        </div>
    );

    return (
        <div
            ref={panelRef}
            className={styles.panelWrapper}
            style={{ left: `${panelPosition.x}px`, top: `${panelPosition.y}px` }}>
            <Panel header={header} onClose={toggleTool} className={styles.panel}>
                {!collapsed && (
                    <>
                        {/* Mode : boutons d'outil natifs, état sélectionné violet vanilla. */}
                        <InfoSection>
                            <InfoRow uppercase left={translate("GridRoadGenerator.UI.Mode", "Mode")} />
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                                right={
                                    <VC.ToolButton
                                        src="Media/Tools/Snap Options/ZoneGrid.svg"
                                        selected={fitMode}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                        onSelect={() => setMode(MODE_FIT)}
                                        className={VT.toolButton.button}
                                    />
                                }
                            />
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")}
                                right={
                                    <VC.ToolButton
                                        src="Media/Glyphs/Length.svg"
                                        selected={!fitMode}
                                        multiSelect={false}
                                        disabled={false}
                                        focusKey={VF.FOCUS_DISABLED}
                                        onSelect={() => setMode(MODE_FIXED)}
                                        className={VT.toolButton.button}
                                    />
                                }
                            />
                        </InfoSection>

                        {/* Colonnes / Lignes / Espacement / Angle : sliders natifs, gardent
                            leur propre libellé interne (déjà éprouvé) plutôt qu'un InfoRow
                            à label séparé, pour éviter un double-libellé. */}
                        <InfoSection>
                            <InfoRow uppercase left={translate("GridRoadGenerator.UI.Grid", "Grid")} />
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
                        </InfoSection>

                        {/* Culs-de-sac : quartier pavillonnaire. */}
                        <InfoSection>
                            <InfoRow uppercase left={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")} />
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                                right={
                                    <VC.ToggleField
                                        value={culDeSacMode}
                                        disabled={false}
                                        onChange={(value: boolean) => setCulDeSacMode(value)}
                                    />
                                }
                            />
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
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.Staggered", "Staggered")}
                                right={
                                    <VC.ToggleField
                                        value={staggered}
                                        disabled={!culDeSacMode}
                                        onChange={(value: boolean) => setStaggered(value)}
                                    />
                                }
                            />
                        </InfoSection>

                        {/* Sélection en cours + réseau utilisé. */}
                        <InfoSection>
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.NodesSelected", "Selected nodes")}
                                right={<span className={perimeterInvalid ? styles.invalid : undefined}>{nodeCount}</span>}
                            />
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.RoadPrefab", "Road")}
                                right={
                                    <button className={styles.prefabRow} onClick={() => setPickerOpen((open) => !open)}>
                                        {roadPrefabIcon && <img src={roadPrefabIcon} className={styles.prefabIcon} />}
                                        <span className={styles.prefabName}>{roadDisplayName}</span>
                                        <span className={styles.prefabChevron}>›</span>
                                    </button>
                                }
                            />
                            {/* Anarchy (mod tiers optionnel) : rangée visible seulement s'il
                                est chargé ; état et toggle passent par les bindings d'Anarchy
                                lui-même, donc synchronisés avec son bouton toolbar et son
                                raccourci. */}
                            {anarchyAvailable && (
                                <InfoRow
                                    left="Anarchy"
                                    right={
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
                                    }
                                />
                            )}
                        </InfoSection>

                        {/* Action : bouton primaire natif custom. Pas de bouton "Tout
                            annuler" — redondant avec Échap et le clic droit. */}
                        <div className={styles.actions}>
                            <Button
                                variant="primary"
                                className={styles.applyButton}
                                disabled={!canApply}
                                onSelect={generateGrid}>
                                {translate("GridRoadGenerator.UI.Generate", "Generate")}
                            </Button>
                        </div>
                    </>
                )}
            </Panel>

            {pickerOpen && <PrefabPicker onClose={() => setPickerOpen(false)} />}
        </div>
    );
};
