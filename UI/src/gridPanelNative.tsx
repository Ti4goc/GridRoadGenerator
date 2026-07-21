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
import { InfoRow, InfoSection, Panel } from "cs2/ui";
import styles from "./gridPanelNative.module.scss";
import gridIcon from "./gridIcon.svg";
import { locString } from "./locHelpers";
import { RoadSelection } from "./roadSelection";
import { SafeButton } from "./safeButton";
import { SelectionRow } from "./selectionRow";
import { VC, VF, VT } from "./vanilla";
import { ViewSelection } from "./viewSelection";
import {
    adaptiveMode$,
    adaptiveRoundedCorners$,
    anarchyAvailable$,
    anarchyEnabled$,
    angleOffset$,
    avenueColumnEnabled$,
    avenueColumnIndex$,
    avenueRowEnabled$,
    avenueRowIndex$,
    canApply$,
    columns$,
    culDeSacAxis$,
    culDeSacCapSize$,
    culDeSacCapStyle$,
    culDeSacDepth$,
    culDeSacMode$,
    culDeSacRatio$,
    followTerrain$,
    generateGrid,
    mode$,
    nodeCount$,
    perimeterInvalid$,
    radialConnections$,
    rows$,
    setAdaptiveMode,
    setAdaptiveRoundedCorners,
    setAngleOffset,
    setAvenueColumnEnabled,
    setAvenueColumnIndex,
    setAvenueRowEnabled,
    setAvenueRowIndex,
    setColumns,
    setCulDeSacAxis,
    setCulDeSacCapSize,
    setCulDeSacCapStyle,
    setCulDeSacDepth,
    setCulDeSacMode,
    setCulDeSacRatio,
    setFollowTerrain,
    setMode,
    setRadialConnections,
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

const CULDESAC_AXIS_COLUMNS = 0;
const CULDESAC_AXIS_ROWS = 1;
const CULDESAC_AXIS_BOTH = 2;

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

// ------------------------------------------------------------------
// Repli par section : InfoSectionFoldout (cs2/ui) a provoqué un crash vécu en
// jeu ("l'UI disparaît en cliquant sur le bouton du mod") — il n'existe pas de
// façon fiable au runtime malgré sa présence dans les types, exactement le
// risque déjà documenté dans gridPanelSwitch.tsx pour Panel/InfoRow/InfoSection
// (jamais étendu à InfoSectionFoldout, ajouté plus tard). Repli sur un chevron
// composé à la main (même mécanisme que gridPanel.tsx), mais construit à partir
// d'InfoSection/InfoRow — déjà garantis par le garde statique de
// gridPanelSwitch.tsx — plutôt que des divs nues, pour rester dans le style
// InfoView natif.
// ------------------------------------------------------------------

type NativeSectionFoldoutProps = {
    title: React.ReactNode;
    /// Contenu additionnel dans l'en-tête (ex. toggle d'activation) ; stoppe
    /// lui-même la propagation du clic pour ne jamais déplier/replier la
    /// section quand on veut juste l'actionner.
    headerExtra?: React.ReactNode;
    expanded: boolean;
    onToggle: () => void;
    /// Vrai si la section est verrouillée fermée (ex. Cul-de-sac quand le mode est
    /// désactivé) : chevron/titre grisés, curseur par défaut. onToggle reste appelé au
    /// clic mais l'appelant l'a déjà rendu no-op dans ce cas ; purement visuel ici.
    locked?: boolean;
    children: React.ReactNode;
};

const NativeSectionFoldout = ({ title, headerExtra, expanded, onToggle, locked, children }: NativeSectionFoldoutProps) => (
    <InfoSection>
        <InfoRow
            uppercase
            left={
                <span
                    className={locked ? `${styles.foldoutTitleRow} ${styles.foldoutTitleRowLocked}` : styles.foldoutTitleRow}
                    onClick={onToggle}>
                    <img
                        src="Media/Glyphs/ThickStrokeArrowDown.svg"
                        className={expanded ? styles.foldoutChevron : styles.foldoutChevronCollapsed}
                    />
                    <span>{title}</span>
                </span>
            }
            right={
                headerExtra && (
                    <span
                        onMouseDown={(event) => event.stopPropagation()}
                        onClick={(event) => event.stopPropagation()}>
                        {headerExtra}
                    </span>
                )
            }
        />
        {expanded && children}
    </InfoSection>
);

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
    const culDeSacAxis = useValue(culDeSacAxis$);
    const culDeSacDepth = useValue(culDeSacDepth$);
    const staggered = useValue(staggered$);
    const culDeSacRatio = useValue(culDeSacRatio$);
    const culDeSacCapSize = useValue(culDeSacCapSize$);
    const culDeSacCapStyle = useValue(culDeSacCapStyle$);
    const adaptiveMode = useValue(adaptiveMode$);
    const radialConnections = useValue(radialConnections$);
    const adaptiveRoundedCorners = useValue(adaptiveRoundedCorners$);
    const avenueColumnEnabled = useValue(avenueColumnEnabled$);
    const avenueColumnIndex = useValue(avenueColumnIndex$);
    const avenueRowEnabled = useValue(avenueRowEnabled$);
    const avenueRowIndex = useValue(avenueRowIndex$);
    const anarchyAvailable = useValue(anarchyAvailable$);
    const anarchyEnabled = useValue(anarchyEnabled$);
    // Replié : garde l'outil actif (seule la fermeture via le X le désactive).
    const [collapsed, setCollapsed] = useState(false);
    const [panelPosition, setPanelPosition] = useState<PanelPosition>(loadPanelPosition);
    const panelRef = useRef<HTMLDivElement>(null);
    // Seule la géométrie s'ouvre par défaut ; les autres sections restent repliées
    // tant que le joueur ne les déplie pas explicitement.
    const [geometryExpanded, setGeometryExpanded] = useState(true);
    const [culDeSacExpanded, setCulDeSacExpanded] = useState(false);
    const [avenueExpanded, setAvenueExpanded] = useState(false);
    const [adaptiveExpanded, setAdaptiveExpanded] = useState(false);

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
    const culDeSacAxisItems = [
        { value: CULDESAC_AXIS_COLUMNS, displayName: loc("GridRoadGenerator.UI.CulDeSacAxisColumns", "Columns") },
        { value: CULDESAC_AXIS_ROWS, displayName: loc("GridRoadGenerator.UI.CulDeSacAxisRows", "Rows") },
        { value: CULDESAC_AXIS_BOTH, displayName: loc("GridRoadGenerator.UI.CulDeSacAxisBoth", "Both") },
    ];
    const handleCapStyleChange = (value: number) => {
        if (value === CAP_STYLE_ASPHALT && culDeSacCapSize === CAP_SIZE_XL) {
            setCulDeSacCapSize(CAP_SIZE_LARGE);
        }
        setCulDeSacCapStyle(value);
    };

    // Toggle d'activation dans l'en-tête de la section "Cul-de-sac" (headerExtra
    // de NativeSectionFoldout, qui stoppe déjà lui-même la propagation du clic). Reste
    // actif en mode Adaptativo : les impasses s'appliquent alors aux rayons plutôt
    // qu'aux colonnes (voir GenerateAdaptiveGrid/EmitRadialConnections) — seuls Axe et
    // Alternance (ci-dessous) n'ont pas d'équivalent pour des rayons.
    const culDeSacToggle = (
        <VC.ToggleField
            value={culDeSacMode}
            disabled={false}
            onChange={(value: boolean) => setCulDeSacMode(value)}
        />
    );

    // Toggle d'activation dans l'en-tête de la section "Adaptativo".
    const adaptiveToggle = (
        <VC.ToggleField
            value={adaptiveMode}
            disabled={false}
            onChange={(value: boolean) => setAdaptiveMode(value)}
        />
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
            {/* Anarchy (mod tiers optionnel) : toujours visible dans l'en-tête, pas
                enterré dans une section repliable. État et toggle passent par les
                bindings d'Anarchy lui-même, donc synchronisés avec son bouton toolbar
                et son raccourci. */}
            {anarchyAvailable && (
                <span
                    className={styles.headerAnarchy}
                    onMouseDown={(event) => event.stopPropagation()}
                    onClick={(event) => event.stopPropagation()}>
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
                </span>
            )}
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
            <SafeButton variant="primary" className={styles.applyButton} disabled={!canApply} onSelect={generateGrid}>
                {translate("GridRoadGenerator.UI.Generate", "Generate")}
            </SafeButton>
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
                        {/* "Vista", "Route" et "Seleção" : pas des sections repliables (voir
                            viewSelection.tsx/roadSelection.tsx/selectionRow.tsx), toujours
                            visibles en haut, avant la première section repliable. */}
                        <ViewSelection />
                        <RoadSelection portalContainer={panelRef.current} />
                        <SelectionRow nodeCount={nodeCount} perimeterInvalid={perimeterInvalid} />

                        {/* Géométrie : mode, colonnes/lignes/espacement, angle, suivi du terrain. */}
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.SectionGeometry", "Geometry")}
                            expanded={geometryExpanded}
                            onToggle={() => setGeometryExpanded((value) => !value)}>
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                                right={
                                    <VC.ToolButton
                                        src="Media/Tools/Snap Options/ZoneGrid.svg"
                                        selected={fitMode}
                                        multiSelect={false}
                                        disabled={adaptiveMode}
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
                                        disabled={adaptiveMode}
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
                                        disabled={!fitMode || adaptiveMode}
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
                                        disabled={!fitMode || adaptiveMode}
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
                                        disabled={fitMode && !adaptiveMode}
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
                                        disabled={adaptiveMode}
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
                        </NativeSectionFoldout>

                        {/* Culs-de-sac : quartier pavillonnaire. Toggle d'activation dans
                            l'en-tête de section ; le reste des contrôles reste visible mais
                            grisé quand il est désactivé (comme avant), pas masqué. */}
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                            headerExtra={culDeSacToggle}
                            expanded={culDeSacMode && culDeSacExpanded}
                            onToggle={() => culDeSacMode && setCulDeSacExpanded((value) => !value)}
                            locked={!culDeSacMode}>
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
                            {/* Axe et alternance haut/bas : concepts propres à la grille de
                                lignes droites, sans équivalent pour les rayons du mode Adaptativo. */}
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.CulDeSacAxis", "Axis")}
                                right={
                                    <VC.DropdownField
                                        items={culDeSacAxisItems}
                                        value={culDeSacAxis}
                                        disabled={!culDeSacMode || adaptiveMode}
                                        onChange={(value: number) => setCulDeSacAxis(value)}
                                    />
                                }
                            />
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
                                        disabled={!culDeSacMode || adaptiveMode}
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
                        </NativeSectionFoldout>

                        {/* Avenue : troisième réseau, colonne et/ou rangée choisie librement par
                            index (grille de lignes droites uniquement — sans effet en mode
                            Adaptativo, section verrouillée fermée dans ce cas). Une rotonde est
                            ajoutée automatiquement côté Core si les deux axes sont actifs (voir
                            EmitAvenueRoundabout). */}
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.SectionAvenue", "Avenue")}
                            expanded={avenueExpanded && !adaptiveMode}
                            onToggle={() => !adaptiveMode && setAvenueExpanded((value) => !value)}
                            locked={adaptiveMode}>
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.AvenueColumn", "Avenue column")}
                                right={
                                    <VC.ToggleField
                                        value={avenueColumnEnabled}
                                        disabled={adaptiveMode}
                                        onChange={(value: boolean) => setAvenueColumnEnabled(value)}
                                    />
                                }
                            />
                            <div className={styles.vanillaRow}>
                                <div className={styles.vanillaField}>
                                    <VC.IntSliderField
                                        label={translate("GridRoadGenerator.UI.AvenueIndex", "Index")}
                                        value={avenueColumnIndex}
                                        min={0}
                                        max={23}
                                        disabled={adaptiveMode || !avenueColumnEnabled}
                                        onChange={(value: number) => setAvenueColumnIndex(Math.round(value))}
                                    />
                                </div>
                            </div>
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.AvenueRow", "Avenue row")}
                                right={
                                    <VC.ToggleField
                                        value={avenueRowEnabled}
                                        disabled={adaptiveMode}
                                        onChange={(value: boolean) => setAvenueRowEnabled(value)}
                                    />
                                }
                            />
                            <div className={styles.vanillaRow}>
                                <div className={styles.vanillaField}>
                                    <VC.IntSliderField
                                        label={translate("GridRoadGenerator.UI.AvenueIndex", "Index")}
                                        value={avenueRowIndex}
                                        min={0}
                                        max={23}
                                        disabled={adaptiveMode || !avenueRowEnabled}
                                        onChange={(value: number) => setAvenueRowIndex(Math.round(value))}
                                    />
                                </div>
                            </div>
                        </NativeSectionFoldout>

                        {/* Adaptativo : anneaux concentriques par offset du périmètre, en
                            remplacement de la grille de lignes droites (voir
                            GenerateAdaptiveGrid). Réutilise le slider Espaçamento (section
                            Géométrie) comme distance entre deux anneaux. */}
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.SectionAdaptive", "Adaptive")}
                            headerExtra={adaptiveToggle}
                            expanded={adaptiveMode && adaptiveExpanded}
                            onToggle={() => adaptiveMode && setAdaptiveExpanded((value) => !value)}
                            locked={!adaptiveMode}>
                            <div className={styles.vanillaRow}>
                                <div className={styles.vanillaField}>
                                    <VC.IntSliderField
                                        label={translate("GridRoadGenerator.UI.RadialConnections", "Radial connections")}
                                        value={radialConnections}
                                        min={0}
                                        max={24}
                                        disabled={!adaptiveMode}
                                        onChange={(value: number) => setRadialConnections(Math.round(value))}
                                    />
                                </div>
                            </div>
                            <InfoRow
                                left={translate("GridRoadGenerator.UI.AdaptiveRoundedCorners", "Rounded corners")}
                                right={
                                    <VC.ToggleField
                                        value={adaptiveRoundedCorners}
                                        disabled={!adaptiveMode}
                                        onChange={(value: boolean) => setAdaptiveRoundedCorners(value)}
                                    />
                                }
                            />
                        </NativeSectionFoldout>
                    </>
                )}
            </Panel>
        </div>
    );
};
