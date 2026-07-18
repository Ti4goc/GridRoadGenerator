// Construit un LocElement (type structuré attendu par displayName sur les items
// de dropdown natifs, ex. DropdownItem de cs2/ui et le DropdownField vanilla
// game-ui/editor/widgets/fields/dropdown-field.tsx) à partir d'une chaîne déjà
// traduite via translate(). Sans ce wrapper "__Type", ces widgets natifs
// n'arrivent pas à reconnaître la forme de la valeur et affichent
// "<INVALID TYPE>" à la place du libellé — un simple string ne suffit pas,
// malgré ce que suggère la déclaration de type (non vérifiée, jamais utilisée
// en pratique) reprise de CS2-NetworkTools pour ce widget éditeur.
import { LocElement, LocElementType } from "cs2/l10n";

export const locString = (value: string): LocElement =>
    ({ __Type: LocElementType.String, id: null, value, args: null } as LocElement);
