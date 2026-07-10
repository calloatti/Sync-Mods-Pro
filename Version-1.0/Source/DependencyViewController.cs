using System.Collections.Generic;
using System.Linq;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncModsPro
{
  public class DependencyViewController
  {
    private readonly ILoc _loc;
    private VisualElement _root;
    private List<ModRecord> _allMods;

    public DependencyViewController(ILoc loc)
    {
      _loc = loc;
    }

    public void Initialize(VisualElement root, List<ModRecord> allMods, Button auditTabButton)
    {
      _root = root;
      _allMods = allMods;

      auditTabButton.RegisterCallback<ClickEvent>(evt => BuildView());
    }

    private void BuildView()
    {
      _root.Clear();

      // Pre-sort the entire master list alphabetically by ModName
      List<ModRecord> sortedMods = _allMods.OrderBy(m => m.ModName).ToList();

      ScrollView scrollView = CreateScrollView();
      scrollView.style.marginTop = 10f;

      VisualElement columnsContainer = new VisualElement();
      columnsContainer.style.flexDirection = FlexDirection.Row;
      columnsContainer.style.flexGrow = 1;

      // Left Column
      VisualElement leftCol = new VisualElement();
      leftCol.style.flexGrow = 1;
      leftCol.style.width = Length.Percent(50);
      leftCol.style.paddingRight = 10;

      // Right Column
      VisualElement rightCol = new VisualElement();
      rightCol.style.flexGrow = 1;
      rightCol.style.width = Length.Percent(50);
      rightCol.style.paddingLeft = 20;
      rightCol.style.borderLeftWidth = 1;
      rightCol.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));

      columnsContainer.Add(leftCol);
      columnsContainer.Add(rightCol);

      bool anyModHasDependencies = false;

      // Dictionary maps a Required ID to the ModRecords of ALL mods that require it
      Dictionary<string, List<ModRecord>> dependentsMap = new Dictionary<string, List<ModRecord>>();

      // 1. Scan ALL active mods to build the Reverse Dictionary
      foreach (var mod in sortedMods)
      {
        if (mod.Source != ModSource.Missing && mod.DupStatus != 0)
        {
          if (mod.RequiredMods != null)
          {
            foreach (var req in mod.RequiredMods)
            {
              if (!dependentsMap.ContainsKey(req.Id))
              {
                dependentsMap.Add(req.Id, new List<ModRecord>());
              }
              dependentsMap[req.Id].Add(mod);
            }
          }
        }
      }

      // 2. Build the Left Column (Only Enabled mods that require things)
      foreach (var mod in sortedMods)
      {
        if (mod.TargetState == ModState.Enabled && mod.RequiredMods != null && mod.RequiredMods.Any())
        {
          anyModHasDependencies = true;

          Label header = new Label($"[{mod.ModName}] requires:");
          header.AddToClassList("text--default");
          header.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
          header.style.marginTop = 15;
          leftCol.Add(header);

          HashSet<string> initialVisitedPath = new HashSet<string> { mod.ModId };
          PrintRequirementsTree(mod, 1, leftCol, initialVisitedPath);
        }
      }

      if (!anyModHasDependencies)
      {
        Label noDepsLeft = new Label("None of the currently enabled mods have required dependencies.");
        noDepsLeft.AddToClassList("text--default");
        noDepsLeft.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        noDepsLeft.style.marginTop = 15;
        leftCol.Add(noDepsLeft);
      }

      // 3. Gather all valid mods for the Right Column (Installed + Missing Dependencies)
      HashSet<string> rightColModIds = new HashSet<string>(dependentsMap.Keys);
      foreach (var mod in sortedMods)
      {
        if (mod.Source != ModSource.Missing && mod.DupStatus != 0)
        {
          rightColModIds.Add(mod.ModId);
        }
      }

      // Sort the right column IDs alphabetically based on their ModName
      List<string> sortedRightColIds = rightColModIds.ToList();
      sortedRightColIds.Sort((a, b) =>
      {
        var modA = sortedMods.Find(m => m.ModId == a && m.DupStatus != 0);
        var modB = sortedMods.Find(m => m.ModId == b && m.DupStatus != 0);
        string nameA = modA != null ? modA.ModName : a;
        string nameB = modB != null ? modB.ModName : b;
        return string.Compare(nameA, nameB, System.StringComparison.OrdinalIgnoreCase);
      });

      if (sortedRightColIds.Count == 0)
      {
        Label noDepsRight = new Label("No mods are currently installed or required.");
        noDepsRight.AddToClassList("text--default");
        noDepsRight.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        noDepsRight.style.marginTop = 15;
        rightCol.Add(noDepsRight);
      }
      else
      {
        foreach (string targetId in sortedRightColIds)
        {
          ModRecord targetRecord = sortedMods.Find(r => r.ModId == targetId && r.Source != ModSource.Missing && r.DupStatus != 0);

          string displayName = targetRecord != null ? targetRecord.ModName : targetId;
          string statusText = targetRecord != null ? (targetRecord.TargetState == ModState.Enabled ? "Enabled" : "Disabled") : "Not Installed";
          Color statusColor = targetRecord != null ? (targetRecord.TargetState == ModState.Enabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f)) : new Color(0.9f, 0.2f, 0.2f);

          VisualElement rightHeaderRow = new VisualElement();
          rightHeaderRow.style.flexDirection = FlexDirection.Row;
          rightHeaderRow.style.flexWrap = Wrap.Wrap;
          rightHeaderRow.style.marginTop = 15;

          Label nameLabel = new Label($"[{displayName}] ");
          nameLabel.AddToClassList("text--default");
          nameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

          Label statusLabel = new Label($"({statusText})");
          statusLabel.AddToClassList("text--default");
          statusLabel.style.color = new StyleColor(statusColor);

          Label suffixLabel = new Label(" required by:");
          suffixLabel.AddToClassList("text--default");
          suffixLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

          rightHeaderRow.Add(nameLabel);
          rightHeaderRow.Add(statusLabel);
          rightHeaderRow.Add(suffixLabel);
          rightCol.Add(rightHeaderRow);

          if (dependentsMap.ContainsKey(targetId))
          {
            // Sort the sub-list alphabetically before printing
            List<ModRecord> sortedDependents = dependentsMap[targetId].OrderBy(m => m.ModName).ToList();

            foreach (var dependentMod in sortedDependents)
            {
              string depStatus = dependentMod.TargetState == ModState.Enabled ? "Enabled" : "Disabled";
              Color depColor = dependentMod.TargetState == ModState.Enabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f);

              VisualElement depRow = new VisualElement();
              depRow.style.flexDirection = FlexDirection.Row;
              depRow.style.marginLeft = 15;

              Label depNameLabel = new Label($"↳ {dependentMod.ModName} ");
              depNameLabel.AddToClassList("text--default");
              depNameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));

              Label depStatusLabel = new Label($"({depStatus})");
              depStatusLabel.AddToClassList("text--default");
              depStatusLabel.style.color = new StyleColor(depColor);

              depRow.Add(depNameLabel);
              depRow.Add(depStatusLabel);
              rightCol.Add(depRow);
            }
          }
          else
          {
            Label noneLabel = new Label($"↳ (No mods require this)");
            noneLabel.AddToClassList("text--default");
            noneLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            noneLabel.style.marginLeft = 15;
            rightCol.Add(noneLabel);
          }
        }
      }

      scrollView.Add(columnsContainer);
      _root.Add(scrollView);

      VisualElement dummySpacer = new VisualElement();
      dummySpacer.style.height = 35f;
      dummySpacer.style.marginTop = 20f;
      dummySpacer.style.marginBottom = 42f;
      _root.Add(dummySpacer);
    }

    private void PrintRequirementsTree(ModRecord mod, int depth, VisualElement container, HashSet<string> visitedPath)
    {
      if (mod.RequiredMods == null) return;

      // Sort the dependencies alphabetically by ID before digging deeper
      var sortedReqs = mod.RequiredMods.OrderBy(r => r.Id).ToList();

      foreach (var req in sortedReqs)
      {
        if (visitedPath.Contains(req.Id))
        {
          Label circLabel = new Label($"↳ {req.Id} (Circular Dependency Warning)");
          circLabel.AddToClassList("text--default");
          circLabel.style.color = new StyleColor(new Color(0.9f, 0.5f, 0.1f));
          circLabel.style.marginLeft = 15 * depth;
          container.Add(circLabel);
          continue;
        }

        ModRecord depRecord = _allMods.Find(r => r.ModId == req.Id && r.Source != ModSource.Missing && r.DupStatus != 0);

        string depName = depRecord != null ? depRecord.ModName : req.Id;
        string statusText = depRecord != null ? (depRecord.TargetState == ModState.Enabled ? "Enabled" : "Disabled") : "Not Installed";
        Color statusColor = depRecord != null ? (depRecord.TargetState == ModState.Enabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f)) : new Color(0.9f, 0.2f, 0.2f);

        Label depLabel = new Label($"↳ {depName} ({statusText})");
        depLabel.AddToClassList("text--default");
        depLabel.style.color = new StyleColor(statusColor);
        depLabel.style.marginLeft = 15 * depth;
        container.Add(depLabel);

        if (depRecord != null)
        {
          HashSet<string> branchVisited = new HashSet<string>(visitedPath) { req.Id };
          PrintRequirementsTree(depRecord, depth + 1, container, branchVisited);
        }
      }
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