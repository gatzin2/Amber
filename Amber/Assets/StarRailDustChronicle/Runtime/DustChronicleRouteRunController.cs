using System.Collections.Generic;
using System.Text;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Amber.StarRailDustChronicle
{
    public enum DustChronicleRunFlowState
    {
        LaneSelection,
        NodePreview,
        NodeRunning,
        NodeComplete,
        RunVictory,
        RunFailure
    }

    public enum DirectBattleType
    {
        None,
        NormalBattle,
        EliteBattle
    }

    public sealed class DustChronicleRouteRunController : MonoBehaviour
    {
        [Header("Route Authoring")]
        [SerializeField] private DustChronicleRouteMap routeMap;
        [SerializeField] private bool autoCreateFallbackMap = true;

        [Header("Flow References")]
        [SerializeField] private DustChronicleCombatController combatController;

        [Header("Whitebox Status")]
        [SerializeField] private TextMesh statusBoard;
        [SerializeField] private bool showPlayModeGui = true;

        [Header("Direct Battle Mode")]
        [SerializeField] private DirectBattleType directBattleType = DirectBattleType.None;


        [Header("Readonly Runtime State")]
        [SerializeField] private DustChronicleRunFlowState flowState = DustChronicleRunFlowState.LaneSelection;
        [SerializeField] private int selectedLaneIndex = -1;
        [SerializeField] private int currentNodeIndex = -1;
        [SerializeField] private int completedNodeCount;
        [SerializeField] private string lastNodeOutcome = "无";

        private readonly StringBuilder builder = new StringBuilder(2048);

        public DustChronicleRouteMap RouteMap => routeMap;
        public DustChronicleRunFlowState FlowState => flowState;
        public int SelectedLaneIndex => selectedLaneIndex;
        public int CurrentNodeIndex => currentNodeIndex;
        public int CompletedNodeCount => completedNodeCount;
        public bool HasSelectedLane => selectedLaneIndex >= 0;

        public DustChronicleRouteLaneDefinition CurrentLane
        {
            get
            {
                if (routeMap == null || !HasSelectedLane || selectedLaneIndex >= routeMap.Lanes.Count)
                {
                    return null;
                }

                return routeMap.Lanes[selectedLaneIndex];
            }
        }

        public DustChronicleRouteNodeDefinition CurrentNode
        {
            get
            {
                var lane = CurrentLane;
                if (lane == null || currentNodeIndex < 0 || currentNodeIndex >= lane.NodeCount)
                {
                    return null;
                }

                return lane.Nodes[currentNodeIndex];
            }
        }

        private void Awake()
        {
            if (directBattleType != DirectBattleType.None)
            {
                SetupDirectBattle();
                return;
            }

            EnsureMap();
            if (combatController == null)
            {
                combatController = GetComponent<DustChronicleCombatController>();
            }

            if (combatController != null)
            {
                combatController.ConfigureFlowMode(false, false);
            }

            ResetRun();
        }

        private void Update()
        {
            if (flowState != DustChronicleRunFlowState.NodeRunning)
            {
                return;
            }

            var node = CurrentNode;
            if (node == null)
            {
                return;
            }

            if (IsCombatNode(node.NodeType))
            {
                PollCombatNode();
            }
        }

        private void SetupDirectBattle()
        {
            if (combatController == null)
            {
                combatController = GetComponent<DustChronicleCombatController>();
            }

            if (combatController == null)
            {
                return;
            }

            // Prevent the combat controller from auto-discovering units
            // because we are manually selecting which units to use
            combatController.SetSuppressAutoRebuild(true);

            var players = new List<DustChronicleUnit>();
            var enemies = new List<DustChronicleUnit>();

            var allUnits = GetComponentsInChildren<DustChronicleUnit>(true);
            var useElite = directBattleType == DirectBattleType.EliteBattle;

            foreach (var unit in allUnits)
            {
                if (unit == null)
                {
                    continue;
                }

                if (unit.Team == DustTeam.Player)
                {
                    unit.gameObject.SetActive(true);
                    players.Add(unit);
                }
                else if (unit.Team == DustTeam.Enemy)
                {
                    var isElite = unit.name.Contains("精英");
                    unit.gameObject.SetActive(isElite == useElite);
                    if (isElite == useElite)
                    {
                        enemies.Add(unit);
                    }
                }
            }

            combatController.Configure(players, enemies, statusBoard);
            combatController.ConfigureFlowMode(true, true);

            flowState = DustChronicleRunFlowState.NodeRunning;
            combatController.StartBattle();
            RefreshStatus();
        }

        public void Configure(DustChronicleRouteMap configuredMap, TextMesh board)
        {
            routeMap = configuredMap;
            statusBoard = board;
            EnsureMap();

            if (combatController == null)
            {
                combatController = GetComponent<DustChronicleCombatController>();
            }

            if (combatController != null)
            {
                combatController.ConfigureFlowMode(false, false);
                combatController.ResetBattle();
            }

            ResetRun();
        }

        public void ResetRun()
        {
            selectedLaneIndex = -1;
            currentNodeIndex = -1;
            completedNodeCount = 0;
            lastNodeOutcome = "无";
            flowState = DustChronicleRunFlowState.LaneSelection;

            if (combatController != null)
            {
                combatController.ResetBattle();
            }

            RefreshStatus();
        }

        public bool SelectLane(int laneIndex)
        {
            EnsureMap();
            if (routeMap == null || laneIndex < 0 || laneIndex >= routeMap.Lanes.Count)
            {
                return false;
            }

            var lane = routeMap.Lanes[laneIndex];
            if (lane == null || lane.NodeCount == 0)
            {
                return false;
            }

            selectedLaneIndex = laneIndex;
            currentNodeIndex = 0;
            completedNodeCount = 0;
            lastNodeOutcome = "已选路线";
            flowState = DustChronicleRunFlowState.NodePreview;

            if (combatController != null)
            {
                combatController.ResetBattle();
            }

            RefreshStatus();
            return true;
        }

        public bool StartCurrentNode()
        {
            var node = CurrentNode;
            if (node == null)
            {
                return false;
            }

            if (combatController != null)
            {
                combatController.ResetBattle();
            }

            flowState = DustChronicleRunFlowState.NodeRunning;
            lastNodeOutcome = "节点已开始";

            if (IsCombatNode(node.NodeType))
            {
                if (combatController != null)
                {
                    combatController.StartBattle();
                }
            }
            else
            {
                lastNodeOutcome = $"{node.NodeType} resolved";
            }

            RefreshStatus();
            return true;
        }

        public bool ResolveNonCombatNode()
        {
            var node = CurrentNode;
            if (node == null || flowState != DustChronicleRunFlowState.NodeRunning || IsCombatNode(node.NodeType))
            {
                return false;
            }

            lastNodeOutcome = $"{node.NodeType} completed";
            flowState = DustChronicleRunFlowState.NodeComplete;
            RefreshStatus();
            return true;
        }

        public bool ContinueAfterNode()
        {
            var lane = CurrentLane;
            if (lane == null)
            {
                return false;
            }

            if (flowState != DustChronicleRunFlowState.NodeComplete)
            {
                return false;
            }

            completedNodeCount = Mathf.Max(completedNodeCount, currentNodeIndex + 1);
            if (currentNodeIndex >= lane.NodeCount - 1)
            {
                flowState = DustChronicleRunFlowState.RunVictory;
                lastNodeOutcome = "路线已通关";
            }
            else
            {
                currentNodeIndex++;
                flowState = DustChronicleRunFlowState.NodePreview;
                lastNodeOutcome = "进入下一节点";
            }

            if (combatController != null)
            {
                combatController.ResetBattle();
            }

            RefreshStatus();
            return true;
        }

        public IReadOnlyList<DustChronicleRouteLaneDefinition> GetLanes()
        {
            EnsureMap();
            return routeMap != null ? routeMap.Lanes : null;
        }

        private void PollCombatNode()
        {
            if (combatController == null || !combatController.IsBattleFinished)
            {
                return;
            }

            switch (combatController.Result)
            {
                case DustBattleResult.PlayerWin:
                    lastNodeOutcome = "战斗胜利";
                    flowState = DustChronicleRunFlowState.NodeComplete;
                    break;
                case DustBattleResult.EnemyWin:
                case DustBattleResult.Draw:
                    lastNodeOutcome = combatController.Result == DustBattleResult.Draw ? "战斗平局" : "战斗败北";
                    flowState = DustChronicleRunFlowState.RunFailure;
                    break;
            }

            RefreshStatus();
        }

        private void EnsureMap()
        {
            if (routeMap == null && autoCreateFallbackMap)
            {
                routeMap = DustChronicleRouteMap.CreateRuntimeDefault();
            }

            if (routeMap != null)
            {
                routeMap.Sanitize();
            }
        }

        private void RefreshStatus()
        {
            if (statusBoard == null)
            {
                return;
            }

            EnsureMap();
            builder.Clear();
            builder.AppendLine("尘灵编年史路线白盒");

            if (routeMap == null)
            {
                builder.AppendLine("未分配路线地图。");
                statusBoard.text = builder.ToString();
                return;
            }

            builder.AppendLine(routeMap.MapName);
            if (!string.IsNullOrWhiteSpace(routeMap.Summary))
            {
                builder.AppendLine(routeMap.Summary);
            }

            builder.AppendLine();
            builder.AppendLine($"流程状态：{flowState}");
            builder.AppendLine($"上次结果：{lastNodeOutcome}");

            if (!HasSelectedLane)
            {
                builder.AppendLine("已选路线：无");
            }
            else
            {
                var lane = CurrentLane;
                builder.AppendLine($"已选路线：{lane.DisplayName}（{selectedLaneIndex + 1}/3）");
                builder.AppendLine($"节点进度：{Mathf.Clamp(currentNodeIndex + 1, 0, lane.NodeCount)}/{lane.NodeCount}");
                builder.AppendLine($"已完成节点：{completedNodeCount}/{lane.NodeCount}");

                var node = CurrentNode;
                if (node != null)
                {
                    builder.AppendLine();
                    builder.AppendLine($"当前节点：{node.DisplayName}");
                    builder.AppendLine($"节点类型：{node.NodeType}");
                    builder.AppendLine($"内容键：{node.ContentKey}");
                    if (!string.IsNullOrWhiteSpace(node.Description))
                    {
                        builder.AppendLine(node.Description);
                    }
                }
            }

            if (combatController != null)
            {
                builder.AppendLine();
                builder.AppendLine("战斗快照：");
                builder.AppendLine($"战斗状态：{combatController.Result}");
                builder.AppendLine($"我方存活：{combatController.GetAliveCount(DustTeam.Player)}/{combatController.PlayerDustSpiritCount}");
                builder.AppendLine($"敌方存活：{combatController.GetAliveCount(DustTeam.Enemy)}/{combatController.EnemyDustSpiritCount}");
                builder.AppendLine($"已用时：{combatController.ElapsedBattleTime:0.0}秒");
            }

            builder.AppendLine();
            builder.AppendLine("创作路线：");
            for (var laneIndex = 0; laneIndex < routeMap.Lanes.Count; laneIndex++)
            {
                var lane = routeMap.Lanes[laneIndex];
                builder.AppendLine($"[{laneIndex + 1}] {lane.DisplayName}");
                for (var nodeIndex = 0; nodeIndex < lane.NodeCount; nodeIndex++)
                {
                    var node = lane.Nodes[nodeIndex];
                    var isCurrent = laneIndex == selectedLaneIndex && nodeIndex == currentNodeIndex;
                    var isDone = laneIndex == selectedLaneIndex && nodeIndex < completedNodeCount;
                    var marker = isCurrent ? ">" : isDone ? "x" : "-";
                    builder.AppendLine($"  {marker} {nodeIndex + 1}. {node.DisplayName} [{node.NodeType}]");
                }
            }

            statusBoard.text = builder.ToString();
        }

        private void OnGUI()
        {
            if (!showPlayModeGui)
            {
                return;
            }

            if (directBattleType != DirectBattleType.None)
            {
                DrawDirectBattleGui();
                return;
            }

            DrawFlowWindow();
            if (HasSelectedLane)
            {
                DrawRouteWindow();
            }
        }

        private void DrawFlowWindow()
        {
            GUILayout.BeginArea(new Rect(16f, 16f, 420f, 320f), GUI.skin.box);
            GUILayout.Label("尘灵编年史运行流程");
            GUILayout.Label($"状态：{flowState}");

            if (routeMap == null)
            {
                GUILayout.Label("未分配路线地图。");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"地图：{routeMap.MapName}");
            GUILayout.Space(6f);

            switch (flowState)
            {
                case DustChronicleRunFlowState.LaneSelection:
                    DrawLaneSelectionUi();
                    break;
                case DustChronicleRunFlowState.NodePreview:
                    DrawNodePreviewUi();
                    break;
                case DustChronicleRunFlowState.NodeRunning:
                    DrawNodeRunningUi();
                    break;
                case DustChronicleRunFlowState.NodeComplete:
                    DrawNodeCompleteUi();
                    break;
                case DustChronicleRunFlowState.RunVictory:
                    DrawRunVictoryUi();
                    break;
                case DustChronicleRunFlowState.RunFailure:
                    DrawRunFailureUi();
                    break;
            }

            GUILayout.Space(10f);
            if (GUILayout.Button("重置整个运行"))
            {
                ResetRun();
            }

            GUILayout.EndArea();
        }

        private void DrawLaneSelectionUi()
        {
            GUILayout.Label("选择一条创作路线开始本次运行。");
            GUILayout.Space(6f);

            var lanes = routeMap.Lanes;
            for (var i = 0; i < lanes.Count; i++)
            {
                var lane = lanes[i];
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{i + 1}. {lane.DisplayName}");
                    GUILayout.Label($"{lane.NodeCount} nodes");
                    GUILayout.Label(BuildLaneSummary(lane));
                    if (GUILayout.Button($"选择 {lane.DisplayName}"))
                    {
                        SelectLane(i);
                    }
                }
            }
        }

        private void DrawNodePreviewUi()
        {
            var lane = CurrentLane;
            var node = CurrentNode;
            if (lane == null || node == null)
            {
                GUILayout.Label("无可用节点。");
                return;
            }

            GUILayout.Label($"路线：{lane.DisplayName}");
            GUILayout.Label($"即将到来的节点 {currentNodeIndex + 1}/{lane.NodeCount}");
            GUILayout.Label(node.DisplayName);
            GUILayout.Label($"类型：{node.NodeType}");
            GUILayout.Label($"内容：{node.ContentKey}");
            GUILayout.TextArea(string.IsNullOrWhiteSpace(node.Description) ? "无描述。" : node.Description, GUILayout.Height(80f));

            if (GUILayout.Button("开始此节点", GUILayout.Height(32f)))
            {
                StartCurrentNode();
            }
        }

        private void DrawNodeRunningUi()
        {
            var node = CurrentNode;
            GUILayout.Label(node != null ? $"运行中：{node.DisplayName}" : "运行节点");

            if (node != null)
            {
                GUILayout.Label($"类型：{node.NodeType}");
            }

            if (node != null && IsCombatNode(node.NodeType))
            {
                if (combatController != null)
                {
                    GUILayout.Label($"战斗：{combatController.Result}");
                    GUILayout.Label($"我方存活：{combatController.GetAliveCount(DustTeam.Player)}/{combatController.PlayerDustSpiritCount}");
                    GUILayout.Label($"敌方存活：{combatController.GetAliveCount(DustTeam.Enemy)}/{combatController.EnemyDustSpiritCount}");
                    GUILayout.Label($"已用时：{combatController.ElapsedBattleTime:0.0}秒");
                }

                GUILayout.Label("战斗节点通过白盒对战自动结算。");
            }
            else if (node != null && node.NodeType == DustChronicleRouteNodeType.Event)
            {
                DrawEventNodePopup(node);
            }
            else
            {
                GUILayout.Label("这是一个非战斗节点占位符。使用下方按钮来结算它。");
                if (GUILayout.Button("结算当前节点", GUILayout.Height(32f)))
                {
                    ResolveNonCombatNode();
                }
            }
        }


        private void DrawEventNodePopup(DustChronicleRouteNodeDefinition node)
        {
            var popupWidth = 420f;
            var popupHeight = 200f;
            var popupRect = new Rect(
                (Screen.width - popupWidth) * 0.5f,
                (Screen.height - popupHeight) * 0.5f,
                popupWidth,
                popupHeight);

            var shadowRect = new Rect(popupRect.x + 3f, popupRect.y + 3f, popupRect.width, popupRect.height);
            GUI.Box(shadowRect, "", GUI.skin.window);
            GUI.Box(popupRect, "事件关卡", GUI.skin.window);

            GUILayout.BeginArea(new Rect(popupRect.x + 20f, popupRect.y + 30f, popupRect.width - 40f, popupRect.height - 50f));
            GUILayout.Label(node.DisplayName, new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 });
            GUILayout.Space(8f);
            if (!string.IsNullOrWhiteSpace(node.Description))
            {
                GUILayout.Label(node.Description);
            }

            GUILayout.Space(12f);
            if (GUILayout.Button("立即通关并判定结束play模式", GUILayout.Height(36f)))
            {
#if UNITY_EDITOR
                EditorApplication.ExitPlaymode();
#endif
            }

            GUILayout.EndArea();
        }

        private void DrawNodeCompleteUi()
        {
            var lane = CurrentLane;
            var node = CurrentNode;
            var isFinalNode = lane != null && currentNodeIndex >= lane.NodeCount - 1;

            GUILayout.Label("节点完成");
            if (node != null)
            {
                GUILayout.Label($"{node.DisplayName} [{node.NodeType}]");
            }

            GUILayout.Label($"结果：{lastNodeOutcome}");
            GUILayout.Label($"进度：{completedNodeCount + 1}/{(lane != null ? lane.NodeCount : 0)}");

            if (GUILayout.Button(isFinalNode ? "打开胜利页面" : "打开下一路线步骤", GUILayout.Height(32f)))
            {
                ContinueAfterNode();
            }
        }

        private void DrawRunVictoryUi()
        {
            GUILayout.Label("胜利");
            GUILayout.Label("所选路线上的所有创作节点均已完成。");
            GUILayout.Label($"路线已通关：{(CurrentLane != null ? CurrentLane.DisplayName : "未知")}");
            GUILayout.Label($"已完成节点：{completedNodeCount}");
            GUILayout.Label("本次运行视为已通关。");
        }

        private void DrawRunFailureUi()
        {
            GUILayout.Label("运行失败");
            GUILayout.Label($"结果：{lastNodeOutcome}");
            GUILayout.Label("本次路线尝试在所有节点完成前已结束。");
        }

        private void DrawRouteWindow()
        {
            var lane = CurrentLane;
            if (lane == null)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(454f, 16f, 420f, 320f), GUI.skin.box);
            GUILayout.Label("路线地图");
            GUILayout.Label($"路线：{lane.DisplayName}");
            GUILayout.Space(6f);

            for (var i = 0; i < lane.NodeCount; i++)
            {
                var node = lane.Nodes[i];
                var isCurrent = i == currentNodeIndex;
                var isDone = i < completedNodeCount;
                var stateLabel = isCurrent ? "当前" : isDone ? "已完成" : "待进行";

                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{i + 1}. {node.DisplayName} [{node.NodeType}]");
                    GUILayout.Label($"状态：{stateLabel}");
                    GUILayout.Label($"键：{node.ContentKey}");
                }
            }

            GUILayout.EndArea();
        }

        private void DrawDirectBattleGui()
        {
            var modeLabel = directBattleType == DirectBattleType.EliteBattle ? "精英战斗白盒" : "普通战斗白盒";
            GUILayout.BeginArea(new Rect(16, 16, 315, 150), GUI.skin.box);
            GUILayout.Label(modeLabel);
            GUILayout.Label("直接战斗模式：路线选择已关闭。");
            GUILayout.Space(6f);
            if (combatController != null)
            {
                var resultLabel = combatController.Result switch
                {
                    DustBattleResult.NotStarted => "未开始",
                    DustBattleResult.Running => "运行中",
                    DustBattleResult.PlayerWin => "我方胜利！",
                    DustBattleResult.EnemyWin => "敌方胜利！",
                    DustBattleResult.Draw => "平局",
                    _ => "未知"
                };
                GUILayout.Label("战斗状态：" + resultLabel);
                if (GUILayout.Button(combatController.Result == DustBattleResult.Running ? "Restart Battle" : "Start Battle"))
                {
                    combatController.StartBattle();
                }
                if (GUILayout.Button("重置满血"))
                {
                    combatController.ResetBattle();
                }
            }
            GUILayout.EndArea();
        }

        private static bool IsCombatNode(DustChronicleRouteNodeType nodeType)
        {
            return nodeType == DustChronicleRouteNodeType.Battle
                || nodeType == DustChronicleRouteNodeType.EliteBattle
                || nodeType == DustChronicleRouteNodeType.Boss;
        }

        private static string BuildLaneSummary(DustChronicleRouteLaneDefinition lane)
        {
            if (lane == null || lane.NodeCount == 0)
            {
                return "空路线";
            }

            var summaryBuilder = new StringBuilder();
            for (var i = 0; i < lane.NodeCount; i++)
            {
                if (i > 0)
                {
                    summaryBuilder.Append(" -> ");
                }

                summaryBuilder.Append(lane.Nodes[i].NodeType);
            }

            return summaryBuilder.ToString();
        }

        private void OnValidate()
        {
            if (routeMap != null)
            {
                routeMap.Sanitize();
            }

            if (selectedLaneIndex < -1)
            {
                selectedLaneIndex = -1;
            }

            if (currentNodeIndex < -1)
            {
                currentNodeIndex = -1;
            }

            completedNodeCount = Mathf.Max(0, completedNodeCount);
        }
    }
}
