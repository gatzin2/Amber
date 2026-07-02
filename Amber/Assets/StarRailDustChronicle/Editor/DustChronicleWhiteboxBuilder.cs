using System.Collections.Generic;
using Amber.StarRailDustChronicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Amber.StarRailDustChronicle.Editor
{
    public static class DustChronicleWhiteboxBuilder
    {
        private const string ScenePath = "Assets/Scenes/DustChronicleWhitebox.unity";
        private const string DustPrefabFolder = "Assets/Dust";
        private const string DustPrefabPath = "Assets/Dust/DustSpirit.prefab";

        [MenuItem("Tools/Whitebox/Build Dust Chronicle Whitebox")]
        public static void Build()
        {
            Build(DustChronicleWhiteboxConfigStore.Load());
        }

        public static void Build(DustChronicleWhiteboxConfig config)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "DustChronicleWhitebox";
            config ??= DustChronicleWhiteboxConfig.CreateDefault();
            config.Sanitize();

            var root = new GameObject("DustChronicleWhitebox_Root");
            var controller = root.AddComponent<DustChronicleCombatController>();
            var routeController = root.AddComponent<DustChronicleRouteRunController>();

            CreateCamera();
            CreateLight();
            CreateFloor();
            CreateLaneMarkers();
            CreateBoundaryRails();

            var statusBoard = CreateText(
                "Status_Board",
                new Vector3(-14.6f, 5.2f, 7.6f),
                new Vector3(55f, 0f, 0f),
                0.34f,
                Color.white,
                "尘灵编年史白盒");

            var routeBoard = CreateText(
                "Route_Board",
                new Vector3(9.8f, 5.2f, 7.6f),
                new Vector3(55f, 0f, 0f),
                0.28f,
                new Color(0.92f, 0.95f, 1f),
                "尘灵编年史路线白盒");

            CreateText(
                "Design_Assumptions",
                new Vector3(0f, 0.06f, 10.4f),
                new Vector3(90f, 0f, 0f),
                0.36f,
                new Color(0.8f, 0.9f, 1f),
                "验证白盒：选择一条创作路线，按顺序启动节点，战斗节点自动对战，所有节点完成=通关。");

            var players = CreateSide(config.players, DustTeam.Player, -6.6f, controller);
            var enemies = CreateSide(config.enemies, DustTeam.Enemy, 6.6f, controller);

            // Create elite enemy for elite battle whitebox
            var eliteEnemyConfig = config.eliteEnemy ?? new DustChronicleWhiteboxUnitConfig("Elite Dust Spirit", 330, 32, 3f, new Color(1f, 0.18f, 0.42f));
            eliteEnemyConfig.Sanitize("Elite Dust Spirit");
            var eliteEnemy = CreateUnit(
                "精英敌方尘灵_01",
                DustTeam.Enemy,
                eliteEnemyConfig.displayName,
                new Vector3(6.6f, 0.05f + eliteEnemyConfig.size * 0.5f, 0f),
                eliteEnemyConfig.hitPoints,
                eliteEnemyConfig.attackPower,
                eliteEnemyConfig.size,
                eliteEnemyConfig.color,
                controller);
            eliteEnemy.transform.SetParent(root.transform);
            eliteEnemy.gameObject.SetActive(false);

            foreach (var player in players)
            {
                player.transform.SetParent(root.transform);
            }

            foreach (var enemy in enemies)
            {
                enemy.transform.SetParent(root.transform);
            }

            controller.Configure(players, enemies, statusBoard);
            routeController.Configure(DustChronicleRouteMapUtility.LoadOrCreateDefaultAsset(), routeBoard);


            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeObject = root;
            EditorGUIUtility.PingObject(root);
            Debug.Log($"Built Dust Chronicle whitebox scene at {ScenePath}");
        }

        private static List<DustChronicleUnit> CreateSide(
            List<DustChronicleWhiteboxUnitConfig> configs,
            DustTeam team,
            float xPosition,
            DustChronicleCombatController controller)
        {
            var units = new List<DustChronicleUnit>();
            var count = configs.Count;
            var spacing = count <= 1 ? 0f : Mathf.Min(2.7f, 8.4f / (count - 1));
            var startZ = -spacing * (count - 1) * 0.5f;

            for (var i = 0; i < count; i++)
            {
                var unitConfig = configs[i];
                var size = Mathf.Clamp(unitConfig.size, 0.45f, 2.2f);
                var position = new Vector3(xPosition, 0.05f + size * 0.5f, startZ + spacing * i);
                units.Add(CreateUnit(
                    $"{(team == DustTeam.Player ? "我方" : "敌方")}尘灵_{i + 1:00}",
                    team,
                    unitConfig.displayName,
                    position,
                    unitConfig.hitPoints,
                    unitConfig.attackPower,
                    size,
                    unitConfig.color,
                    controller));
            }

            return units;
        }

        private static DustChronicleUnit CreateUnit(
            string objectName,
            DustTeam team,
            string displayName,
            Vector3 position,
            int hp,
            int attackPower,
            float size,
            Color color,
            DustChronicleCombatController controller)
        {
            var prefab = EnsureDustSpiritPrefab();
            var unitObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            unitObject.name = objectName;
            unitObject.transform.position = position;

            var boxCollider = unitObject.GetComponent<BoxCollider>();
            if (boxCollider != null)
            {
                boxCollider.size = Vector3.one * size;
            }

            var visual = unitObject.transform.Find("Visual");
            if (visual != null)
            {
                visual.localScale = new Vector3(size, size * 1.5f, size);
                var renderer = visual.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var shader = Shader.Find("Standard");
                    renderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"))
                    {
                        color = color
                    };
                }
                var jellyBreath = visual.GetComponent<DustJellyBreath>();
                if (jellyBreath != null)
                {
                    jellyBreath.SetBaseScale(visual.localScale);
                }
            }

            var unit = unitObject.GetComponent<DustChronicleUnit>();
            if (unit == null)
            {
                unit = unitObject.AddComponent<DustChronicleUnit>();
            }

            var label = CreateText(
                $"{objectName}_Label",
                position + new Vector3(-1.1f * size, 0.65f + size * 0.65f, -0.45f * size),
                new Vector3(55f, 0f, 0f),
                0.22f,
                Color.white,
                displayName);
            label.transform.SetParent(unitObject.transform);
            unit.Configure(team, displayName, hp, attackPower, color, visual, controller);
            unit.SetLabel(label);

            return unit;
        }

        private static GameObject EnsureDustSpiritPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DustPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureDustFolder();

            var root = new GameObject("DustSpirit");

            var collider = root.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            collider.center = Vector3.zero;

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = false;
            body.drag = 0.05f;
            body.angularDrag = 4f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            root.AddComponent<DustChronicleUnit>();

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = new Vector3(1f, 1.5f, 1f);
            Object.DestroyImmediate(visual.GetComponent<BoxCollider>());
            visual.AddComponent<DustJellyBreath>();
            SetMaterial(visual, Color.white);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, DustPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureDustFolder()
        {
            if (!AssetDatabase.IsValidFolder(DustPrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Dust");
            }
        }

        private static void CreateCamera()
        {
            var cameraObject = new GameObject("Whitebox_Camera");
            cameraObject.transform.position = new Vector3(0f, 14.4f, -15.6f);
            cameraObject.transform.rotation = Quaternion.Euler(58f, 0f, 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.07f, 0.08f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100f;
            cameraObject.tag = "MainCamera";
        }

        private static void CreateLight()
        {
            var lightObject = new GameObject("Whitebox_KeyLight");
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
        }

        private static void CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Battlefield_Base";
            floor.transform.position = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(17f, 0.1f, 10.4f);
            SetMaterial(floor, new Color(0.16f, 0.17f, 0.18f));
        }

        private static void CreateLaneMarkers()
        {
            for (var i = -1; i <= 1; i++)
            {
                var lane = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lane.name = $"LaneMarker_{i + 2}";
                lane.transform.position = new Vector3(0f, 0.02f, i * 3.2f);
                lane.transform.localScale = new Vector3(16.4f, 0.04f, 0.06f);
                SetMaterial(lane, new Color(0.42f, 0.42f, 0.42f));
            }

            var centerLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centerLine.name = "TurnBoundary_CenterLine";
            centerLine.transform.position = new Vector3(0f, 0.03f, 0f);
            centerLine.transform.localScale = new Vector3(0.1f, 0.05f, 10f);
            SetMaterial(centerLine, new Color(0.95f, 0.86f, 0.25f));
        }

        private static void CreateBoundaryRails()
        {
            CreateRail("BoundaryRail_North", new Vector3(0f, 0.35f, 5.35f), new Vector3(17.2f, 0.7f, 0.2f));
            CreateRail("BoundaryRail_South", new Vector3(0f, 0.35f, -5.35f), new Vector3(17.2f, 0.7f, 0.2f));
            CreateRail("BoundaryRail_West", new Vector3(-8.6f, 0.35f, 0f), new Vector3(0.2f, 0.7f, 10.8f));
            CreateRail("BoundaryRail_East", new Vector3(8.6f, 0.35f, 0f), new Vector3(0.2f, 0.7f, 10.8f));
        }

        private static void CreateRail(string objectName, Vector3 position, Vector3 scale)
        {
            var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = objectName;
            rail.transform.position = position;
            rail.transform.localScale = scale;
            SetMaterial(rail, new Color(0.12f, 0.13f, 0.14f));
        }

        private static TextMesh CreateText(string objectName, Vector3 position, Vector3 eulerAngles, float size, Color color, string text)
        {
            var textObject = new GameObject(objectName);
            textObject.transform.position = position;
            textObject.transform.rotation = Quaternion.Euler(eulerAngles);

            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.characterSize = size;
            textMesh.anchor = TextAnchor.UpperLeft;
            textMesh.alignment = TextAlignment.Left;
            textMesh.color = color;
            return textMesh;
        }

        private static void SetMaterial(GameObject target, Color color)
        {
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null)
            {
                return;
            }

            var shader = Shader.Find("Standard");
            renderer.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Diffuse"))
            {
                color = color
            };
        }
    }
}
