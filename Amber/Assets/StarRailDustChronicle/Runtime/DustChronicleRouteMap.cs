using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amber.StarRailDustChronicle
{
    public enum DustChronicleRouteNodeType
    {
        Battle,
        EliteBattle,
        Shop,
        Event,
        Rest,
        Treasure,
        Boss,
        Custom
    }

    [Serializable]
    public sealed class DustChronicleRouteNodeDefinition
    {
        [SerializeField] private string displayName = "Battle Node";
        [SerializeField] private DustChronicleRouteNodeType nodeType = DustChronicleRouteNodeType.Battle;
        [SerializeField] private string contentKey = "enemy/basic";
        [TextArea(2, 4)]
        [SerializeField] private string description = "Describe what this node should load or represent.";

        public string DisplayName => displayName;
        public DustChronicleRouteNodeType NodeType => nodeType;
        public string ContentKey => contentKey;
        public string Description => description;

        public DustChronicleRouteNodeDefinition()
        {
        }

        public DustChronicleRouteNodeDefinition(
            string nodeDisplayName,
            DustChronicleRouteNodeType type,
            string nodeContentKey,
            string nodeDescription)
        {
            displayName = nodeDisplayName;
            nodeType = type;
            contentKey = nodeContentKey;
            description = nodeDescription;
        }

        public void Sanitize(int laneIndex, int nodeIndex)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = $"{nodeType} {nodeIndex + 1}";
            }

            if (string.IsNullOrWhiteSpace(contentKey))
            {
                contentKey = $"lane-{laneIndex + 1}/node-{nodeIndex + 1}";
            }

            description ??= string.Empty;
        }
    }

    [Serializable]
    public sealed class DustChronicleRouteLaneDefinition
    {
        [SerializeField] private string displayName = "路线 1";
        [SerializeField] private List<DustChronicleRouteNodeDefinition> nodes = new List<DustChronicleRouteNodeDefinition>();

        public string DisplayName => displayName;
        public IReadOnlyList<DustChronicleRouteNodeDefinition> Nodes => nodes;
        public int NodeCount => nodes != null ? nodes.Count : 0;

        public DustChronicleRouteLaneDefinition()
        {
        }

        public DustChronicleRouteLaneDefinition(string laneDisplayName, List<DustChronicleRouteNodeDefinition> laneNodes)
        {
            displayName = laneDisplayName;
            nodes = laneNodes ?? new List<DustChronicleRouteNodeDefinition>();
        }

        public void Sanitize(int laneIndex)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = $"Lane {laneIndex + 1}";
            }

            nodes ??= new List<DustChronicleRouteNodeDefinition>();
            for (var i = 0; i < nodes.Count; i++)
            {
                nodes[i] ??= new DustChronicleRouteNodeDefinition();
                nodes[i].Sanitize(laneIndex, i);
            }
        }
    }

    [CreateAssetMenu(
        fileName = "DustChronicleRouteMap",
        menuName = "Amber/Star Rail Dust Chronicle/Route Map")]
    public sealed class DustChronicleRouteMap : ScriptableObject
    {
        public const int LaneCount = 3;

        [SerializeField] private string mapName = "尘灵编年史白盒路线";
        [TextArea(2, 4)]
        [SerializeField] private string summary = "Three authored routes for a single run.";
        [SerializeField] private List<DustChronicleRouteLaneDefinition> lanes = new List<DustChronicleRouteLaneDefinition>();

        public string MapName => mapName;
        public string Summary => summary;
        public IReadOnlyList<DustChronicleRouteLaneDefinition> Lanes => lanes;

        public static DustChronicleRouteMap CreateRuntimeDefault()
        {
            var map = CreateInstance<DustChronicleRouteMap>();
            map.ResetToSample();
            return map;
        }

        public void ResetToSample()
        {
            mapName = "尘灵编年史白盒路线";
            summary = "示例三线路线地图。将节点名称和内容键替换为你的创作遭遇。";
            lanes = new List<DustChronicleRouteLaneDefinition>
            {
                CreateLane(
                    "左路",
                    ("开局遭遇战", DustChronicleRouteNodeType.Battle, "combat/slime_intro", "左路第一个战斗示例。"),
                    ("补给站", DustChronicleRouteNodeType.Shop, "shop/basic_supplies", "商店节点占位符。"),
                    ("尘灵伏击", DustChronicleRouteNodeType.Battle, "combat/dust_ambush", "第二个创作战斗遭遇。"),
                    ("路线终章", DustChronicleRouteNodeType.Boss, "combat/route_boss_left", "左路Boss占位符。")),
                CreateLane(
                    "中路",
                    ("坍塌的堤道", DustChronicleRouteNodeType.Event, "event/causeway", "路线中途事件选择。"),
                    ("巡逻冲突", DustChronicleRouteNodeType.Battle, "combat/patrol_clash", "标准战斗遭遇。"),
                    ("篝火", DustChronicleRouteNodeType.Rest, "rest/campfire", "休息或升级类型节点。"),
                    ("金库之门", DustChronicleRouteNodeType.EliteBattle, "combat/vault_guard", "精英遭遇占位符。"),
                    ("路线终章", DustChronicleRouteNodeType.Boss, "combat/route_boss_center", "中路Boss占位符。")),
                CreateLane(
                    "右路",
                    ("破碎市场", DustChronicleRouteNodeType.Shop, "shop/broken_market", "以商店开局的路线。"),
                    ("寂静殿堂", DustChronicleRouteNodeType.Event, "event/silent_hall", "叙事或选择节点。"),
                    ("遗物宝库", DustChronicleRouteNodeType.Treasure, "treasure/relic_cache", "宝物节点占位符。"),
                    ("后卫部队", DustChronicleRouteNodeType.Battle, "combat/rear_guard", "终章前的标准战斗。"),
                    ("路线终章", DustChronicleRouteNodeType.Boss, "combat/route_boss_right", "右路Boss占位符。"))
            };

            Sanitize();
        }

        public void Sanitize()
        {
            if (string.IsNullOrWhiteSpace(mapName))
            {
                mapName = "尘灵编年史白盒路线";
            }

            summary ??= string.Empty;
            lanes ??= new List<DustChronicleRouteLaneDefinition>();

            while (lanes.Count < LaneCount)
            {
                lanes.Add(new DustChronicleRouteLaneDefinition());
            }

            while (lanes.Count > LaneCount)
            {
                lanes.RemoveAt(lanes.Count - 1);
            }

            for (var i = 0; i < lanes.Count; i++)
            {
                lanes[i] ??= new DustChronicleRouteLaneDefinition();
                lanes[i].Sanitize(i);
            }
        }

        private void OnValidate()
        {
            Sanitize();
        }

        private static DustChronicleRouteLaneDefinition CreateLane(
            string laneName,
            params (string displayName, DustChronicleRouteNodeType nodeType, string contentKey, string description)[] nodeSpecs)
        {
            var nodes = new List<DustChronicleRouteNodeDefinition>();
            foreach (var spec in nodeSpecs)
            {
                nodes.Add(CreateNode(spec.displayName, spec.nodeType, spec.contentKey, spec.description));
            }

            var lane = new DustChronicleRouteLaneDefinition(laneName, nodes);
            return lane;
        }

        private static DustChronicleRouteNodeDefinition CreateNode(
            string displayName,
            DustChronicleRouteNodeType nodeType,
            string contentKey,
            string description)
        {
            return new DustChronicleRouteNodeDefinition(displayName, nodeType, contentKey, description);
        }
    }
}
