using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private void AttachLinkBehavior(Label label, RowData data)
    {
      if (data == null || string.IsNullOrEmpty(data.Url)) return;

      label.pickingMode = PickingMode.Position;
      Color hoverColor = new Color(0.4f, 0.7f, 1.0f);
      Color storedColor = Color.white;

      label.RegisterCallback<PointerEnterEvent>(evt =>
      {
        storedColor = label.resolvedStyle.color;
        label.style.color = new StyleColor(hoverColor);
        ShowCustomTooltip(label, data.Url);
      });

      label.RegisterCallback<PointerLeaveEvent>(evt =>
      {
        label.style.color = new StyleColor(storedColor);
        HideCustomTooltip();
      });

      label.RegisterCallback<PointerDownEvent>(evt =>
      {
        if (evt.button == 0)
        {
          Application.OpenURL(data.Url);
          evt.StopPropagation();
        }
      }, TrickleDown.TrickleDown);
    }

    private void AttachFolderLinkBehavior(Label label, RowData data)
    {
      if (data == null || string.IsNullOrEmpty(data.DirectoryPath)) return;

      label.pickingMode = PickingMode.Position;
      Color hoverColor = new Color(0.4f, 0.7f, 1.0f);
      Color storedColor = Color.white;

      label.RegisterCallback<PointerEnterEvent>(evt =>
      {
        storedColor = label.resolvedStyle.color;
        label.style.color = new StyleColor(hoverColor);
        ShowCustomTooltip(label, data.DirectoryPath);
      });

      label.RegisterCallback<PointerLeaveEvent>(evt =>
      {
        label.style.color = new StyleColor(storedColor);
        HideCustomTooltip();
      });

      label.RegisterCallback<PointerDownEvent>(evt =>
      {
        if (evt.button == 0)
        {
          string formattedPath = "file://" + data.DirectoryPath.Replace("\\", "/");
          Application.OpenURL(formattedPath);
          evt.StopPropagation();
        }
      }, TrickleDown.TrickleDown);
    }

    private void AttachManifestLinkBehavior(Label label, RowData data)
    {
      if (data == null || string.IsNullOrEmpty(data.DirectoryPath)) return;

      string manifestPath = System.IO.Path.Combine(data.DirectoryPath, "manifest.json");

      label.pickingMode = PickingMode.Position;
      Color hoverColor = new Color(0.4f, 0.7f, 1.0f);
      Color storedColor = Color.white;

      label.RegisterCallback<PointerEnterEvent>(evt =>
      {
        storedColor = label.resolvedStyle.color;
        label.style.color = new StyleColor(hoverColor);
        ShowCustomTooltip(label, manifestPath);
      });

      label.RegisterCallback<PointerLeaveEvent>(evt =>
      {
        label.style.color = new StyleColor(storedColor);
        HideCustomTooltip();
      });

      label.RegisterCallback<PointerDownEvent>(evt =>
      {
        if (evt.button == 0)
        {
          string formattedPath = "file://" + manifestPath.Replace("\\", "/");
          Application.OpenURL(formattedPath);
          evt.StopPropagation();
        }
      }, TrickleDown.TrickleDown);
    }

    // Handles hover states for the bottom layout utility buttons
    private void AttachButtonTooltipBehavior(Button button, string locKey)
    {
      button.RegisterCallback<PointerEnterEvent>(evt => ShowCustomTooltip(button, _loc.T(locKey)));
      button.RegisterCallback<PointerLeaveEvent>(evt => HideCustomTooltip());

      // Clear the tooltip instantly if the user clicks the button
      button.RegisterCallback<ClickEvent>(evt => HideCustomTooltip());
    }
  }
}