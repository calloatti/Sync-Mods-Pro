using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    // Tracks our active runtime tooltip container element to prevent layout clones
    private VisualElement _activeTooltipElement;

    private void ShowCustomTooltip(VisualElement targetElement, string text)
    {
      HideCustomTooltip(); // Ensure any existing tooltip is cleared first

      _activeTooltipElement = new VisualElement();
      _activeTooltipElement.pickingMode = PickingMode.Ignore;
      _activeTooltipElement.style.position = Position.Absolute;
      _activeTooltipElement.style.left = 0;
      _activeTooltipElement.style.right = 0;
      _activeTooltipElement.style.bottom = 45; // Positioned 45 pixels from the bottom of the screen
      _activeTooltipElement.style.alignItems = Align.Center;
      _activeTooltipElement.style.justifyContent = Justify.Center;

      // Build the inner styled box using the game's native design specs
      VisualElement tooltipBox = new VisualElement();
      tooltipBox.pickingMode = PickingMode.Ignore;
      tooltipBox.AddToClassList("tooltip");
      tooltipBox.AddToClassList("text--grey");

      tooltipBox.style.backgroundColor = new StyleColor(new Color(0.18f, 0.12f, 0.12f, 0.95f)); // Timberborn Maroon/Brown

      // Golden trim framing
      tooltipBox.style.borderTopColor = tooltipBox.style.borderBottomColor =
        tooltipBox.style.borderLeftColor = tooltipBox.style.borderRightColor = new StyleColor(new Color(0.71f, 0.61f, 0.44f)); // Timberborn Gold
      tooltipBox.style.borderTopWidth = tooltipBox.style.borderBottomWidth =
        tooltipBox.style.borderLeftWidth = tooltipBox.style.borderRightWidth = 2;
      tooltipBox.style.borderTopLeftRadius = tooltipBox.style.borderTopRightRadius =
        tooltipBox.style.borderBottomLeftRadius = tooltipBox.style.borderBottomRightRadius = 4;

      // Compact vertical padding
      tooltipBox.style.paddingLeft = tooltipBox.style.paddingRight = 14;
      tooltipBox.style.paddingTop = tooltipBox.style.paddingBottom = 3;
      tooltipBox.style.whiteSpace = WhiteSpace.NoWrap;

      // Create the inner text block adhering to game fonts
      Label descriptionLabel = new Label(text);
      descriptionLabel.pickingMode = PickingMode.Ignore;
      descriptionLabel.AddToClassList("game-text-normal");
      descriptionLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f)); // Soft white text
      descriptionLabel.style.fontSize = 12;

      // Nest the elements together
      tooltipBox.Add(descriptionLabel);
      _activeTooltipElement.Add(tooltipBox);

      // Push layout directly to the top-level canvas rendering tree
      if (targetElement.panel != null && targetElement.panel.visualTree != null)
      {
        targetElement.panel.visualTree.Add(_activeTooltipElement);
      }
    }

    private void HideCustomTooltip()
    {
      if (_activeTooltipElement != null)
      {
        _activeTooltipElement.RemoveFromHierarchy();
        _activeTooltipElement = null;
      }
    }
  }
}