using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Amber.StarRailDustChronicle
{
    /// <summary>
    /// Draws the layered branching map on screen using OnGUI.
    /// Shows rooms as nodes, connection lines, and handles room selection.
    /// </summary>
    public sealed class DustChronicleMapVisualizer : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private Vector2 mapOrigin = new Vector2(60f, 40f);
        [SerializeField] private float mapScale = 0.55f;
        [SerializeField] private float nodeSize = 36f;
        [SerializeField] private float lineWidth = 3f;

        [Header("Colors")]
        [SerializeField] private Color currentRoomColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color availableRoomColor = new Color(0.3f, 0.85f, 0.4f);
        [SerializeField] private Color completedRoomColor = new Color(0.35f, 0.35f, 0.38f);
        [SerializeField] private Color unavailableRoomColor = new Color(0.2f, 0.2f, 0.22f);
        [SerializeField] private Color connectionLineColor = new Color(0.45f, 0.45f, 0.5f);
        [SerializeField] private Color bossRoomColor = new Color(0.9f, 0.25f, 0.2f);

        private DustChronicleMapController controller;
        private Texture2D whitePixel;
        private Dictionary<string, Rect> roomRects = new Dictionary<string, Rect>();
        private string hoveredRoomId;

        private void Awake()
        {
            whitePixel = new Texture2D(1, 1);
            whitePixel.SetPixel(0, 0, Color.white);
            whitePixel.Apply();
        }

        private void OnDestroy()
        {
            if (whitePixel != null)
                Destroy(whitePixel);
        }

        public void OnGUI() { }

        public void Initialize(DustChronicleMapController mapController)
        {
            controller = mapController;
        }

        public void DrawMapGui()
        {
            if (controller?.MapData == null) return;

            var mapData = controller.MapData;
            if (mapData.Layers.Count == 0) return;

            // Calculate map panel bounds
            float panelWidth = 480f;
            float panelHeight = Screen.height - 80f;
            Rect panelRect = new Rect(mapOrigin.x, mapOrigin.y, panelWidth, panelHeight);

            // Backdrop
            GUI.Box(panelRect, "Route Map", new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperCenter
            });

            roomRects.Clear();

            // Draw connections first (behind nodes)
            DrawAllConnections(mapData, panelRect);

            // Draw room nodes
            for (int layerIdx = 0; layerIdx < mapData.Layers.Count; layerIdx++)
            {
                var layer = mapData.Layers[layerIdx];
                foreach (var room in layer.Rooms)
                {
                    DrawRoomNode(room, mapData, panelRect);
                }
            }

            // Handle layer labels
            DrawLayerLabels(mapData, panelRect);
        }

        private Vector2 MapToScreen(Vector2 mapPos, Rect panelRect)
        {
            // Map Y increases upward, screen Y increases downward
            // We want the start layer at the bottom of the panel
            float x = panelRect.x + panelRect.width / 2f + mapPos.x * mapScale;
            float y = panelRect.y + panelRect.height - 40f - mapPos.y * mapScale;
            return new Vector2(x, y);
        }

        private Rect GetRoomRect(Vector2 screenPos)
        {
            float halfSize = nodeSize / 2f;
            return new Rect(screenPos.x - halfSize, screenPos.y - halfSize, nodeSize, nodeSize);
        }

        private void DrawRoomNode(MapRoomNode room, GeneratedMapData mapData, Rect panelRect)
        {
            var screenPos = MapToScreen(room.Position, panelRect);
            var rect = GetRoomRect(screenPos);
            roomRects[room.RoomId] = rect;

            // Determine room state color
            Color roomColor = unavailableRoomColor;
            bool isClickable = false;

            bool hasCurrentRoom = mapData.CurrentRoom != null;
            bool isRunStarted = hasCurrentRoom || controller.CompletedRoomIds.Count > 0;

            if (room.RoomId == mapData.CurrentRoom?.RoomId)
            {
                // This is the currently active room
                roomColor = currentRoomColor;
                isClickable = false;
            }
            else if (controller.CompletedRoomIds.Contains(room.RoomId))
            {
                // Already completed
                roomColor = completedRoomColor;
                isClickable = false;
            }
            else if (!isRunStarted && room.LayerIndex == 0)
            {
                // Run not started yet: all layer-0 rooms are selectable starting points
                roomColor = availableRoomColor;
                isClickable = true;
            }
            else if (hasCurrentRoom)
            {
                // Run in progress: only rooms connected from the current room are selectable
                var available = controller.GetAvailableNextRooms();
                if (available.Exists(r => r.RoomId == room.RoomId))
                {
                    roomColor = availableRoomColor;
                    isClickable = true;
                }
            }

            DrawCircleNode(rect, roomColor, room);

            // Click detection
            if (isClickable)
            {
                var evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(evt.mousePosition))
                {
                    controller.SelectRoom(room);
                    evt.Use();
                }

                if (rect.Contains(evt.mousePosition))
                {
                    hoveredRoomId = room.RoomId;
                }
            }

            // Room label
            string label = GetNodeTypeSymbol(room.NodeType);
            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(rect, label, labelStyle);
        }

        private void DrawCircleNode(Rect rect, Color color, MapRoomNode room)
        {
            Color prevColor = GUI.color;
            GUI.color = color;

            float border = 2f;
            // Background (slightly larger for border effect)
            GUI.Box(new Rect(rect.x - border, rect.y - border,
                rect.width + border * 2, rect.height + border * 2), "");

            // Inner
            GUI.Box(new Rect(rect.x + border, rect.y + border,
                rect.width - border * 2, rect.height - border * 2), "");

            GUI.color = prevColor;
        }

        private void DrawAllConnections(GeneratedMapData mapData, Rect panelRect)
        {
            var drawnConnections = new HashSet<string>();

            for (int layerIdx = 0; layerIdx < mapData.Layers.Count - 1; layerIdx++)
            {
                var layer = mapData.Layers[layerIdx];
                var upperLayer = mapData.Layers[layerIdx + 1];

                foreach (var room in layer.Rooms)
                {
                    var fromPos = MapToScreen(room.Position, panelRect);
                    fromPos = new Vector2(fromPos.x, fromPos.y); // center of node

                    foreach (int targetIdx in room.ConnectedToRoomIndices)
                    {
                        var targetRoom = upperLayer.GetRoom(targetIdx);
                        if (targetRoom == null) continue;

                        string connKey = $"{room.RoomId}->{targetRoom.RoomId}";
                        if (drawnConnections.Contains(connKey)) continue;
                        drawnConnections.Add(connKey);

                        var toPos = MapToScreen(targetRoom.Position, panelRect);

                        Color lineColor = connectionLineColor;
                        bool isActive = room.RoomId == mapData.CurrentRoom?.RoomId;
                        bool isCompleted = controller.CompletedRoomIds.Contains(room.RoomId);

                        if (isActive)
                            lineColor = Color.Lerp(currentRoomColor, Color.white, 0.5f);
                        else if (isCompleted)
                            lineColor = Color.Lerp(completedRoomColor, Color.white, 0.3f);

                        DrawLine(fromPos, toPos, lineColor, lineWidth);
                    }
                }
            }
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float width)
        {
            Color prevColor = GUI.color;
            GUI.color = color;

            Vector2 direction = to - from;
            float length = direction.magnitude;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            // Save matrix and apply rotation
            Matrix4x4 prevMatrix = GUI.matrix;
            Vector2 center = (from + to) / 2f;

            GUIUtility.RotateAroundPivot(angle, center);
            GUI.DrawTexture(new Rect(center.x - length / 2f, center.y - width / 2f, length, width), whitePixel);

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        private void DrawLayerLabels(GeneratedMapData mapData, Rect panelRect)
        {
            for (int i = 0; i < mapData.Layers.Count; i++)
            {
                var layer = mapData.Layers[i];
                if (layer.RoomCount == 0) continue;

                var firstRoom = layer.Rooms[0];
                var screenPos = MapToScreen(firstRoom.Position, panelRect);

                string label = i == 0 ? "START" : i == mapData.Layers.Count - 1 ? "BOSS" : $"L{i + 1}";
                var labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    alignment = TextAnchor.MiddleRight,
                    normal = { textColor = new Color(0.6f, 0.6f, 0.65f) }
                };

                GUI.Label(new Rect(panelRect.x + 4, screenPos.y - 8, panelRect.width - 8, 16), label, labelStyle);
            }
        }

        private static string GetNodeTypeSymbol(DustChronicleRouteNodeType nodeType)
        {
            return nodeType switch
            {
                DustChronicleRouteNodeType.Battle => "⚔",
                DustChronicleRouteNodeType.EliteBattle => "☠",
                DustChronicleRouteNodeType.Boss => "💀",
                DustChronicleRouteNodeType.Shop => "💰",
                DustChronicleRouteNodeType.Event => "?",
                DustChronicleRouteNodeType.Rest => "🏕",
                DustChronicleRouteNodeType.Treasure => "💎",
                _ => "●"
            };
        }
    }
}
