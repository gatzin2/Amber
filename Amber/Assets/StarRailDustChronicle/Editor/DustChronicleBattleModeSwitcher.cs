using Amber.StarRailDustChronicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Amber.StarRailDustChronicle.Editor
{
    public sealed class DustChronicleBattleModeSwitcher : EditorWindow
    {
        private const string ControllerFieldName = "directBattleType";

        private DustChronicleRouteRunController cachedController;
        private DirectBattleType currentMode;
        private bool hasController;
        private double lastScanTime;

        [MenuItem("Tools/Whitebox/Battle Mode Switcher")]
        public static void Open()
        {
            var window = GetWindow<DustChronicleBattleModeSwitcher>("战斗模式切换");
            window.minSize = new Vector2(310f, 260f);
            window.Show();
        }

        private void OnEnable()
        {
            EditorSceneManager.sceneOpened += OnSceneChanged;
            ScanController();
        }

        private void OnDisable()
        {
            EditorSceneManager.sceneOpened -= OnSceneChanged;
        }

        private void OnSceneChanged(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            cachedController = null;
            hasController = false;
            Repaint();
        }

        private void OnGUI()
        {
            if (EditorApplication.timeSinceStartup - lastScanTime > 1.0)
            {
                ScanController();
                lastScanTime = EditorApplication.timeSinceStartup;
            }

            EditorGUILayout.LabelField("战斗模式切换器", EditorStyles.boldLabel);
            EditorGUILayout.Space(6f);

            if (!hasController)
            {
                DrawNoController();
                return;
            }

            DrawModeInfo();
            EditorGUILayout.Space(10f);
            DrawModeButtons();
            EditorGUILayout.Space(10f);
            DrawQuickActions();
        }

        private void DrawNoController()
        {
            EditorGUILayout.HelpBox(
                "当前场景中未找到 DustChronicleRouteRunController。\n\n" +
                "请先打开白盒场景：\n" +
                "Tools → Whitebox → Build Dust Chronicle Whitebox\n" +
                "或在场景层级中手动添加该组件。",
                MessageType.Warning);

            EditorGUILayout.Space(8f);
            if (GUILayout.Button("构建白盒场景", GUILayout.Height(32f)))
            {
                DustChronicleWhiteboxBuilder.Build();
            }

            EditorGUILayout.Space(4f);
            if (GUILayout.Button("重新扫描", GUILayout.Height(24f)))
            {
                ScanController();
            }
        }

        private void DrawModeInfo()
        {
            var style = new GUIStyle(EditorStyles.helpBox)
            {
                richText = true,
                padding = new RectOffset(12, 12, 10, 10)
            };

            using (new EditorGUILayout.VerticalScope(style))
            {
                EditorGUILayout.LabelField("当前模式", EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(2f);

                var description = currentMode switch
                {
                    DirectBattleType.None => "路线选择模式（默认）",
                    DirectBattleType.NormalBattle => "普通战斗白盒 — 3v3 对战",
                    DirectBattleType.EliteBattle => "精英战斗白盒 — 1v3 巨型精英",
                    _ => "未知"
                };

                var color = currentMode switch
                {
                    DirectBattleType.None => new Color(0.6f, 0.7f, 0.8f),
                    DirectBattleType.NormalBattle => new Color(0.3f, 0.7f, 0.4f),
                    DirectBattleType.EliteBattle => new Color(0.95f, 0.35f, 0.3f),
                    _ => Color.white
                };

                var colorHex = ColorUtility.ToHtmlStringRGB(color);
                EditorGUILayout.LabelField(
                    $"<b><color=#{colorHex}>{currentMode}</color></b> — {description}",
                    new GUIStyle(EditorStyles.label) { richText = true, wordWrap = true });

                EditorGUILayout.Space(4f);
                if (currentMode != DirectBattleType.None)
                {
                    EditorGUILayout.LabelField(
                        "点击 Play 即可直接进入战斗，无需路线选择。",
                        new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
                }
                else
                {
                    EditorGUILayout.LabelField(
                        "Play 模式下将显示路线选择界面。",
                        new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
                }
            }
        }

        private void DrawModeButtons()
        {
            EditorGUILayout.LabelField("切换模式", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(2f);

            var isNone = currentMode == DirectBattleType.None;
            var isNormal = currentMode == DirectBattleType.NormalBattle;
            var isElite = currentMode == DirectBattleType.EliteBattle;

            using (new EditorGUILayout.HorizontalScope())
            {
                var prevColor = GUI.backgroundColor;

                GUI.backgroundColor = isNone ? new Color(0.5f, 0.6f, 0.7f) : Color.white;
                if (GUILayout.Button("路线模式", GUILayout.Height(36f)))
                {
                    SwitchMode(DirectBattleType.None);
                }

                GUI.backgroundColor = isNormal ? new Color(0.3f, 0.7f, 0.4f) : Color.white;
                if (GUILayout.Button("普通战斗", GUILayout.Height(36f)))
                {
                    SwitchMode(DirectBattleType.NormalBattle);
                }

                GUI.backgroundColor = isElite ? new Color(0.95f, 0.35f, 0.3f) : Color.white;
                if (GUILayout.Button("精英战斗", GUILayout.Height(36f)))
                {
                    SwitchMode(DirectBattleType.EliteBattle);
                }

                GUI.backgroundColor = prevColor;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                "路线模式 = 完整路线选择流程\n普通战斗 = 3v3 直接对战\n精英战斗 = 1 个巨型精英 vs 3 个我方尘灵",
                new GUIStyle(EditorStyles.miniLabel) { wordWrap = true });
        }

        private void DrawQuickActions()
        {
            EditorGUILayout.LabelField("快捷操作", EditorStyles.miniBoldLabel);
            EditorGUILayout.Space(2f);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("构建场景", GUILayout.Height(28f)))
                {
                    DustChronicleWhiteboxBuilder.Build();
                    ScanController();
                }

                if (GUILayout.Button("配置面板", GUILayout.Height(28f)))
                {
                    DustChronicleWhiteboxConfigWindow.Open();
                }

                if (GUILayout.Button("进入 Play", GUILayout.Height(28f)))
                {
                    if (!EditorApplication.isPlaying)
                    {
                        EditorApplication.EnterPlaymode();
                    }
                }
            }
        }

        private void SwitchMode(DirectBattleType mode)
        {
            if (cachedController == null)
            {
                ScanController();
                if (cachedController == null) return;
            }

            Undo.RecordObject(cachedController, "切换战斗模式");
            var field = typeof(DustChronicleRouteRunController).GetField(
                ControllerFieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(cachedController, mode);
                EditorUtility.SetDirty(cachedController);
                currentMode = mode;
                Repaint();
            }
        }

        private void ScanController()
        {
            cachedController = FindObjectOfType<DustChronicleRouteRunController>();
            hasController = cachedController != null;

            if (hasController)
            {
                var field = typeof(DustChronicleRouteRunController).GetField(
                    ControllerFieldName,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (field != null)
                {
                    currentMode = (DirectBattleType)field.GetValue(cachedController);
                }
            }
        }
    }
}
