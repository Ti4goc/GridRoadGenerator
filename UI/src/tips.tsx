// Infobulles de description au survol (demande utilisateur : "cada opção e ferramenta tenha
// uma descrição quando passo o rato por cima"). Même rendu que les infobulles des outils du
// jeu : bulle native (game-ui/common/tooltip/tooltip.tsx) et mise en forme titre + description
// du DescriptionTooltip natif (classes description-tooltip.module.scss), résolues via vanilla.ts.
import React, { ReactElement, ReactNode } from "react";
import { VC, VT } from "./vanilla";

/// Contenu d'une infobulle : titre en gras, description en dessous (comme les outils du jeu).
export const TipContent = ({ title, description }: { title?: ReactNode; description: ReactNode }) => {
    const classes = VT.descriptionTooltip ?? {};
    return (
        <div>
            {title ? (
                <div className={classes.header}>
                    <div className={classes.title}>{title}</div>
                </div>
            ) : null}
            {description ? <div className={classes.description}>{description}</div> : null}
        </div>
    );
};

/// Enveloppe `children` (un seul élément DOM, qui reçoit la ref de la bulle) d'une infobulle
/// native (titre seul possible : nom des zones). Sans titre ni description, ou si le module du jeu
/// est introuvable, rend `children` tel quel.
export const Tip = ({
    title,
    description,
    children,
}: {
    title?: ReactNode;
    description?: ReactNode;
    children: ReactElement;
}) => {
    if ((!description && !title) || !VC.Tooltip) {
        return children;
    }
    return <VC.Tooltip tooltip={<TipContent title={title} description={description} />}>{children}</VC.Tooltip>;
};

// Descriptions des infobulles (anglais d'abord, demande utilisateur : "vamos primeiro fazer em
// inglês"). Clés de localisation "GridRoadGenerator.UI.Tip.<clé>" : tant qu'une langue ne les
// définit pas, translate() retombe sur ce texte anglais.
export const TIP_TEXT: Record<string, string> = {
    PatternGrid: "A regular grid of streets fitted to the selected area. Can include an avenue column or row and cul-de-sacs.",
    PatternLoop: "Loops of local streets fed by collector roads, with optional cul-de-sacs. Good for quiet residential neighbourhoods.",
    PatternSuperblock: "Large blocks bounded by roads, with a pedestrian path network inside. Keeps through traffic out of the blocks.",
    PatternConcentric: "Rings that follow the shape of the selected area, linked to the centre by connecting roads.",
    PatternRadial: "A roundabout at the centre with straight avenues radiating to the perimeter, crossed by circular layers.",
    PatternTree: "A collector road through the area, with branch streets on both sides and short cul-de-sacs off the branches. All through traffic stays on the collector.",
    TreeBranchSpacing: "Distance between two branch streets on the same side of the collector.",
    TreeCulDeSacSpacing: "Distance between two pairs of cul-de-sacs along a branch street.",
    TreeCulDeSacLength: "Length of each cul-de-sac. It is shortened where needed so that tips of neighbouring branches and the perimeter stay clear.",
    PatternOrganic: "A suburban layout: a main street winds between two entrances, and curving streets grow off it, mostly ending in cul-de-sacs, some closing into loops.",
    OrganicStreetSpacing: "Distance kept between neighbouring streets, so there is room for lots on both sides.",
    OrganicCurviness: "How much the streets bend. At 0% they are straight.",
    OrganicLoopShare: "Share of streets that join another street to form a loop instead of ending in a cul-de-sac.",
    OrganicSeed: "Picks another random layout. The same value always gives the same layout.",
    PatternContour: "Streets follow the contour lines of the terrain, so they stay nearly level, linked by short uphill streets. Needs a hill or a slope.",
    ContourSpacing: "Distance between two neighbouring level streets, measured on the map.",
    ContourConnectorSpacing: "Distance between two uphill links along a level street. Links are never steeper than 15%.",
    ResetDefaults: "Restores every value in this panel to its default and turns off all upgrades. The pattern and the chosen networks are kept.",
    Anarchy: "Turns the Anarchy mod on or off. With Anarchy on, roads are built even where the game reports a collision.",
    ModeFit: "Divides the selected area into the number of columns and rows set below.",
    ModeFixed: "Uses a fixed distance between streets. The number of blocks depends on the size of the area.",
    Columns: "Number of blocks across the area. Used in Fit to area mode.",
    Rows: "Number of blocks along the area. Used in Fit to area mode.",
    Spacing: "Distance between parallel streets. Used in Fixed spacing mode.",
    SuperblockZone: "Size of each block inside the superblock. Larger values give fewer, bigger blocks.",
    ConcentricLayers: "Number of rings inside the shape. The maximum depends on the size of the selected area.",
    ConcentricConnections: "Number of roads that link the rings, from the perimeter to the centre.",
    RadialLayers: "Number of circular layers around the roundabout, spread evenly up to the farthest point of the area. Where a layer runs close to the perimeter, it curves in to meet it.",
    RadialAvenues: "Number of straight avenues leaving the roundabout, equally spaced. An avenue that would meet the perimeter at a tight angle is left out.",
    RadialRoundabout: "Radius of the central roundabout. The minimum grows with the number of avenues, so their junctions stay far enough apart.",
    CollectorSpacing: "Distance between the collector roads that feed the loops.",
    LoopCulDeSacRatio: "Share of loops that become cul-de-sacs instead of full loops.",
    Angle: "Rotates the pattern inside the selected area.",
    FollowTerrain: "Roads follow the height of the terrain instead of staying level.",
    CulDeSacMode: "Adds dead-end streets between the grid roads.",
    CulDeSacDepth: "Length of each cul-de-sac, as a share of the block depth.",
    CulDeSacAxis: "Direction in which the cul-de-sacs are laid out.",
    CulDeSacRatio: "Share of the streets that become cul-de-sacs.",
    Staggered: "Alternates cul-de-sacs from one side of the street to the other instead of lining them up.",
    CulDeSacCapSize: "Size of the turning circle at the end of each cul-de-sac.",
    CulDeSacCapStyle: "What fills the middle of the turning circle.",
    AvenueColumn: "Turns one column of the grid into an avenue. A roundabout is added where it crosses an avenue row.",
    AvenueColumnIndex: "Which column becomes the avenue, counted from the start of the grid.",
    AvenueRow: "Turns one row of the grid into an avenue. A roundabout is added where it crosses an avenue column.",
    AvenueRowIndex: "Which row becomes the avenue, counted from the start of the grid.",
    NetworkTabs: "Choose which network to set up: its road type and its upgrades.",
    NetworkPrefab: "Road type used for this network. Click to choose another one.",
    UpgradeMiddleTrees: "Adds trees on the median.",
    UpgradeMiddleGrass: "Adds grass on the median.",
    UpgradeSideTrees: "Adds trees along this side of the road.",
    UpgradeSideGrass: "Adds a grass strip along this side of the road. Can't be combined with a wide sidewalk.",
    UpgradeWideSidewalk: "Widens the sidewalk on this side and removes parking. Can't be combined with a grass strip.",
    UpgradeBikeLane: "Adds a bicycle lane on this side of the road.",
    ViewUnderground: "Shows underground networks while the tool is open.",
    ViewZoneGrid: "Shows the zoning grid along roads while the tool is open.",
    ViewInvisibleNetworks: "Shows invisible networks (such as markers and hidden paths) while the tool is open.",
    SelectionExisting: "Build inside a loop of roads that already exist: click the nodes around the area.",
    SelectionFreeArea: "Draw the area yourself on the terrain, point by point, like a district. A road is built along its edge, then the pattern fills it.",
    AreaPoints: "Points of the area drawn so far. Click the terrain to add points; click the first point or double-click to close the area. Right-click removes the last point (or reopens the area), Esc clears it.",
    SelectionBrush: "Paint the area on the terrain with a brush: hold left-click to paint, right-click to erase. The outline is smoothed and a road is built along it.",
    BrushSize: "Diameter of the brush circle, or side of the square brush.",
    AlignTerrain: "Turns the grid so that one direction of streets follows the contour lines of the terrain: those streets stay nearly level, the others go straight up the slope. The angle setting still adds to it.",
    PatternMixed: "A radial centre (roundabout, avenues and rings) surrounded by winding streets with cul-de-sacs. A ring road joins both, and the avenues of the centre carry on into the neighbourhood.",
    MixedCoreRadius: "Radius of the radial centre. On a narrow area the centre is made smaller, or left out.",
    PedestrianLinks: "Links the end of each cul-de-sac to the nearest street ahead with a short path (up to 90 m), as in real suburbs: people on foot or by bike no longer walk around the whole block.",
    NetworkPath: "Network used for the paths between cul-de-sacs: pedestrian path by default, or a bike path, etc.",
    Zoning: "Zone automatically painted along the new roads once they are built. Only empty cells are zoned: existing zoning and buildings are never changed.",
    Summary: "What Generate will build: number of road segments, total length and estimated construction cost (bridges, tunnels and intersections can add to it).",
    Undo: "Undo the last change: points, brush strokes and panel settings. Shortcut: Ctrl+Z.",
    Redo: "Redo what was undone. Shortcut: Ctrl+Y or Ctrl+Shift+Z.",
    BrushShape: "Shape of the brush: round, or square (aligned with the map).",
    BrushAngle: "Rotation of the square brush. In the game, hold Shift and move the mouse sideways to turn it.",
    PaintedArea: "Size of the painted area, in hectares. Only the largest painted patch is used, and holes inside it are filled.",
    NetworkRoundabout: "Road type of the central roundabout. A one-way road is laid in the driving direction of your city. Auto uses the local street network.",
    NodesSelected: "Nodes of the perimeter selected so far. Click the nodes of existing roads around the area; double-click a node to select the whole loop. Right-click removes the last node, Esc clears the selection.",
    Generate: "Builds the roads shown in the preview. If they would collide with something, the real preview shows where so you can adjust.",
};
