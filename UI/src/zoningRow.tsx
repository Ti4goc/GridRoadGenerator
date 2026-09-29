// Zonage automatique, en une ligne (retour utilisateur : panneau trop grand sur un petit écran) :
// la zone choisie (icône + nom) ; un clic ouvre la grille des zones du jeu, un choix la referme.
// Zones du menu de zonage du jeu, mods compris (icônes relues côté C#). Voir GridRoadToolSystem.Zoning.
import React, { useState } from "react";
import { useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import { TIP_TEXT, Tip } from "./tips";
import { setZoningPrefab, zoneOptions$, zoningPrefab$ } from "./bindings";
import styles from "./zoningRow.module.scss";
import zoneBaseTop from "./zoneBaseTop.svg";
import zoneBaseSides from "./zoneBaseSides.svg";

type Zone = { name: string; icon: string; color: string };

/// Icône d'une zone ; si l'image ne se charge pas (zones de certains mods), socle isométrique
/// des icônes de zone du jeu, à la couleur de la zone.
const ZoneIcon = ({ zone, className }: { zone: Zone; className: string }) => {
    const [failed, setFailed] = useState(false);
    if (zone.icon && !failed) {
        return <img src={zone.icon} className={className} onError={() => setFailed(true)} />;
    }
    const color = zone.color || "#808080";
    return (
        <div className={`${className} ${styles.base}`}>
            <div className={styles.baseLayer} style={{ maskImage: `url(${zoneBaseSides})`, backgroundColor: color }} />
            <div className={`${styles.baseLayer} ${styles.baseShade}`} style={{ maskImage: `url(${zoneBaseSides})` }} />
            <div className={styles.baseLayer} style={{ maskImage: `url(${zoneBaseTop})`, backgroundColor: color }} />
        </div>
    );
};

export const ZoningRow = () => {
    const { translate } = useLocalization();
    const [open, setOpen] = useState(false);
    const selected = useValue(zoningPrefab$);
    const zones = useValue(zoneOptions$)
        .split("\n")
        .filter((line) => line.length > 0)
        .map((line) => {
            const [name, icon, color] = line.split("\t");
            return { name, icon: icon ?? "", color: color ?? "" } as Zone;
        });
    const zoneLabel = (name: string) => translate(`Assets.NAME[${name}]`, name) ?? name;
    const none = translate("GridRoadGenerator.UI.ZoningNone", "No zoning") ?? "No zoning";
    const current = zones.find((zone) => zone.name === selected);
    const choose = (name: string) => {
        setZoningPrefab(name);
        setOpen(false);
    };

    return (
        <div className={styles.zoning}>
            <div className={styles.row}>
                <Tip
                    title={translate("GridRoadGenerator.UI.Zoning", "Zoning") ?? "Zoning"}
                    description={translate("GridRoadGenerator.UI.Tip.Zoning", TIP_TEXT.Zoning) ?? TIP_TEXT.Zoning}>
                    <span className={styles.label}>{translate("GridRoadGenerator.UI.Zoning", "Zoning")}</span>
                </Tip>
                <button type="button" className={styles.chip} onClick={() => setOpen(!open)}>
                    {current ? <ZoneIcon key={current.name} zone={current} className={styles.chipIcon} /> : <span className={styles.chipNone}>∅</span>}
                    <span className={styles.chipName}>{current ? zoneLabel(current.name) : none}</span>
                    <span className={styles.chipChevron}>›</span>
                </button>
            </div>
            {open && (
                <div className={styles.grid}>
                    <Tip title={none} description="">
                        <button type="button" className={selected === "" ? `${styles.zone} ${styles.zoneActive}` : styles.zone} onClick={() => choose("")}>
                            <span className={styles.none}>∅</span>
                        </button>
                    </Tip>
                    {zones.map((zone) => (
                        <Tip key={zone.name} title={zoneLabel(zone.name)} description="">
                            <button
                                type="button"
                                className={selected === zone.name ? `${styles.zone} ${styles.zoneActive}` : styles.zone}
                                onClick={() => choose(zone.name)}>
                                <ZoneIcon zone={zone} className={styles.icon} />
                            </button>
                        </Tip>
                    ))}
                </div>
            )}
        </div>
    );
};
