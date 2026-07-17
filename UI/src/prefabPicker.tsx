// Sélecteur de réseau adapté du PrefabSearchPanel de CS2-NetworkTools
// (c) Luca Rager, licence MIT — https://github.com/lucarager/CS2-NetworkTools
// (recherche, onglets de catégories, récents, liste scrollable à miniatures).
import React, { useMemo, useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import styles from "./prefabPicker.module.scss";
import { VC } from "./vanilla";
import {
    PrefabEntry,
    pickAuto,
    pickPrefab,
    pickerData$,
    pickerType$,
    recentPrefabs$,
    roadPrefabAuto$,
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
};

export const PrefabPicker: React.FC<PrefabPickerProps> = ({ onClose }) => {
    const { translate } = useLocalization();
    const pickerType = useValue(pickerType$);
    const pickerData = useValue(pickerData$);
    const recentPrefabs = useValue(recentPrefabs$);
    const isAuto = useValue(roadPrefabAuto$);
    const [searchQuery, setSearchQuery] = useState("");

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
        pickPrefab(entry.Entity);
        onClose();
    };

    return (
        <div className={styles.panel}>
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
                            pickAuto();
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
