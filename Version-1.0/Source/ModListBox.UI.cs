using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public partial class ModListBox
  {
    private static readonly int IconWidth = 40;
    private static readonly int NameWidth = 220;
    private static readonly int IdWidth = 220;
    private static readonly int FolderWidth = 220;
    private static readonly int MinVerWidth = 80;
    private static readonly int SavedVerWidth = 80;
    private static readonly int CurrentVerWidth = 80;
    private static readonly int StatusWidth = 60;
    private static readonly int StateColWidth = 50;

    private const int BasePadding = 28;
    private const float ScrollViewHeight = 480f;
    private const float TopBarMarginBottom = 10f;
    private const float ScrollViewMarginBottom = 0f;
    private const float HeaderRowHeight = 28f;
    private const float DataRowHeight = 32f;
    private const float TextVerticalOffset = -3f;

    private static readonly Color TextNormal = new Color(0.9f, 0.9f, 0.9f);

    private static readonly Color BgOddRow = new Color(0f, 0f, 0f, 0f);
    private static readonly Color BgEvenRow = new Color(0f, 0f, 0f, 0.15f);

    private Dictionary<string, List<RowUIElements>> _duplicateGroups = new Dictionary<string, List<RowUIElements>>();
    private List<RowUIElements> _orderedRows = new List<RowUIElements>();
    private HashSet<ModStatus> _activeFilters = new HashSet<ModStatus>();
    private bool _hideNotApplicable = true;

    private class RowUIElements
    {
      public RowData Data;
      public VisualElement Root;
      public Toggle TargetToggle; // Upgraded to Toggle
      public Label StatusLabel;
    }

    private int GetTotalTableWidth()
    {
      return IconWidth + NameWidth + IdWidth + FolderWidth + MinVerWidth + SavedVerWidth + CurrentVerWidth + StatusWidth + (StateColWidth * 3) + BasePadding + 5;
    }

    private VisualElement CreateTopBar()
    {
      VisualElement topBar = new VisualElement();
      topBar.style.flexDirection = FlexDirection.Row;
      topBar.style.alignItems = Align.Center;
      topBar.style.justifyContent = Justify.SpaceBetween; // Space left and right aligned items
      topBar.style.marginBottom = TopBarMarginBottom;

      // Label logic
      string saveText = "Unknown Save";
      if (_currentSaveReference != null && _currentSaveReference.SettlementReference != null)
      {
        saveText = $"{_currentSaveReference.SettlementReference.SettlementName} - {_currentSaveReference.SaveName}";
      }

      Label saveLabel = new Label(saveText);
      saveLabel.AddToClassList("text--default");
      saveLabel.style.fontSize = 14;
      saveLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
      saveLabel.pickingMode = PickingMode.Position;
      // ADDED: Align with "Mod Name" (8px row padding + 40px IconWidth)
      saveLabel.style.marginLeft = 20;

      Color hoverColor = new Color(0.4f, 0.7f, 1.0f);
      Color storedColor = Color.white;

      saveLabel.RegisterCallback<PointerEnterEvent>(evt =>
      {
        storedColor = saveLabel.resolvedStyle.color;
        saveLabel.style.color = new StyleColor(hoverColor);
      });

      saveLabel.RegisterCallback<PointerLeaveEvent>(evt =>
      {
        saveLabel.style.color = new StyleColor(storedColor);
      });

      saveLabel.RegisterCallback<ClickEvent>(evt =>
      {
        HandleSaveLabelClick();
      });

      topBar.Add(saveLabel);

      VisualElement filtersContainer = new VisualElement();
      filtersContainer.style.flexDirection = FlexDirection.Row;
      filtersContainer.style.alignItems = Align.Center;
      filtersContainer.style.justifyContent = Justify.FlexEnd;

      var masterStatuses = new Dictionary<string, ModStatus>
      {
        { "Calloatti.SyncModsPro.Status.Match", ModStatus.Match },
        { "Calloatti.SyncModsPro.Status.Version", ModStatus.Version },
        { "Calloatti.SyncModsPro.Status.Disabled", ModStatus.Disabled },
        { "Calloatti.SyncModsPro.Status.Missing", ModStatus.Missing },
        { "Calloatti.SyncModsPro.Status.New", ModStatus.New },
        { "Calloatti.SyncModsPro.Status.Duplicate", ModStatus.Duplicate }
      };

      foreach (var kvp in masterStatuses)
      {
        string statusKey = kvp.Key;
        ModStatus targetStatus = kvp.Value;

        VisualElement itemWrapper = new VisualElement();
        itemWrapper.style.flexDirection = FlexDirection.Row;
        itemWrapper.style.alignItems = Align.Center;
        itemWrapper.style.marginLeft = 12;

        Label label = new Label(_loc.T(statusKey));
        label.AddToClassList("text--default");
        label.style.fontSize = 12;
        label.style.marginRight = 4;
        itemWrapper.Add(label);

        Toggle statusToggle = new Toggle();
        statusToggle.AddToClassList("game-toggle");
        // Scale down to 90%
        statusToggle.style.scale = new StyleScale(new Vector2(0.9f, 0.9f));
        statusToggle.viewDataKey = statusKey;
        statusToggle.value = false;

        statusToggle.RegisterValueChangedCallback(evt =>
        {
          if (evt.newValue) _activeFilters.Add(targetStatus);
          else _activeFilters.Remove(targetStatus);

          ApplyFilters();
        });

        itemWrapper.Add(statusToggle);
        filtersContainer.Add(itemWrapper);
      }

      // Add Hide Not Applicable toggle
      VisualElement hideNaWrapper = new VisualElement();
      hideNaWrapper.style.flexDirection = FlexDirection.Row;
      hideNaWrapper.style.alignItems = Align.Center;
      hideNaWrapper.style.marginLeft = 12;

      Label hideNaLabel = new Label(_loc.T("Calloatti.SyncModsPro.Status.HideNotApplicable"));
      hideNaLabel.AddToClassList("text--default");
      hideNaLabel.style.fontSize = 12;
      hideNaLabel.style.marginRight = 4;
      hideNaWrapper.Add(hideNaLabel);

      Toggle hideNaToggle = new Toggle();
      hideNaToggle.AddToClassList("game-toggle");
      // Scale down to 90%
      hideNaToggle.style.scale = new StyleScale(new Vector2(0.9f, 0.9f));
      hideNaToggle.viewDataKey = "Calloatti.SyncModsPro.Status.HideNotApplicable";
      hideNaToggle.value = true;

      hideNaToggle.RegisterValueChangedCallback(evt =>
      {
        _hideNotApplicable = evt.newValue;
        ApplyFilters();
      });

      hideNaWrapper.Add(hideNaToggle);
      filtersContainer.Add(hideNaWrapper);

      topBar.Add(filtersContainer);
      return topBar;
    }

    private void ApplyFilters()
    {
      int visibleCount = 0;
      foreach (var rowUI in _orderedRows)
      {
        bool isVisible = _activeFilters.Count == 0 || _activeFilters.Contains(rowUI.Data.Status);

        if (_hideNotApplicable && rowUI.Data.Status == ModStatus.NotApplicable)
        {
          isVisible = false;
        }

        if (isVisible)
        {
          rowUI.Root.style.display = DisplayStyle.Flex;
          rowUI.Root.style.backgroundColor = new StyleColor(visibleCount % 2 == 0 ? BgEvenRow : BgOddRow);
          visibleCount++;
        }
        else
        {
          rowUI.Root.style.display = DisplayStyle.None;
        }
      }
    }

    private ScrollView CreateScrollView()
    {
      ScrollView scrollView = new ScrollView();
      scrollView.style.marginBottom = ScrollViewMarginBottom;
      scrollView.style.height = ScrollViewHeight;
      scrollView.style.flexGrow = 1;
      scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
      scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

      var dragger = scrollView.Q<VisualElement>(className: "unity-base-slider__dragger");
      if (dragger != null)
      {
        dragger.style.width = 20;
        dragger.style.minHeight = 58;
        dragger.style.backgroundColor = Color.clear;
        dragger.style.borderTopWidth = dragger.style.borderBottomWidth = dragger.style.borderLeftWidth = dragger.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-button-nine-slice");
        if (tex != null)
        {
          dragger.style.backgroundImage = new StyleBackground(tex);
          dragger.style.unitySliceTop = dragger.style.unitySliceBottom = dragger.style.unitySliceLeft = dragger.style.unitySliceRight = 14;
        }
      }

      var tracker = scrollView.Q<VisualElement>(className: "unity-base-slider__tracker");
      if (tracker != null)
      {
        tracker.style.width = 20;
        tracker.style.backgroundColor = Color.clear;
        tracker.style.borderTopWidth = tracker.style.borderBottomWidth = tracker.style.borderLeftWidth = tracker.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-bar-nine-slice");
        if (tex != null)
        {
          tracker.style.backgroundImage = new StyleBackground(tex);
          tracker.style.unitySliceTop = tracker.style.unitySliceBottom = 16;
        }
      }

      return scrollView;
    }

    private string GetStateString(ModState state)
    {
      switch (state)
      {
        case ModState.Enabled: return "[E]";
        case ModState.Disabled: return "[D]";
        case ModState.Missing: return "[M]";
        default: return "[-]";
      }
    }

    private void RepaintRow(RowUIElements rowUI)
    {
      if (rowUI.TargetToggle != null)
      {
        // Using SetValueWithoutNotify ensures repainting doesn't trigger an infinite update loop
        rowUI.TargetToggle.SetValueWithoutNotify(rowUI.Data.TargetState == ModState.Enabled);
      }

      if (rowUI.StatusLabel != null)
      {
        rowUI.Data.UpdateStatus();
        rowUI.StatusLabel.text = _loc.T($"Calloatti.SyncModsPro.Status.{rowUI.Data.Status}");
      }

      UnityEngine.Color freshStatusColor = rowUI.Data.GetStatusColor();
      rowUI.Root.Query<Label>().ForEach(lbl => lbl.style.color = new StyleColor(freshStatusColor));
    }

    private VisualElement CreateRow(string name, string id, string verFolder, string minVer, string savVer, string curVer, string status, string currentStr, string savedStr, string targetStr, Color color, bool isHeader, RowData data, bool isEven = false)
    {
      VisualElement row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      row.style.borderBottomWidth = 1;
      row.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));
      row.style.paddingLeft = 8;

      RowUIElements rowElements = null;
      if (!isHeader && data != null)
      {
        rowElements = new RowUIElements { Data = data, Root = row };
        _orderedRows.Add(rowElements);

        if (data.Source != ModSource.Missing)
        {
          if (!_duplicateGroups.ContainsKey(data.ModId)) _duplicateGroups[data.ModId] = new List<RowUIElements>();
          _duplicateGroups[data.ModId].Add(rowElements);
        }
      }

      if (isHeader)
      {
        row.style.height = HeaderRowHeight;
        row.style.borderBottomWidth = 2;
        Label headerIconCell = CreateCell("", IconWidth, color);
        row.Add(headerIconCell);
      }
      else
      {
        row.style.height = DataRowHeight;
        row.style.backgroundColor = new StyleColor(isEven ? BgEvenRow : BgOddRow);

        VisualElement iconColumnContainer = new VisualElement();
        iconColumnContainer.style.width = IconWidth;
        iconColumnContainer.style.flexShrink = 0;
        iconColumnContainer.style.justifyContent = Justify.Center;
        iconColumnContainer.style.alignItems = Align.Center;

        if (data != null)
        {
          if (data.Source == ModSource.Local)
          {
            VisualElement localIcon = new VisualElement();
            localIcon.style.width = 20;
            localIcon.style.height = 20;
            localIcon.AddToClassList("mod-item__icon--local");
            iconColumnContainer.Add(localIcon);
          }
          else if (data.Source == ModSource.Steam || !string.IsNullOrEmpty(data.SteamId))
          {
            VisualElement workshopIcon = new VisualElement();
            workshopIcon.style.width = 20;
            workshopIcon.style.height = 20;
            workshopIcon.AddToClassList("mod-item__icon--cloud");

            workshopIcon.pickingMode = PickingMode.Position;
            workshopIcon.RegisterCallback<PointerEnterEvent>(evt => workshopIcon.style.unityBackgroundImageTintColor = new StyleColor(Color.gray));
            workshopIcon.RegisterCallback<PointerLeaveEvent>(evt => workshopIcon.style.unityBackgroundImageTintColor = new StyleColor(Color.white));

            workshopIcon.RegisterCallback<ClickEvent>(evt =>
            {
              this.HandleCloudIconClick(data.SteamId, data.DisplayName);
            });

            iconColumnContainer.Add(workshopIcon);
          }
        }
        row.Add(iconColumnContainer);
      }

      if (!isHeader)
      {
        status = _loc.T($"Calloatti.SyncModsPro.Status.{status}");
      }

      Label cName = CreateCell(name, NameWidth, color);
      Label cId = CreateCell(id, IdWidth, color);
      Label cFolder = CreateCell(verFolder, FolderWidth, color);
      Label cMinVer = CreateCell(minVer, MinVerWidth, color);
      Label cSavVer = CreateCell(savVer, SavedVerWidth, color);
      Label cCurVer = CreateCell(curVer, CurrentVerWidth, color);
      Label cStatus = CreateCell(status, StatusWidth, color);

      if (!isHeader)
      {
        cName.style.marginTop = TextVerticalOffset;
        cId.style.marginTop = TextVerticalOffset;
        cFolder.style.marginTop = TextVerticalOffset;
        cMinVer.style.marginTop = TextVerticalOffset;
        cSavVer.style.marginTop = TextVerticalOffset;
        cCurVer.style.marginTop = TextVerticalOffset;
        cStatus.style.marginTop = TextVerticalOffset;

        if (rowElements != null) rowElements.StatusLabel = cStatus;

        AttachLinkBehavior(cName, data);
        AttachManifestLinkBehavior(cId, data);
        AttachFolderLinkBehavior(cFolder, data);
      }

      row.Add(cName);
      row.Add(cId);
      row.Add(cFolder);
      row.Add(cMinVer);
      row.Add(cSavVer);
      row.Add(cCurVer);
      row.Add(cStatus);

      Label cCurrent = CreateCell(currentStr, StateColWidth, color);
      Label cSaved = CreateCell(savedStr, StateColWidth, color);

      cCurrent.style.unityTextAlign = TextAnchor.MiddleCenter;
      cSaved.style.unityTextAlign = TextAnchor.MiddleCenter;

      VisualElement cTarget;

      if (isHeader)
      {
        Label cTargetLabel = CreateCell(targetStr, StateColWidth, color);
        cTargetLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        cTarget = cTargetLabel;
      }
      else
      {
        cCurrent.style.marginTop = TextVerticalOffset;
        cSaved.style.marginTop = TextVerticalOffset;

        VisualElement toggleContainer = new VisualElement();
        toggleContainer.style.width = StateColWidth;
        toggleContainer.style.flexShrink = 0;
        toggleContainer.style.alignItems = Align.Center;
        toggleContainer.style.justifyContent = Justify.Center;

        Toggle targetToggle = new Toggle();
        targetToggle.AddToClassList("game-toggle");

        // Scale the toggle down to 90% of its original size
        targetToggle.style.scale = new StyleScale(new Vector2(0.9f, 0.9f));

        targetToggle.SetValueWithoutNotify(data.TargetState == ModState.Enabled);

        // Missing mods cannot be interacted with
        if (data.Source == ModSource.Missing || data.TargetState == ModState.Missing)
        {
          targetToggle.SetEnabled(false);
        }
        else
        {
          targetToggle.RegisterValueChangedCallback(evt =>
          {
            bool isEnabling = evt.newValue;
            data.TargetState = isEnabling ? ModState.Enabled : ModState.Disabled;

            if (isEnabling)
            {
              if (data.DupStatus != -1)
              {
                data.DupStatus = 1;
              }

              foreach (var duplicateRow in _duplicateGroups[data.ModId])
              {
                if (duplicateRow.Data.UniqueRowKey != data.UniqueRowKey)
                {
                  duplicateRow.Data.TargetState = ModState.Disabled;
                  if (duplicateRow.Data.DupStatus != -1)
                  {
                    duplicateRow.Data.DupStatus = 0;
                  }
                  RepaintRow(duplicateRow);
                }
              }
            }
            if (rowElements != null) RepaintRow(rowElements);
          });
        }

        if (rowElements != null) rowElements.TargetToggle = targetToggle;
        toggleContainer.Add(targetToggle);
        cTarget = toggleContainer;
      }

      row.Add(cCurrent);
      row.Add(cSaved);
      row.Add(cTarget);

      return row;
    }

    private Label CreateCell(string text, float width, Color color)
    {
      Label lbl = new Label(text);
      lbl.AddToClassList("text--default");
      lbl.style.fontSize = 12;
      lbl.style.width = width;
      lbl.style.flexShrink = 0;
      lbl.style.color = color;
      lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
      lbl.style.whiteSpace = WhiteSpace.NoWrap;
      lbl.style.overflow = Overflow.Hidden;
      lbl.style.textOverflow = TextOverflow.Ellipsis;
      return lbl;
    }
  }
}