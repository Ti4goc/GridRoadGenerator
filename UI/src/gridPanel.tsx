// Structure de panneau et intégration des composants vanilla adaptées de
// CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React, { useRef, useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import styles from "./gridPanel.module.scss";
import { locString } from "./locHelpers";
import { RoadSelection } from "./roadSelection";
import { SafeButton } from "./safeButton";
import { VC, VF, VT } from "./vanilla";
import { ViewSelection } from "./viewSelection";
import {
    adaptiveMode$,
    adaptiveRoundedCorners$,
    anarchyAvailable$,
    anarchyEnabled$,
    angleOffset$,
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

// ------------------------------------------------------------------
// Repli par section : ce panneau évite volontairement toute la famille
// Panel/InfoRow/InfoSection/InfoSectionFoldout de cs2/ui (c'est justement
// pourquoi il existe — filet de secours si cette famille est absente au
// runtime, voir gridPanelSwitch.tsx), donc pas de foldout natif disponible
// ici. Chevron manuel reprenant la même icône vanilla que le repli du
// panneau natif, pour un look cohérent entre les deux panneaux.
// ------------------------------------------------------------------

type SectionFoldoutProps = {
    title: React.ReactNode;
    /// Contenu additionnel dans l'en-tête (ex. toggle d'activation) ; stoppe
    /// lui-même la propagation du clic pour ne jamais déplier/replier la
    /// section quand on veut juste l'actionner.
    headerExtra?: React.ReactNode;
    expanded: boolean;
    onToggle: () => void;
    /// Vrai si la section est verrouillée fermée (ex. Cul-de-sac quand le mode est
    /// désactivé) : en-tête grisé, curseur par défaut. onToggle reste appelé au clic
    /// mais l'appelant l'a déjà rendu no-op dans ce cas ; purement visuel ici.
    locked?: boolean;
    children: React.ReactNode;
};

const SectionFoldout = ({ title, headerExtra, expanded, onToggle, locked, children }: SectionFoldoutProps) => (
    <div className={styles.foldout}>
        <div
            className={locked ? `${styles.foldoutHeader} ${styles.foldoutHeaderLocked}` : styles.foldoutHeader}
            onClick={onToggle}>
            <img
                src="Media/Glyphs/ThickStrokeArrowDown.svg"
                className={expanded ? styles.foldoutChevron : styles.foldoutChevronCollapsed}
            />
            <span className={styles.foldoutTitle}>{title}</span>
            {headerExtra && (
                <span
                    className={styles.foldoutHeaderExtra}
                    onMouseDown={(event) => event.stopPropagation()}
                    onClick={(event) => event.stopPropagation()}>
                    {headerExtra}
                </span>
            )}
        </div>
        {expanded && <div className={styles.foldoutBody}>{children}</div>}
    </div>
);

/// Panneau custom éprouvé (fonctionne en jeu, confirmé). Utilisé directement si
/// le chrome InfoView natif n'est pas disponible/sûr — voir gridPanelSwitch.tsx.
export const LegacyGridPanel = () => {
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
    const anarchyAvailable = useValue(anarchyAvailable$);
    const anarchyEnabled = useValue(anarchyEnabled$);
    const [panelPosition, setPanelPosition] = useState<PanelPosition>(loadPanelPosition);
    const panelRef = useRef<HTMLDivElement>(null);
    // Seule la géométrie s'ouvre par défaut ; les autres sections restent repliées
    // tant que le joueur ne les déplie pas explicitement.
    const [geometryExpanded, setGeometryExpanded] = useState(true);
    const [culDeSacExpanded, setCulDeSacExpanded] = useState(false);
    const [adaptiveExpanded, setAdaptiveExpanded] = useState(false);
    const [selectionExpanded, setSelectionExpanded] = useState(false);

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

    // Taille et style du cercle de retournement : voir le commentaire équivalent
    // dans gridPanelNative.tsx (même logique, panneau custom éprouvé ici).
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

    return (
        <div
            ref={panelRef}
            className={styles.panelWrapper}
            style={{ left: `${panelPosition.x}px`, top: `${panelPosition.y}px` }}>
            <div className={styles.panel}>
                <div className={styles.header} onMouseDown={startDrag}>
                    <span className={styles.headerTitle}>
                        {translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}
                    </span>
                    {/* Anarchy (mod tiers optionnel) : toujours visible dans l'en-tête, pas
                        enterré dans une section repliable. État et toggle passent par les
                        bindings d'Anarchy lui-même, donc synchronisés avec son bouton
                        toolbar et son raccourci. */}
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
                </div>

                <div className={styles.content}>
                  <div className={styles.scrollWrapper}>
                    <VC.Scrollable>
                    {/* "Vista" et "Route" : pas des sections repliables (voir viewSelection.tsx
                        et roadSelection.tsx), toujours visibles en haut, avant la première section. */}
                    <ViewSelection />
                    <RoadSelection portalContainer={panelRef.current} />

                    {/* Géométrie : mode, colonnes/lignes/espacement, angle, suivi du terrain. */}
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionGeometry", "Geometry")}
                        expanded={geometryExpanded}
                        onToggle={() => setGeometryExpanded((value) => !value)}>
                        <div className={styles.vanillaRow}>
                            <VC.Section
                                focusKey={VF.FOCUS_DISABLED}
                                title={translate("GridRoadGenerator.UI.Mode", "Mode")}>
                                <VC.ToolButton
                                    src="Media/Tools/Snap Options/ZoneGrid.svg"
                                    selected={fitMode}
                                    multiSelect={false}
                                    disabled={adaptiveMode}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                                    onSelect={() => setMode(MODE_FIT)}
                                    className={VT.toolButton.button}
                                />
                                <VC.ToolButton
                                    src="Media/Glyphs/Length.svg"
                                    selected={!fitMode}
                                    multiSelect={false}
                                    disabled={adaptiveMode}
                                    focusKey={VF.FOCUS_DISABLED}
                                    tooltip={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")}
                                    onSelect={() => setMode(MODE_FIXED)}
                                    className={VT.toolButton.button}
                                />
                            </VC.Section>
                        </div>
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
                        <div className={styles.vanillaRow}>
                            <VC.Section
                                focusKey={VF.FOCUS_DISABLED}
                                title={translate("GridRoadGenerator.UI.FollowTerrain", "Follow terrain")}>
                                <VC.ToggleField
                                    value={followTerrain}
                                    disabled={false}
                                    onChange={(value: boolean) => setFollowTerrain(value)}
                                />
                            </VC.Section>
                        </div>
                    </SectionFoldout>

                    {/* Culs-de-sac : quartier pavillonnaire (collectrices traversantes,
                        résidentielles en impasse). Toggle d'activation dans l'en-tête de
                        section ; sliders et champs restent visibles mais grisés tant que
                        le mode est désactivé (comme avant), jamais masqués. */}
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                        headerExtra={
                            <VC.ToggleField
                                value={culDeSacMode}
                                disabled={false}
                                onChange={(value: boolean) => setCulDeSacMode(value)}
                            />
                        }
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
                        {/* Axe et alternance haut/bas : concepts propres à la grille de lignes
                            droites, sans équivalent pour les rayons du mode Adaptativo. */}
                        <div className={styles.vanillaRow}>
                            <VC.Section
                                focusKey={VF.FOCUS_DISABLED}
                                title={translate("GridRoadGenerator.UI.CulDeSacAxis", "Axis")}>
                                <VC.DropdownField
                                    items={culDeSacAxisItems}
                                    value={culDeSacAxis}
                                    disabled={!culDeSacMode || adaptiveMode}
                                    onChange={(value: number) => setCulDeSacAxis(value)}
                                />
                            </VC.Section>
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
                                    disabled={!culDeSacMode || adaptiveMode}
                                    onChange={(value: boolean) => setStaggered(value)}
                                />
                            </VC.Section>
                        </div>
                        <div className={styles.vanillaRow}>
                            <VC.Section
                                focusKey={VF.FOCUS_DISABLED}
                                title={translate("GridRoadGenerator.UI.CulDeSacCapSize", "Turnaround size")}>
                                <VC.DropdownField
                                    items={capSizeItems}
                                    value={culDeSacCapSize}
                                    disabled={!culDeSacMode}
                                    onChange={(value: number) => setCulDeSacCapSize(value)}
                                />
                            </VC.Section>
                        </div>
                        <div className={styles.vanillaRow}>
                            <VC.Section
                                focusKey={VF.FOCUS_DISABLED}
                                title={translate("GridRoadGenerator.UI.CulDeSacCapStyle", "Turnaround style")}>
                                <VC.DropdownField
                                    items={capStyleItems}
                                    value={culDeSacCapStyle}
                                    disabled={!culDeSacMode}
                                    onChange={handleCapStyleChange}
                                />
                            </VC.Section>
                        </div>
                    </SectionFoldout>

                    {/* Adaptativo : anneaux concentriques par offset du périmètre, en
                        remplacement de la grille de lignes droites (voir GenerateAdaptiveGrid).
                        Réutilise le slider Espaçamento (section Géométrie) comme distance entre
                        deux anneaux. */}
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionAdaptive", "Adaptive")}
                        headerExtra={
                            <VC.ToggleField
                                value={adaptiveMode}
                                disabled={false}
                                onChange={(value: boolean) => setAdaptiveMode(value)}
                            />
                        }
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
                        <div className={styles.vanillaRow}>
                            <VC.Section
                                focusKey={VF.FOCUS_DISABLED}
                                title={translate("GridRoadGenerator.UI.AdaptiveRoundedCorners", "Rounded corners")}>
                                <VC.ToggleField
                                    value={adaptiveRoundedCorners}
                                    disabled={!adaptiveMode}
                                    onChange={(value: boolean) => setAdaptiveRoundedCorners(value)}
                                />
                            </VC.Section>
                        </div>
                    </SectionFoldout>

                    {/* Sélection en cours (réseau utilisé : voir la barre permanente "Route" ci-dessus). */}
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionSelection", "Selection")}
                        expanded={selectionExpanded}
                        onToggle={() => setSelectionExpanded((value) => !value)}>
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
                    </SectionFoldout>
                    </VC.Scrollable>
                  </div>
                </div>

                {/* Action : bouton primaire natif, hors de la zone défilante ci-dessus
                    (.content a sa propre zone défilante, VC.Scrollable natif) pour rester
                    toujours visible. Pas de bouton "Tout annuler" — redondant avec Échap
                    (vide la sélection) et le clic droit (retire le dernier nœud), déjà bien
                    plus rapides d'accès. */}
                <div className={styles.actions}>
                    <SafeButton
                        variant="primary"
                        className={styles.applyButton}
                        disabled={!canApply}
                        onSelect={generateGrid}>
                        {translate("GridRoadGenerator.UI.Generate", "Generate")}
                    </SafeButton>
                    <span className={styles.generateHint}>
                        {translate(
                            "GridRoadGenerator.UI.GenerateHint",
                            "Double-click a node to auto-select the whole perimeter.",
                        )}
                    </span>
                </div>
            </div>
        </div>
    );
};
