// Rangée "Vista" (View) : bascule l'affichage des réseaux souterrains, de la grille de
// zonage et des réseaux normalement invisibles pendant que l'outil est actif — pattern
// ET icônes (Assets/View/*.svg) repris tels quels de CS2-NetworkTools (c) yenyang,
// licence MIT — https://github.com/lucarager/CS2-NetworkTools
// N'est PAS une section repliable (pas de chevron/titre cliquable comme les
// SectionFoldout/NativeSectionFoldout) : toujours visible en haut du panneau.
import React from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { availableViews$, selectedViews$, setSelectedViews } from "bindings";
import { VC, VF, VT } from "./vanilla";
import viewUnderground from "./viewUnderground.svg";
import viewZoneGrid from "./viewZoneGrid.svg";
import viewInvisibleNetworks from "./viewInvisibleNetworks.svg";
import styles from "./viewSelection.module.scss";

const VIEW_UNDERGROUND = 1 << 0;
const VIEW_ZONE_GRID = 1 << 1;
const VIEW_INVISIBLE_NETWORKS = 1 << 2;

const VIEW_FLAGS = [
    {
        flag: VIEW_UNDERGROUND,
        icon: viewUnderground,
        tooltipKey: "GridRoadGenerator.UI.ViewUnderground",
        tooltipFallback: "Underground",
    },
    {
        flag: VIEW_ZONE_GRID,
        icon: viewZoneGrid,
        tooltipKey: "GridRoadGenerator.UI.ViewZoneGrid",
        tooltipFallback: "Zone grid",
    },
    {
        flag: VIEW_INVISIBLE_NETWORKS,
        icon: viewInvisibleNetworks,
        tooltipKey: "GridRoadGenerator.UI.ViewInvisibleNetworks",
        tooltipFallback: "Invisible networks",
    },
];

export const ViewSelection = () => {
    const { translate } = useLocalization();
    const available = useValue(availableViews$);
    const selected = useValue(selectedViews$);

    const visibleFlags = VIEW_FLAGS.filter((view) => (available & view.flag) !== 0);
    if (visibleFlags.length === 0) {
        return null;
    }

    return (
        <div className={styles.viewRow}>
            <span className={styles.viewLabel}>{translate("GridRoadGenerator.UI.ViewLabel", "View")}</span>
            <div className={styles.viewButtons}>
                {visibleFlags.map((view) => (
                    <VC.ToolButton
                        key={view.flag}
                        src={view.icon}
                        selected={(selected & view.flag) !== 0}
                        multiSelect={true}
                        disabled={false}
                        focusKey={VF.FOCUS_DISABLED}
                        tooltip={translate(view.tooltipKey, view.tooltipFallback)}
                        onSelect={() => setSelectedViews(selected ^ view.flag)}
                        className={VT.toolButton.button}
                    />
                ))}
            </div>
        </div>
    );
};
