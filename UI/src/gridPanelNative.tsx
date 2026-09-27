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
import { createPortal } from "react-dom";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
// Panel : confirmé fiable directement depuis cs2/ui (vérifié dans le bundle compilé du mod
// Move It, qui l'utilise ainsi sans aucun repli). InfoRow/InfoSection PAS depuis cs2/ui en
// revanche (voir vanilla.ts) : ni Move It ni CS2-NetworkTools ne les y trouvent non plus —
// tous deux passent par le registre de modules direct, comme VC.InfoRow/VC.InfoSection
// ci-dessous.
import { Panel } from "cs2/ui";
import styles from "./gridPanelNative.module.scss";
import gridIcon from "./gridIcon.svg";
import { locString } from "./locHelpers";
import { PrefabPicker } from "./prefabPicker";
import { RoadSelection } from "./roadSelection";
import { SafeButton } from "./safeButton";
import { SelectionRow } from "./selectionRow";
import { VC, VF, VT } from "./vanilla";
import { ViewSelection } from "./viewSelection";
import patternGrid from "./patternGrid.svg";
import patternLoop from "./patternLoop.svg";
import iconColumns from "./iconColumns.svg";
import iconRows from "./iconRows.svg";
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
    loopCulDeSacRatio$,
    loopMode$,
    mode$,
    nodeCount$,
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
    superblockMode$,
    setSuperblockZone,
    superblockZone$,
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

// Enveloppé dans un <div> custom à NOUS (styles.foldoutWrapper), PAS directement le
// VC.InfoSection natif — retour utilisateur en jeu : la marge entre sections restait
// invisible même avec !important, signe possible que VC.InfoSection n'a pas de vraie boîte
// CSS classique (ex. display: contents), auquel cas AUCUNE marge posée dessus, même en
// !important, ne peut jamais produire d'espace visuel. Un div plein, entièrement sous notre
// contrôle, garantit une vraie boîte quel que soit le rendu interne du composant natif.
const NativeSectionFoldout = ({ title, headerExtra, expanded, onToggle, locked, children }: NativeSectionFoldoutProps) => (
    <div className={styles.foldoutWrapper}>
        <VC.InfoSection>
            <VC.InfoRow
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
            {expanded && <div className={styles.foldoutBody}>{children}</div>}
        </VC.InfoSection>
    </div>
);

/// Divisoire de hauteur FIXE (style inline, pas une classe CSS) entre deux blocs — retour
/// utilisateur en jeu, "continua tudo igualzinho" même après margin-top !important sur
/// .panelContent/.foldoutBody : signe probable que Panel/InfoSection natifs imposent LEUR
/// PROPRE système d'espacement (peut-être via gap, silencieusement ignoré par cohtml — même
/// piège déjà rencontré ailleurs dans ce fichier) qui RÉINITIALISE la margin de leurs enfants
/// avec une règle qu'aucun !important externe ne peut battre de façon fiable. Un vrai élément
/// avec une HAUTEUR fixe, propriété qu'aucun système de marge/gap ne touche, ne peut PAS être
/// neutralisé de cette manière — solution de dernier recours mais garantie de fonctionner.
/// Divisoire de hauteur FIXE (style inline, pas une classe CSS) entre deux blocs — voir
/// l'enquête "está tudo igual"/canari rouge (Spacer confirmé fonctionnel : 80rem produisait
/// une barre énorme, donc 1rem ≈ 1px dans ce panneau — 10rem d'origine créait bien un espace,
/// juste trop discret pour être perçu comme tel). 14rem = ~14px, calibré pour être clairement
/// visible sans être excessif. Hauteur plutôt que margin/gap CSS : ni l'un ni l'autre n'était
/// fiable ici (gap silencieusement ignoré par cohtml, margin possiblement réinitialisée par le
/// système d'espacement propre à Panel/InfoSection natifs) — voir le fil complet de l'enquête.
const Spacer = ({ height = 14 }: { height?: number }) => <div style={{ height: `${height}rem`, flexShrink: 0 }} />;

/// Pastille icône + tooltip CSS (voir [data-tip] dans gridPanelNative.module.scss),
/// remplace un libellé texte à côté d'un champ natif (VC.FloatSliderField/
/// IntSliderField gardent label="" — voir .iconRow) ou dans un VC.InfoRow (left).
const RowIcon = ({ icon, tip }: { icon: string; tip: string }) => (
    <span className={styles.rowIcon} data-tip={tip} tabIndex={0}>
        <img src={icon} />
    </span>
);

/// Slider CUSTOM (piste/poignée en div, PAS VC.FloatSliderField/IntSliderField) — retour
/// utilisateur en jeu, "o painel continua lento... sempre, mesmo em Grid simples" : même
/// avec très peu de sections ouvertes (Geometria seule, ouverte par défaut), le panneau
/// restait lent. VC.*SliderField sont des widgets d'ÉDITEUR (voir vanilla.ts) détournés pour
/// un usage en jeu — jamais prévus pour être redessinés à haute fréquence pendant un drag, au
/// contraire de ce slider (mêmes piste/poignée/track légers, éprouvés sans lenteur signalée,
/// que SliderControl du panneau custom gridPanel.tsx). Remplit le même rôle visuel (icône +
/// piste + valeur) que l'ancien VC.FloatSliderField dans .vanillaField, mêmes classes CSS
/// $input-bg-color etc. réutilisées pour rester visuellement cohérent avec le reste du panneau.
const NativeSlider = ({
    icon,
    tip,
    value,
    min,
    max,
    unit,
    disabled,
    round = true,
    onChange,
}: {
    icon: string;
    tip: string;
    value: number;
    min: number;
    max: number;
    unit?: string;
    disabled?: boolean;
    round?: boolean;
    onChange: (value: number) => void;
}) => {
    const trackRef = useRef<HTMLDivElement>(null);
    const pct = ((value - min) / (max - min)) * 100;

    const valueFromClientX = (clientX: number) => {
        const rect = trackRef.current?.getBoundingClientRect();
        if (!rect || rect.width === 0) {
            return value;
        }
        const ratio = Math.min(Math.max((clientX - rect.left) / rect.width, 0), 1);
        const raw = min + ratio * (max - min);
        return Math.min(Math.max(round ? Math.round(raw) : raw, min), max);
    };

    const startDrag = (event: React.MouseEvent) => {
        if (disabled) {
            return;
        }
        onChange(valueFromClientX(event.clientX));
        const onMove = (ev: MouseEvent) => onChange(valueFromClientX(ev.clientX));
        const onUp = () => {
            window.removeEventListener("mousemove", onMove);
            window.removeEventListener("mouseup", onUp);
        };
        window.addEventListener("mousemove", onMove);
        window.addEventListener("mouseup", onUp);
    };

    return (
        <div className={disabled ? `${styles.iconRow} ${styles.iconRowDisabled}` : styles.iconRow}>
            <RowIcon icon={icon} tip={tip} />
            <div className={styles.nativeSliderField}>
                <div ref={trackRef} className={styles.nativeSliderTrack} onMouseDown={startDrag}>
                    <div className={styles.nativeSliderFill} style={{ width: `${pct}%` }} />
                    <div className={styles.nativeSliderHandle} style={{ left: `${pct}%` }} />
                </div>
                <span className={styles.nativeSliderValue}>
                    {round ? Math.round(value) : Math.round(value * 10) / 10}
                    {unit ? ` ${unit}` : ""}
                </span>
            </div>
        </div>
    );
};

/// Grand carré Padrão (icône + libellé) — voir la section Padrão pour le pourquoi (retour
/// utilisateur : préférence pour ce format vs. une petite rangée d'icônes).
const PatternTile = ({
    icon,
    label,
    selected,
    onSelect,
}: {
    icon: string;
    label: string;
    selected: boolean;
    onSelect: () => void;
}) => (
    <button
        type="button"
        className={selected ? `${styles.patternTile} ${styles.patternTileSelected}` : styles.patternTile}
        onClick={onSelect}>
        <img src={icon} className={styles.patternTileIcon} />
        <span className={styles.patternTileLabel}>{label}</span>
    </button>
);

/// Rangée "Rede" en haut de chaque groupe Arterial/Avenue/Principal (voir Redes) : le chip
/// de choix de prefab, même bindings que gridPanel.tsx (NetworkPrefabRow) mais styles natifs
/// (voir .networkPrefabButton dans gridPanelNative.module.scss) — parité fonctionnelle avec
/// le panneau custom, remplace l'ancienne absence totale de sélecteur de prefab par réseau
/// dans ce panneau (seul RoadSelection existait, réservé à la Grille classique).
const NativeNetworkPrefabRow = ({
    icon,
    tip,
    name,
    prefabIcon,
    onOpenPicker,
}: {
    icon: string;
    tip: string;
    name: string;
    prefabIcon: string;
    onOpenPicker: () => void;
}) => (
    <VC.InfoRow
        left={<RowIcon icon={icon} tip={tip} />}
        right={
            <button className={styles.networkPrefabButton} onClick={onOpenPicker}>
                {prefabIcon && <img src={prefabIcon} className={styles.networkPrefabIcon} />}
                <span className={styles.networkPrefabName}>{name || "—"}</span>
                <span className={styles.prefabChevron}>›</span>
            </button>
        }
    />
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
    const avenueColumnEnabled = useValue(avenueColumnEnabled$);
    const avenueColumnIndex = useValue(avenueColumnIndex$);
    const avenueRowEnabled = useValue(avenueRowEnabled$);
    const avenueRowIndex = useValue(avenueRowIndex$);
    const loopMode = useValue(loopMode$);
    const collectorSpacing = useValue(collectorSpacing$);
    const superblockMode = useValue(superblockMode$);
    const superblockZone = useValue(superblockZone$);
    const loopCulDeSacRatio = useValue(loopCulDeSacRatio$);
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
    const anarchyAvailable = useValue(anarchyAvailable$);
    const anarchyEnabled = useValue(anarchyEnabled$);
    const roadPrefabName = useValue(roadPrefabName$);
    const roadPrefabIcon = useValue(roadPrefabIcon$);
    const avenueRoadPrefabName = useValue(avenueRoadPrefabName$);
    const avenueRoadPrefabIcon = useValue(avenueRoadPrefabIcon$);
    const secondaryRoadPrefabName = useValue(secondaryRoadPrefabName$);
    const secondaryRoadPrefabIcon = useValue(secondaryRoadPrefabIcon$);
    // Replié : garde l'outil actif (seule la fermeture via le X le désactive).
    const [collapsed, setCollapsed] = useState(false);
    const [panelPosition, setPanelPosition] = useState<PanelPosition>(loadPanelPosition);
    const panelRef = useRef<HTMLDivElement>(null);
    // Seule la géométrie s'ouvre par défaut ; les autres sections restent repliées
    // tant que le joueur ne les déplie pas explicitement.
    const [geometryExpanded, setGeometryExpanded] = useState(true);
    const [culDeSacExpanded, setCulDeSacExpanded] = useState(false);
    const [avenueExpanded, setAvenueExpanded] = useState(false);
    const [networksExpanded, setNetworksExpanded] = useState(false);
    // Redes (mode Loop uniquement) : un seul déclencheur PrefabPicker à la fois, même
    // principe que gridPanel.tsx (networksPickerOpen) et roadSelection.tsx — portail vers
    // panelRef pour s'afficher hors de la zone défilante du panneau.
    const [networksPickerOpen, setNetworksPickerOpen] = useState<"primary" | "secondary" | "avenue" | null>(null);
    const networkAssetName = (name: string) => (name ? (translate(`Assets.NAME[${name}]`, name) ?? name) : "");

    if (!toolActive) {
        return null;
    }

    // Pendant le drag : déplace panelRef.current directement en style CSS (mutation DOM
    // impérative, PAS de setPanelPosition à chaque pixel) — retour utilisateur en jeu, "o
    // menu está lento a qualquer interação, quando o movo no ecrã". Contrairement au panneau
    // custom (surtout des div/button simples), ce panneau est fait de dizaines de composants
    // VC.* liés au moteur natif (ToolButton/Section/SliderField...), bien plus coûteux à
    // rendre — un setPanelPosition par mousemove (comme avant) re-rendait TOUT cet arbre à
    // chaque pixel parcouru pendant le drag. setPanelPosition n'est plus appelé qu'UNE fois,
    // à la fin du drag (mouseup), donc un seul re-render pour toute l'opération.
    const startDrag = (event: React.MouseEvent) => {
        const rect = panelRef.current?.getBoundingClientRect();
        if (!rect) {
            return;
        }
        const offsetX = event.clientX - rect.left;
        const offsetY = event.clientY - rect.top;

        const positionFrom = (ev: MouseEvent) =>
            clampToScreen(ev.clientX - offsetX, ev.clientY - offsetY, rect.width);
        const onMove = (ev: MouseEvent) => {
            const pos = positionFrom(ev);
            const el = panelRef.current;
            if (el) {
                el.style.left = `${pos.x}px`;
                el.style.top = `${pos.y}px`;
            }
        };
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
    // de NativeSectionFoldout, qui stoppe déjà lui-même la propagation du clic).
    const culDeSacToggle = (
        <VC.ToggleField
            value={culDeSacMode}
            disabled={false}
            onChange={(value: boolean) => setCulDeSacMode(value)}
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
                    <div className={styles.panelContent}>
                        {/* Padrão : grands carrés icône + libellé (retour utilisateur en jeu —
                            "gostava mais quando apresentavas os paterns... grandes quadrados
                            visuais", comme l'ancien panneau custom) plutôt que la petite rangée
                            d'icônes ToolButton par défaut. Bouton custom (pas VC.ToolButton —
                            son DOM interne n'est pas documenté côté cs2/ui, un redimensionnement
                            forcé en !important risquerait de casser son rendu) mais accent violet
                            sélectionné identique à celui du vrai ToolButton natif (voir
                            $accent-violet-tile, "état sélectionné violet vanilla" — commentaire
                            de vanilla.ts), donc visuellement fidèle malgré le DOM custom. */}
                        <div className={styles.vanillaRow}>
                            <span className={styles.networkGroupLabel}>
                                {translate("GridRoadGenerator.UI.PatternLabel", "Pattern")}
                            </span>
                            <div className={styles.patternTiles}>
                                <PatternTile
                                    icon={patternGrid}
                                    label={translate("GridRoadGenerator.UI.PatternGrid", "Grid") as string}
                                    selected={!loopMode}
                                    onSelect={() => setLoopMode(false)}
                                />
                                <PatternTile
                                    icon={patternLoop}
                                    label={translate("GridRoadGenerator.UI.PatternLoop", "Loop") as string}
                                    selected={loopMode}
                                    onSelect={() => setLoopMode(true)}
                                />
                            </div>
                        </div>

                        {/* "Vista", "Route" et "Seleção" : pas des sections repliables (voir
                            viewSelection.tsx/roadSelection.tsx/selectionRow.tsx), toujours
                            visibles en haut, avant la première section repliable. "Route"
                            (RoadSelection) grille classique uniquement — voir gridPanel.tsx :
                            en Loop, son contenu (3 chips de prefab) vit DANS la section Redes
                            (NativeNetworkPrefabRow ci-dessous), même parité que le panneau
                            custom. */}
                        <Spacer />
                        <ViewSelection />
                        <Spacer />
                        {!loopMode && (
                            <>
                                <RoadSelection portalContainer={panelRef.current} />
                                <Spacer />
                            </>
                        )}
                        <SelectionRow nodeCount={nodeCount} perimeterInvalid={perimeterInvalid} />
                        <Spacer />

                        {/* Géométrie : contenu dépend du Padrão sélectionné tout en haut — voir
                            gridPanel.tsx pour les mêmes notes (Colunas/Linhas/Modo/Espaçamento
                            grille classique uniquement, Espaçamento coletores/Frequência becos
                            Loop uniquement, Ângulo/Seguir o terreno partagés). */}
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.SectionGeometry", "Geometry")}
                            expanded={geometryExpanded}
                            onToggle={() => setGeometryExpanded((value) => !value)}>
                            {!loopMode && (
                                <>
                                    <VC.InfoRow
                                        left={<RowIcon icon={patternGrid} tip={translate("GridRoadGenerator.UI.ModeFit", "Fit to area") as string} />}
                                        right={
                                            <VC.ToolButton
                                                src="Media/Tools/Snap Options/ZoneGrid.svg"
                                                selected={fitMode}
                                                multiSelect={false}
                                                focusKey={VF.FOCUS_DISABLED}
                                                tooltip={translate("GridRoadGenerator.UI.ModeFit", "Fit to area")}
                                                onSelect={() => setMode(MODE_FIT)}
                                                className={VT.toolButton.button}
                                            />
                                        }
                                    />
                                    <Spacer height={6} />
                                    <VC.InfoRow
                                        left={<RowIcon icon="Media/Glyphs/Length.svg" tip={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing") as string} />}
                                        right={
                                            <VC.ToolButton
                                                src="Media/Glyphs/Length.svg"
                                                selected={!fitMode}
                                                multiSelect={false}
                                                focusKey={VF.FOCUS_DISABLED}
                                                tooltip={translate("GridRoadGenerator.UI.ModeFixed", "Fixed spacing")}
                                                onSelect={() => setMode(MODE_FIXED)}
                                                className={VT.toolButton.button}
                                            />
                                        }
                                    />
                                    <Spacer height={6} />
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon={iconColumns}
                                            tip={translate("GridRoadGenerator.UI.Columns", "Columns") as string}
                                            value={columns}
                                            min={1}
                                            max={12}
                                            disabled={!fitMode}
                                            onChange={(value) => setColumns(Math.round(value))}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon={iconRows}
                                            tip={translate("GridRoadGenerator.UI.Rows", "Rows") as string}
                                            value={rows}
                                            min={1}
                                            max={12}
                                            disabled={!fitMode}
                                            onChange={(value) => setRows(Math.round(value))}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon="Media/Glyphs/Length.svg"
                                            tip={translate("GridRoadGenerator.UI.SpacingShort", "Spacing") as string}
                                            value={spacing}
                                            min={10}
                                            max={300}
                                            unit="m"
                                            disabled={fitMode}
                                            onChange={setSpacing}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                </>
                            )}
                            {loopMode && superblockMode && (
                                <>
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon="Media/Glyphs/Length.svg"
                                            tip={translate("GridRoadGenerator.UI.SuperblockZoneSize", "Zone size") as string}
                                            value={superblockZone}
                                            min={100}
                                            max={400}
                                            unit="m"
                                            onChange={setSuperblockZone}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                </>
                            )}
                            {loopMode && !superblockMode && (
                                <>
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon="Media/Glyphs/Length.svg"
                                            tip={translate("GridRoadGenerator.UI.CollectorSpacing", "Collector spacing") as string}
                                            value={collectorSpacing}
                                            min={200}
                                            max={400}
                                            unit="m"
                                            onChange={setCollectorSpacing}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon={iconFrequency}
                                            tip={translate("GridRoadGenerator.UI.LoopCulDeSacRatio", "Cul-de-sac frequency") as string}
                                            value={loopCulDeSacRatio}
                                            min={0}
                                            max={100}
                                            unit="%"
                                            onChange={setLoopCulDeSacRatio}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                </>
                            )}
                            <div className={styles.vanillaRow}>
                                <NativeSlider
                                    icon={iconAngle}
                                    tip={translate("GridRoadGenerator.UI.Angle", "Angle") as string}
                                    value={angleOffset}
                                    min={-90}
                                    max={90}
                                    unit="°"
                                    onChange={setAngleOffset}
                                />
                            </div>
                            <Spacer height={6} />
                            <VC.InfoRow
                                left={<RowIcon icon={iconTerrain} tip={translate("GridRoadGenerator.UI.FollowTerrain", "Follow terrain") as string} />}
                                right={
                                    <VC.ToggleField
                                        value={followTerrain}
                                        disabled={false}
                                        onChange={(value: boolean) => setFollowTerrain(value)}
                                    />
                                }
                            />
                        </NativeSectionFoldout>
                        <Spacer />

                        {/* Culs-de-sac : quartier pavillonnaire — Axis/Frequency/Staggered/le
                            toggle d'activation lui-même n'ont pas de sens en mode Loop, qui a
                            déjà SA PROPRE fréquence dans Geometria (LoopCulDeSacRatio). Depth,
                            lui, s'applique aussi en mode Loop (voir Core/GridGenerator.cs
                            EmitLoopBlock, spurLength = ... * CulDeSacDepth) — masqué à tort avant
                            ce correctif. Le cercle de retournement (CapSize/CapStyle) reste
                            partagé, toujours visible, même en mode Loop. */}
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.CulDeSac", "Cul-de-sac")}
                            headerExtra={!loopMode ? culDeSacToggle : undefined}
                            expanded={(culDeSacMode || loopMode) && culDeSacExpanded}
                            onToggle={() => (culDeSacMode || loopMode) && setCulDeSacExpanded((value) => !value)}
                            locked={!culDeSacMode && !loopMode}>
                            <div className={styles.vanillaRow}>
                                <NativeSlider
                                    icon={iconCulDeSacDepth}
                                    tip={translate("GridRoadGenerator.UI.CulDeSacDepth", "Depth") as string}
                                    value={culDeSacDepth}
                                    min={50}
                                    max={90}
                                    unit="%"
                                    disabled={!culDeSacMode && !loopMode}
                                    onChange={setCulDeSacDepth}
                                />
                            </div>
                            <Spacer height={6} />
                            {!loopMode && (
                                <>
                                    <VC.InfoRow
                                        left={<RowIcon icon={iconAxis} tip={translate("GridRoadGenerator.UI.CulDeSacAxis", "Axis") as string} />}
                                        right={
                                            <VC.DropdownField
                                                items={culDeSacAxisItems}
                                                value={culDeSacAxis}
                                                disabled={!culDeSacMode}
                                                onChange={(value: number) => setCulDeSacAxis(value)}
                                            />
                                        }
                                    />
                                    <Spacer height={6} />
                                    <div className={styles.vanillaRow}>
                                        <NativeSlider
                                            icon={iconFrequency}
                                            tip={translate("GridRoadGenerator.UI.CulDeSacRatio", "Frequency") as string}
                                            value={culDeSacRatio}
                                            min={0}
                                            max={100}
                                            unit="%"
                                            disabled={!culDeSacMode}
                                            onChange={setCulDeSacRatio}
                                        />
                                    </div>
                                    <Spacer height={6} />
                                    <VC.InfoRow
                                        left={<RowIcon icon={iconStaggered} tip={translate("GridRoadGenerator.UI.Staggered", "Staggered") as string} />}
                                        right={
                                            <VC.ToggleField
                                                value={staggered}
                                                disabled={!culDeSacMode}
                                                onChange={(value: boolean) => setStaggered(value)}
                                            />
                                        }
                                    />
                                    <Spacer height={6} />
                                </>
                            )}
                            <VC.InfoRow
                                left={<RowIcon icon={iconCircleOutline} tip={translate("GridRoadGenerator.UI.CulDeSacCapSize", "Turnaround size") as string} />}
                                right={
                                    <VC.DropdownField
                                        items={capSizeItems}
                                        value={culDeSacCapSize}
                                        disabled={false}
                                        onChange={(value: number) => setCulDeSacCapSize(value)}
                                    />
                                }
                            />
                            <Spacer height={6} />
                            <VC.InfoRow
                                left={<RowIcon icon={iconStyleGrass} tip={translate("GridRoadGenerator.UI.CulDeSacCapStyle", "Turnaround style") as string} />}
                                right={
                                    <VC.DropdownField
                                        items={capStyleItems}
                                        value={culDeSacCapStyle}
                                        disabled={false}
                                        onChange={handleCapStyleChange}
                                    />
                                }
                            />
                        </NativeSectionFoldout>
                        <Spacer />

                        {/* Avenue : troisième réseau, colonne et/ou rangée choisie librement par
                            index (grille classique uniquement — masquée en mode Loop). Une
                            rotonde est ajoutée automatiquement côté Core si les deux axes sont
                            actifs (voir EmitAvenueRoundabout). */}
                        {!loopMode && (
                        <>
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.SectionAvenue", "Avenue")}
                            expanded={avenueExpanded}
                            onToggle={() => setAvenueExpanded((value) => !value)}>
                            <VC.InfoRow
                                left={<RowIcon icon={iconAvenueColumn} tip={translate("GridRoadGenerator.UI.AvenueColumn", "Avenue column") as string} />}
                                right={
                                    <VC.ToggleField
                                        value={avenueColumnEnabled}
                                        disabled={false}
                                        onChange={(value: boolean) => setAvenueColumnEnabled(value)}
                                    />
                                }
                            />
                            <Spacer height={6} />
                            <div className={styles.vanillaRow}>
                                <NativeSlider
                                    icon={iconFrequency}
                                    tip={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string}
                                    value={avenueColumnIndex}
                                    min={0}
                                    max={23}
                                    disabled={!avenueColumnEnabled}
                                    onChange={(value) => setAvenueColumnIndex(Math.round(value))}
                                />
                            </div>
                            <Spacer height={6} />
                            <VC.InfoRow
                                left={<RowIcon icon={iconAvenueRow} tip={translate("GridRoadGenerator.UI.AvenueRow", "Avenue row") as string} />}
                                right={
                                    <VC.ToggleField
                                        value={avenueRowEnabled}
                                        disabled={false}
                                        onChange={(value: boolean) => setAvenueRowEnabled(value)}
                                    />
                                }
                            />
                            <Spacer height={6} />
                            <div className={styles.vanillaRow}>
                                <NativeSlider
                                    icon={iconFrequency}
                                    tip={translate("GridRoadGenerator.UI.AvenueIndex", "Index") as string}
                                    value={avenueRowIndex}
                                    min={0}
                                    max={23}
                                    disabled={!avenueRowEnabled}
                                    onChange={(value) => setAvenueRowIndex(Math.round(value))}
                                />
                            </div>
                        </NativeSectionFoldout>
                        <Spacer />
                        </>
                        )}

                        {/* Redes (mode Loop uniquement, masquée sinon) : melhoramentos
                            automáticos aplicados au moment de la création via Game.Net.Upgraded
                            (voir GridRoadToolSystem.BuildAvenueUpgradeFlags/
                            BuildPrincipalUpgradeFlags). Icônes réels du jeu (Media/Game/Icons/*). */}
                        {loopMode && (
                        <NativeSectionFoldout
                            title={translate("GridRoadGenerator.UI.SectionNetworks", "Networks")}
                            expanded={networksExpanded}
                            onToggle={() => setNetworksExpanded((value) => !value)}>
                            <span className={styles.networkGroupLabel}>
                                {translate("GridRoadGenerator.UI.NetworkAvenue", "Avenue (collector)")}
                            </span>
                            <NativeNetworkPrefabRow
                                icon="Media/Game/Icons/Roads.svg"
                                tip={translate("GridRoadGenerator.UI.RoadPrefab", "Road") as string}
                                name={networkAssetName(avenueRoadPrefabName)}
                                prefabIcon={avenueRoadPrefabIcon}
                                onOpenPicker={() => setNetworksPickerOpen("avenue")}
                            />
                            <Spacer height={6} />
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
                            <Spacer height={10} />

                            <span className={styles.networkGroupLabel}>
                                {translate("GridRoadGenerator.UI.NetworkPrincipal", "Main (loop)")}
                            </span>
                            <NativeNetworkPrefabRow
                                icon="Media/Game/Icons/Roads.svg"
                                tip={translate("GridRoadGenerator.UI.RoadPrefab", "Road") as string}
                                name={networkAssetName(roadPrefabName)}
                                prefabIcon={roadPrefabIcon}
                                onOpenPicker={() => setNetworksPickerOpen("primary")}
                            />
                            <Spacer height={6} />
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
                        </NativeSectionFoldout>
                        )}
                        {networksPickerOpen &&
                            panelRef.current &&
                            createPortal(
                                <PrefabPicker slot={networksPickerOpen} onClose={() => setNetworksPickerOpen(null)} />,
                                panelRef.current,
                            )}
                    </div>
                )}
            </Panel>
        </div>
    );
};
