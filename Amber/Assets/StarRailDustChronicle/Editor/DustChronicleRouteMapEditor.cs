using UnityEditor;
using UnityEngine;

namespace Amber.StarRailDustChronicle.Editor
{
    public static class DustChronicleRouteMapUtility
    {
        public const string DefaultAssetFolder = "Assets/StarRailDustChronicle/Data";
        public const string DefaultAssetPath = DefaultAssetFolder + "/DustChronicleRouteMap.asset";

        [MenuItem("Tools/Whitebox/Open Dust Chronicle Route Map")]
        public static void OpenDefaultRouteMap()
        {
            var asset = LoadOrCreateDefaultAsset();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        public static DustChronicleRouteMap LoadOrCreateDefaultAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<DustChronicleRouteMap>(DefaultAssetPath);
            if (asset != null)
            {
                asset.Sanitize();
                EditorUtility.SetDirty(asset);
                return asset;
            }

            EnsureFolders();
            asset = ScriptableObject.CreateInstance<DustChronicleRouteMap>();
            asset.ResetToSample();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return asset;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/StarRailDustChronicle"))
            {
                AssetDatabase.CreateFolder("Assets", "StarRailDustChronicle");
            }

            if (!AssetDatabase.IsValidFolder(DefaultAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/StarRailDustChronicle", "Data");
            }
        }
    }

    [CustomEditor(typeof(DustChronicleRouteMap))]
    public sealed class DustChronicleRouteMapEditor : UnityEditor.Editor
    {
        private SerializedProperty mapNameProperty;
        private SerializedProperty summaryProperty;
        private SerializedProperty lanesProperty;

        private void OnEnable()
        {
            mapNameProperty = serializedObject.FindProperty("mapName");
            summaryProperty = serializedObject.FindProperty("summary");
            lanesProperty = serializedObject.FindProperty("lanes");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var routeMap = (DustChronicleRouteMap)target;
            routeMap.Sanitize();

            EditorGUILayout.LabelField("尘灵编年史路线地图", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(mapNameProperty);
            EditorGUILayout.PropertyField(summaryProperty);
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("重置为示例布局", GUILayout.Height(28f)))
            {
                Undo.RecordObject(routeMap, "重置路线地图");
                routeMap.ResetToSample();
                EditorUtility.SetDirty(routeMap);
                serializedObject.Update();
            }

            EditorGUILayout.Space(8f);
            DrawLanes();

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawLanes()
        {
            for (var laneIndex = 0; laneIndex < lanesProperty.arraySize; laneIndex++)
            {
                var laneProperty = lanesProperty.GetArrayElementAtIndex(laneIndex);
                var laneNameProperty = laneProperty.FindPropertyRelative("displayName");
                var nodesProperty = laneProperty.FindPropertyRelative("nodes");

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"路线 {laneIndex + 1}", EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(laneNameProperty, new GUIContent("路线名"));

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"节点数： {nodesProperty.arraySize}", GUILayout.Width(110f));
                        if (GUILayout.Button("添加节点", GUILayout.Width(90f)))
                        {
                            nodesProperty.InsertArrayElementAtIndex(nodesProperty.arraySize);
                            InitializeNode(nodesProperty.GetArrayElementAtIndex(nodesProperty.arraySize - 1), laneIndex, nodesProperty.arraySize - 1);
                        }
                    }

                    for (var nodeIndex = 0; nodeIndex < nodesProperty.arraySize; nodeIndex++)
                    {
                        DrawNode(nodesProperty, laneIndex, nodeIndex);
                    }
                }

                EditorGUILayout.Space(6f);
            }
        }

        private void DrawNode(SerializedProperty nodesProperty, int laneIndex, int nodeIndex)
        {
            var nodeProperty = nodesProperty.GetArrayElementAtIndex(nodeIndex);
            var displayNameProperty = nodeProperty.FindPropertyRelative("displayName");
            var nodeTypeProperty = nodeProperty.FindPropertyRelative("nodeType");
            var contentKeyProperty = nodeProperty.FindPropertyRelative("contentKey");
            var descriptionProperty = nodeProperty.FindPropertyRelative("描述");

            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField($"节点 {nodeIndex + 1}", EditorStyles.miniBoldLabel);
                    if (GUILayout.Button("Up", GUILayout.Width(44f)) && nodeIndex > 0)
                    {
                        nodesProperty.MoveArrayElement(nodeIndex, nodeIndex - 1);
                    }

                    if (GUILayout.Button("Down", GUILayout.Width(54f)) && nodeIndex < nodesProperty.arraySize - 1)
                    {
                        nodesProperty.MoveArrayElement(nodeIndex, nodeIndex + 1);
                    }

                    if (GUILayout.Button("删除", GUILayout.Width(60f)))
                    {
                        DeleteNode(nodesProperty, nodeIndex);
                        return;
                    }
                }

                EditorGUILayout.PropertyField(displayNameProperty, new GUIContent("显示名称"));
                EditorGUILayout.PropertyField(nodeTypeProperty, new GUIContent("节点类型"));
                EditorGUILayout.PropertyField(contentKeyProperty, new GUIContent("内容键"));
                EditorGUILayout.PropertyField(descriptionProperty, new GUIContent("描述"));

                if (string.IsNullOrWhiteSpace(displayNameProperty.stringValue))
                {
                    displayNameProperty.stringValue = $"{((DustChronicleRouteNodeType)nodeTypeProperty.enumValueIndex)} {nodeIndex + 1}";
                }

                if (string.IsNullOrWhiteSpace(contentKeyProperty.stringValue))
                {
                    contentKeyProperty.stringValue = $"lane-{laneIndex + 1}/node-{nodeIndex + 1}";
                }
            }
        }

        private static void InitializeNode(SerializedProperty nodeProperty, int laneIndex, int nodeIndex)
        {
            nodeProperty.FindPropertyRelative("displayName").stringValue = $"战斗 {nodeIndex + 1}";
            nodeProperty.FindPropertyRelative("nodeType").enumValueIndex = (int)DustChronicleRouteNodeType.Battle;
            nodeProperty.FindPropertyRelative("contentKey").stringValue = $"lane-{laneIndex + 1}/node-{nodeIndex + 1}";
            nodeProperty.FindPropertyRelative("描述").stringValue = string.Empty;
        }

        private static void DeleteNode(SerializedProperty nodesProperty, int nodeIndex)
        {
            var previousSize = nodesProperty.arraySize;
            nodesProperty.DeleteArrayElementAtIndex(nodeIndex);
            if (nodesProperty.arraySize == previousSize)
            {
                nodesProperty.DeleteArrayElementAtIndex(nodeIndex);
            }
        }
    }
}
