// Sélecteur de réseau adapté du PrefabSearchPanel de CS2-NetworkTools
// (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
// (recherche, onglets de catégories, récents, liste scrollable à miniatures).
import React, { useLayoutEffect, useMemo, useRef, useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import styles from "./prefabPicker.module.scss";
import { VC } from "./vanilla";
import {
    PrefabEntry,
    pickAuto,
    pickAvenueAuto,
    pickAvenuePrefab,
    pickPrefab,
    pickSecondaryAuto,
    pickSecondaryPrefab,
    pickerData$,
    pickerType$,
    recentPrefabs$,
    roadPrefabAuto$,
    avenueRoadPrefabAuto$,
    secondaryRoadPrefabAuto$,
    setPickerType,
} from "bindings";

const TABS: { type: number; localeKey: string; fallback: string }[] = [
    { type: 0, localeKey: "GridRoadGenerator.UI.RoadPrefab", fallback: "Road" },
    { type: 1, localeKey: "GridRoadGenerator.UI.TabPath", fallback: "Path" },
    { type: 2, localeKey: "GridRoadGenerator.UI.TabRail", fallback: "Track" },
    { type: 3, localeKey: "GridRoadGenerator.UI.TabWaterway", fallback: "Waterway" },
];

type PrefabPickerProps = {
    onClose: () => void;
    /// Quel réseau ce picker modifie : "primary" (défaut, réseau principal), "secondary"
    /// (impasses/rayons) ou "avenue" (voir roadSelection.tsx). Détermine quels bindings/
    /// déclencheurs lire.
    slot?: "primary" | "secondary" | "avenue";
};

export const PrefabPicker: React.FC<PrefabPickerProps> = ({ onClose, slot = "primary" }) => {
    const { translate } = useLocalization();
    const pickerType = useValue(pickerType$);
    const pickerData = useValue(pickerData$);
    const recentPrefabs = useValue(recentPrefabs$);
    const isAutoPrimary = useValue(roadPrefabAuto$);
    const isAutoSecondary = useValue(secondaryRoadPrefabAuto$);
    const isAutoAvenue = useValue(avenueRoadPrefabAuto$);
    const isAuto = slot === "secondary" ? isAutoSecondary : slot === "avenue" ? isAutoAvenue : isAutoPrimary;
    const pick = slot === "secondary" ? pickSecondaryPrefab : slot === "avenue" ? pickAvenuePrefab : pickPrefab;
    const pickAutoForSlot =
        slot === "secondary" ? pickSecondaryAuto : slot === "avenue" ? pickAvenueAuto : pickAuto;
    const [searchQuery, setSearchQuery] = useState("");

    // Bascule à gauche du panneau si le côté droit déborde de l'écran (panneau
    // principal déplacé près du bord droit) — mesure le rendu réel plutôt que de
    // deviner en rem (pas de conversion rem→px fiable côté cohtml), donc un flash
    // d'un frame côté droit par défaut avant correction est possible mais discret
    // (le picker vient déjà de s'ouvrir, pas d'animation en cours à cet instant).
    // Ne bascule que dans un sens (jamais de retour à droite) : évite toute
    // oscillation si aucun des deux côtés ne suffit (panneau très large / écran
    // très étroit, cas limite non traité au-delà de ce choix déterministe).
    const panelElementRef = useRef<HTMLDivElement>(null);
    const [flipped, setFlipped] = useState(false);
    useLayoutEffect(() => {
        if (flipped) {
            return;
        }
        const measure = () => {
            const rect = panelElementRef.current?.getBoundingClientRect();
            if (rect && rect.right > window.innerWidth) {
                setFlipped(true);
            }
        };
        measure();
        window.addEventListener("resize", measure);
        return () => window.removeEventListener("resize", measure);
    }, [flipped]);

    const displayName = (entry: PrefabEntry) =>
        translate(`Assets.NAME[${entry.Name}]`, entry.Name) ?? entry.Name;

    const matchesQuery = (entry: PrefabEntry) =>
        displayName(entry).toLowerCase().includes(searchQuery.toLowerCase());

    const filtered = useMemo(
        () => (searchQuery.trim() ? pickerData.filter(matchesQuery) : pickerData),
        [pickerData, searchQuery, translate],
    );
    const filteredRecent = useMemo(
        () => (searchQuery.trim() ? recentPrefabs.filter(matchesQuery) : recentPrefabs),
        [recentPrefabs, searchQuery, translate],
    );

    const select = (entry: PrefabEntry) => {
        pick(entry.Entity);
        onClose();
    };

    return (
        <div ref={panelElementRef} className={flipped ? `${styles.panel} ${styles.panelFlipped}` : styles.panel}>
            <div className={styles.header}>
                <span className={styles.title}>
                    {translate("GridRoadGenerator.UI.PickerTitle", "Select network")}
                </span>
                <button className={styles.closeButton} onClick={onClose}>
                    ✕
                </button>
            </div>

            <div className={styles.searchBar}>
                <input
                    type="text"
                    placeholder={translate("GridRoadGenerator.UI.PickerSearch", "Search…") ?? ""}
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                />
            </div>

            <div className={styles.tabs}>
                {TABS.map((tab) => (
                    <button
                        key={tab.type}
                        className={
                            pickerType === tab.type ? `${styles.tab} ${styles.tabActive}` : styles.tab
                        }
                        onClick={() => setPickerType(tab.type)}>
                        {translate(tab.localeKey, tab.fallback)}
                    </button>
                ))}
            </div>

            <div className={styles.list}>
                <VC.Scrollable>
                    {/* Mode auto : suivre le prefab de l'outil route natif. */}
                    <div
                        className={isAuto ? `${styles.listItem} ${styles.listItemActive}` : styles.listItem}
                        onClick={() => {
                            pickAutoForSlot();
                            onClose();
                        }}>
                        <img src="Media/Tools/Snap Options/ExistingGeometry.svg" className={styles.listItemIcon} />
                        {translate("GridRoadGenerator.UI.PickerAuto", "Auto (follow road tool)")}
                    </div>

                    {filteredRecent.length > 0 && (
                        <>
                            <div className={styles.sectionLabel}>
                                {translate("GridRoadGenerator.UI.PickerRecent", "Recently used")}
                            </div>
                            {filteredRecent.map((entry) => (
                                <div
                                    key={`recent-${entry.Entity.index}-${entry.Entity.version}`}
                                    className={styles.listItem}
                                    onClick={() => select(entry)}>
                                    <img src={entry.Icon} className={styles.listItemIcon} />
                                    {displayName(entry)}
                                </div>
                            ))}
                            <div className={styles.sectionLabel}>
                                {translate("GridRoadGenerator.UI.PickerAll", "All")}
                            </div>
                        </>
                    )}

                    {filtered.length === 0 && (
                        <div className={styles.empty}>
                            {translate("GridRoadGenerator.UI.PickerEmpty", "No results")}
                        </div>
                    )}
                    {filtered.map((entry) => (
                        <div
                            key={`${entry.Entity.index}-${entry.Entity.version}`}
                            className={styles.listItem}
                            onClick={() => select(entry)}>
                            <img src={entry.Icon} className={styles.listItemIcon} />
                            {displayName(entry)}
                        </div>
                    ))}
                </VC.Scrollable>
            </div>
        </div>
    );
};
