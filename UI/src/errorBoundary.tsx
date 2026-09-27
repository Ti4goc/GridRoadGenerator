// Garde-fou anti-crash : une erreur de rendu dans NOTRE arbre React ne doit
// JAMAIS faire disparaître le reste de l'UI du jeu (vécu en jeu : une React
// error #130 — "Element type is invalid... got: undefined" — sur un composant
// cs2/ui absent au runtime malgré sa présence dans les types a cassé tout
// l'arbre cohtml, pas seulement notre panneau). Un composant classe est requis
// ici : React n'expose la capture d'erreurs de rendu (getDerivedStateFromError/
// componentDidCatch) qu'aux composants classe, pas aux hooks.
import React from "react";

interface ErrorBoundaryProps {
    children: React.ReactNode;
    /// Rendu à la place des enfants si leur rendu lève une exception. Fonction plutôt que
    /// noeud statique quand l'appelant veut afficher le message d'erreur lui-même (aucun
    /// des logs C# du jeu ne capture les erreurs JS/React, donc c'est le seul moyen pour le
    /// joueur de nous rapporter le message exact sans console de dev).
    fallback: React.ReactNode | ((error: unknown) => React.ReactNode);
}

interface ErrorBoundaryState {
    hasError: boolean;
    error: unknown;
}

export class RenderErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
    state: ErrorBoundaryState = { hasError: false, error: null };

    static getDerivedStateFromError(error: unknown): ErrorBoundaryState {
        return { hasError: true, error };
    }

    componentDidCatch(error: unknown) {
        // eslint-disable-next-line no-console
        console.error("[GridRoadGenerator] Erreur de rendu interceptée, repli sur le panneau de secours :", error);
    }

    render() {
        if (!this.state.hasError) {
            return this.props.children;
        }
        return typeof this.props.fallback === "function" ? this.props.fallback(this.state.error) : this.props.fallback;
    }
}
