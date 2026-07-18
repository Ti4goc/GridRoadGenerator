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
import { Button, InfoRow, InfoSectionFoldout, Panel } from "cs2/ui";
import styles from "./gridPanelNative.module.scss";
import gridIcon from "./gridIcon.svg";
import { locString } from "./locHelpers";
import { PrefabPicker } from "./prefabPicker";
import { VC, VF, VT } from "./vanilla";
import {
    anarchyAvailable$,
    anarchyEnabled$,
    angleOffset$,
    canApply$,
    columns$,
    culDeSacCapSize$,
    culDeSacCapStyle$,
    culDeSacDepth$,
    culDeSacMode$,
    culDeSacRatio$,
    followTerrain$,
    generateGrid,
    jitterAmount$,
    mode$,
    nodeCount$,
    perimeterInvalid$,
    regenerateJitterSeed,
    roadPrefabIcon$,
    roadPrefabName$,
    rows$,
    setAngleOffset,
    setColumns,
    setCulDeSacCapSize,
    setCulDeSacCapStyle,
    setCulDeSacDepth,
    setCulDeSacMode,
    setCulDeSacRatio,
    setFollowTerrain,
    setJitterAmount,
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

const CAP_SIZE_AUTO = 0;
const CAP_SIZE_SMALL = 1;
const CAP_SIZE_MEDIUM = 2;
const CAP_SIZE_LARGE = 3;
const CAP_SIZE_XL = 4;

const CAP_STYLE_ASPHALT = 0;
const CAP_STYLE_GRASS = 1;
const CAP_STYLE_TREES = 2;

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
    const followTerrain = useValue(followTerrain$);
    const culDeSacMode = useValue(culDeSacMode$);
    const culDeSacDepth = useValue(culDeSacDepth$);
    const staggered = useValue(staggered$);
    const culDeSacRatio = useValue(culDeSacRatio$);
    const culDeSacCapSize = useValue(culDeSacCapSize$);
    const culDeSacCapStyle = useValue(culDeSacCapStyle$);
    const jitterAmount = useValue(jitterAmount$);
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

    // Taille et style du cercle de retournement : deux listes déroulantes natives
    // combinées pour désigner le prefab "CulDeSac<Taille><Style>" exact. Le style
    // Asphalt n'a pas de variante XL (prefab inexistant) : l'option est masquée
    // dans la liste plutôt que de proposer une combinaison invalide, et un
    // changement de style vers Asphalt replie une sélection XL existante sur
    // Large (même repli que celui déjà fait côté C# si la combinaison survient
    // par un autre chemin, ex. Options > Mods).
    const loc = (key: string, fallback: string) => locString(translate(key, fallback) ?? fallback);
    const capSizeItems = [
        { value: CAP_SIZE_AUTO, displayName: loc("GridRoadGenerator.UI.CulDeSacCapSizeAuto", "Automatic (road width)") },
        { value: CAP_SIZE_SMALL, displayName: loc("GridRoadGenerator.UI.CulDeSacCapSizeSmall", "Small") },
        { value: CAP_SIZE_MEDIUM, displayName: loc("GridRoadGenerator.UI.CulDeSacCapSizeMedium", "Medium") },
        { value: CAP_SIZE_LARGE, displayName: loc("GridRoadGenerator.UI.CulDeSacCapSizeLarge", "Large") },
        ...(culDeSacCapStyle !== CAP_STYLE_ASPHALT
            ? [{ value: CAP_SIZE_XL, displayName: loc("GridRoadGenerator.UI.CulDeSacCapSizeXL", "XL") }]
            : []),
    ];
    const capStyleItems = [
        { value: CAP_STYLE_ASPHALT, displayName: loc("GridRoadGenerator.UI.CulDeSacCapStyleAsphalt", "Asphalt") },
        { value: CAP_STYLE_GRASS, displayName: loc("GridRoadGenerator.UI.CulDeSacCapStyleGrass", "Grass") },
        { value: CAP_STYLE_TREES, displayName: loc("GridRoadGenerator.UI.CulDeSacCapStyleTrees", "Trees") },
    ];
    const handleCapStyleChange = (value: number) => {
        if (value === CAP_STYLE_ASPHALT && culDeSacCapSize === CAP_SIZE_XL) {
            setCulDeSacCapSize(CAP_SIZE_LARGE);
        }
        setCulDeSacCapStyle(value);
    };

    // En-tête de la section "Cul-de-sac" : titre + toggle d'activation, dans le
    // slot `header` du InfoSectionFoldout natif (même famille que le chevron du
    // panneau lui-même). Le toggle stoppe la propagation du clic pour ne jamais
    // déplier/replier la section quand on veut juste l'activer/désactiver.
    const culDeSacFoldoutHeader = (
        <div className={styles.foldoutHeaderRow}>
            <span>{translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}</span>
            <span
                className={styles.foldoutHeaderToggle}
                onMouseDown={(event) => event.stopPropagation()}
                onClick={(event) => event.stopPropagation()}>
                <VC.ToggleField
                    value={culDeSacMode}
                    disabled={false}
                    onChange={(value: boolean) => setCulDeSacMode(value)}
                />
            </span>
        </div>
    );

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

    // Action : bouton primaire natif custom, passé au slot `footer` du Panel natif
    // (hors de la zone de contenu défilante des sections) pour qu'il reste visible
    // même si le panneau scrolle. Pas de bouton "Tout annuler" — redondant avec
    // Échap et le clic droit.
    const footer = !collapsed && (
        <div className={styles.actions}>
            <Button variant="primary" className={styles.applyButton} disabled={!canApply} onSelect={generateGrid}>
                {translate("GridRoadGenerator.UI.Generate", "Generate")}
            </Button>
            <span className={styles.generateHint}>
                {translate(
                    "GridRoadGenerator.UI.GenerateHint",
                    "Double-click a node to auto-select the whole perimeter.",
                )}
            </span>
        </div>
    );

    return (
        <div
            ref={panelRef}
            className={styles.panelWrapper}
            style={{ left: `${panelPosition.x}px`, top: `${panelPosition.y}px` }}>
            <Panel header={header} footer={footer} onClose={toggleTool} className={styles.panel}>
                {!collapsed && (
                    <>
                        {/* Géométrie : mode, colonnes/lignes/espacement, angle, suivi du terrain. */}
                        <InfoSectionFoldout
                            header={translate("GridRoadGenerator.UI.SectionGeometry", "Geometry")}
                            initialExpanded>
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
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.FollowTerrain", "Follow terrain")}
                                right={
                                    <VC.ToggleField
                                        value={followTerrain}
                                        disabled={false}
                                        onChange={(value: boolean) => setFollowTerrain(value)}
                                    />
                                }
                            />
                        </InfoSectionFoldout>

                        {/* Culs-de-sac : quartier pavillonnaire. Toggle d'activation dans
                            l'en-tête de section ; le reste des contrôles reste visible mais
                            grisé quand il est désactivé (comme avant), pas masqué. */}
                        <InfoSectionFoldout header={culDeSacFoldoutHeader} initialExpanded>
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
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.CulDeSacCapSize", "Turnaround size")}
                                right={
                                    <VC.DropdownField
                                        items={capSizeItems}
                                        value={culDeSacCapSize}
                                        disabled={!culDeSacMode}
                                        onChange={(value: number) => setCulDeSacCapSize(value)}
                                    />
                                }
                            />
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.CulDeSacCapStyle", "Turnaround style")}
                                right={
                                    <VC.DropdownField
                                        items={capStyleItems}
                                        value={culDeSacCapStyle}
                                        disabled={!culDeSacMode}
                                        onChange={handleCapStyleChange}
                                    />
                                }
                            />
                        </InfoSectionFoldout>

                        {/* Variation organique : jitter des lignes internes (chantiers
                            suivants : courbure des collectrices, orientation par bloc). */}
                        <InfoSectionFoldout
                            header={translate("GridRoadGenerator.UI.SectionOrganic", "Organic variation")}
                            initialExpanded>
                            <div className={styles.vanillaRow}>
                                <div className={styles.vanillaField}>
                                    <VC.FloatSliderField
                                        label={translate("GridRoadGenerator.UI.JitterAmount", "Jitter")}
                                        value={jitterAmount}
                                        min={0}
                                        max={15}
                                        fractionDigits={1}
                                        disabled={false}
                                        onChange={(value: number) => setJitterAmount(value)}
                                    />
                                    <span className={styles.unitLabel}>m</span>
                                </div>
                            </div>
                            <Button variant="flat" className={styles.reseedButton} onSelect={regenerateJitterSeed}>
                                {translate("GridRoadGenerator.UI.JitterReseedButton", "New seed")}
                            </Button>
                        </InfoSectionFoldout>

                        {/* Sélection en cours + réseau utilisé. */}
                        <InfoSectionFoldout
                            header={translate("GridRoadGenerator.UI.SectionSelection", "Selection")}
                            initialExpanded>
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
                        </InfoSectionFoldout>
                    </>
                )}
            </Panel>

            {pickerOpen && <PrefabPicker onClose={() => setPickerOpen(false)} />}
        </div>
    );
};
