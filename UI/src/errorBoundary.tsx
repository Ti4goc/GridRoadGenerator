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
    /// Rendu à la place des enfants si leur rendu lève une exception.
    fallback: React.ReactNode;
}

interface ErrorBoundaryState {
    hasError: boolean;
}

export class RenderErrorBoundary extends React.Component<ErrorBoundaryProps, ErrorBoundaryState> {
    state: ErrorBoundaryState = { hasError: false };

    static getDerivedStateFromError(): ErrorBoundaryState {
        return { hasError: true };
    }

    componentDidCatch(error: unknown) {
        // eslint-disable-next-line no-console
        console.error("[GridRoadGenerator] Erreur de rendu interceptée, repli sur le panneau de secours :", error);
    }

    render() {
        return this.state.hasError ? this.props.fallback : this.props.children;
    }
}
