using System;
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
    private static readonly int MinVerWidth = 70;
    private static readonly int SavedVerWidth = 60;
    private static readonly int CurrentVerWidth = 60;
    private static readonly int StatusWidth = 60;
    private static readonly int StateColWidth = 50;

    private const int BasePadding = 28;
    private const float ScrollViewHeight = 480f;
    private const float TopBarMarginBottom = 6f;
    private const float TopBarMarginTop = 0f;
    private const float ScrollViewMarginBottom = 0f;
    private const float HeaderRowHeight = 32f;
    private const float DataRowHeight = 32f;
    private const float TextVerticalOffset = -3f;

    private static readonly Color TextNormal = new Color(0.9f, 0.9f, 0.9f);

    private static readonly Color BgOddRow = new Color(0f, 0f, 0f, 0f);
    private static readonly Color BgEvenRow = new Color(0f, 0f, 0f, 0.15f);

    private Dictionary<string, List<RowUIElements>> _duplicateGroups = new Dictionary<string, List<RowUIElements>>();
    private List<RowUIElements> _orderedRows = new List<RowUIElements>();

    private HashSet<ModStatus> _activeFilters = new HashSet<ModStatus>();
    private bool _filterByTargetState = false;
    private Toggle _filterByTargetStateToggle;
    private Dictionary<ModStatus, Toggle> _statusFilterToggles = new Dictionary<ModStatus, Toggle>();

    private class RowUIElements
    {
      public ModRecord Data;
      public VisualElement Root;
      public Toggle TargetToggle;
      public Label StatusLabel;
      // Added text cell tracking so we can recolor the entire row dynamically
      public List<Label> TextCells = new List<Label>();
    }

    private void UpdateEnabledStats()
    {
      if (_filterByTargetStateToggle == null) return;

      int savedEnabledCount = 0;
      int targetEnabledCount = 0;

      // Track dynamic status counts
      Dictionary<ModStatus, int> dynamicStatusCounts = new Dictionary<ModStatus, int>();
      foreach (ModStatus status in Enum.GetValues(typeof(ModStatus)))
      {
        dynamicStatusCounts[status] = 0;
      }

      foreach (var rowUI in _orderedRows)
      {
        var data = rowUI.Data;

        if (data.SavedState == ModState.Enabled) savedEnabledCount++;
        if (data.TargetState == ModState.Enabled) targetEnabledCount++;

        dynamicStatusCounts[data.Status]++;
      }

      // Update the main enabled stats toggle
      _filterByTargetStateToggle.text = $"Mods ({targetEnabledCount}/{savedEnabledCount})";

      // Update the specific status filter toggles in the top bar
      foreach (var kvp in _statusFilterToggles)
      {
        string locKey = $"Calloatti.SyncModsPro.Status.{kvp.Key}";
        string labelText = _loc.T(locKey);

        labelText += $" ({dynamicStatusCounts[kvp.Key]})";

        kvp.Value.text = labelText;
      }

      // --- NEW LABEL LOGIC INJECTION ---
      if (_syncWarningLabel != null)
      {
        // 1. Perfect Match (Ready to go)
        if (dynamicStatusCounts[ModStatus.Match] == _orderedRows.Count)
        {
          _syncWarningLabel.text = "Mod list matches the save file. Ready to [Load Game].";
          _syncWarningLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f)); // Default white/grey
        }
        // 2. Only deviations are Missing mods (Nothing left to sync)
        else if (dynamicStatusCounts[ModStatus.Match] + dynamicStatusCounts[ModStatus.Missing] == _orderedRows.Count)
        {
          _syncWarningLabel.text = "Some mods are missing. Syncing won't fix this, but you can [Load Game] anyway.";
          _syncWarningLabel.style.color = new StyleColor(new Color(0.9f, 0.5f, 0.1f)); // Orange
        }
        // 3. Mixed State (They need to sync, BUT they also have missing mods)
        else if (dynamicStatusCounts[ModStatus.Missing] > 0)
        {
          _syncWarningLabel.text = "[Sync] and [Restart] the game. Syncing won't fix missing mods.";
          _syncWarningLabel.style.color = new StyleColor(new Color(0.9f, 0.3f, 0.3f)); // Slightly softer red
        }
        // 4. Standard pending sync (No missing mods)
        else
        {
          _syncWarningLabel.text = "[Sync] and [Restart] the game.";
          _syncWarningLabel.style.color = new StyleColor(new Color(0.9f, 0.2f, 0.2f)); // Red
        }
      }
    }
    private int GetTotalTableWidth()
    {
      return IconWidth + NameWidth + IdWidth + FolderWidth + MinVerWidth + SavedVerWidth + CurrentVerWidth + StatusWidth + (StateColWidth * 3) + BasePadding + 5;
    }

    private VisualElement CreateTopBar(List<ModRecord> modTable)
    {
      _statusFilterToggles.Clear();

      VisualElement topBar = new VisualElement();
      topBar.style.flexDirection = FlexDirection.Row;
      topBar.style.alignItems = Align.Center;
      topBar.style.justifyContent = Justify.SpaceBetween;
      topBar.style.marginBottom = TopBarMarginBottom;
      topBar.style.marginTop = TopBarMarginTop;

      string saveText = "Unknown Save";
      if (_currentSaveReference != null && _currentSaveReference.SettlementReference != null)
      {
        saveText = $"{_currentSaveReference.SettlementReference.SettlementName} - {_currentSaveReference.SaveName}";
      }

      Label saveLabel = new Label(saveText);
      saveLabel.AddToClassList("text--default");
      //saveLabel.style.fontSize = 12;
      saveLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
      saveLabel.pickingMode = PickingMode.Position;
      saveLabel.style.marginLeft = 20;
      saveLabel.style.top = 0;

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
        { "Calloatti.SyncModsPro.Status.Disabled", ModStatus.Disabled },
        { "Calloatti.SyncModsPro.Status.Missing", ModStatus.Missing },
        { "Calloatti.SyncModsPro.Status.New", ModStatus.New },
      };

      Dictionary<ModStatus, int> staticCounts = new Dictionary<ModStatus, int>();
      foreach (ModStatus status in Enum.GetValues(typeof(ModStatus)))
      {
        staticCounts[status] = 0;
      }

      foreach (var row in modTable)
      {
        staticCounts[row.Status]++;
      }

      foreach (var kvp in masterStatuses)
      {
        string statusKey = kvp.Key;
        ModStatus targetStatus = kvp.Value;

        Toggle statusToggle = new Toggle();
        statusToggle.AddToClassList("game-toggle");
        statusToggle.style.scale = new StyleScale(new Vector2(0.923f, 0.923f));
        statusToggle.style.marginLeft = 4;
        statusToggle.style.fontSize = 13;
        statusToggle.viewDataKey = statusKey;


        // Only append the count if it is NOT the Match status
        string labelText = _loc.T(statusKey);

        labelText += $" ({staticCounts[targetStatus]})";
   
        statusToggle.text = labelText;

        statusToggle.SetValueWithoutNotify(false);

        statusToggle.RegisterValueChangedCallback(evt =>
        {
          if (evt.newValue)
          {
            _activeFilters.Add(targetStatus);

            // Mutually Exclusive: Turn off the main Mods toggle if any status is checked
            if (_filterByTargetState)
            {
              _filterByTargetState = false;
              if (_filterByTargetStateToggle != null)
              {
                _filterByTargetStateToggle.SetValueWithoutNotify(false);
              }
            }
          }
          else
          {
            _activeFilters.Remove(targetStatus);
          }

          ApplyFilters();
        });

        _statusFilterToggles[targetStatus] = statusToggle;
        filtersContainer.Add(statusToggle);
      }

      _filterByTargetStateToggle = new Toggle();
      _filterByTargetStateToggle.AddToClassList("game-toggle");
      _filterByTargetStateToggle.style.scale = new StyleScale(new Vector2(0.923f, 0.923f));
      _filterByTargetStateToggle.style.marginLeft = 4;
      _filterByTargetStateToggle.style.fontSize = 13;
      _filterByTargetStateToggle.SetValueWithoutNotify(false);
      _filterByTargetStateToggle.RegisterValueChangedCallback(evt =>
      {
        _filterByTargetState = evt.newValue;

        if (evt.newValue)
        {
          // Mutually Exclusive: Turn off all status toggles if the main Mods toggle is checked
          _activeFilters.Clear();
          foreach (var toggle in _statusFilterToggles.Values)
          {
            toggle.SetValueWithoutNotify(false);
          }
        }

        ApplyFilters();
      });

      filtersContainer.Add(_filterByTargetStateToggle);
      topBar.Add(filtersContainer);
      return topBar;
    }
    private void ApplyFilters()
    {
      int visibleCount = 0;
      bool hasStatusFilter = _activeFilters.Count > 0;

      foreach (var rowUI in _orderedRows)
      {
        bool isVisible = false;

        if (_filterByTargetState)
        {
          if (rowUI.Data.TargetState == ModState.Enabled || rowUI.Data.CurrentState == ModState.Missing)
          {
            isVisible = true;
          }
        }
        else
        {
          if (!hasStatusFilter)
          {
            isVisible = true;
          }
          else if (_activeFilters.Contains(rowUI.Data.Status))
          {
            isVisible = true;
          }
        }

        if (isVisible)
        {
          rowUI.Root.style.display = DisplayStyle.Flex;
          if (visibleCount % 2 == 0)
          {
            rowUI.Root.style.backgroundColor = new StyleColor(BgEvenRow);
          }
          else
          {
            rowUI.Root.style.backgroundColor = new StyleColor(BgOddRow);
          }
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
        rowUI.TargetToggle.SetValueWithoutNotify(rowUI.Data.TargetState == ModState.Enabled);
      }

      // Re-evaluate math for the row and update the translated text string
      rowUI.Data.UpdateStatus();
      if (rowUI.StatusLabel != null)
      {
        rowUI.StatusLabel.text = _loc.T($"Calloatti.SyncModsPro.Status.{rowUI.Data.Status}");
      }

      // Dynamically repaint all cached text cells to match the new status
      Color newColor = rowUI.Data.GetStatusColor();
      if (rowUI.TextCells != null)
      {
        foreach (var cell in rowUI.TextCells)
        {
          cell.style.color = newColor;
        }
      }
    }

    private void ProcessDependencies(ModRecord sourceMod, bool isEnabling)
    {
      if (sourceMod.RequiredMods == null) return;

      foreach (var req in sourceMod.RequiredMods)
      {
        if (_duplicateGroups.TryGetValue(req.Id, out var depRows))
        {
          var depRow = depRows.Find(r => r.Data.DupStatus == 1 || r.Data.DupStatus == -1);
          if (depRow == null) continue;

          var depData = depRow.Data;

          if (isEnabling)
          {
            if (depData.TargetState == ModState.Disabled)
            {
              depData.TargetState = ModState.Enabled;
              depData.AutoEnabledBy.Add(sourceMod.ModId);
              RepaintRow(depRow);
              ProcessDependencies(depData, true);
            }
            else if (depData.AutoEnabledBy.Count > 0)
            {
              depData.AutoEnabledBy.Add(sourceMod.ModId);
            }
          }
          else
          {
            if (depData.AutoEnabledBy.Contains(sourceMod.ModId))
            {
              depData.AutoEnabledBy.Remove(sourceMod.ModId);

              if (depData.AutoEnabledBy.Count == 0)
              {
                depData.TargetState = ModState.Disabled;
                RepaintRow(depRow);
                ProcessDependencies(depData, false);
              }
            }
          }
        }
      }
    }

    private VisualElement CreateRow(string name, string id, string verFolder, string minVer, string savVer, string curVer, string status, string currentStr, string savedStr, string targetStr, Color color, bool isHeader, ModRecord data, bool isEven = false)
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

        // --- ADDED: Top border for the header ---
        row.style.borderTopWidth = 2;
        row.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));
        // ----------------------------------------

        Label headerIconCell = CreateCell("", IconWidth, color);
        row.Add(headerIconCell);
      }
      else
      {
        row.style.height = DataRowHeight;
        if (isEven)
        {
          row.style.backgroundColor = new StyleColor(BgEvenRow);
        }
        else
        {
          row.style.backgroundColor = new StyleColor(BgOddRow);
        }

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
              this.HandleCloudIconClick(data.SteamId, data.ModName);
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

      Label cCurrent = CreateCell(currentStr, StateColWidth, color);
      Label cSaved = CreateCell(savedStr, StateColWidth, color);

      if (!isHeader)
      {
        cName.style.marginTop = TextVerticalOffset;
        cId.style.marginTop = TextVerticalOffset;
        cFolder.style.marginTop = TextVerticalOffset;
        cMinVer.style.marginTop = TextVerticalOffset;
        cSavVer.style.marginTop = TextVerticalOffset;
        cCurVer.style.marginTop = TextVerticalOffset;
        cStatus.style.marginTop = TextVerticalOffset;

        cCurrent.style.marginTop = TextVerticalOffset;
        cSaved.style.marginTop = TextVerticalOffset;

        if (rowElements != null)
        {
          rowElements.StatusLabel = cStatus;

          // Cache references to all text-based cells so RepaintRow can recolor them
          rowElements.TextCells.Add(cName);
          rowElements.TextCells.Add(cId);
          rowElements.TextCells.Add(cFolder);
          rowElements.TextCells.Add(cMinVer);
          rowElements.TextCells.Add(cCurVer);
          rowElements.TextCells.Add(cSavVer);
          rowElements.TextCells.Add(cCurrent);
          rowElements.TextCells.Add(cSaved);
          rowElements.TextCells.Add(cStatus);
        }

        AttachLinkBehavior(cName, data);
        AttachManifestLinkBehavior(cId, data);
        AttachFolderLinkBehavior(cFolder, data);
      }

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
        VisualElement toggleContainer = new VisualElement();
        toggleContainer.style.width = StateColWidth;
        toggleContainer.style.flexShrink = 0;
        toggleContainer.style.alignItems = Align.Center;
        toggleContainer.style.justifyContent = Justify.Center;

        Toggle targetToggle = new Toggle();
        targetToggle.AddToClassList("game-toggle");
        targetToggle.style.scale = new StyleScale(new Vector2(0.9f, 0.9f));
        targetToggle.SetValueWithoutNotify(data.TargetState == ModState.Enabled);

        if (data.Source == ModSource.Missing || data.TargetState == ModState.Missing)
        {
          targetToggle.SetEnabled(false);
        }
        else
        {
          targetToggle.RegisterValueChangedCallback(evt =>
          {
            bool isEnabling = evt.newValue;

            data.AutoEnabledBy.Clear();
            if (isEnabling)
            {
              data.TargetState = ModState.Enabled;
            }
            else
            {
              data.TargetState = ModState.Disabled;
            }

            // Instantly repaint this specific row's status and color
            RepaintRow(rowElements);

            if (isEnabling)
            {
              if (data.DupStatus != -1) data.DupStatus = 1;

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

            ProcessDependencies(data, isEnabling);
            UpdateEnabledStats();

            // REMOVED: ApplyFilters() here to prevent UX disappearing act on click
          });
        }

        if (rowElements != null) rowElements.TargetToggle = targetToggle;
        toggleContainer.Add(targetToggle);
        cTarget = toggleContainer;
      }

      // Appending to the layout in visual order
      row.Add(cName);
      row.Add(cId);
      row.Add(cFolder);
      row.Add(cMinVer);
      row.Add(cCurVer);
      row.Add(cSavVer);

      row.Add(cCurrent);
      row.Add(cSaved);
      row.Add(cTarget);

      // Status column moved completely to the right, after TargetState
      row.Add(cStatus);

      if (!isHeader)
      {
        row.style.height = DataRowHeight;
        if (isEven)
        {
          row.style.backgroundColor = new StyleColor(BgEvenRow);
        }
        else
        {
          row.style.backgroundColor = new StyleColor(BgOddRow);
        }

        Color storedRowColor = Color.clear;
        Color highlightColor = new Color(0.3f, 0.5f, 0.7f, 0.3f);

        row.RegisterCallback<PointerEnterEvent>(evt =>
        {
          storedRowColor = row.resolvedStyle.backgroundColor;
          row.style.backgroundColor = new StyleColor(highlightColor);
        });

        row.RegisterCallback<PointerLeaveEvent>(evt =>
        {
          row.style.backgroundColor = new StyleColor(storedRowColor);
        });
      }

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