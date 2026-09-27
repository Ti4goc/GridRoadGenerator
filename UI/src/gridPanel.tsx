// Structure de panneau et intégration des composants vanilla adaptées de
// CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React, { useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import styles from "./gridPanel.module.scss";
import { locString } from "./locHelpers";
import { PrefabPicker } from "./prefabPicker";
import { RoadSelection } from "./roadSelection";
import { SafeButton } from "./safeButton";
import { SelectionRow } from "./selectionRow";
import { VC, VF, VT } from "./vanilla";
import { ViewSelection } from "./viewSelection";
import patternGrid from "./patternGrid.svg";
import patternLoop from "./patternLoop.svg";
import patternSuperblock from "./patternSuperblock.svg";
import patternConcentric from "./patternConcentric.svg";
import gridIcon from "./gridIcon.svg";
import iconColumns from "./iconColumns.svg";
import iconRows from "./iconRows.svg";
import iconCollectorSpacing from "./iconCollectorSpacing.svg";
import iconCulDeSacFrequency from "./iconCulDeSacFrequency.svg";
import iconAngle from "./iconAngle.svg";
import iconTerrain from "./iconTerrain.svg";
import iconCulDeSacDepth from "./iconCulDeSacDepth.svg";
import iconAxis from "./iconAxis.svg";
import iconFrequency from "./iconFrequency.svg";
import iconStaggered from "./iconStaggered.svg";
import iconAvenueColumn from "./iconAvenueColumn.svg";
import iconAvenueRow from "./iconAvenueRow.svg";
import iconCircleOutline from "./iconCircleOutline.svg";
import iconStyleAsphalt from "./iconStyleAsphalt.svg";
import iconStyleGrass from "./iconStyleGrass.svg";
import iconStyleTrees from "./iconStyleTrees.svg";
import {
    anarchyAvailable$,
    anarchyEnabled$,
    angleOffset$,
    avenueBikeLaneLeft$,
    avenueBikeLaneRight$,
    avenueColumnEnabled$,
    avenueColumnIndex$,
    avenueMiddleGrass$,
    avenueMiddleTrees$,
    avenueRoadPrefabIcon$,
    avenueRoadPrefabName$,
    avenueRowEnabled$,
    avenueRowIndex$,
    avenueSideTreesLeft$,
    avenueSideTreesRight$,
    canApply$,
    clearLivePreview,
    collectorSpacing$,
    columns$,
    culDeSacAxis$,
    culDeSacCapSize$,
    culDeSacCapStyle$,
    culDeSacDepth$,
    culDeSacMode$,
    culDeSacRatio$,
    followTerrain$,
    generateGrid,
    LiveField,
    loopCulDeSacRatio$,
    loopMode$,
    mode$,
    nodeCount$,
    perimeterCollision$,
    perimeterInvalid$,
    principalBikeLaneLeft$,
    principalBikeLaneRight$,
    principalSideTreesLeft$,
    principalSideTreesRight$,
    principalWideSidewalkLeft$,
    principalWideSidewalkRight$,
    roadPrefabIcon$,
    roadPrefabName$,
    rows$,
    secondaryRoadPrefabIcon$,
    secondaryRoadPrefabName$,
    setAngleOffset,
    resetDefaults,
    setAvenueBikeLaneLeft,
    setAvenueBikeLaneRight,
    setAvenueColumnEnabled,
    setAvenueColumnIndex,
    setAvenueMiddleGrass,
    setAvenueMiddleTrees,
    setAvenueRowEnabled,
    setAvenueRowIndex,
    setAvenueSideTreesLeft,
    setAvenueSideTreesRight,
    setCollectorSpacing,
    setSuperblockZone,
    superblockZone$,
    concentricMode$,
    concentricLayers$,
    concentricConnections$,
    concentricMaxLayers$,
    setConcentricMode,
    setConcentricLayers,
    setConcentricConnections,
    setColumns,
    setCulDeSacAxis,
    setCulDeSacCapSize,
    setCulDeSacCapStyle,
    setCulDeSacDepth,
    setCulDeSacMode,
    setCulDeSacRatio,
    setFollowTerrain,
    setLoopCulDeSacRatio,
    setLoopMode,
    setMode,
    setPrincipalBikeLaneLeft,
    setPrincipalBikeLaneRight,
    setPrincipalSideTreesLeft,
    setPrincipalSideTreesRight,
    setPrincipalWideSidewalkLeft,
    setPrincipalWideSidewalkRight,
    setLivePreview,
    setRows,
    setSpacing,
    setStaggered,
    setSuperblockMode,
    spacing$,
    staggered$,
    superblockMode$,
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
            <span className={styles.foldoutTitle}>{title}</span>
            {headerExtra && (
                <span
                    className={styles.foldoutHeaderExtra}
                    onMouseDown={(event) => event.stopPropagation()}
                    onClick={(event) => event.stopPropagation()}>
                    {headerExtra}
                </span>
            )}
            <img
                src="Media/Glyphs/ThickStrokeArrowDown.svg"
                className={expanded ? styles.foldoutChevron : styles.foldoutChevronCollapsed}
            />
        </div>
        {expanded && <div className={styles.foldoutBody}>{children}</div>}
    </div>
);

// ------------------------------------------------------------------
// Contrôles entièrement custom (pas les widgets vanilla de l'éditeur) : calqués
// sur le mockup "Seletor segmentado (ícones)" approuvé — piste pleine largeur
// avec curseur glissable pour les sliders, piste+curseur pour les interrupteurs,
// gros boutons icône+libellé empilés pour Padrão. Glisser-déposer manuel
// (mousemove/mouseup sur window), même recette que le drag du panneau lui-même
// (voir startDrag plus bas dans LegacyGridPanel).
// ------------------------------------------------------------------

type SliderControlProps = {
    label: React.ReactNode;
    value: number;
    min: number;
    max: number;
    step?: number;
    unit?: string;
    disabled?: boolean;
    onChange: (value: number) => void;
    /// Croquis léger (voir GridRoadOverlaySystem.DrawLiveSpacingSketch) : appelé à CHAQUE
    /// pixel parcouru pendant le drag (onChange, lui, n'est appelé qu'au relâchement) — permet
    /// un retour visuel en direct sans jamais régénérer la vraie grille pendant le drag.
    /// Optionnel : seul le slider Espaçamento le branche pour l'instant.
    onDragPreview?: (value: number) => void;
    onDragEnd?: () => void;
};

const SliderControl = ({ label, value, min, max, step = 1, unit, disabled, onChange, onDragPreview, onDragEnd }: SliderControlProps) => {
    const trackRef = useRef<HTMLDivElement>(null);
    // Valeur locale PENDANT le drag (voir startDrag) — retour utilisateur en jeu : régénérer
    // toute la grille (destruction + recréation des entités ECS de l'aperçu) à CHAQUE pixel
    // parcouru pendant un drag de slider est trop coûteux avec une grille dense (voir l'enquête
    // de performance complète). onChange (qui déclenche cette régénération côté C#) n'est donc
    // plus appelé qu'UNE fois, à la fin du drag (mouseup) — dragValue garde juste le retour
    // visuel local (piste/poignée/texte) fluide PENDANT le drag, sans toucher au binding tant
    // que le joueur n'a pas relâché.
    const [dragValue, setDragValue] = useState<number | null>(null);
    const displayValue = dragValue ?? value;
    const pct = ((displayValue - min) / (max - min)) * 100;

    const valueFromClientX = (clientX: number) => {
        const rect = trackRef.current?.getBoundingClientRect();
        if (!rect || rect.width === 0) {
            return displayValue;
        }
        const ratio = Math.min(Math.max((clientX - rect.left) / rect.width, 0), 1);
        const raw = min + ratio * (max - min);
        return Math.min(Math.max(Math.round(raw / step) * step, min), max);
    };

    const startDrag = (event: React.MouseEvent) => {
        if (disabled) {
            return;
        }
        const startValue = valueFromClientX(event.clientX);
        setDragValue(startValue);
        onDragPreview?.(startValue);
        const onMove = (ev: MouseEvent) => {
            const v = valueFromClientX(ev.clientX);
            setDragValue(v);
            onDragPreview?.(v);
        };
        const onUp = (ev: MouseEvent) => {
            window.removeEventListener("mousemove", onMove);
            window.removeEventListener("mouseup", onUp);
            onChange(valueFromClientX(ev.clientX));
            setDragValue(null);
            onDragEnd?.();
        };
        window.addEventListener("mousemove", onMove);
        window.addEventListener("mouseup", onUp);
    };

    return (
        <div className={disabled ? `${styles.sliderControl} ${styles.sliderControlDisabled}` : styles.sliderControl}>
            <div className={styles.sliderControlHeader}>
                <span className={styles.sliderControlLabel}>{label}</span>
                <span className={styles.sliderControlValue}>
                    {Math.round(displayValue)}
                    {unit ? ` ${unit}` : ""}
                </span>
            </div>
            <div ref={trackRef} className={styles.sliderControlTrack} onMouseDown={startDrag}>
                <div className={styles.sliderControlFill} style={{ width: `${pct}%` }} />
                <div className={styles.sliderControlHandle} style={{ left: `${pct}%` }} />
            </div>
        </div>
    );
};

/// Icône + libellé court côte à côte, remplace un SliderControl/ToggleRow purement textuel
/// (label: React.ReactNode). PAS icône seule + tooltip CSS ([data-tip]/content: attr(),
/// essayé puis abandonné — retour utilisateur en jeu : rien n'apparaît au survol, cohtml
/// (moteur CSS du jeu) ne supporte probablement pas content: attr(), même limite déjà vue
/// avec hsla() — voir webpack.config.js). Le texte reste donc toujours visible, l'icône n'est
/// qu'un repère visuel en plus, jamais le seul porteur du sens.
const RowIcon = ({ icon, text }: { icon: string; text: string }) => (
    <span className={styles.rowIconLabel}>
        <img src={icon} className={styles.rowIconGlyph} />
        {text}
    </span>
);

const ToggleControl = ({
    checked,
    disabled,
    onChange,
}: {
    checked: boolean;
    disabled?: boolean;
    onChange: (value: boolean) => void;
}) => (
    <button
        type="button"
        disabled={disabled}
        className={checked ? `${styles.toggleControl} ${styles.toggleControlOn}` : styles.toggleControl}
        onClick={() => onChange(!checked)}>
        <span className={styles.toggleControlKnob} />
    </button>
);

/// Rangée label + interrupteur (ex. "Seguir o terreno", "Coluna avenida").
const ToggleRow = ({
    label,
    checked,
    disabled,
    onChange,
}: {
    label: React.ReactNode;
    checked: boolean;
    disabled?: boolean;
    onChange: (value: boolean) => void;
}) => (
    <div className={styles.toggleRow}>
        <span className={styles.toggleRowLabel}>{label}</span>
        <ToggleControl checked={checked} disabled={disabled} onChange={onChange} />
    </div>
);

type PatternButtonProps = {
    icon: string;
    label: React.ReactNode;
    selected: boolean;
    locked?: boolean;
    soonLabel?: React.ReactNode;
    tooltip?: string | null;
    onSelect: () => void;
};

/// Bouton "Padrão" : icône + libellé empilés, dégradé violet à l'état sélectionné,
/// ruban "em breve" en coin pour Curva/Orgânico (pas encore implémentés).
const PatternButton = ({ icon, label, selected, locked, soonLabel, tooltip, onSelect }: PatternButtonProps) => (
    <button
        type="button"
        disabled={locked}
        title={tooltip ?? undefined}
        onClick={onSelect}
        className={[styles.patternButton, selected ? styles.patternButtonSelected : "", locked ? styles.patternButtonLocked : ""]
            .filter(Boolean)
            .join(" ")}>
        <img src={icon} className={styles.patternButtonIcon} />
        <span className={styles.patternButtonLabel}>{label}</span>
        {locked && soonLabel && <span className={styles.patternButtonSoon}>{soonLabel}</span>}
    </button>
);

/// Séparateurs (Avenida/Principal/Secundária) de la section Redes fusionnée — voir
/// LegacyGridPanel. Un seul onglet visible à la fois, plus compact que les 3 réseaux
/// empilés d'avant la fusion (Estrada+Redes).
const NetworkTabs = ({
    tabs,
    active,
    onSelect,
}: {
    tabs: React.ReactNode[];
    active: number;
    onSelect: (index: number) => void;
}) => (
    <div className={styles.networkTabs}>
        {tabs.map((label, index) => (
            <button
                key={index}
                type="button"
                className={index === active ? `${styles.networkTab} ${styles.networkTabActive}` : styles.networkTab}
                onClick={() => onSelect(index)}>
                {label}
            </button>
        ))}
    </div>
);

/// Rangée "Rede" en haut de chaque onglet (voir NetworkTabs) : le chip de choix de
/// prefab qui vivait dans l'ancienne section "Estrada" (toujours visible, hors Redes)
/// pour ce SEUL slot, fusionné ici — un déclencheur PrefabPicker(slot) + portail, même
/// principe que roadSelection.tsx mais un seul slot à la fois au lieu des 3 en rangée.
const NetworkPrefabRow = ({
    label,
    name,
    icon,
    onOpenPicker,
}: {
    label: React.ReactNode;
    name: string;
    icon: string;
    onOpenPicker: () => void;
}) => (
    <div className={styles.toggleRow}>
        <span className={styles.toggleRowLabel}>{label}</span>
        <button className={styles.networkPrefabButton} onClick={onOpenPicker}>
            {icon && <img src={icon} className={styles.networkPrefabIcon} />}
            <span className={styles.networkPrefabName}>{name || "—"}</span>
            <span className={styles.prefabChevron}>›</span>
        </button>
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
    const perimeterCollision = useValue(perimeterCollision$);
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
    const avenueColumnEnabled = useValue(avenueColumnEnabled$);
    const avenueColumnIndex = useValue(avenueColumnIndex$);
    const avenueRowEnabled = useValue(avenueRowEnabled$);
    const avenueRowIndex = useValue(avenueRowIndex$);
    const loopMode = useValue(loopMode$);
    const superblockMode = useValue(superblockMode$);
    const collectorSpacing = useValue(collectorSpacing$);
    const superblockZone = useValue(superblockZone$);
    const concentricMode = useValue(concentricMode$);
    const concentricLayers = useValue(concentricLayers$);
    const concentricConnections = useValue(concentricConnections$);
    const concentricMaxLayers = useValue(concentricMaxLayers$);    const loopCulDeSacRatio = useValue(loopCulDeSacRatio$);
    const avenueMiddleTrees = useValue(avenueMiddleTrees$);
    const avenueMiddleGrass = useValue(avenueMiddleGrass$);
    const avenueSideTreesLeft = useValue(avenueSideTreesLeft$);
    const avenueSideTreesRight = useValue(avenueSideTreesRight$);
    const avenueBikeLaneLeft = useValue(avenueBikeLaneLeft$);
    const avenueBikeLaneRight = useValue(avenueBikeLaneRight$);
    const principalSideTreesLeft = useValue(principalSideTreesLeft$);
    const principalSideTreesRight = useValue(principalSideTreesRight$);
    const principalWideSidewalkLeft = useValue(principalWideSidewalkLeft$);
    const principalWideSidewalkRight = useValue(principalWideSidewalkRight$);
    const principalBikeLaneLeft = useValue(principalBikeLaneLeft$);
    const principalBikeLaneRight = useValue(principalBikeLaneRight$);
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const avenueRoadPrefabName = useValue(avenueRoadPrefabName$);
    const avenueRoadPrefabIcon = useValue(avenueRoadPrefabIcon$);
    const secondaryRoadPrefabName = useValue(secondaryRoadPrefabName$);
    const secondaryRoadPrefabIcon = useValue(secondaryRoadPrefabIcon$);
    const anarchyAvailable = useValue(anarchyAvailable$);
    const anarchyEnabled = useValue(anarchyEnabled$);
    const [panelPosition, setPanelPosition] = useState<PanelPosition>(loadPanelPosition);
    const panelRef = useRef<HTMLDivElement>(null);
    // Seule la géométrie s'ouvre par défaut ; les autres sections restent repliées
    // tant que le joueur ne les déplie pas explicitement.
    const [geometryExpanded, setGeometryExpanded] = useState(true);
    const [culDeSacExpanded, setCulDeSacExpanded] = useState(false);
    const [avenueExpanded, setAvenueExpanded] = useState(false);
    const [networksExpanded, setNetworksExpanded] = useState(false);
    // Onglets Avenida/Principal/Secundária (fusion Estrada+Redes, option D approuvée) :
    // un seul déclencheur PrefabPicker à la fois, comme roadSelection.tsx (portail vers
    // panelRef, même raison : s'afficher hors de la zone défilante/overflow:hidden).
    const [networksTab, setNetworksTab] = useState(0);
    const [networksPickerOpen, setNetworksPickerOpen] = useState<"primary" | "secondary" | "avenue" | null>(null);
    const networkAssetName = (name: string) => (name ? (translate(`Assets.NAME[${name}]`, name) ?? name) : "");

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
                {/* Barre de titre calquée sur les panneaux du jeu (infoview) : icône à
                    gauche, titre centré, fermeture à droite, fond sombre du jeu. */}
                <div className={styles.header} onMouseDown={startDrag}>
                    <img src={gridIcon} className={styles.headerIcon} />
                    <span className={styles.headerTitle}>
                        {translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}
                    </span>
                    <div className={styles.headerSpacer} />
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
                    <span
                        className={styles.headerClose}
                        onMouseDown={(event) => event.stopPropagation()}
                        onClick={(event) => {
                            event.stopPropagation();
                            toggleTool();
                        }}>
                        <div className={styles.headerCloseIcon} style={{ maskImage: "url(Media/Glyphs/Close.svg)" }} />
                    </span>
                </div>

                <div className={styles.content}>
                  <div className={styles.scrollWrapper}>
                    <VC.Scrollable className={styles.scrollable}>
                    {/* Padrão : 3 types (Grelha classique / Loop / Super-quarteirão) — mod
                        simplifié au maximum, Curva/European/Fused Grid/Garden Suburb/Transit-
                        Oriented retirés (trop de modes peu fiables/jamais implémentés pour la
                        valeur qu'ils apportaient). loopMode+superblockMode pilotent directement
                        le générateur (voir GridRoadUISystem, SET_LOOP_MODE/SET_SUPERBLOCK_MODE) ;
                        Super-quarteirão n'est PAS un réglage indépendant à l'intérieur de Loop
                        (essayé d'abord comme interrupteur dans Geometria, retour utilisateur :
                        "tem que ser um novo modo como a grelha e o loop") — c'est loopMode=true
                        ET superblockMode=true ensemble, jamais superblockMode seul sans loopMode. */}
                    <div className={styles.patternRow}>
                        {/* Bouton "Repor valores" sur la ligne du titre Padrão : visible quel que
                            soit le motif (retour utilisateur : "o botão para voltar a pôr os
                            valores padrão, em todos os modos"). */}
                        <div className={styles.patternRowHeader}>
                            <span className={styles.patternRowLabel}>
                                {translate("GridRoadGenerator.UI.PatternLabel", "Pattern")}
                            </span>
                            <button className={styles.resetButton} onClick={() => resetDefaults()}>
                                <span className={styles.resetButtonGlyph}>↺</span>
                                {translate("GridRoadGenerator.UI.ResetDefaults", "Reset values")}
                            </button>
                        </div>
                        <div className={styles.patternButtons}>
                            <PatternButton
                                icon={patternGrid}
                                label={translate("GridRoadGenerator.UI.PatternGrid", "Grid")}
                                selected={!loopMode}
                                onSelect={() => {
                                    setLoopMode(false);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                }}
                            />
                            <PatternButton
                                icon={patternLoop}
                                label={translate("GridRoadGenerator.UI.PatternLoop", "Loop")}
                                selected={loopMode && !superblockMode && !concentricMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                }}
                            />
                            <PatternButton
                                icon={patternSuperblock}
                                label={translate("GridRoadGenerator.UI.PatternSuperblock", "Superblock")}
                                selected={loopMode && superblockMode && !concentricMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(true);
                                    setConcentricMode(false);
                                }}
                            />
                            {/* Concêntrico : même famille que Loop/Superblock (loopMode=true, voir
                                GridGenerator.GenerateLoopGrid qui délègue à ConcentricGenerator). */}
                            <PatternButton
                                icon={patternConcentric}
                                label={translate("GridRoadGenerator.UI.PatternConcentric", "Concentric")}
                                selected={loopMode && concentricMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(false);
                                    setConcentricMode(true);
                                }}
                            />
                        </div>
                    </div>

                    {/* "Vista" et "Seleção" : pas des sections repliables (voir
                        viewSelection.tsx/selectionRow.tsx), toujours visibles en haut, avant la
                        première section repliable. "Estrada" (roadSelection.tsx) affichée en
                        Grille classique uniquement (un seul réseau, pas de melhoramentos
                        automáticos) : en Loop, son contenu (3 chips de prefab) a été fusionné DANS
                        la section Redes (onglets Avenida/Principal/Secundária, voir plus bas) —
                        option D approuvée après comparaison de 5 esquisses. */}
                    <ViewSelection />
                    {!loopMode && <RoadSelection portalContainer={panelRef.current} />}
                    <SelectionRow nodeCount={nodeCount} perimeterInvalid={perimeterInvalid} />

                    {/* Géométrie : contenu dépend du Padrão sélectionné tout en haut — Colunas/
                        Linhas/Modo/Espaçamento n'ont de sens que pour la grille classique (voir
                        GenerateGrid) ; Espaçamento coletores/Frequência becos que pour Loop (voir
                        GenerateLoopGrid). Ângulo et Seguir o terreno s'appliquent aux DEUX (voir
                        BuildLocalFrame, utilisé par les deux générateurs), donc jamais masqués ni
                        grisés selon le Padrão. */}
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionGeometry", "Geometry")}
                        expanded={geometryExpanded}
                        onToggle={() => setGeometryExpanded((value) => !value)}>
                        {!loopMode && (
                            <>
                                <div className={styles.modeRow}>
                                    <span className={styles.modeRowLabel}>
                                        {translate("GridRoadGenerator.UI.Mode", "Mode")}
                                    </span>
                                    <div className={styles.modeButtons}>
                                        <VC.ToolButton
                                            src="Media/Tools/Snap Options/ZoneGrid.svg"
                                            selected={fitMode}
                                            multiSelect={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                                            onSelect={() => setMode(MODE_FIT)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Glyphs/Length.svg"
                                            selected={!fitMode}
                                            multiSelect={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")}
                                            onSelect={() => setMode(MODE_FIXED)}
                                            className={VT.toolButton.button}
                                        />
                                    </div>
                                </div>
                                <SliderControl
                                    label={<RowIcon icon={iconColumns} text={translate("GridRoadGenerator.UI.Columns", "Columns") as string} />}
                                    value={columns}
                                    min={1}
                                    max={12}
                                    disabled={!fitMode}
                                    onChange={(value) => setColumns(Math.round(value))}
                                    onDragPreview={(value) => setLivePreview(LiveField.Columns, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconRows} text={translate("GridRoadGenerator.UI.Rows", "Rows") as string} />}
                                    value={rows}
                                    min={1}
                                    max={12}
                                    disabled={!fitMode}
                                    onChange={(value) => setRows(Math.round(value))}
                                    onDragPreview={(value) => setLivePreview(LiveField.Rows, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon="Media/Glyphs/Length.svg" text={translate("GridRoadGenerator.UI.SpacingShort", "Spacing") as string} />}
                                    value={spacing}
                                    min={100}
                                    max={300}
                                    unit="m"
                                    disabled={fitMode}
                                    onChange={setSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.Spacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {/* Superblock : taille des zones (collectrice toutes les 3 zones, voir
                            GridGenerator.GenerateLoopGrid) au lieu de l'espacement des collectrices. */}
                        {loopMode && superblockMode && !concentricMode && (
                            <SliderControl
                                label={<RowIcon icon={iconCollectorSpacing} text={translate("GridRoadGenerator.UI.SuperblockZoneSize", "Zone size") as string} />}
                                value={superblockZone}
                                min={100}
                                max={400}
                                unit="m"
                                onChange={setSuperblockZone}
                                onDragPreview={(value) => setLivePreview(LiveField.SuperblockZone, value)}
                                onDragEnd={clearLivePreview}
                            />
                        )}
                        {/* Concêntrico : nombre d'anneaux (l'espacement en découle, voir
                            ConcentricGenerator) et nombre de rayons. */}
                        {loopMode && concentricMode && (
                            <>
                                {/* Borne haute = nombre d'anneaux que la forme sélectionnée permet
                                    (retour utilisateur : "no painel simplesmente bloqueia além do
                                    limite") ; désactivé si la forme est trop petite pour un anneau. */}
                                <SliderControl
                                    label={<RowIcon icon={iconRows} text={translate("GridRoadGenerator.UI.ConcentricLayers", "Layers") as string} />}
                                    value={Math.max(1, Math.min(concentricLayers, concentricMaxLayers))}
                                    min={1}
                                    max={Math.max(2, concentricMaxLayers)}
                                    disabled={concentricMaxLayers < 2}
                                    onChange={setConcentricLayers}
                                    onDragPreview={(value) => setLivePreview(LiveField.ConcentricLayers, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.ConcentricConnections", "Connections") as string} />}
                                    value={concentricConnections}
                                    min={2}
                                    max={12}
                                    onChange={setConcentricConnections}
                                    onDragPreview={(value) => setLivePreview(LiveField.ConcentricConnections, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {loopMode && !superblockMode && !concentricMode && (
                            <>
                                <SliderControl
                                    label={<RowIcon icon={iconCollectorSpacing} text={translate("GridRoadGenerator.UI.CollectorSpacing", "Collector spacing") as string} />}
                                    value={collectorSpacing}
                                    min={200}
                                    max={400}
                                    unit="m"
                                    onChange={setCollectorSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.CollectorSpacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconCulDeSacFrequency} text={translate("GridRoadGenerator.UI.LoopCulDeSacRatio", "Cul-de-sac frequency") as string} />}
                                    value={loopCulDeSacRatio}
                                    min={0}
                                    max={100}
                                    unit="%"
                                    onChange={setLoopCulDeSacRatio}
                                    onDragPreview={(value) => setLivePreview(LiveField.LoopCulDeSacRatio, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {/* Concêntrico : les anneaux suivent la forme, aucune orientation à régler. */}
                        {!concentricMode && (
                            <SliderControl
                                label={<RowIcon icon={iconAngle} text={translate("GridRoadGenerator.UI.Angle", "Angle") as string} />}
                                value={angleOffset}
                                min={-90}
                                max={90}
                                unit="°"
                                onChange={setAngleOffset}
                                onDragPreview={(value) => setLivePreview(LiveField.Angle, value)}
                                onDragEnd={clearLivePreview}
                            />
                        )}
                        <ToggleRow
                            label={<RowIcon icon={iconTerrain} text={translate("GridRoadGenerator.UI.FollowTerrain", "Follow terrain") as string} />}
                            checked={followTerrain}
                            onChange={setFollowTerrain}
                        />
                    </SectionFoldout>

                    {/* Culs-de-sac : quartier pavillonnaire (collectrices traversantes,
                        résidentielles en impasse) — Axis/Frequency/Staggered/le toggle
                        d'activation lui-même n'ont pas de sens en mode Loop, qui a déjà SA PROPRE
                        fréquence dans Geometria (LoopCulDeSacRatio). Depth, en revanche,
                        s'applique aussi en mode Loop (voir Core/GridGenerator.cs EmitLoopBlock,
                        spurLength = ... * CulDeSacDepth : contrôle la longueur de la ramification
                        cul-de-sac vers le centre de chaque laço) — masqué à tort avant ce
                        correctif, ce qui empêchait de raccourcir des impasses jugées "trop
                        profondes" en mode Loop alors que le réglage existait déjà côté Core. Le
                        cercle de retournement (CapSize/CapStyle) reste partagé et toujours
                        visible, même en mode Loop. Section ENTIÈREMENT masquée en mode Super-
                        quarteirão (retour utilisateur en jeu : "retira por completo os cul de sac
                        desse modo") : GenerateSuperblockInterior ne pose jamais aucun cul-de-sac
                        ni cercle de retournement (réseau piéton, toujours entièrement connecté —
                        voir PruneDeadEndPedestrianSegments), donc plus aucun réglage ici n'a de
                        prise sur le résultat. */}
                    {!(loopMode && (superblockMode || concentricMode)) && (
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                        headerExtra={
                            !loopMode && <ToggleControl checked={culDeSacMode} onChange={setCulDeSacMode} />
                        }
                        expanded={(culDeSacMode || loopMode) && culDeSacExpanded}
                        onToggle={() => (culDeSacMode || loopMode) && setCulDeSacExpanded((value) => !value)}
                        locked={!culDeSacMode && !loopMode}>
                        <SliderControl
                            label={<RowIcon icon={iconCulDeSacDepth} text={translate("GridRoadGenerator.UI.CulDeSacDepth", "Depth") as string} />}
                            value={culDeSacDepth}
                            min={50}
                            max={100}
                            unit="%"
                            disabled={!culDeSacMode && !loopMode}
                            onChange={setCulDeSacDepth}
                            onDragPreview={(value) => setLivePreview(LiveField.CulDeSacDepth, value)}
                            onDragEnd={clearLivePreview}
                        />
                        {!loopMode && (
                            <>
                                <div className={styles.dropdownRow}>
                                    <RowIcon icon={iconAxis} text={translate("GridRoadGenerator.UI.CulDeSacAxis", "Axis") as string} />
                                    <div className={styles.dropdownControl}>
                                        <VC.DropdownField
                                            items={culDeSacAxisItems}
                                            value={culDeSacAxis}
                                            disabled={!culDeSacMode}
                                            onChange={(value: number) => setCulDeSacAxis(value)}
                                        />
                                    </div>
                                </div>
                                <SliderControl
                                    label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.CulDeSacRatio", "Frequency") as string} />}
                                    value={culDeSacRatio}
                                    min={0}
                                    max={100}
                                    unit="%"
                                    disabled={!culDeSacMode}
                                    onChange={setCulDeSacRatio}
                                    onDragPreview={(value) => setLivePreview(LiveField.CulDeSacRatio, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <ToggleRow
                                    label={<RowIcon icon={iconStaggered} text={translate("GridRoadGenerator.UI.Staggered", "Staggered") as string} />}
                                    checked={staggered}
                                    disabled={!culDeSacMode}
                                    onChange={setStaggered}
                                />
                            </>
                        )}
                        <div className={styles.dropdownRow}>
                            <RowIcon icon={iconCircleOutline} text={translate("GridRoadGenerator.UI.CulDeSacCapSize", "Turnaround size") as string} />
                            <div className={styles.dropdownControl}>
                                <VC.DropdownField
                                    items={capSizeItems}
                                    value={culDeSacCapSize}
                                    disabled={false}
                                    onChange={(value: number) => setCulDeSacCapSize(value)}
                                />
                            </div>
                        </div>
                        <div className={styles.dropdownRow}>
                            <RowIcon icon={iconStyleGrass} text={translate("GridRoadGenerator.UI.CulDeSacCapStyle", "Turnaround style") as string} />
                            <div className={styles.dropdownControl}>
                                <VC.DropdownField
                                    items={capStyleItems}
                                    value={culDeSacCapStyle}
                                    disabled={false}
                                    onChange={handleCapStyleChange}
                                />
                            </div>
                        </div>
                    </SectionFoldout>
                    )}

                    {/* Avenue : troisième réseau, colonne et/ou rangée choisie librement par
                        index (grille classique uniquement — masquée en mode Loop, où elle n'a
                        aucun sens). Une rotonde est ajoutée automatiquement côté Core si les
                        deux axes sont actifs (voir EmitAvenueRoundabout). */}
                    {!loopMode && (
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionAvenue", "Avenue")}
                        expanded={avenueExpanded}
                        onToggle={() => setAvenueExpanded((value) => !value)}>
                        <ToggleRow
                            label={<RowIcon icon={iconAvenueColumn} text={translate("GridRoadGenerator.UI.AvenueColumn", "Avenue column") as string} />}
                            checked={avenueColumnEnabled}
                            onChange={setAvenueColumnEnabled}
                        />
                        <SliderControl
                            label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string} />}
                            value={avenueColumnIndex}
                            min={0}
                            max={23}
                            disabled={!avenueColumnEnabled}
                            onChange={(value) => setAvenueColumnIndex(Math.round(value))}
                            onDragPreview={(value) => setLivePreview(LiveField.AvenueColumnIndex, value)}
                            onDragEnd={clearLivePreview}
                        />
                        <ToggleRow
                            label={<RowIcon icon={iconAvenueRow} text={translate("GridRoadGenerator.UI.AvenueRow", "Avenue row") as string} />}
                            checked={avenueRowEnabled}
                            onChange={setAvenueRowEnabled}
                        />
                        <SliderControl
                            label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string} />}
                            value={avenueRowIndex}
                            min={0}
                            max={23}
                            disabled={!avenueRowEnabled}
                            onChange={(value) => setAvenueRowIndex(Math.round(value))}
                            onDragPreview={(value) => setLivePreview(LiveField.AvenueRowIndex, value)}
                            onDragEnd={clearLivePreview}
                        />
                    </SectionFoldout>
                    )}

                    {/* Redes (mode Loop uniquement, masquée sinon) : fusionnée avec l'ancienne
                        section "Estrada" (option D approuvée après comparaison de 5 esquisses,
                        voir NetworkTabs/NetworkPrefabRow) — un onglet par réseau : Avenida/Coletor
                        (relie les vias locais, networksTab===0) > Principal/Via locale (dessert
                        directement — laço/rua sinuosa ET beco sem saída sont UN SEUL réseau,
                        jamais deux séparés, voir isLocalSegment dans GridRoadToolSystem.
                        CreateGridDefinitions, networksTab===1). Un 3ᵉ onglet "Arterial" existait
                        ici (réutilisant le prefab "secundária") tant que le niveau Arterial du
                        générateur existait (voir GridGenerator.GenerateLoopGrid) — supprimé avec
                        lui, ce prefab n'ayant plus aucun consommateur en mode Loop.
                        "Esquerda"/"Direita" suivent la même convention que la barre native du jeu
                        (Left/Right, relatif au sens de tracé, pas un côté fixe du monde). Icônes
                        réels du jeu (Media/Game/Icons/*), jamais d'approximation dessinée à la
                        main. */}
                    {loopMode && (
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionNetworks", "Networks")}
                        expanded={networksExpanded}
                        onToggle={() => setNetworksExpanded((value) => !value)}>
                        <NetworkTabs
                            tabs={[
                                translate("GridRoadGenerator.UI.NetworkAvenue", "Collector"),
                                superblockMode
                                    ? translate("GridRoadGenerator.UI.NetworkPedestrian", "Pedestrian")
                                    : translate("GridRoadGenerator.UI.NetworkPrincipal", "Local street"),
                            ]}
                            active={networksTab}
                            onSelect={setNetworksTab}
                        />

                        {networksTab === 0 && (
                            <>
                                <NetworkPrefabRow
                                    label={translate("GridRoadGenerator.UI.RoadPrefab", "Road")}
                                    name={networkAssetName(avenueRoadPrefabName)}
                                    icon={avenueRoadPrefabIcon}
                                    onOpenPicker={() => setNetworksPickerOpen("avenue")}
                                />
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("GridRoadGenerator.UI.NetworkGeneral", "General")}>
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={avenueMiddleTrees}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeMiddleTrees", "Trees (median)")}
                                            onSelect={() => setAvenueMiddleTrees(!avenueMiddleTrees)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Grass.svg"
                                            selected={avenueMiddleGrass}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeMiddleGrass", "Grass (median)")}
                                            onSelect={() => setAvenueMiddleGrass(!avenueMiddleGrass)}
                                            className={VT.toolButton.button}
                                        />
                                    </VC.Section>
                                </div>
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("GridRoadGenerator.UI.NetworkLeft", "Left")}>
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={avenueSideTreesLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")}
                                            onSelect={() => setAvenueSideTreesLeft(!avenueSideTreesLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/BikeLane.svg"
                                            selected={avenueBikeLaneLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")}
                                            onSelect={() => setAvenueBikeLaneLeft(!avenueBikeLaneLeft)}
                                            className={VT.toolButton.button}
                                        />
                                    </VC.Section>
                                </div>
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("GridRoadGenerator.UI.NetworkRight", "Right")}>
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={avenueSideTreesRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")}
                                            onSelect={() => setAvenueSideTreesRight(!avenueSideTreesRight)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/BikeLane.svg"
                                            selected={avenueBikeLaneRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")}
                                            onSelect={() => setAvenueBikeLaneRight(!avenueBikeLaneRight)}
                                            className={VT.toolButton.button}
                                        />
                                    </VC.Section>
                                </div>
                            </>
                        )}

                        {networksTab === 1 && superblockMode && (
                            <>
                                {/* Réseau piéton de l'intérieur du super-quarteirão — voir
                                    RoadSegmentDef.IsPedestrian, réutilise l'emplacement
                                    "secundária" (inutilisé par ailleurs en mode Loop). Aucun
                                    melhoramento (voir GridRoadToolSystem.CreateGridDefinitions,
                                    IsPedestrian ? default) : trees/passeio/ciclovia du réseau
                                    routier n'ont pas de sens ici. */}
                                <NetworkPrefabRow
                                    label={translate("GridRoadGenerator.UI.PedestrianPrefab", "Path")}
                                    name={networkAssetName(secondaryRoadPrefabName)}
                                    icon={secondaryRoadPrefabIcon}
                                    onOpenPicker={() => setNetworksPickerOpen("secondary")}
                                />
                            </>
                        )}

                        {networksTab === 1 && !superblockMode && (
                            <>
                                <NetworkPrefabRow
                                    label={translate("GridRoadGenerator.UI.RoadPrefab", "Road")}
                                    name={networkAssetName(roadPrefabName)}
                                    icon={roadPrefabIcon}
                                    onOpenPicker={() => setNetworksPickerOpen("primary")}
                                />
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("GridRoadGenerator.UI.NetworkLeft", "Left")}>
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={principalSideTreesLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")}
                                            onSelect={() => setPrincipalSideTreesLeft(!principalSideTreesLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/WideSidewalk.svg"
                                            selected={principalWideSidewalkLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeWideSidewalk", "Wide sidewalk (removes parking)")}
                                            onSelect={() => setPrincipalWideSidewalkLeft(!principalWideSidewalkLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/BikeLane.svg"
                                            selected={principalBikeLaneLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")}
                                            onSelect={() => setPrincipalBikeLaneLeft(!principalBikeLaneLeft)}
                                            className={VT.toolButton.button}
                                        />
                                    </VC.Section>
                                </div>
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={translate("GridRoadGenerator.UI.NetworkRight", "Right")}>
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={principalSideTreesRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")}
                                            onSelect={() => setPrincipalSideTreesRight(!principalSideTreesRight)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/WideSidewalk.svg"
                                            selected={principalWideSidewalkRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeWideSidewalk", "Wide sidewalk (removes parking)")}
                                            onSelect={() => setPrincipalWideSidewalkRight(!principalWideSidewalkRight)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Game/Icons/BikeLane.svg"
                                            selected={principalBikeLaneRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")}
                                            onSelect={() => setPrincipalBikeLaneRight(!principalBikeLaneRight)}
                                            className={VT.toolButton.button}
                                        />
                                    </VC.Section>
                                </div>
                            </>
                        )}
                    </SectionFoldout>
                    )}
                    {networksPickerOpen &&
                        panelRef.current &&
                        createPortal(
                            <PrefabPicker slot={networksPickerOpen} onClose={() => setNetworksPickerOpen(null)} />,
                            panelRef.current,
                        )}
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
                    {perimeterCollision ? (
                        <span className={styles.collisionHint}>
                            {translate(
                                "GridRoadGenerator.UI.CollisionHint",
                                "Collision detected — the real preview is now shown so you can see where. Adjust and try again.",
                            )}
                        </span>
                    ) : (
                        <span className={styles.generateHint}>
                            {translate(
                                "GridRoadGenerator.UI.GenerateHint",
                                "Double-click a node to auto-select the whole perimeter.",
                            )}
                        </span>
                    )}
                </div>
            </div>
        </div>
    );
};
