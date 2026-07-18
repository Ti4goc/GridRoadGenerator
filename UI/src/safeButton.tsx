// Garde-fou anti-crash pour Button/Tooltip (cs2/ui), même risque que celui déjà
// documenté dans gridPanelSwitch.tsx et corrigé pour InfoSectionFoldout (commit
// "Corrige le crash 'l'UI disparaît en cliquant sur le bouton du mod'") : ces
// composants existent dans les types mais pas forcément au runtime, et un accès
// direct fait planter tout l'arbre cohtml, pas seulement notre panneau. Contrairement
// à InfoSectionFoldout (remplaçable par une combinaison d'InfoSection/InfoRow déjà
// garantis), Button/Tooltip n'ont pas d'équivalent composable à partir d'autres
// composants garantis : on bascule donc sur du HTML natif en repli, fonctionnel
// mais non stylé, plutôt que de risquer un crash.
import React from "react";
import { Button, Tooltip } from "cs2/ui";

const HAS_BUTTON = Button != null;
const HAS_TOOLTIP = Tooltip != null;

if (!HAS_BUTTON) {
    // eslint-disable-next-line no-console
    console.warn("[GridRoadGenerator] Button (cs2/ui) indisponible au runtime : repli sur <button> natif.");
}
if (!HAS_TOOLTIP) {
    // eslint-disable-next-line no-console
    console.warn("[GridRoadGenerator] Tooltip (cs2/ui) indisponible au runtime : infobulle désactivée.");
}

type SafeButtonProps = {
    variant?: "primary" | "flat" | "floating";
    className?: string;
    disabled?: boolean;
    selected?: boolean;
    src?: string;
    onSelect?: () => void;
    children?: React.ReactNode;
};

export const SafeButton = ({ variant, className, disabled, selected, src, onSelect, children }: SafeButtonProps) =>
    HAS_BUTTON ? (
        <Button variant={variant} className={className} disabled={disabled} selected={selected} src={src} onSelect={onSelect}>
            {children}
        </Button>
    ) : (
        <button type="button" className={className} disabled={disabled} onClick={onSelect}>
            {src && <img src={src} alt="" />}
            {children}
        </button>
    );

type SafeTooltipProps = {
    tooltip: React.ReactNode;
    delayTime?: number;
    direction?: "up" | "down" | "left" | "right";
    /// Tooltip (cs2/ui) exige un unique élément React (il lui attache une ref) ;
    /// jamais du texte brut ou plusieurs enfants.
    children: React.ReactElement;
};

// Sans Tooltip natif, on rend l'enfant tel quel : pas d'infobulle plutôt qu'un crash.
export const SafeTooltip = ({ tooltip, delayTime, direction, children }: SafeTooltipProps) =>
    HAS_TOOLTIP ? (
        <Tooltip tooltip={tooltip} delayTime={delayTime} direction={direction}>
            {children}
        </Tooltip>
    ) : (
        <>{children}</>
    );
