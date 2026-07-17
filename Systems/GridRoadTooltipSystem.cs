// Pattern de tooltips contextuels adapté de CS2-NetworkTools (c) Luca Rager,
// licence MIT — https://github.com/lucarager/CS2-NetworkTools
using Game.Tools;
using Game.UI.Localization;
using Game.UI.Tooltip;

namespace GridRoadGenerator.Systems
{
    /// <summary>
    /// Affiche des tooltips contextuels près du curseur quand l'outil de grille est actif :
    /// quoi faire ensuite (sélectionner un nœud, valider), comment revenir en arrière
    /// (retirer le dernier nœud), et l'avertissement de périmètre dégénéré.
    /// </summary>
    public partial class GridRoadTooltipSystem : TooltipSystemBase
    {
        private ToolSystem m_ToolSystem;
        private StringTooltip _selectNodeTooltip;
        private StringTooltip _removeLastTooltip;
        private StringTooltip _confirmTooltip;
        private StringTooltip _invalidTooltip;
        private StringTooltip _perimeterDetectionFailedTooltip;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();

            _selectNodeTooltip = new StringTooltip
            {
                path = "gridRoadSelectNode",
                value = LocalizedString.Id("GridRoadGenerator.Tooltip.SelectNode")
            };
            _removeLastTooltip = new StringTooltip
            {
                path = "gridRoadRemoveLast",
                value = LocalizedString.Id("GridRoadGenerator.Tooltip.RemoveLast")
            };
            _confirmTooltip = new StringTooltip
            {
                path = "gridRoadConfirm",
                value = LocalizedString.Id("GridRoadGenerator.Tooltip.Confirm")
            };
            _invalidTooltip = new StringTooltip
            {
                path = "gridRoadInvalid",
                value = LocalizedString.Id("GridRoadGenerator.Tooltip.InvalidPerimeter")
            };
            _perimeterDetectionFailedTooltip = new StringTooltip
            {
                path = "gridRoadPerimeterDetectionFailed",
                value = LocalizedString.Id("GridRoadGenerator.Tooltip.PerimeterDetectionFailed")
            };
        }

        protected override void OnUpdate()
        {
            if (m_ToolSystem.activeTool is not GridRoadToolSystem tool)
            {
                return;
            }

            AddMouseTooltip(_selectNodeTooltip);
            if (tool.NodeCount > 0)
            {
                AddMouseTooltip(_removeLastTooltip);
            }
            if (tool.CanApply)
            {
                AddMouseTooltip(_confirmTooltip);
            }
            else if (tool.PerimeterInvalid)
            {
                AddMouseTooltip(_invalidTooltip);
            }
            if (tool.PerimeterDetectionFailed)
            {
                AddMouseTooltip(_perimeterDetectionFailedTooltip);
            }
        }
    }
}
