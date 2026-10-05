import { useEffect, useState } from "react";

// Lecture de droite à gauche (arabe, persan) : les mods de traduction Arabic Localization et
// Persian Localization miroitent l'interface du jeu en remplissant leur <style> (vide quand
// leur bouton RTL est coupé). L'attribut dir="rtl" qu'ils posent sur <html> n'est pas fiable
// (vu absent en jeu alors que le miroir était actif), d'où la lecture du contenu des styles.
const RTL_STYLE_IDS = ["arabic-locale-rtl-style", "persian-localization-rtl-style"];

const isRtlActive = () => RTL_STYLE_IDS.some((id) => !!document.getElementById(id)?.textContent);

// Vrai tant qu'un de ces mods miroite l'interface ; suit le bouton RTL en direct (le mod
// remplace le texte du <style>, ce qui déclenche une mutation dans <head>).
export function useRtl(): boolean {
    const [rtl, setRtl] = useState(isRtlActive);
    useEffect(() => {
        const observer = new MutationObserver(() => setRtl(isRtlActive()));
        observer.observe(document.head, { childList: true, subtree: true, characterData: true });
        return () => observer.disconnect();
    }, []);
    return rtl;
}
