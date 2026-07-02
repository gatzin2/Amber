using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Amber.StarRailDustChronicle.Editor
{
    [Serializable]
    public sealed class DustChronicleWhiteboxUnitConfig
    {
        public string displayName;
        public int hitPoints;
        public int attackPower;
        public float size;
        public Color color;

        public DustChronicleWhiteboxUnitConfig()
        {
            displayName = "尘灵";
            hitPoints = 100;
            attackPower = 12;
            size = 1f;
            color = Color.white;
        }

        public DustChronicleWhiteboxUnitConfig(string name, int hp, int attack, float unitSize, Color unitColor)
        {
            displayName = name;
            hitPoints = hp;
            attackPower = attack;
            size = unitSize;
            color = unitColor;
        }

        public void Sanitize(string fallbackName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = fallbackName;
            }

            hitPoints = Mathf.Max(1, hitPoints);
            attackPower = Mathf.Max(1, attackPower);
            size = Mathf.Clamp(size, 0.45f, 3.0f);
        }
    }

    [Serializable]
    public sealed class DustChronicleWhiteboxConfig
    {
        public List<DustChronicleWhiteboxUnitConfig> players = new List<DustChronicleWhiteboxUnitConfig>();
        public List<DustChronicleWhiteboxUnitConfig> enemies = new List<DustChronicleWhiteboxUnitConfig>();
        public DustChronicleWhiteboxUnitConfig eliteEnemy;

        public static DustChronicleWhiteboxConfig CreateDefault()
        {
            return new DustChronicleWhiteboxConfig
            {
                players = new List<DustChronicleWhiteboxUnitConfig>
                {
                    new DustChronicleWhiteboxUnitConfig("我方尘灵 A", 120, 14, 1f, new Color(0.2f, 0.55f, 1f)),
                    new DustChronicleWhiteboxUnitConfig("我方尘灵 B", 96, 20, 1f, new Color(0.2f, 0.85f, 0.65f)),
                    new DustChronicleWhiteboxUnitConfig("我方尘灵 C", 140, 10, 1f, new Color(0.85f, 0.75f, 0.25f))
                },
                enemies = new List<DustChronicleWhiteboxUnitConfig>
                {
                    new DustChronicleWhiteboxUnitConfig("敌方尘灵 A", 110, 16, 1f, new Color(1f, 0.35f, 0.3f)),
                    new DustChronicleWhiteboxUnitConfig("敌方尘灵 B", 130, 12, 1f, new Color(0.9f, 0.22f, 0.45f)),
                    new DustChronicleWhiteboxUnitConfig("敌方尘灵 C", 82, 24, 1f, new Color(0.75f, 0.25f, 0.9f))
                },
                eliteEnemy = new DustChronicleWhiteboxUnitConfig("精英尘灵", 330, 32, 3f, new Color(1f, 0.18f, 0.42f))
            };
        }

        public void Sanitize()
        {
            EnsureCount(players, Mathf.Clamp(players.Count, 1, 8), DustTeam.Player);
            EnsureCount(enemies, Mathf.Clamp(enemies.Count, 1, 8), DustTeam.Enemy);
            SanitizeList(players, DustTeam.Player);
            SanitizeList(enemies, DustTeam.Enemy);
            if (eliteEnemy == null)
            {
                eliteEnemy = new DustChronicleWhiteboxUnitConfig("精英尘灵", 330, 32, 3f, new Color(1f, 0.18f, 0.42f));
            }
            eliteEnemy.Sanitize("精英尘灵");
        }

        public void SetCount(DustTeam team, int count)
        {
            EnsureCount(GetList(team), Mathf.Clamp(count, 1, 8), team);
            SanitizeList(GetList(team), team);
        }

        public List<DustChronicleWhiteboxUnitConfig> GetList(DustTeam team)
        {
            return team == DustTeam.Player ? players : enemies;
        }

        private static void EnsureCount(List<DustChronicleWhiteboxUnitConfig> units, int count, DustTeam team)
        {
            while (units.Count < count)
            {
                var index = units.Count;
                var isPlayer = team == DustTeam.Player;
                units.Add(new DustChronicleWhiteboxUnitConfig(
                    $"{(isPlayer ? "我方" : "敌方")}尘灵 {IndexToLetter(index)}",
                    isPlayer ? 110 : 105,
                    isPlayer ? 14 : 16,
                    1f,
                    isPlayer ? PlayerColor(index) : EnemyColor(index)));
            }

            while (units.Count > count)
            {
                units.RemoveAt(units.Count - 1);
            }
        }

        private static void SanitizeList(List<DustChronicleWhiteboxUnitConfig> units, DustTeam team)
        {
            for (var i = 0; i < units.Count; i++)
            {
                units[i] ??= new DustChronicleWhiteboxUnitConfig(string.Empty, 100, 12, 1f, Color.white);
                units[i].Sanitize($"{(team == DustTeam.Player ? "我方" : "敌方")}尘灵 {IndexToLetter(i)}");
            }
        }

        private static string IndexToLetter(int index)
        {
            return ((char)('A' + Mathf.Clamp(index, 0, 25))).ToString();
        }

        private static Color PlayerColor(int index)
        {
            var colors = new[]
            {
                new Color(0.2f, 0.55f, 1f),
                new Color(0.2f, 0.85f, 0.65f),
                new Color(0.85f, 0.75f, 0.25f),
                new Color(0.55f, 0.85f, 1f)
            };
            return colors[index % colors.Length];
        }

        private static Color EnemyColor(int index)
        {
            var colors = new[]
            {
                new Color(1f, 0.35f, 0.3f),
                new Color(0.9f, 0.22f, 0.45f),
                new Color(0.75f, 0.25f, 0.9f),
                new Color(1f, 0.55f, 0.25f)
            };
            return colors[index % colors.Length];
        }
    }

    public static class DustChronicleWhiteboxConfigStore
    {
        private const string EditorPrefsKey = "Amber.StarRailDustChronicle.WhiteboxConfig";

        public static DustChronicleWhiteboxConfig Load()
        {
            var json = EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                return DustChronicleWhiteboxConfig.CreateDefault();
            }

            var config = JsonUtility.FromJson<DustChronicleWhiteboxConfig>(json) ?? DustChronicleWhiteboxConfig.CreateDefault();
            config.Sanitize();
            return config;
        }

        public static void Save(DustChronicleWhiteboxConfig config)
        {
            config ??= DustChronicleWhiteboxConfig.CreateDefault();
            config.Sanitize();
            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(config));
        }
    }

    public sealed class DustChronicleWhiteboxConfigWindow : EditorWindow
    {
        private DustChronicleWhiteboxConfig config;
        private Vector2 scrollPosition;

        [MenuItem("Tools/Whitebox/Dust Chronicle Config Panel")]
        public static void Open()
        {
            var window = GetWindow<DustChronicleWhiteboxConfigWindow>("Dust Chronicle Config");
            window.minSize = new Vector2(420f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            config = DustChronicleWhiteboxConfigStore.Load();
        }

        private void OnGUI()
        {
            config ??= DustChronicleWhiteboxConfigStore.Load();

            EditorGUILayout.LabelField("Dust Chronicle Whitebox Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);

            EditorGUI.BeginChangeCheck();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawSide(DustTeam.Player, "我方尘灵", config.players);
            EditorGUILayout.Space(8f);
            DrawSide(DustTeam.Enemy, "敌方尘灵", config.enemies);
            EditorGUILayout.Space(12f);
            DrawEliteEnemy();
            EditorGUILayout.EndScrollView();

            if (EditorGUI.EndChangeCheck())
            {
                DustChronicleWhiteboxConfigStore.Save(config);
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Whitebox Scene", GUILayout.Height(32f)))
                {
                    DustChronicleWhiteboxConfigStore.Save(config);
                    DustChronicleWhiteboxBuilder.Build(config);
                }

                if (GUILayout.Button("Open Route Map", GUILayout.Height(32f), GUILayout.Width(120f)))
                {
                    DustChronicleRouteMapUtility.OpenDefaultRouteMap();
                }

                if (GUILayout.Button("Reset Defaults", GUILayout.Height(32f), GUILayout.Width(120f)))
                {
                    config = DustChronicleWhiteboxConfig.CreateDefault();
                    DustChronicleWhiteboxConfigStore.Save(config);
                }
            }
        }

        private void DrawSide(DustTeam team, string title, List<DustChronicleWhiteboxUnitConfig> units)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            var count = EditorGUILayout.IntSlider("Count", units.Count, 1, 8);
            if (count != units.Count)
            {
                config.SetCount(team, count);
            }

            for (var i = 0; i < units.Count; i++)
            {
                DrawUnit(team, i, units[i]);
            }
        }

        private static void DrawUnit(DustTeam team, int index, DustChronicleWhiteboxUnitConfig unit)
        {
            var label = $"{(team == DustTeam.Player ? "Player" : "Enemy")} {index + 1}";
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
                unit.displayName = EditorGUILayout.TextField("Name", unit.displayName);
                unit.hitPoints = EditorGUILayout.IntField("HP", Mathf.Max(1, unit.hitPoints));
                unit.attackPower = EditorGUILayout.IntField("ATK", Mathf.Max(1, unit.attackPower));
                unit.size = EditorGUILayout.Slider("Size", unit.size, 0.45f, 2.2f);
                unit.color = EditorGUILayout.ColorField("Color", unit.color);
                unit.Sanitize(label);
            }
        }

        private void DrawEliteEnemy()
        {
            EditorGUILayout.LabelField("精英敌人（精英战斗白盒）", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("单个巨型精英敌人，3倍体积/生命，2倍攻击", EditorStyles.miniLabel);
                config.eliteEnemy.displayName = EditorGUILayout.TextField("Name", config.eliteEnemy.displayName);
                config.eliteEnemy.hitPoints = EditorGUILayout.IntField("HP", Mathf.Max(1, config.eliteEnemy.hitPoints));
                config.eliteEnemy.attackPower = EditorGUILayout.IntField("ATK", Mathf.Max(1, config.eliteEnemy.attackPower));
                config.eliteEnemy.size = EditorGUILayout.Slider("Size", config.eliteEnemy.size, 0.45f, 5.0f);
                config.eliteEnemy.color = EditorGUILayout.ColorField("Color", config.eliteEnemy.color);
                config.eliteEnemy.Sanitize("精英尘灵");
            }
        }
    }
}
