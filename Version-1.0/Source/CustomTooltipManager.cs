using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public static class CustomTooltipManager
  {
    // Tracks our active runtime tooltip container element statically to prevent layout clones across the app
    private static VisualElement _activeTooltipElement;

    public static void ShowTooltip(VisualElement targetElement, string text)
    {
      HideTooltip(); // Ensure any existing tooltip is cleared first

      // Safety check to ensure we have a valid target to attach the tooltip to
      if (targetElement == null || targetElement.panel == null || targetElement.panel.visualTree == null)
      {
        return;
      }

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

      tooltipBox.style.backgroundColor = new StyleColor(new Color(0.15f, 0.11f, 0.11f, 1.0f)); // Vanilla dark brown

      // Golden trim framing
      tooltipBox.style.borderTopColor = tooltipBox.style.borderBottomColor =
        tooltipBox.style.borderLeftColor = tooltipBox.style.borderRightColor = new StyleColor(new Color(0.6f, 0.5f, 0.35f)); // Vanilla gold border
      tooltipBox.style.borderTopWidth = tooltipBox.style.borderBottomWidth =
        tooltipBox.style.borderLeftWidth = tooltipBox.style.borderRightWidth = 1; // Thinner border
      tooltipBox.style.borderTopLeftRadius = tooltipBox.style.borderTopRightRadius =
        tooltipBox.style.borderBottomLeftRadius = tooltipBox.style.borderBottomRightRadius = 2; // Slightly sharper corners

      // Compact padding
      tooltipBox.style.paddingLeft = tooltipBox.style.paddingRight = 8;
      tooltipBox.style.paddingTop = tooltipBox.style.paddingBottom = 4;
      tooltipBox.style.whiteSpace = WhiteSpace.NoWrap;

      // Create the inner text block adhering to game fonts
      Label descriptionLabel = new Label(text);
      descriptionLabel.pickingMode = PickingMode.Ignore;
      descriptionLabel.AddToClassList("game-text-normal");
      descriptionLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f)); // True light gray
      descriptionLabel.style.fontSize = 12; // Vanilla default text size

      // Nest the elements together
      tooltipBox.Add(descriptionLabel);
      _activeTooltipElement.Add(tooltipBox);

      // Push layout directly to the top-level canvas rendering tree
      targetElement.panel.visualTree.Add(_activeTooltipElement);
    }

    public static void HideTooltip()
    {
      if (_activeTooltipElement != null)
      {
        _activeTooltipElement.RemoveFromHierarchy();
        _activeTooltipElement = null;
      }
    }
  }
}