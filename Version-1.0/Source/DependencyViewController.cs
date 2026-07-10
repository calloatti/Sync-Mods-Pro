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

      // Left Column (Forward Dependencies)
      VisualElement leftCol = new VisualElement();
      leftCol.style.flexGrow = 1;
      leftCol.style.width = Length.Percent(33.33f);
      leftCol.style.paddingRight = 10;

      // Middle Column (Reverse Dependencies)
      VisualElement middleCol = new VisualElement();
      middleCol.style.flexGrow = 1;
      middleCol.style.width = Length.Percent(33.33f);
      middleCol.style.paddingLeft = 10;
      middleCol.style.paddingRight = 10;
      middleCol.style.borderLeftWidth = 1;
      middleCol.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));

      // Right Column (Issues & Health Check)
      VisualElement rightCol = new VisualElement();
      rightCol.style.flexGrow = 1;
      rightCol.style.width = Length.Percent(33.33f);
      rightCol.style.paddingLeft = 10;
      rightCol.style.borderLeftWidth = 1;
      rightCol.style.borderLeftColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.5f));

      columnsContainer.Add(leftCol);
      columnsContainer.Add(middleCol);
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

      // 2. Build the Left Column (All installed mods that require things)
      foreach (var mod in sortedMods)
      {
        if (mod.Source != ModSource.Missing && mod.DupStatus != 0 && mod.RequiredMods != null && mod.RequiredMods.Any())
        {
          anyModHasDependencies = true;

          bool isParentEnabled = mod.TargetState == ModState.Enabled;
          string statusText = isParentEnabled ? "Enabled" : "Disabled";
          Color statusColor = isParentEnabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f);

          VisualElement leftHeaderRow = new VisualElement();
          leftHeaderRow.style.flexDirection = FlexDirection.Row;
          leftHeaderRow.style.flexWrap = Wrap.Wrap;
          leftHeaderRow.style.marginTop = 15;

          Label nameLabel = new Label($"[{mod.ModName}] ");
          nameLabel.AddToClassList("text--default");
          nameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
          nameLabel.style.fontSize = 12;

          Label statusLabel = new Label($"({statusText})");
          statusLabel.AddToClassList("text--default");
          statusLabel.style.color = new StyleColor(statusColor);
          statusLabel.style.fontSize = 12;

          Label suffixLabel = new Label(" requires:");
          suffixLabel.AddToClassList("text--default");
          suffixLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
          suffixLabel.style.fontSize = 12;

          leftHeaderRow.Add(nameLabel);
          leftHeaderRow.Add(statusLabel);
          leftHeaderRow.Add(suffixLabel);
          leftCol.Add(leftHeaderRow);

          HashSet<string> initialVisitedPath = new HashSet<string> { mod.ModId };
          PrintRequirementsTree(mod, 1, leftCol, initialVisitedPath, isParentEnabled);
        }
      }

      if (!anyModHasDependencies)
      {
        Label noDepsLeft = new Label("None of the installed mods have required dependencies.");
        noDepsLeft.AddToClassList("text--default");
        noDepsLeft.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        noDepsLeft.style.fontSize = 12;
        noDepsLeft.style.marginTop = 15;
        leftCol.Add(noDepsLeft);
      }

      // 3. Gather all valid mods for the Middle Column (Only Missing Dependencies + Mods Required By Others)
      HashSet<string> middleColModIds = new HashSet<string>(dependentsMap.Keys);

      // Sort the middle column IDs alphabetically based on their ModName
      List<string> sortedMiddleColIds = middleColModIds.ToList();
      sortedMiddleColIds.Sort((a, b) =>
      {
        var modA = sortedMods.Find(m => m.ModId == a && m.DupStatus != 0);
        var modB = sortedMods.Find(m => m.ModId == b && m.DupStatus != 0);
        string nameA = modA != null ? modA.ModName : a;
        string nameB = modB != null ? modB.ModName : b;
        return string.Compare(nameA, nameB, System.StringComparison.OrdinalIgnoreCase);
      });

      if (sortedMiddleColIds.Count == 0)
      {
        Label noDepsMiddle = new Label("No mods are currently required by other installed mods.");
        noDepsMiddle.AddToClassList("text--default");
        noDepsMiddle.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
        noDepsMiddle.style.fontSize = 12;
        noDepsMiddle.style.marginTop = 15;
        middleCol.Add(noDepsMiddle);
      }
      else
      {
        foreach (string targetId in sortedMiddleColIds)
        {
          ModRecord targetRecord = sortedMods.Find(r => r.ModId == targetId && r.Source != ModSource.Missing && r.DupStatus != 0);

          string displayName = targetRecord != null ? targetRecord.ModName : targetId;
          string statusText = targetRecord != null ? (targetRecord.TargetState == ModState.Enabled ? "Enabled" : "Disabled") : "Not Installed";
          Color statusColor = targetRecord != null ? (targetRecord.TargetState == ModState.Enabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f)) : new Color(0.9f, 0.2f, 0.2f);

          VisualElement middleHeaderRow = new VisualElement();
          middleHeaderRow.style.flexDirection = FlexDirection.Row;
          middleHeaderRow.style.flexWrap = Wrap.Wrap;
          middleHeaderRow.style.marginTop = 15;

          Label nameLabel = new Label($"[{displayName}] ");
          nameLabel.AddToClassList("text--default");
          nameLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
          nameLabel.style.fontSize = 12;

          Label statusLabel = new Label($"({statusText})");
          statusLabel.AddToClassList("text--default");
          statusLabel.style.color = new StyleColor(statusColor);
          statusLabel.style.fontSize = 12;

          Label suffixLabel = new Label(" required by:");
          suffixLabel.AddToClassList("text--default");
          suffixLabel.style.color = new StyleColor(new Color(0.9f, 0.9f, 0.9f));
          suffixLabel.style.fontSize = 12;

          middleHeaderRow.Add(nameLabel);
          middleHeaderRow.Add(statusLabel);
          middleHeaderRow.Add(suffixLabel);
          middleCol.Add(middleHeaderRow);

          // We know this key exists because middleColModIds is built strictly from dependentsMap.Keys
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
              depNameLabel.style.fontSize = 12;

              Label depStatusLabel = new Label($"({depStatus})");
              depStatusLabel.AddToClassList("text--default");
              depStatusLabel.style.color = new StyleColor(depColor);
              depStatusLabel.style.fontSize = 12;

              depRow.Add(depNameLabel);
              depRow.Add(depStatusLabel);
              middleCol.Add(depRow);
            }
          }
        }
      }

      // 4. Build the Right Column (Issues & Health Check Grouped by Dependency)
      bool hasIssues = false;

      // Iterate through all known dependencies in the reverse lookup map
      List<string> sortedReqIds = dependentsMap.Keys.ToList();
      sortedReqIds.Sort((a, b) =>
      {
        var modA = sortedMods.Find(m => m.ModId == a && m.DupStatus != 0);
        var modB = sortedMods.Find(m => m.ModId == b && m.DupStatus != 0);
        string nameA = modA != null ? modA.ModName : a;
        string nameB = modB != null ? modB.ModName : b;
        return string.Compare(nameA, nameB, System.StringComparison.OrdinalIgnoreCase);
      });

      foreach (string reqId in sortedReqIds)
      {
        ModRecord reqMod = sortedMods.Find(r => r.ModId == reqId && r.Source != ModSource.Missing && r.DupStatus != 0);

        // If the dependency is already enabled, there is no issue here.
        if (reqMod != null && reqMod.TargetState == ModState.Enabled)
        {
          continue;
        }

        // The dependency is missing or disabled. Let's see if any ENABLED mod requires it.
        List<ModRecord> enabledDependents = new List<ModRecord>();
        foreach (var dep in dependentsMap[reqId])
        {
          if (dep.TargetState == ModState.Enabled && dep.Source != ModSource.Missing && dep.DupStatus != 0)
          {
            enabledDependents.Add(dep);
          }
        }

        // If at least one enabled mod needs this missing/disabled dependency, we flag an issue
        if (enabledDependents.Count > 0)
        {
          hasIssues = true;

          VisualElement issueBlock = new VisualElement();
          issueBlock.style.marginTop = 15;

          string reqName = reqMod != null ? reqMod.ModName : reqId;
          string actionVerb = reqMod != null ? "Enable" : "Missing";
          Color actionColor = reqMod != null ? new Color(0.9f, 0.9f, 0.2f) : new Color(0.9f, 0.5f, 0.1f); // Yellow for Enable, Orange for Missing

          Label titleLabel = new Label($"[{actionVerb}] {reqName} required by:");
          titleLabel.AddToClassList("text--default");
          titleLabel.style.color = new StyleColor(actionColor);
          titleLabel.style.fontSize = 12;
          issueBlock.Add(titleLabel);

          // Sort the enabled dependents alphabetically
          enabledDependents = enabledDependents.OrderBy(m => m.ModName).ToList();

          foreach (var ed in enabledDependents)
          {
            Label l = new Label($"↳ {ed.ModName}");
            l.AddToClassList("text--default");
            l.style.color = new StyleColor(new Color(0.9f, 0.2f, 0.2f)); // Red context to show the dependent mod is broken
            l.style.fontSize = 12;
            l.style.marginLeft = 15;
            issueBlock.Add(l);
          }

          rightCol.Add(issueBlock);
        }
      }

      if (!hasIssues)
      {
        Label okLabel = new Label("Everything looks OK!\nNo dependency issues found.");
        okLabel.AddToClassList("text--default");
        okLabel.style.color = new StyleColor(new Color(0.2f, 0.8f, 0.2f)); // Green
        okLabel.style.fontSize = 12;
        okLabel.style.marginTop = 15;
        rightCol.Add(okLabel);
      }

      scrollView.Add(columnsContainer);
      _root.Add(scrollView);

      VisualElement dummySpacer = new VisualElement();
      dummySpacer.style.height = 35f;
      dummySpacer.style.marginTop = 20f;
      dummySpacer.style.marginBottom = 42f;
      _root.Add(dummySpacer);
    }

    private void PrintRequirementsTree(ModRecord mod, int depth, VisualElement container, HashSet<string> visitedPath, bool activeContext)
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
          Color circColor = activeContext ? new Color(0.9f, 0.5f, 0.1f) : new Color(0.6f, 0.6f, 0.6f);
          circLabel.style.color = new StyleColor(circColor);
          circLabel.style.fontSize = 12;
          circLabel.style.marginLeft = 15 * depth;
          container.Add(circLabel);
          continue;
        }

        ModRecord depRecord = _allMods.Find(r => r.ModId == req.Id && r.Source != ModSource.Missing && r.DupStatus != 0);

        string depName = depRecord != null ? depRecord.ModName : req.Id;
        string statusText = depRecord != null ? (depRecord.TargetState == ModState.Enabled ? "Enabled" : "Disabled") : "Not Installed";

        Color statusColor;
        if (!activeContext)
        {
          statusColor = new Color(0.6f, 0.6f, 0.6f); // Neutral grey if the parent mod isn't active
        }
        else
        {
          statusColor = depRecord != null ? (depRecord.TargetState == ModState.Enabled ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.5f, 0.1f)) : new Color(0.9f, 0.2f, 0.2f);
        }

        Label depLabel = new Label($"↳ {depName} ({statusText})");
        depLabel.AddToClassList("text--default");
        depLabel.style.color = new StyleColor(statusColor);
        depLabel.style.fontSize = 12;
        depLabel.style.marginLeft = 15 * depth;
        container.Add(depLabel);

        if (depRecord != null)
        {
          HashSet<string> branchVisited = new HashSet<string>(visitedPath) { req.Id };
          PrintRequirementsTree(depRecord, depth + 1, container, branchVisited, activeContext);
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