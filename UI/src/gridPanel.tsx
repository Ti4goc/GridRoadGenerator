// Structure de panneau et intégration des composants vanilla adaptées de
// CS2-NetworkTools (c) Luca Rager, licence MIT
// https://github.com/lucarager/CS2-NetworkTools
import React, { useRef, useState } from "react";
import { createPortal } from "react-dom";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import styles from "./gridPanel.module.scss";
import { locString } from "./locHelpers";
import { PrefabPicker, PrefabSlot } from "./prefabPicker";
import { SafeButton } from "./safeButton";
import { AreaModeBar, BrushShapeRow, SelectionCount } from "./selectionRow";
import { ZoningRow } from "./zoningRow";
import { VC, VF, VT } from "./vanilla";
import { ViewSelection } from "./viewSelection";
import { TIP_TEXT, Tip, TipContent } from "./tips";
import patternGrid from "./patternGrid.svg";
import patternLoop from "./patternLoop.svg";
import patternSuperblock from "./patternSuperblock.svg";
import patternConcentric from "./patternConcentric.svg";
import patternRadial from "./patternRadial.svg";
import patternTree from "./patternTree.svg";
import patternOrganic from "./patternOrganic.svg";
import patternMixed from "./patternMixed.svg";
import patternContour from "./patternContour.svg";
// En-tête du panneau : icône isométrique en couleur (style des icônes du jeu) ; la barre
// d'outils garde son icône (toolbarButton.tsx).
import panelIcon from "./panelIcon.svg";
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
    roundaboutRoadPrefabIcon$,
    primaryUpgradeSupport$,
    secondaryUpgradeSupport$,
    avenueUpgradeSupport$,
    UpgradeSupport,
    pathRoadPrefabIcon$,
    pathRoadPrefabName$,
    roundaboutRoadPrefabName$,
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
    alignTerrain$,
    generateGrid,
    LiveField,
    loopCulDeSacRatio$,
    loopMode$,
    mode$,
    nodeCount$,
    selectionMode$,
    canUndo$,
    summarySegments$,
    summaryLength$,
    summaryCost$,
    canRedo$,
    undo,
    redo,
    brushSize$,
    brushSquare$,
    brushAngle$,
    setBrushAngle,
    setBrushSize,
    perimeterCollision$,
    perimeterInvalid$,
    principalBikeLaneLeft$,
    principalBikeLaneRight$,
    avenueSideGrassLeft$,
    avenueSideGrassRight$,
    principalSideGrassLeft$,
    principalSideGrassRight$,
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
    radialMode$,
    treeMode$,
    organicMode$,
    mixedMode$,
    mixedCoreRadius$,
    setMixedMode,
    setMixedCoreRadius,
    contourMode$,
    contourSpacing$,
    contourConnectorSpacing$,
    contourFlat$,
    organicStreetSpacing$,
    organicCurviness$,
    organicLoopShare$,
    organicSeed$,
    treeBranchSpacing$,
    treeCulDeSacSpacing$,
    treeCulDeSacLength$,
    radialAvenues$,
    radialRoundabout$,
    radialLayers$,
    radialMaxLayers$,
    concentricLayers$,
    concentricConnections$,
    concentricMaxLayers$,
    setConcentricMode,
    setRadialMode,
    setTreeMode,
    setOrganicMode,
    setContourMode,
    setContourSpacing,
    setContourConnectorSpacing,
    setOrganicStreetSpacing,
    setOrganicCurviness,
    setOrganicLoopShare,
    setOrganicSeed,
    setTreeBranchSpacing,
    setTreeCulDeSacSpacing,
    setTreeCulDeSacLength,
    setRadialAvenues,
    setRadialRoundabout,
    setRadialLayers,
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
    setAlignTerrain,
    setLoopCulDeSacRatio,
    setLoopMode,
    setMode,
    setPrincipalBikeLaneLeft,
    setPrincipalBikeLaneRight,
    setAvenueSideGrassLeft,
    setAvenueSideGrassRight,
    setPrincipalSideGrassLeft,
    setPrincipalSideGrassRight,
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

/// Hauteur (rem, écran 1080p) réservée en bas de l'écran à la barre d'outils du jeu. Le menu de choix
/// des routes ne s'ouvre plus avec l'outil (GetPrefab nul) : plus besoin de lui réserver de place.
const BOTTOM_RESERVE_REM = 90;
/// Hauteur minimale du panneau (px), même panneau placé très bas.
const MIN_PANEL_HEIGHT = 240;

/// Hauteur maximale du panneau (px) pour un haut de panneau à `top` px : l'interface du jeu grandit
/// avec la hauteur de l'écran (1rem = hauteur / 1080 px), la réserve du bas suit donc la résolution.
const maxPanelHeight = (top: number) =>
    Math.max(MIN_PANEL_HEIGHT, window.innerHeight - (BOTTOM_RESERVE_REM * window.innerHeight) / 1080 - top);

// Largeur redimensionnable par les bords gauche et droit (retour utilisateur), en rem (1rem =
// hauteur d'écran / 1080 px, comme toute l'interface du jeu), mémorisée.
const PANEL_WIDTH_KEY = "grg.panelWidth";
const MIN_PANEL_WIDTH_REM = 300;
/// À partir de cette largeur, la colonne Redes / Zoneamento passe à droite.
const TWO_COLUMNS_MIN_REM = 680;
/// Sous cette largeur de colonne : icônes seules (le texte reste en infobulle), titre masqué.
const COMPACT_BELOW_REM = 380;
const remPx = () => window.innerHeight / 1080;
const maxPanelWidthRem = () => window.innerWidth / remPx() - 20;
const clampWidth = (rem: number) =>
    Math.round(Math.min(Math.max(rem, MIN_PANEL_WIDTH_REM), Math.max(MIN_PANEL_WIDTH_REM, maxPanelWidthRem())));

const loadPanelWidth = (): number | null => {
    try {
        const raw = Number(localStorage.getItem(PANEL_WIDTH_KEY));
        return raw > 0 ? clampWidth(raw) : null;
    } catch {
        return null;
    }
};

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

const SectionFoldout = ({ title, headerExtra, expanded, onToggle, locked, children }: SectionFoldoutProps) => {
    // Clic commencé sur un contrôle de l'en-tête (ex. case "Cul-de-sac") : ne replie/déplie pas la
    // section. La case native du jeu laisse passer le clic jusqu'à l'en-tête malgré
    // stopPropagation (retour utilisateur : activer le cul-de-sac ouvrait aussi la section).
    const pressedOnExtra = useRef(false);
    return (
        <div className={styles.foldout}>
            <div
                className={locked ? `${styles.foldoutHeader} ${styles.foldoutHeaderLocked}` : styles.foldoutHeader}
                onMouseDown={() => {
                    pressedOnExtra.current = false;
                }}
                onClick={() => {
                    if (pressedOnExtra.current) {
                        pressedOnExtra.current = false;
                        return;
                    }
                    onToggle();
                }}>
                <span className={styles.foldoutTitle}>{title}</span>
                {headerExtra && (
                    <span
                        className={styles.foldoutHeaderExtra}
                        onMouseDownCapture={() => {
                            pressedOnExtra.current = true;
                        }}
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
};

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
    /// Valeur minimale réelle quand `min` est un cran de la piste (pinceau : 1 m au lieu de 0).
    floor?: number;
    unit?: string;
    disabled?: boolean;
    onChange: (value: number) => void;
    /// Croquis léger (voir GridRoadOverlaySystem.DrawLiveSpacingSketch) : appelé à CHAQUE
    /// pixel parcouru pendant le drag (onChange, lui, n'est appelé qu'au relâchement) — permet
    /// un retour visuel en direct sans jamais régénérer la vraie grille pendant le drag.
    /// Optionnel : seul le slider Espaçamento le branche pour l'instant.
    onDragPreview?: (value: number) => void;
    onDragEnd?: () => void;
    /// Infobulle au survol (voir tips.tsx) : titre et description.
    tipTitle?: string;
    tip?: string;
};

const SliderControl = ({ label, value, min, max, step = 1, floor, unit, disabled, onChange, onDragPreview, onDragEnd, tipTitle, tip }: SliderControlProps) => {
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
    // Slider natif du jeu (VC.Slider, voir vanilla.ts) — demande utilisateur : "o mesmo
    // mecanismo que no jogo para estas barras". Même règle qu'avant : onChange (régénération
    // côté C#) seulement au relâchement ; pendant le drag, valeur locale + croquis léger.
    const draggingRef = useRef(false);
    const lastDragValueRef = useRef<number | null>(null);
    const snap = (raw: number) => Math.max(Math.min(Math.max(Math.round((raw - min) / step) * step + min, min), max), floor ?? min);
    if (VC.Slider) {
        const header = (
            <div className={styles.sliderControlHeader}>
                <Tip title={tipTitle} description={tip}><span className={styles.sliderControlLabel}>{label}</span></Tip>
                <span className={styles.sliderControlValue}>
                    {Math.round(displayValue)}
                    {unit ? ` ${unit}` : ""}
                </span>
            </div>
        );
        return (
            <div className={disabled ? `${styles.sliderControl} ${styles.sliderControlDisabled}` : styles.sliderControl}>
                {header}
                <VC.Slider
                    focusKey={VF.FOCUS_DISABLED}
                    value={displayValue}
                    start={min}
                    end={max}
                    gamepadStep={step}
                    disabled={disabled}
                    valueTransformer={(start: number, end: number, ratio: number) => snap(start + ratio * (end - start))}
                    onDragStart={() => {
                        draggingRef.current = true;
                    }}
                    onChange={(v: number) => {
                        if (draggingRef.current) {
                            lastDragValueRef.current = v;
                            setDragValue(v);
                            onDragPreview?.(v);
                        } else {
                            // Manette/clavier : pas de drag, valeur appliquée tout de suite.
                            onChange(v);
                        }
                    }}
                    onDragEnd={() => {
                        draggingRef.current = false;
                        const finalValue = lastDragValueRef.current;
                        lastDragValueRef.current = null;
                        if (finalValue !== null && finalValue !== value) {
                            onChange(finalValue);
                        }
                        setDragValue(null);
                        onDragEnd?.();
                    }}
                />
            </div>
        );
    }

    const valueFromClientX = (clientX: number) => {
        const rect = trackRef.current?.getBoundingClientRect();
        if (!rect || rect.width === 0) {
            return displayValue;
        }
        const ratio = Math.min(Math.max((clientX - rect.left) / rect.width, 0), 1);
        const raw = min + ratio * (max - min);
        return Math.max(Math.min(Math.max(Math.round(raw / step) * step, min), max), floor ?? min);
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
                <Tip title={tipTitle} description={tip}><span className={styles.sliderControlLabel}>{label}</span></Tip>
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
        <span className={styles.rowIconText}>{text}</span>
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
}) =>
    // Case à cocher native du jeu (carré des menus Options) — demande utilisateur, à la place
    // de l'interrupteur custom (gardé en repli si le module du jeu est introuvable).
    VC.Checkbox ? (
        <VC.Checkbox
            focusKey={VF.FOCUS_DISABLED}
            className={VT.infomodeItem?.checkbox}
            checked={checked}
            disabled={disabled}
            onChange={onChange}
        />
    ) : (
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
    tipTitle,
    tip,
}: {
    label: React.ReactNode;
    checked: boolean;
    disabled?: boolean;
    onChange: (value: boolean) => void;
    tipTitle?: string;
    tip?: string;
}) => (
    <div className={styles.toggleRow}>
        <Tip title={tipTitle} description={tip}>
            <span className={styles.toggleRowLabel}>{label}</span>
        </Tip>
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
    <Tip title={label} description={tooltip ?? undefined}>
    <button
        type="button"
        disabled={locked}
        onClick={onSelect}
        className={[styles.patternButton, selected ? styles.patternButtonSelected : "", locked ? styles.patternButtonLocked : ""]
            .filter(Boolean)
            .join(" ")}>
        {/* Icône seule (faixa de ícones) : le nom est dans l'infobulle et sous la rangée (patternInfo). */}
        <img src={icon} className={styles.patternButtonIcon} />
        {locked && soonLabel && <span className={styles.patternButtonSoon}>{soonLabel}</span>}
    </button>
    </Tip>
);

/// Petit titre de sous-groupe dans une section (Geometria : Forma / Terreno / Ligações), toujours
/// dans le même ordre quel que soit le motif — retour utilisateur : "tudo muito desorganizado".
const SubGroup = ({ children }: { children: React.ReactNode }) => <div className={styles.subgroup}>{children}</div>;

/// Rangée d'un réseau de la section Redes (nom du réseau + route choisie) : le chip de choix de
/// prefab qui vivait dans l'ancienne section "Estrada" (toujours visible, hors Redes)
/// pour ce SEUL slot, fusionné ici — un déclencheur PrefabPicker(slot) + portail, même
/// principe que roadSelection.tsx mais un seul slot à la fois au lieu des 3 en rangée.
const NetworkPrefabRow = ({
    label,
    name,
    icon,
    onOpenPicker,
    tip,
}: {
    label: React.ReactNode;
    name: string;
    icon: string;
    onOpenPicker: () => void;
    tip?: string;
}) => (
    <div className={styles.toggleRow}>
        <Tip title={label} description={tip}>
            <span className={styles.toggleRowLabel}>{label}</span>
        </Tip>
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
    // Description d'infobulle localisée ("GridRoadGenerator.UI.Tip.<clé>"), anglais par défaut.
    const tip = (key: string) => translate(`GridRoadGenerator.UI.Tip.${key}`, TIP_TEXT[key]) ?? TIP_TEXT[key];
    const toolActive = useValue(toolActive$);
    const nodeCount = useValue(nodeCount$);
    const selectionMode = useValue(selectionMode$);
    const canUndo = useValue(canUndo$);
    const summarySegments = useValue(summarySegments$);
    const summaryLength = useValue(summaryLength$);
    const summaryCost = useValue(summaryCost$);
    // Nombres lisibles : espace fine entre les milliers (ex. 1 234 567).
    const formatNumber = (value: number, decimals = 0) =>
        value.toFixed(decimals).replace(/\B(?=(\d{3})+(?!\d))/g, "\u2009");
    const canRedo = useValue(canRedo$);
    const brushSize = useValue(brushSize$);
    const brushSquare = useValue(brushSquare$);
    const brushAngle = useValue(brushAngle$);
    const canApply = useValue(canApply$);
    const perimeterInvalid = useValue(perimeterInvalid$);
    const perimeterCollision = useValue(perimeterCollision$);
    const mode = useValue(mode$);
    const columns = useValue(columns$);
    const rows = useValue(rows$);
    const spacing = useValue(spacing$);
    const angleOffset = useValue(angleOffset$);
    const followTerrain = useValue(followTerrain$);
    const alignTerrain = useValue(alignTerrain$);
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
    const radialMode = useValue(radialMode$);
    const treeMode = useValue(treeMode$);
    const organicMode = useValue(organicMode$);
    const organicStreetSpacing = useValue(organicStreetSpacing$);
    const organicCurviness = useValue(organicCurviness$);
    const organicLoopShare = useValue(organicLoopShare$);
    const organicSeed = useValue(organicSeed$);
    const contourMode = useValue(contourMode$);
    const contourSpacing = useValue(contourSpacing$);
    const contourConnectorSpacing = useValue(contourConnectorSpacing$);
    const contourFlat = useValue(contourFlat$);
    // Motifs de la famille Grelha (loopMode faux) qui ne sont pas la grelha classique.
    const mixedMode = useValue(mixedMode$);
    const mixedCoreRadius = useValue(mixedCoreRadius$);
    const gridVariant = treeMode || organicMode || contourMode || mixedMode;
    // Motif sélectionné : nom et description affichés sous la faixa de ícones.
    const selectedPattern = !loopMode
        ? mixedMode ? "PatternMixed" : treeMode ? "PatternTree" : organicMode ? "PatternOrganic" : contourMode ? "PatternContour" : "PatternGrid"
        : radialMode ? "PatternRadial" : concentricMode ? "PatternConcentric" : superblockMode ? "PatternSuperblock" : "PatternLoop";
    const patternNames: Record<string, string> = {
        PatternGrid: "Grid", PatternTree: "Tree", PatternOrganic: "Organic", PatternContour: "Terrain",
        PatternLoop: "Loop", PatternSuperblock: "Superblock", PatternConcentric: "Concentric", PatternRadial: "Radial",
        PatternMixed: "Mixed",
    };
    const treeBranchSpacing = useValue(treeBranchSpacing$);
    const treeCulDeSacSpacing = useValue(treeCulDeSacSpacing$);
    const treeCulDeSacLength = useValue(treeCulDeSacLength$);
    const radialAvenues = useValue(radialAvenues$);
    // Rayon minimal réel de la rotonde (voir ConcentricGenerator.RadialLayout) : les raccords des
    // avenues y sont espacés d'au moins 40 m (MinAvenueJointSpacing), donc rayon ≥ avenues × 40 / 2π
    // — 51 m pour 8 avenues, 102 m pour 16. En dessous, le générateur agrandissait la rotonde en
    // silence et la barre semblait sans effet (retour utilisateur : "só começa a mudar a partir de
    // 100 m") ; la barre commence maintenant à ce minimum.
    const radialMinRoundabout = Math.max(25, Math.ceil((radialAvenues * 40) / (2 * Math.PI)));
    const radialRoundabout = useValue(radialRoundabout$);
    const radialLayers = useValue(radialLayers$);
    const radialMaxLayers = useValue(radialMaxLayers$);
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
    const avenueSideGrassLeft = useValue(avenueSideGrassLeft$);
    const avenueSideGrassRight = useValue(avenueSideGrassRight$);
    const principalSideGrassLeft = useValue(principalSideGrassLeft$);
    const principalSideGrassRight = useValue(principalSideGrassRight$);
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const avenueRoadPrefabName = useValue(avenueRoadPrefabName$);
    const avenueRoadPrefabIcon = useValue(avenueRoadPrefabIcon$);
    const roundaboutRoadPrefabName = useValue(roundaboutRoadPrefabName$);
    const roundaboutRoadPrefabIcon = useValue(roundaboutRoadPrefabIcon$);
    // Melhoramentos proposés = ceux que la route choisie sait afficher (Travessa, cascalho : aucun).
    const primaryUpgradeSupport = useValue(primaryUpgradeSupport$);
    const secondaryUpgradeSupport = useValue(secondaryUpgradeSupport$);
    const avenueUpgradeSupport = useValue(avenueUpgradeSupport$);
    const pathRoadPrefabName = useValue(pathRoadPrefabName$);
    const pathRoadPrefabIcon = useValue(pathRoadPrefabIcon$);
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
    const [networksExpanded, setNetworksExpanded] = useState(true);
    // Panneau en deux colonnes (retour utilisateur : "e que tal se alargares o painel?") : à gauche
    // zone et forme (Padrão, Geometria…), à droite l'aspect (Redes, Zoneamento, Predefinições).
    // Colonne droite repliable (choix mémorisé) ; une seule colonne sur un écran étroit.
    const [sideOpen, setSideOpenState] = useState(() => {
        try {
            return window.localStorage.getItem("grg.sideColumn") !== "0";
        } catch {
            return true;
        }
    });
    const setSideOpen = (open: boolean) => {
        setSideOpenState(open);
        try {
            window.localStorage.setItem("grg.sideColumn", open ? "1" : "0");
        } catch {
            // stockage indisponible : le choix vaut pour la session
        }
    };
    // Deux colonnes selon la PROPORTION de l'écran, pas sa largeur en pixels : l'interface du jeu
    // grandit avec la hauteur de l'écran, donc le panneau prend la même part de la largeur quelle
    // que soit la résolution. Écran 16:9 ou plus large : deux colonnes ; 16:10, 4:3, fenêtre étroite : une.
    const wideScreen = window.innerWidth / Math.max(1, window.innerHeight) >= 1.7;
    // Largeur choisie aux bords du panneau ; sans choix, l'ancienne (deux colonnes sur écran large).
    const [storedWidth, setStoredWidth] = useState<number | null>(loadPanelWidth);
    const panelWidth = storedWidth ?? (sideOpen && wideScreen ? 720 : 460);
    const twoColumns = sideOpen && panelWidth >= TWO_COLUMNS_MIN_REM;
    const columnWidth = twoColumns ? (panelWidth * 16) / 25 : panelWidth;
    const compact = columnWidth < COMPACT_BELOW_REM;
    // Réseaux de la section Redes selon le motif. Orgânico : Avenida (rue principale) et Cul-de-sac
    // (toutes les autres rues, avec les melhoramentos du principal). Relevo : un seul réseau. Radial
    // et Misto : la rotonde a le sien.
    const organicNetworks = !loopMode && organicMode;
    const contourNetworks = !loopMode && contourMode;
    const radialNetworks = loopMode && concentricMode && radialMode;
    const mixedNetworks = !loopMode && mixedMode;
    // Réseaux en liste (retour utilisateur : panneau "muito desorganizado" à partir de Geometria) :
    // chaque réseau utilisé par le motif a sa ligne, plus d'onglets.
    const showAvenueTab = !contourNetworks;
    const showPrincipalTab = loopMode ? !superblockMode : !organicNetworks;
    const showPedestrianTab = loopMode && superblockMode;
    const showCulDeSacTab = !loopMode && !organicNetworks && !contourNetworks;
    const showOrganicStreetsTab = organicNetworks;
    const showRoundaboutTab = radialNetworks || mixedNetworks;
    const [networksPickerOpen, setNetworksPickerOpen] = useState<PrefabSlot | null>(null);
    const networkAssetName = (name: string) => (name ? (translate(`Assets.NAME[${name}]`, name) ?? name) : "");

    if (!toolActive) {
        return null;
    }

    // Bords gauche/droit : largeur (et position, bord gauche) ; double clic : largeur d'origine.
    const startResize = (side: "left" | "right") => (event: React.MouseEvent) => {
        event.stopPropagation();
        event.preventDefault();
        const startX = event.clientX;
        const startWidth = panelWidth;
        const startLeft = panelPosition.x;
        const top = panelPosition.y;
        let latest = startWidth;
        const leftFor = (width: number) => startLeft + (startWidth - width) * remPx();
        const onMove = (ev: MouseEvent) => {
            const delta = (ev.clientX - startX) / remPx();
            latest = clampWidth(side === "right" ? startWidth + delta : startWidth - delta);
            setStoredWidth(latest);
            if (side === "left") {
                setPanelPosition({ x: leftFor(latest), y: top });
            }
        };
        const onUp = () => {
            window.removeEventListener("mousemove", onMove);
            window.removeEventListener("mouseup", onUp);
            try {
                localStorage.setItem(PANEL_WIDTH_KEY, String(latest));
                if (side === "left") {
                    localStorage.setItem(PANEL_POSITION_KEY, JSON.stringify({ x: leftFor(latest), y: top }));
                }
            } catch {
                // stockage indisponible : la largeur vaut pour la session
            }
        };
        window.addEventListener("mousemove", onMove);
        window.addEventListener("mouseup", onUp);
    };
    const resetWidth = () => {
        setStoredWidth(null);
        try {
            localStorage.removeItem(PANEL_WIDTH_KEY);
        } catch {
            // rien à effacer
        }
    };

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

    const rightColumn = (
        <>
                    {/* Redes : un bloc par réseau utilisé par le motif (nom, route choisie, puis ses
                        melhoramentos). "Esquerda"/"Direita" suivent la convention de la barre native
                        du jeu (relatif au sens de tracé). Icônes réelles du jeu (Media/Game/Icons/*) ;
                        ciclovia = Bicycle.svg (BikeLane.svg se confond avec Grass.svg à cette taille). */}
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionNetworks", "Networks")}
                        expanded={networksExpanded}
                        onToggle={() => setNetworksExpanded((value) => !value)}>
                        {(showPrincipalTab || showOrganicStreetsTab) && (
                            <div className={styles.networkBlock}>
                                {/* Orgânico : les rues (réseau cul-de-sac) reçoivent les melhoramentos du principal. */}
                                <NetworkPrefabRow
                                    label={loopMode ? translate("GridRoadGenerator.UI.NetworkPrincipal", "Local street") : showOrganicStreetsTab ? translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac") : translate("GridRoadGenerator.UI.RoadPrefab", "Road")}
                                    name={networkAssetName(showOrganicStreetsTab ? secondaryRoadPrefabName : roadPrefabName)}
                                    icon={showOrganicStreetsTab ? secondaryRoadPrefabIcon : roadPrefabIcon}
                                    onOpenPicker={() => setNetworksPickerOpen(showOrganicStreetsTab ? "secondary" : "primary")}
                                    tip={tip("NetworkPrefab")}
                                />
                                {(((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideTrees) !== 0 || ((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideGrass) !== 0 || ((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.WideSidewalk) !== 0 || ((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.BikeLane) !== 0) && (
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={<span className={styles.upgradeSideLabel}>{translate("GridRoadGenerator.UI.NetworkLeft", "Left")}</span>}>
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideTrees) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={principalSideTreesLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")} description={tip("UpgradeSideTrees")} />}
                                            onSelect={() => setPrincipalSideTreesLeft(!principalSideTreesLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideGrass) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Grass.svg"
                                            selected={principalSideGrassLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideGrass", "Grass (roadside)")} description={tip("UpgradeSideGrass")} />}
                                            onSelect={() => setPrincipalSideGrassLeft(!principalSideGrassLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.WideSidewalk) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/WideSidewalk.svg"
                                            selected={principalWideSidewalkLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeWideSidewalk", "Wide sidewalk (removes parking)")} description={tip("UpgradeWideSidewalk")} />}
                                            onSelect={() => setPrincipalWideSidewalkLeft(!principalWideSidewalkLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.BikeLane) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Bicycle.svg"
                                            selected={principalBikeLaneLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")} description={tip("UpgradeBikeLane")} />}
                                            onSelect={() => setPrincipalBikeLaneLeft(!principalBikeLaneLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                    </VC.Section>
                                </div>
                                )}
                                {(((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideTrees) !== 0 || ((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideGrass) !== 0 || ((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.WideSidewalk) !== 0 || ((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.BikeLane) !== 0) && (
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={<span className={styles.upgradeSideLabel}>{translate("GridRoadGenerator.UI.NetworkRight", "Right")}</span>}>
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideTrees) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={principalSideTreesRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")} description={tip("UpgradeSideTrees")} />}
                                            onSelect={() => setPrincipalSideTreesRight(!principalSideTreesRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.SideGrass) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Grass.svg"
                                            selected={principalSideGrassRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideGrass", "Grass (roadside)")} description={tip("UpgradeSideGrass")} />}
                                            onSelect={() => setPrincipalSideGrassRight(!principalSideGrassRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.WideSidewalk) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/WideSidewalk.svg"
                                            selected={principalWideSidewalkRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeWideSidewalk", "Wide sidewalk (removes parking)")} description={tip("UpgradeWideSidewalk")} />}
                                            onSelect={() => setPrincipalWideSidewalkRight(!principalWideSidewalkRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {((showOrganicStreetsTab ? secondaryUpgradeSupport : primaryUpgradeSupport) & UpgradeSupport.BikeLane) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Bicycle.svg"
                                            selected={principalBikeLaneRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")} description={tip("UpgradeBikeLane")} />}
                                            onSelect={() => setPrincipalBikeLaneRight(!principalBikeLaneRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                    </VC.Section>
                                </div>
                                )}
                            </div>
                        )}

                        {showAvenueTab && (
                            <div className={styles.networkBlock}>
                                <NetworkPrefabRow
                                    label={loopMode ? translate("GridRoadGenerator.UI.NetworkAvenue", "Collector") : translate("GridRoadGenerator.UI.SectionAvenue", "Avenue")}
                                    name={networkAssetName(avenueRoadPrefabName)}
                                    icon={avenueRoadPrefabIcon}
                                    onOpenPicker={() => setNetworksPickerOpen("avenue")}
                                    tip={tip("NetworkPrefab")}
                                />
                                {((avenueUpgradeSupport & UpgradeSupport.MiddleTrees) !== 0 || (avenueUpgradeSupport & UpgradeSupport.MiddleGrass) !== 0) && (
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={<span className={styles.upgradeSideLabel}>{translate("GridRoadGenerator.UI.NetworkGeneral", "General")}</span>}>
                                        {(avenueUpgradeSupport & UpgradeSupport.MiddleTrees) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={avenueMiddleTrees}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeMiddleTrees", "Trees (median)")} description={tip("UpgradeMiddleTrees")} />}
                                            onSelect={() => setAvenueMiddleTrees(!avenueMiddleTrees)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {(avenueUpgradeSupport & UpgradeSupport.MiddleGrass) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Grass.svg"
                                            selected={avenueMiddleGrass}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeMiddleGrass", "Grass (median)")} description={tip("UpgradeMiddleGrass")} />}
                                            onSelect={() => setAvenueMiddleGrass(!avenueMiddleGrass)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                    </VC.Section>
                                </div>
                                )}
                                {((avenueUpgradeSupport & UpgradeSupport.SideTrees) !== 0 || (avenueUpgradeSupport & UpgradeSupport.SideGrass) !== 0 || (avenueUpgradeSupport & UpgradeSupport.BikeLane) !== 0) && (
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={<span className={styles.upgradeSideLabel}>{translate("GridRoadGenerator.UI.NetworkLeft", "Left")}</span>}>
                                        {(avenueUpgradeSupport & UpgradeSupport.SideTrees) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={avenueSideTreesLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")} description={tip("UpgradeSideTrees")} />}
                                            onSelect={() => setAvenueSideTreesLeft(!avenueSideTreesLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {(avenueUpgradeSupport & UpgradeSupport.SideGrass) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Grass.svg"
                                            selected={avenueSideGrassLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideGrass", "Grass (roadside)")} description={tip("UpgradeSideGrass")} />}
                                            onSelect={() => setAvenueSideGrassLeft(!avenueSideGrassLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {(avenueUpgradeSupport & UpgradeSupport.BikeLane) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Bicycle.svg"
                                            selected={avenueBikeLaneLeft}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")} description={tip("UpgradeBikeLane")} />}
                                            onSelect={() => setAvenueBikeLaneLeft(!avenueBikeLaneLeft)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                    </VC.Section>
                                </div>
                                )}
                                {((avenueUpgradeSupport & UpgradeSupport.SideTrees) !== 0 || (avenueUpgradeSupport & UpgradeSupport.SideGrass) !== 0 || (avenueUpgradeSupport & UpgradeSupport.BikeLane) !== 0) && (
                                <div className={styles.vanillaRow}>
                                    <VC.Section focusKey={VF.FOCUS_DISABLED} title={<span className={styles.upgradeSideLabel}>{translate("GridRoadGenerator.UI.NetworkRight", "Right")}</span>}>
                                        {(avenueUpgradeSupport & UpgradeSupport.SideTrees) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Trees.svg"
                                            selected={avenueSideTreesRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideTrees", "Trees (roadside)")} description={tip("UpgradeSideTrees")} />}
                                            onSelect={() => setAvenueSideTreesRight(!avenueSideTreesRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {(avenueUpgradeSupport & UpgradeSupport.SideGrass) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Grass.svg"
                                            selected={avenueSideGrassRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeSideGrass", "Grass (roadside)")} description={tip("UpgradeSideGrass")} />}
                                            onSelect={() => setAvenueSideGrassRight(!avenueSideGrassRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                        {(avenueUpgradeSupport & UpgradeSupport.BikeLane) !== 0 && (
                                        <VC.ToolButton
                                            src="Media/Game/Icons/Bicycle.svg"
                                            selected={avenueBikeLaneRight}
                                            multiSelect={true}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.UpgradeBikeLane", "Bike lane")} description={tip("UpgradeBikeLane")} />}
                                            onSelect={() => setAvenueBikeLaneRight(!avenueBikeLaneRight)}
                                            className={VT.toolButton.button}
                                        />
                                        )}
                                    </VC.Section>
                                </div>
                                )}
                            </div>
                        )}

                        {showCulDeSacTab && (
                            <div className={styles.networkBlock}>
                                {/* Impasses de la Grelha : réseau secondaire ; leurs melhoramentos
                                    sont ceux de l'onglet Estrada (voir GridRoadToolSystem). */}
                                <NetworkPrefabRow
                                    label={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                                    name={networkAssetName(secondaryRoadPrefabName)}
                                    icon={secondaryRoadPrefabIcon}
                                    onOpenPicker={() => setNetworksPickerOpen("secondary")}
                                    tip={tip("NetworkPrefab")}
                                />
                            </div>
                        )}

                        {showPedestrianTab && (
                            <div className={styles.networkBlock}>
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
                                    tip={tip("NetworkPrefab")}
                                />
                            </div>
                        )}

                        {showRoundaboutTab && (
                            // Rotonde du motif Radial : réseau seul ; une route à sens unique y est posée
                            // dans le sens de circulation de la ville (voir GridRoadToolSystem).
                            <div className={styles.networkBlock}>
                            <NetworkPrefabRow
                                label={translate("GridRoadGenerator.UI.NetworkRoundabout", "Roundabout")}
                                name={networkAssetName(roundaboutRoadPrefabName)}
                                icon={roundaboutRoadPrefabIcon}
                                onOpenPicker={() => setNetworksPickerOpen("roundabout")}
                                tip={tip("NetworkRoundabout")}
                            />
                            </div>
                        )}

                    </SectionFoldout>
                    {/* Zonage : une ligne (zone choisie), grille des zones dépliée au clic — pas de section repliable. */}
                    <div className={styles.foldout}>
                        <ZoningRow />
                    </div>
        </>
    );

    return (
        <div
            ref={panelRef}
            className={compact ? `${styles.panelWrapper} grg-compact` : styles.panelWrapper}
            style={{ left: `${panelPosition.x}px`, top: `${panelPosition.y}px`, width: `${panelWidth}rem` }}>
            <div className={`${styles.resizeHandle} ${styles.resizeHandleLeft}`} onMouseDown={startResize("left")} onDoubleClick={resetWidth} />
            <div className={`${styles.resizeHandle} ${styles.resizeHandleRight}`} onMouseDown={startResize("right")} onDoubleClick={resetWidth} />
            <div className={styles.panel} style={{ maxHeight: `${maxPanelHeight(panelPosition.y)}px` }}>
                {/* Barre de titre calquée sur les panneaux du jeu (infoview) : icône à
                    gauche, titre centré, fermeture à droite, fond sombre du jeu. */}
                <div className={styles.header} onMouseDown={startDrag}>
                    <img src={panelIcon} className={styles.headerIcon} />
                    <span className={styles.headerTitle}>
                        {translate("GridRoadGenerator.UI.Title", "Grid Road Generator")}
                    </span>
                    <div className={styles.headerSpacer} />
                    {/* Anarchy (mod tiers optionnel) : toujours visible dans l'en-tête, pas
                        enterré dans une section repliable. État et toggle passent par les
                        bindings d'Anarchy lui-même, donc synchronisés avec son bouton
                        toolbar et son raccourci. */}
                    {/* Vues (souterrain, grille de zonage, réseaux invisibles) : dans l'en-tête, à côté
                        d'Anarchy (retour utilisateur), pour libérer la ligne des types de zone. */}
                    <span
                        className={styles.headerViews}
                        onMouseDown={(event) => event.stopPropagation()}
                        onClick={(event) => event.stopPropagation()}>
                        <ViewSelection compact />
                    </span>
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
                                tooltip={<TipContent title={translate("GridRoadGenerator.UI.AnarchyTooltip", "Toggle Anarchy")} description={tip("Anarchy")} />}
                                onSelect={toggleAnarchy}
                                className={VT.toolButton.button}
                            />
                        </span>
                    )}
                    {panelWidth >= TWO_COLUMNS_MIN_REM && (
                        <span
                            className={styles.headerSide}
                            onMouseDown={(event) => event.stopPropagation()}
                            onClick={(event) => {
                                event.stopPropagation();
                                setSideOpen(!sideOpen);
                            }}>
                            <Tip title={translate("GridRoadGenerator.UI.SideColumn", "Networks and zoning column") ?? ""} description="">
                                <div
                                    className={sideOpen ? styles.headerSideIconOpen : styles.headerSideIcon}
                                    style={{ maskImage: "url(Media/Glyphs/ThickStrokeArrowDown.svg)" }}
                                />
                            </Tip>
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
                 <div className={styles.columns}>
                  <div className={styles.leftColumn}>
                  {/* Haut fixe (retour utilisateur : "o scroll só existe a partir de geometria") :
                      motifs, nom du motif, types de zone et vues ne défilent jamais. */}
                  <div className={styles.fixedTop}>
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
                            <div className={styles.headerActions}>
                            <Tip title={translate("GridRoadGenerator.UI.Undo", "Undo")} description={tip("Undo")}>
                                <button className={canUndo ? styles.historyButton : `${styles.historyButton} ${styles.historyButtonDisabled}`} onClick={() => canUndo && undo()}>
                                    ↶
                                </button>
                            </Tip>
                            <Tip title={translate("GridRoadGenerator.UI.Redo", "Redo")} description={tip("Redo")}>
                                <button className={canRedo ? styles.historyButton : `${styles.historyButton} ${styles.historyButtonDisabled}`} onClick={() => canRedo && redo()}>
                                    ↷
                                </button>
                            </Tip>
                            <Tip title={translate("GridRoadGenerator.UI.ResetDefaults", "Reset values")} description={tip("ResetDefaults")}>
                                <button className={styles.resetButton} onClick={() => resetDefaults()}>
                                    <span className={styles.resetButtonGlyph}>↺</span>
                                    {translate("GridRoadGenerator.UI.ResetDefaults", "Reset values")}
                                </button>
                            </Tip>
                            </div>
                        </div>
                        <div className={styles.patternButtons}>
                            <PatternButton
                                icon={patternGrid}
                                label={translate("GridRoadGenerator.UI.PatternGrid", "Grid")}
                                tooltip={tip("PatternGrid")}
                                selected={!loopMode && !gridVariant}
                                onSelect={() => {
                                    setLoopMode(false);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                            {/* Cul-de-sac em árvore : famille de la Grelha (loopMode faux), voir
                                GridGenerator.GenerateTree. */}
                            <PatternButton
                                icon={patternTree}
                                label={translate("GridRoadGenerator.UI.PatternTree", "Tree")}
                                tooltip={tip("PatternTree")}
                                selected={!loopMode && treeMode}
                                onSelect={() => {
                                    setLoopMode(false);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(true);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                            {/* Orgânico : lotissement à rues sinueuses et impasses, famille de la Grelha
                                (loopMode faux), voir GridGenerator.GenerateOrganic. */}
                            <PatternButton
                                icon={patternOrganic}
                                label={translate("GridRoadGenerator.UI.PatternOrganic", "Organic")}
                                tooltip={tip("PatternOrganic")}
                                selected={!loopMode && organicMode}
                                onSelect={() => {
                                    setLoopMode(false);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(true);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                            {/* Misto : Radial au centre, Orgânico autour (voir GridGenerator.GenerateMixed). */}
                            <PatternButton
                                icon={patternMixed}
                                label={translate("GridRoadGenerator.UI.PatternMixed", "Mixed")}
                                tooltip={tip("PatternMixed")}
                                selected={!loopMode && mixedMode}
                                onSelect={() => {
                                    setLoopMode(false);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(true);
                                }}
                            />
                            {/* Relevo : rues de niveau le long des courbes du terrain, famille de la Grelha
                                (loopMode faux), voir GridGenerator.GenerateContour. */}
                            <PatternButton
                                icon={patternContour}
                                label={translate("GridRoadGenerator.UI.PatternContour", "Terrain")}
                                tooltip={tip("PatternContour")}
                                selected={!loopMode && contourMode}
                                onSelect={() => {
                                    setLoopMode(false);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(true);
                                    setMixedMode(false);
                                }}
                            />
                            <PatternButton
                                icon={patternLoop}
                                label={translate("GridRoadGenerator.UI.PatternLoop", "Loop")}
                                tooltip={tip("PatternLoop")}
                                selected={loopMode && !superblockMode && !concentricMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(false);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                            <PatternButton
                                icon={patternSuperblock}
                                label={translate("GridRoadGenerator.UI.PatternSuperblock", "Superblock")}
                                tooltip={tip("PatternSuperblock")}
                                selected={loopMode && superblockMode && !concentricMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(true);
                                    setConcentricMode(false);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                            {/* Concêntrico : même famille que Loop/Superblock (loopMode=true, voir
                                GridGenerator.GenerateLoopGrid qui délègue à ConcentricGenerator). */}
                            <PatternButton
                                icon={patternConcentric}
                                label={translate("GridRoadGenerator.UI.PatternConcentric", "Concentric")}
                                tooltip={tip("PatternConcentric")}
                                selected={loopMode && concentricMode && !radialMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(false);
                                    setConcentricMode(true);
                                    setRadialMode(false);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                            {/* Radial : les anneaux du Concêntrico (concentricMode reste vrai) traversés
                                par des avenues droites depuis le centre (voir ConcentricGenerator.
                                EmitStraightAvenues). */}
                            <PatternButton
                                icon={patternRadial}
                                label={translate("GridRoadGenerator.UI.PatternRadial", "Radial")}
                                tooltip={tip("PatternRadial")}
                                selected={loopMode && concentricMode && radialMode}
                                onSelect={() => {
                                    setLoopMode(true);
                                    setSuperblockMode(false);
                                    setConcentricMode(true);
                                    setRadialMode(true);
                                    setTreeMode(false);
                                    setOrganicMode(false);
                                    setContourMode(false);
                                    setMixedMode(false);
                                }}
                            />
                        </div>
                        <div className={styles.patternInfo}>
                            <div className={styles.patternInfoName}>
                                {translate(`GridRoadGenerator.UI.${selectedPattern}`, patternNames[selectedPattern])}
                            </div>
                            <Tip title={translate(`GridRoadGenerator.UI.${selectedPattern}`, patternNames[selectedPattern]) ?? ""} description={tip(selectedPattern)}>
                                <div className={styles.patternInfoDescription}>{tip(selectedPattern)}</div>
                            </Tip>
                        </div>
                    </div>

                    <div className={styles.topBar}>
                        <AreaModeBar />
                    </div>
                  </div>
                  <div className={styles.scrollWrapper}>
                    <VC.Scrollable className={styles.scrollable}>
                    {/* Pinceau : réglages en tête de la zone défilante (le haut fixe garde toujours
                        la même hauteur, quel que soit le type de zone). */}
                    {selectionMode === 2 && (
                        <div className={styles.foldout}>
                            <SubGroup>{translate("GridRoadGenerator.UI.SelectionBrush", "Paint area")}</SubGroup>
                            <BrushShapeRow />
                            <SliderControl
                                label={<RowIcon icon={iconCircleOutline} text={translate("GridRoadGenerator.UI.BrushSize", "Brush size") as string} />}
                                tipTitle={translate("GridRoadGenerator.UI.BrushSize", "Brush size") as string}
                                tip={tip("BrushSize")}
                                value={brushSize}
                                min={0}
                                max={1000}
                                step={50}
                                floor={1}
                                unit="m"
                                onChange={setBrushSize}
                            />
                        {brushSquare && (
                            <SliderControl
                                label={<RowIcon icon={iconAngle} text={translate("GridRoadGenerator.UI.BrushAngle", "Brush rotation") as string} />}
                                tipTitle={translate("GridRoadGenerator.UI.BrushAngle", "Brush rotation") as string}
                                tip={tip("BrushAngle")}
                                value={brushAngle}
                                min={0}
                                max={89}
                                unit="°"
                                onChange={setBrushAngle}
                            />
                        )}
                        </div>
                    )}

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
                        <SubGroup>{translate("GridRoadGenerator.UI.GroupShape", "Shape")}</SubGroup>
                        {!loopMode && treeMode && (
                            <>
                                <SliderControl
                                    label={<RowIcon icon={iconRows} text={translate("GridRoadGenerator.UI.TreeBranchSpacing", "Branch spacing") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.TreeBranchSpacing", "Branch spacing") as string}
                                    tip={tip("TreeBranchSpacing")}
                                    value={treeBranchSpacing}
                                    min={160}
                                    max={400}
                                    unit="m"
                                    onChange={setTreeBranchSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.TreeBranchSpacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconCulDeSacFrequency} text={translate("GridRoadGenerator.UI.TreeCulDeSacSpacing", "Cul-de-sac spacing") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.TreeCulDeSacSpacing", "Cul-de-sac spacing") as string}
                                    tip={tip("TreeCulDeSacSpacing")}
                                    value={treeCulDeSacSpacing}
                                    min={60}
                                    max={150}
                                    unit="m"
                                    onChange={setTreeCulDeSacSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.TreeCulDeSacSpacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconCulDeSacDepth} text={translate("GridRoadGenerator.UI.TreeCulDeSacLength", "Cul-de-sac length") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.TreeCulDeSacLength", "Cul-de-sac length") as string}
                                    tip={tip("TreeCulDeSacLength")}
                                    value={treeCulDeSacLength}
                                    min={40}
                                    max={150}
                                    unit="m"
                                    onChange={setTreeCulDeSacLength}
                                    onDragPreview={(value) => setLivePreview(LiveField.TreeCulDeSacLength, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {!loopMode && contourMode && (
                            <>
                                {contourFlat && (
                                    <div className={styles.contourFlatHint}>
                                        {translate(
                                            "GridRoadGenerator.UI.ContourFlat",
                                            "This area is too flat: there are no contour lines to follow. Pick an area on a hill or a slope.",
                                        )}
                                    </div>
                                )}
                                <SliderControl
                                    label={<RowIcon icon={iconRows} text={translate("GridRoadGenerator.UI.ContourSpacing", "Street spacing") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.ContourSpacing", "Street spacing") as string}
                                    tip={tip("ContourSpacing")}
                                    value={contourSpacing}
                                    min={60}
                                    max={150}
                                    unit="m"
                                    onChange={setContourSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.ContourSpacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconCollectorSpacing} text={translate("GridRoadGenerator.UI.ContourConnectorSpacing", "Uphill link spacing") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.ContourConnectorSpacing", "Uphill link spacing") as string}
                                    tip={tip("ContourConnectorSpacing")}
                                    value={contourConnectorSpacing}
                                    min={150}
                                    max={500}
                                    unit="m"
                                    onChange={setContourConnectorSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.ContourConnectorSpacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {!loopMode && mixedMode && (
                            <>
                                <SliderControl
                                    label={<RowIcon icon={iconCircleOutline} text={translate("GridRoadGenerator.UI.MixedCoreRadius", "Centre radius") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.MixedCoreRadius", "Centre radius") as string}
                                    tip={tip("MixedCoreRadius")}
                                    value={mixedCoreRadius}
                                    min={100}
                                    max={450}
                                    step={10}
                                    unit="m"
                                    onChange={setMixedCoreRadius}
                                    onDragPreview={(value) => setLivePreview(LiveField.MixedCoreRadius, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.RadialAvenues", "Avenues") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.RadialAvenues", "Avenues") as string}
                                    tip={tip("RadialAvenues")}
                                    value={radialAvenues}
                                    min={3}
                                    max={16}
                                    onChange={setRadialAvenues}
                                    onDragPreview={(value) => setLivePreview(LiveField.RadialAvenues, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {!loopMode && (organicMode || mixedMode) && (
                            <>
                                <SliderControl
                                    label={<RowIcon icon={iconCollectorSpacing} text={translate("GridRoadGenerator.UI.OrganicStreetSpacing", "Street spacing") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.OrganicStreetSpacing", "Street spacing") as string}
                                    tip={tip("OrganicStreetSpacing")}
                                    value={organicStreetSpacing}
                                    min={60}
                                    max={140}
                                    unit="m"
                                    onChange={setOrganicStreetSpacing}
                                    onDragPreview={(value) => setLivePreview(LiveField.OrganicStreetSpacing, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconAngle} text={translate("GridRoadGenerator.UI.OrganicCurviness", "Curviness") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.OrganicCurviness", "Curviness") as string}
                                    tip={tip("OrganicCurviness")}
                                    value={organicCurviness}
                                    min={0}
                                    max={100}
                                    unit="%"
                                    onChange={setOrganicCurviness}
                                    onDragPreview={(value) => setLivePreview(LiveField.OrganicCurviness, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconCircleOutline} text={translate("GridRoadGenerator.UI.OrganicLoopShare", "Loops") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.OrganicLoopShare", "Loops") as string}
                                    tip={tip("OrganicLoopShare")}
                                    value={organicLoopShare}
                                    min={0}
                                    max={100}
                                    unit="%"
                                    onChange={setOrganicLoopShare}
                                    onDragPreview={(value) => setLivePreview(LiveField.OrganicLoopShare, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.OrganicSeed", "Variation") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.OrganicSeed", "Variation") as string}
                                    tip={tip("OrganicSeed")}
                                    value={organicSeed}
                                    min={1}
                                    max={100}
                                    onChange={setOrganicSeed}
                                    onDragPreview={(value) => setLivePreview(LiveField.OrganicSeed, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {!loopMode && !gridVariant && (
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
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")} description={tip("ModeFit")} />}
                                            onSelect={() => setMode(MODE_FIT)}
                                            className={VT.toolButton.button}
                                        />
                                        <VC.ToolButton
                                            src="Media/Glyphs/Length.svg"
                                            selected={!fitMode}
                                            multiSelect={false}
                                            focusKey={VF.FOCUS_DISABLED}
                                            tooltip={<TipContent title={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")} description={tip("ModeFixed")} />}
                                            onSelect={() => setMode(MODE_FIXED)}
                                            className={VT.toolButton.button}
                                        />
                                    </div>
                                </div>
                                <SliderControl
                                    label={<RowIcon icon={iconColumns} text={translate("GridRoadGenerator.UI.Columns", "Columns") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.Columns", "Columns") as string}
                                    tip={tip("Columns")}
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
                                    tipTitle={translate("GridRoadGenerator.UI.Rows", "Rows") as string}
                                    tip={tip("Rows")}
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
                                    tipTitle={translate("GridRoadGenerator.UI.SpacingShort", "Spacing") as string}
                                    tip={tip("Spacing")}
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
                                tipTitle={translate("GridRoadGenerator.UI.SuperblockZoneSize", "Zone size") as string}
                                tip={tip("SuperblockZone")}
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
                        {loopMode && concentricMode && !radialMode && (
                            <>
                                {/* Borne haute = nombre d'anneaux que la forme sélectionnée permet
                                    (retour utilisateur : "no painel simplesmente bloqueia além do
                                    limite") ; désactivé si la forme est trop petite pour un anneau. */}
                                <SliderControl
                                    label={<RowIcon icon={iconRows} text={translate("GridRoadGenerator.UI.ConcentricLayers", "Layers") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.ConcentricLayers", "Layers") as string}
                                    tip={tip("ConcentricLayers")}
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
                                    tipTitle={translate("GridRoadGenerator.UI.ConcentricConnections", "Connections") as string}
                                    tip={tip("ConcentricConnections")}
                                    value={concentricConnections}
                                    min={2}
                                    max={12}
                                    onChange={setConcentricConnections}
                                    onDragPreview={(value) => setLivePreview(LiveField.ConcentricConnections, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {/* Radial : pas d'anneaux (retour utilisateur : "deixemos apenas as avenidas
                            e não as camadas, e no centro uma rotunda") — nombre d'avenues et rayon
                            demandé de la rotonde (ajusté à la forme, voir GenerateRadial). */}
                        {loopMode && concentricMode && radialMode && (
                            <>
                                {/* Anneaux circulaires autour de la rotonde (retour utilisateur : "de
                                    acordo com a rotunda e não a estrada exterior") : 0 à ce qui tient
                                    dans la forme, voir ConcentricGenerator.RadialMaxLayers. */}
                                <SliderControl
                                    label={<RowIcon icon={iconRows} text={translate("GridRoadGenerator.UI.ConcentricLayers", "Layers") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.ConcentricLayers", "Layers") as string}
                                    tip={tip("RadialLayers")}
                                    value={Math.max(0, Math.min(radialLayers, radialMaxLayers))}
                                    min={0}
                                    max={Math.max(1, radialMaxLayers)}
                                    disabled={radialMaxLayers < 1}
                                    onChange={setRadialLayers}
                                    onDragPreview={(value) => setLivePreview(LiveField.RadialLayers, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.RadialAvenues", "Avenues") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.RadialAvenues", "Avenues") as string}
                                    tip={tip("RadialAvenues")}
                                    value={radialAvenues}
                                    min={3}
                                    max={16}
                                    onChange={setRadialAvenues}
                                    onDragPreview={(value) => setLivePreview(LiveField.RadialAvenues, value)}
                                    onDragEnd={clearLivePreview}
                                />
                                <SliderControl
                                    label={<RowIcon icon={iconCircleOutline} text={translate("GridRoadGenerator.UI.RadialRoundabout", "Roundabout radius") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.RadialRoundabout", "Roundabout radius") as string}
                                    tip={tip("RadialRoundabout")}
                                    value={Math.max(radialRoundabout, radialMinRoundabout)}
                                    min={radialMinRoundabout}
                                    max={150}
                                    unit="m"
                                    onChange={setRadialRoundabout}
                                    onDragPreview={(value) => setLivePreview(LiveField.RadialRoundabout, value)}
                                    onDragEnd={clearLivePreview}
                                />
                            </>
                        )}
                        {loopMode && !superblockMode && !concentricMode && (
                            <>
                                <SliderControl
                                    label={<RowIcon icon={iconCollectorSpacing} text={translate("GridRoadGenerator.UI.CollectorSpacing", "Collector spacing") as string} />}
                                    tipTitle={translate("GridRoadGenerator.UI.CollectorSpacing", "Collector spacing") as string}
                                    tip={tip("CollectorSpacing")}
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
                                    tipTitle={translate("GridRoadGenerator.UI.LoopCulDeSacRatio", "Cul-de-sac frequency") as string}
                                    tip={tip("LoopCulDeSacRatio")}
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
                        {!concentricMode && !organicMode && !contourMode && (
                            <SliderControl
                                label={<RowIcon icon={iconAngle} text={translate("GridRoadGenerator.UI.Angle", "Angle") as string} />}
                                tipTitle={translate("GridRoadGenerator.UI.Angle", "Angle") as string}
                                tip={tip("Angle")}
                                value={angleOffset}
                                min={-90}
                                max={90}
                                unit="°"
                                onChange={setAngleOffset}
                                onDragPreview={(value) => setLivePreview(LiveField.Angle, value)}
                                onDragEnd={clearLivePreview}
                            />
                        )}
                        {/* Terreno : Relevo exclu (routes toujours sur le terrain, orientation imposée). */}
                        {!contourMode && <SubGroup>{translate("GridRoadGenerator.UI.GroupTerrain", "Terrain")}</SubGroup>}
                        {!contourMode && (
                        <ToggleRow
                            label={<RowIcon icon={iconTerrain} text={translate("GridRoadGenerator.UI.FollowTerrain", "Follow terrain") as string} />}
                            tipTitle={translate("GridRoadGenerator.UI.FollowTerrain", "Follow terrain") as string}
                            tip={tip("FollowTerrain")}
                            checked={followTerrain}
                            onChange={setFollowTerrain}
                        />
                        )}
                        {/* Grelha et Loop (hors Concêntrico/Radial) : un axe suit les courbes de niveau. */}
                        {((!loopMode && !gridVariant) || (loopMode && !concentricMode)) && (
                        <ToggleRow
                            label={<RowIcon icon={iconTerrain} text={translate("GridRoadGenerator.UI.AlignTerrain", "Align with the terrain") as string} />}
                            tipTitle={translate("GridRoadGenerator.UI.AlignTerrain", "Align with the terrain") as string}
                            tip={tip("AlignTerrain")}
                            checked={alignTerrain}
                            onChange={setAlignTerrain}
                        />
                        )}
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
                    {/* Relevo : pas d'impasse, donc pas de réglage de cercle de retournement. */}
                    {!(loopMode && (superblockMode || concentricMode)) && !(!loopMode && contourMode) && (
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                        headerExtra={
                            !loopMode && !gridVariant && (
                                <Tip title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")} description={tip("CulDeSacMode")}>
                                    <span>
                                        <ToggleControl checked={culDeSacMode} onChange={setCulDeSacMode} />
                                    </span>
                                </Tip>
                            )
                        }
                        expanded={(culDeSacMode || loopMode || gridVariant) && culDeSacExpanded}
                        onToggle={() => (culDeSacMode || loopMode || gridVariant) && setCulDeSacExpanded((value) => !value)}
                        locked={!culDeSacMode && !loopMode && !gridVariant}>
                        {!gridVariant && (
                        <SliderControl
                            label={<RowIcon icon={iconCulDeSacDepth} text={translate("GridRoadGenerator.UI.CulDeSacDepth", "Depth") as string} />}
                            tipTitle={translate("GridRoadGenerator.UI.CulDeSacDepth", "Depth") as string}
                            tip={tip("CulDeSacDepth")}
                            value={culDeSacDepth}
                            min={50}
                            max={100}
                            unit="%"
                            disabled={!culDeSacMode && !loopMode}
                            onChange={setCulDeSacDepth}
                            onDragPreview={(value) => setLivePreview(LiveField.CulDeSacDepth, value)}
                            onDragEnd={clearLivePreview}
                        />
                        )}
                        {!loopMode && !gridVariant && (
                            <>
                                <div className={styles.dropdownRow}>
                                    <Tip title={translate("GridRoadGenerator.UI.CulDeSacAxis", "Axis")} description={tip("CulDeSacAxis")}><span><RowIcon icon={iconAxis} text={translate("GridRoadGenerator.UI.CulDeSacAxis", "Axis") as string} /></span></Tip>
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
                                    tipTitle={translate("GridRoadGenerator.UI.CulDeSacRatio", "Frequency") as string}
                                    tip={tip("CulDeSacRatio")}
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
                                    tipTitle={translate("GridRoadGenerator.UI.Staggered", "Staggered") as string}
                                    tip={tip("Staggered")}
                                    checked={staggered}
                                    disabled={!culDeSacMode}
                                    onChange={setStaggered}
                                />
                            </>
                        )}
                        <div className={styles.dropdownRow}>
                            <Tip title={translate("GridRoadGenerator.UI.CulDeSacCapSize", "Turnaround size")} description={tip("CulDeSacCapSize")}><span><RowIcon icon={iconCircleOutline} text={translate("GridRoadGenerator.UI.CulDeSacCapSize", "Turnaround size") as string} /></span></Tip>
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
                            <Tip title={translate("GridRoadGenerator.UI.CulDeSacCapStyle", "Turnaround style")} description={tip("CulDeSacCapStyle")}><span><RowIcon icon={iconStyleGrass} text={translate("GridRoadGenerator.UI.CulDeSacCapStyle", "Turnaround style") as string} /></span></Tip>
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
                    {!loopMode && !gridVariant && (
                    <SectionFoldout
                        title={translate("GridRoadGenerator.UI.SectionAvenue", "Avenue")}
                        expanded={avenueExpanded}
                        onToggle={() => setAvenueExpanded((value) => !value)}>
                        <ToggleRow
                            label={<RowIcon icon={iconAvenueColumn} text={translate("GridRoadGenerator.UI.AvenueColumn", "Avenue column") as string} />}
                            tipTitle={translate("GridRoadGenerator.UI.AvenueColumn", "Avenue column") as string}
                            tip={tip("AvenueColumn")}
                            checked={avenueColumnEnabled}
                            onChange={setAvenueColumnEnabled}
                        />
                        <SliderControl
                            label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string} />}
                            tipTitle={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string}
                            tip={tip("AvenueColumnIndex")}
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
                            tipTitle={translate("GridRoadGenerator.UI.AvenueRow", "Avenue row") as string}
                            tip={tip("AvenueRow")}
                            checked={avenueRowEnabled}
                            onChange={setAvenueRowEnabled}
                        />
                        <SliderControl
                            label={<RowIcon icon={iconFrequency} text={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string} />}
                            tipTitle={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string}
                            tip={tip("AvenueRowIndex")}
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

                    {!twoColumns && rightColumn}
                    {networksPickerOpen &&
                        panelRef.current &&
                        createPortal(
                            <PrefabPicker slot={networksPickerOpen} onClose={() => setNetworksPickerOpen(null)} />,
                            panelRef.current,
                        )}
                    </VC.Scrollable>
                  </div>
                  </div>
                  {twoColumns && (
                    <div className={`${styles.scrollWrapper} ${styles.sideColumn}`}>
                      <VC.Scrollable className={styles.scrollable}>{rightColumn}</VC.Scrollable>
                    </div>
                  )}
                 </div>
                </div>

                {/* Action : bouton primaire natif, hors de la zone défilante ci-dessus
                    (.content a sa propre zone défilante, VC.Scrollable natif) pour rester
                    toujours visible. Pas de bouton "Tout annuler" — redondant avec Échap
                    (vide la sélection) et le clic droit (retire le dernier nœud), déjà bien
                    plus rapides d'accès. */}
                <div className={styles.actions}>
                    {/* Résumé : sélection (nœuds / points / hectares), puis ce que Générer construira. */}
                    {(nodeCount > 0 || summarySegments > 0) && (
                        <div className={styles.summary}>
                            <SelectionCount count={nodeCount} invalid={perimeterInvalid} />
                            {summarySegments > 0 && (
                                <Tip title={translate("GridRoadGenerator.UI.Summary", "Summary")} description={tip("Summary")}>
                                    <div className={styles.summaryFigures}>
                                        <span className={styles.summarySeparator}>·</span>
                                        <span>{summarySegments} {translate("GridRoadGenerator.UI.SummarySegments", "segments")}</span>
                                        <span className={styles.summarySeparator}>·</span>
                                        <span>{formatNumber(summaryLength / 1000, 1)} km</span>
                                        <span className={styles.summarySeparator}>·</span>
                                        <span className={styles.summaryCost}>≈ ¢{formatNumber(summaryCost)}</span>
                                    </div>
                                </Tip>
                            )}
                        </div>
                    )}
                    <Tip title={translate("GridRoadGenerator.UI.Generate", "Generate")} description={tip("Generate")}>
                        <div>
                            <SafeButton
                                variant="primary"
                                className={styles.applyButton}
                                disabled={!canApply}
                                onSelect={generateGrid}>
                                {translate("GridRoadGenerator.UI.Generate", "Generate")}
                            </SafeButton>
                        </div>
                    </Tip>
                    {perimeterCollision ? (
                        <span className={styles.collisionHint}>
                            {translate(
                                "GridRoadGenerator.UI.CollisionHint",
                                "Collision detected — the real preview is now shown so you can see where. Adjust and try again. If it persists, turn on Anarchy.",
                            )}
                        </span>
                    ) : selectionMode === 0 ? (
                        // Double-clic sur un nœud : n'a de sens qu'avec un périmètre existant.
                        <span className={styles.generateHint}>
                            {translate(
                                "GridRoadGenerator.UI.GenerateHint",
                                "Double-click a node to auto-select the whole perimeter.",
                            )}
                        </span>
                    ) : null}
                </div>
            </div>
        </div>
    );
};
