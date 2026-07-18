// Construit un LocElement (type structuré attendu par displayName sur les items
// de dropdown natifs, ex. DropdownItem de cs2/ui et le DropdownField vanilla
// game-ui/editor/widgets/fields/dropdown-field.tsx) à partir d'une chaîne déjà
// traduite via translate(). Sans ce wrapper "__Type", ces widgets natifs
// n'arrivent pas à reconnaître la forme de la valeur et affichent
// "<INVALID TYPE>" à la place du libellé — un simple string ne suffit pas,
// malgré ce que suggère la déclaration de type (non vérifiée, jamais utilisée
// en pratique) reprise de CS2-NetworkTools pour ce widget éditeur.
import { LocElement } from "cs2/l10n";

// "Game.UI.Localization.LocalizedString" est la valeur brute de LocElementType.String
// (cs2/l10n), inlinée directement : LocElementType s'est révélé indisponible au runtime
// (TypeError "Cannot read properties of undefined (reading 'String')" observé en jeu, le
// panneau plantait dès son rendu) — même catégorie de risque que Button/InfoSectionFoldout
// de cs2/ui, déjà rencontrée ailleurs dans ce mod. C'est un simple identifiant de type
// stable côté jeu, jamais amené à changer, donc rien ne justifie de dépendre du binding
// runtime pour cette seule valeur.
export const locString = (value: string): LocElement =>
    ({ __Type: "Game.UI.Localization.LocalizedString", id: null, value, args: null } as LocElement);
