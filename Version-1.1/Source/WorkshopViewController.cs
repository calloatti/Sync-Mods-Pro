using System.Collections.Generic;
using Timberborn.CoreUI;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public class WorkshopViewController
  {
    private readonly ILoc _loc;
    private readonly WorkshopManager _workshopManager;
    private VisualElement _root;
    private List<ModRecord> _allMods;

    public WorkshopViewController(ILoc loc, WorkshopManager workshopManager)
    {
      _loc = loc;
      _workshopManager = workshopManager;
    }

    public void Initialize(VisualElement root, List<ModRecord> allMods, Button historyTabButton)
    {
      _root = root;
      _allMods = allMods;

      historyTabButton.RegisterCallback<ClickEvent>(evt => BuildView());

      _workshopManager.OnHistoryUpdated += BuildView;
    }

    public void Cleanup()
    {
      _workshopManager.OnHistoryUpdated -= BuildView;
    }

    private void BuildView()
    {
      if (_root == null) return;
      _root.Clear();

      // --- NEW TITLE PANEL INJECTION ---
      VisualElement titleBar = new VisualElement();
      titleBar.style.flexDirection = FlexDirection.Row;
      titleBar.style.alignItems = Align.Center;
      titleBar.style.marginBottom = 20f;
      titleBar.style.marginTop = 0f;

      Label panelTitleLabel = new Label("Steam Workshop History");
      panelTitleLabel.AddToClassList("text--default");
      panelTitleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
      panelTitleLabel.style.marginLeft = 20;
      titleBar.Add(panelTitleLabel);
      _root.Add(titleBar);
      // ---------------------------------

      List<WorkshopLogEntry> logEntries = _workshopManager.GetLogEntries();

      ScrollView scrollView = CreateScrollView();
      scrollView.style.marginTop = 10f;

      // 1. Header Row
      VisualElement headerRow = new VisualElement();
      headerRow.style.flexDirection = FlexDirection.Row;
      headerRow.style.borderBottomWidth = 2;
      headerRow.style.borderTopWidth = 2;
      headerRow.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));
      headerRow.style.borderTopColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));
      headerRow.style.paddingLeft = 8;
      headerRow.style.height = 32f;
      headerRow.style.alignItems = Align.Center;

      Color headerColor = new Color(0.9f, 0.9f, 0.9f);
      headerRow.Add(CreateCell("Date/Time", 140, headerColor));
      headerRow.Add(CreateCell("Action", 100, headerColor));
      headerRow.Add(CreateCell("Steam ID", 90, headerColor));
      headerRow.Add(CreateCell("Mod Name", 160, headerColor, true));
      headerRow.Add(CreateCell("Result", 240, headerColor));
      headerRow.Add(CreateCell("Manage", 160, headerColor));

      scrollView.Add(headerRow);

      // 2. No Logs state
      if (logEntries.Count == 0)
      {
        Label noLogsLabel = new Label("No Steam Workshop history found.");
        noLogsLabel.AddToClassList("text--default");
        noLogsLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        noLogsLabel.style.fontSize = 12;
        noLogsLabel.style.marginTop = 15;
        noLogsLabel.style.marginLeft = 8;
        scrollView.Add(noLogsLabel);
      }
      else
      {
        // Track which mods have already received a button
        HashSet<string> seenSteamIds = new HashSet<string>();

        // 3. Data Rows
        int rowIndex = 0;
        foreach (var entry in logEntries)
        {
          bool isEven = (rowIndex % 2 == 0);

          VisualElement dataRow = new VisualElement();
          dataRow.style.flexDirection = FlexDirection.Row;
          dataRow.style.borderBottomWidth = 1;
          dataRow.style.borderBottomColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));
          dataRow.style.paddingLeft = 8;
          dataRow.style.height = 36f;
          dataRow.style.alignItems = Align.Center;
          dataRow.style.backgroundColor = isEven ? new StyleColor(new Color(0f, 0f, 0f, 0.15f)) : new StyleColor(new Color(0f, 0f, 0f, 0f));

          Color rowColor = new Color(0.8f, 0.8f, 0.8f);
          Color resultColor = entry.Result == "SUCCESS" ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.2f, 0.2f);

          dataRow.Add(CreateCell(entry.Timestamp, 140, rowColor));
          dataRow.Add(CreateCell(entry.Action, 100, rowColor));
          dataRow.Add(CreateCell(entry.SteamId, 90, rowColor));
          dataRow.Add(CreateCell(entry.ModName, 160, rowColor, true));
          dataRow.Add(CreateCell(entry.Result, 240, resultColor));

          VisualElement buttonContainer = new VisualElement();
          buttonContainer.style.width = 160;
          buttonContainer.style.flexShrink = 0;
          buttonContainer.style.justifyContent = Justify.Center;
          buttonContainer.style.alignItems = Align.Center;

          // Only add the button if this is the first time we've seen this Steam ID
          if (!seenSteamIds.Contains(entry.SteamId))
          {
            seenSteamIds.Add(entry.SteamId);

            NineSliceButton actionBtn = new NineSliceButton();
            actionBtn.AddToClassList("menu-button");
            actionBtn.AddToClassList("menu-button--medium");
            actionBtn.style.minWidth = 150;
            actionBtn.style.maxWidth = 150;
            actionBtn.style.overflow = Overflow.Hidden;
            actionBtn.style.textOverflow = TextOverflow.Ellipsis;
            actionBtn.style.fontSize = 12;
            actionBtn.style.marginTop = 0;
            actionBtn.style.marginBottom = 0;

            if (entry.IsSubscribed)
            {
              actionBtn.text = "Unsubscribe";
            }
            else
            {
              actionBtn.text = "Subscribe";
            }

            actionBtn.RegisterCallback<ClickEvent>(evt =>
            {
              _workshopManager.PromptSubscriptionChange(entry.SteamId, entry.ModName);
            });

            buttonContainer.Add(actionBtn);
          }

          dataRow.Add(buttonContainer);
          scrollView.Add(dataRow);
          rowIndex++;
        }
      }

      _root.Add(scrollView);

      VisualElement dummySpacer = new VisualElement();
      dummySpacer.style.height = 35f;
      dummySpacer.style.marginTop = 20f;
      dummySpacer.style.marginBottom = 42f;
      _root.Add(dummySpacer);
    }

    private Label CreateCell(string text, float width, Color color, bool flexGrow = false)
    {
      Label lbl = new Label(text);
      lbl.AddToClassList("text--default");
      lbl.style.fontSize = 12;

      if (flexGrow)
      {
        lbl.style.flexGrow = 1;
        lbl.style.width = StyleKeyword.Auto;
      }
      else
      {
        lbl.style.width = width;
        lbl.style.flexShrink = 0;
      }

      lbl.style.color = color;
      lbl.style.unityTextAlign = TextAnchor.MiddleLeft;
      lbl.style.whiteSpace = WhiteSpace.NoWrap;
      lbl.style.overflow = Overflow.Hidden;
      lbl.style.textOverflow = TextOverflow.Ellipsis;
      return lbl;
    }

    private ScrollView CreateScrollView()
    {
      ScrollView scrollView = new ScrollView();
      scrollView.style.marginBottom = 0f;
      scrollView.style.flexGrow = 1;
      scrollView.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
      scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

      var dragger = scrollView.Q<VisualElement>(className: "unity-base-slider__dragger");
      if (dragger != null)
      {
        dragger.style.width = 20;
        dragger.style.minHeight = 58;
        dragger.style.backgroundColor = Color.clear;
        dragger.style.borderTopWidth = 0;
        dragger.style.borderBottomWidth = 0;
        dragger.style.borderLeftWidth = 0;
        dragger.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-button-nine-slice");
        if (tex != null)
        {
          dragger.style.backgroundImage = new StyleBackground(tex);
          dragger.style.unitySliceTop = 14;
          dragger.style.unitySliceBottom = 14;
          dragger.style.unitySliceLeft = 14;
          dragger.style.unitySliceRight = 14;
        }
      }

      var tracker = scrollView.Q<VisualElement>(className: "unity-base-slider__tracker");
      if (tracker != null)
      {
        tracker.style.width = 20;
        tracker.style.backgroundColor = Color.clear;
        tracker.style.borderTopWidth = 0;
        tracker.style.borderBottomWidth = 0;
        tracker.style.borderLeftWidth = 0;
        tracker.style.borderRightWidth = 0;
        var tex = Resources.Load<Texture2D>("UI/Images/Core/vertical-scroll-bar-nine-slice");
        if (tex != null)
        {
          tracker.style.backgroundImage = new StyleBackground(tex);
          tracker.style.unitySliceTop = 16;
          tracker.style.unitySliceBottom = 16;
        }
      }

      return scrollView;
    }
  }
}