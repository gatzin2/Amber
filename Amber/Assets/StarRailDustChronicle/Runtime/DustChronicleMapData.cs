using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amber.StarRailDustChronicle
{
    [Serializable]
    public sealed class MapRoomNode
    {
        [SerializeField] private string roomId;
        [SerializeField] private int layerIndex;
        [SerializeField] private int roomIndex;
        [SerializeField] private DustChronicleRouteNodeType nodeType;
        [SerializeField] private string contentKey;
        [SerializeField] private Vector2 position;
        [SerializeField] private List<int> connectedToRoomIndices = new List<int>();
        [SerializeField] private List<int> connectedFromRoomIndices = new List<int>();

        public string RoomId { get { return roomId; } }
        public int LayerIndex { get { return layerIndex; } }
        public int RoomIndex { get { return roomIndex; } }
        public DustChronicleRouteNodeType NodeType { get { return nodeType; } }
        public string ContentKey { get { return contentKey; } }
        public Vector2 Position { get { return position; } }
        public IReadOnlyList<int> ConnectedToRoomIndices { get { return connectedToRoomIndices; } }
        public IReadOnlyList<int> ConnectedFromRoomIndices { get { return connectedFromRoomIndices; } }

        public MapRoomNode(int layerIndex, int roomIndex, DustChronicleRouteNodeType nodeType, string contentKey, Vector2 position)
        {
            this.layerIndex = layerIndex;
            this.roomIndex = roomIndex;
            this.nodeType = nodeType;
            this.contentKey = contentKey;
            this.position = position;
            roomId = "L" + layerIndex + "_R" + roomIndex;
        }

        public void AddConnectionTo(int targetRoomIndex)
        {
            if (!connectedToRoomIndices.Contains(targetRoomIndex))
                connectedToRoomIndices.Add(targetRoomIndex);
        }

        public void AddConnectionFrom(int sourceRoomIndex)
        {
            if (!connectedFromRoomIndices.Contains(sourceRoomIndex))
                connectedFromRoomIndices.Add(sourceRoomIndex);
        }

        public bool IsStartRoom { get { return layerIndex == 0; } }
        public bool IsBossRoom { get { return nodeType == DustChronicleRouteNodeType.Boss; } }
    }

    [Serializable]
    public sealed class MapLayerData
    {
        [SerializeField] private int layerIndex;
        [SerializeField] private List<MapRoomNode> rooms = new List<MapRoomNode>();

        public int LayerIndex { get { return layerIndex; } }
        public IReadOnlyList<MapRoomNode> Rooms { get { return rooms; } }
        public int RoomCount { get { return rooms.Count; } }

        public MapLayerData(int layerIndex) { this.layerIndex = layerIndex; }

        public void AddRoom(MapRoomNode room) { rooms.Add(room); }

        public MapRoomNode GetRoom(int roomIndex)
        {
            if (roomIndex < 0 || roomIndex >= rooms.Count) return null;
            return rooms[roomIndex];
        }
    }

    [CreateAssetMenu(fileName = "DustChronicleMapConfig", menuName = "Amber/Star Rail Dust Chronicle/Map Generation Config")]
    public sealed class MapGenerationConfig : ScriptableObject
    {
        [Header("Layer Settings")]
        [Range(3, 12)] [SerializeField] private int totalLayerCount = 7;
        [Range(1, 5)] [SerializeField] private int startLayerRoomCount = 3;
        [Range(2, 10)] [SerializeField] private int midLayerMaxRoomCount = 5;

        [Header("Position Settings")]
        [SerializeField] private float layerVerticalSpacing = 120f;
        [SerializeField] private float maxHorizontalSpread = 400f;
        [Range(0f, 60f)] [SerializeField] private float randomHorizontalOffset = 30f;
        [Range(0f, 30f)] [SerializeField] private float randomVerticalOffset = 10f;

        [Header("Room Type Weights")]
        [Range(0f, 1f)] [SerializeField] private float battleWeight = 0.5f;
        [Range(0f, 1f)] [SerializeField] private float eventWeight = 0.2f;
        [Range(0f, 1f)] [SerializeField] private float shopWeight = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float restWeight = 0.1f;
        [Range(0f, 1f)] [SerializeField] private float treasureWeight = 0.05f;

        public int TotalLayerCount { get { return totalLayerCount; } }
        public int StartLayerRoomCount { get { return startLayerRoomCount; } }
        public int MidLayerMaxRoomCount { get { return midLayerMaxRoomCount; } }
        public float LayerVerticalSpacing { get { return layerVerticalSpacing; } }
        public float MaxHorizontalSpread { get { return maxHorizontalSpread; } }
        public float RandomHorizontalOffset { get { return randomHorizontalOffset; } }
        public float RandomVerticalOffset { get { return randomVerticalOffset; } }
        public float BattleWeight { get { return battleWeight; } }
        public float EventWeight { get { return eventWeight; } }
        public float ShopWeight { get { return shopWeight; } }
        public float RestWeight { get { return restWeight; } }
        public float TreasureWeight { get { return treasureWeight; } }

        public void Sanitize()
        {
            totalLayerCount = Mathf.Clamp(totalLayerCount, 3, 12);
            startLayerRoomCount = Mathf.Clamp(startLayerRoomCount, 1, 5);
            midLayerMaxRoomCount = Mathf.Clamp(midLayerMaxRoomCount, 2, 10);
            layerVerticalSpacing = Mathf.Max(40f, layerVerticalSpacing);
            maxHorizontalSpread = Mathf.Max(100f, maxHorizontalSpread);
            randomHorizontalOffset = Mathf.Clamp(randomHorizontalOffset, 0f, 60f);
            randomVerticalOffset = Mathf.Clamp(randomVerticalOffset, 0f, 30f);
        }
    }

    [CreateAssetMenu(fileName = "DustChronicleGeneratedMap", menuName = "Amber/Star Rail Dust Chronicle/Generated Map")]
    public sealed class GeneratedMapData : ScriptableObject
    {
        [SerializeField] private string mapName = "Generated Map";
        [SerializeField] private List<MapLayerData> layers = new List<MapLayerData>();
        [SerializeField] private int currentLayerIndex = 0;
        [SerializeField] private int currentRoomIndex = -1;

        public string MapName { get { return mapName; } }
        public IReadOnlyList<MapLayerData> Layers { get { return layers; } }
        public int CurrentLayerIndex { get { return currentLayerIndex; } }
        public int CurrentRoomIndex { get { return currentRoomIndex; } }
        public int TotalLayerCount { get { return layers.Count; } }

        public MapLayerData CurrentLayer
        {
            get
            {
                if (currentLayerIndex >= 0 && currentLayerIndex < layers.Count)
                    return layers[currentLayerIndex];
                return null;
            }
        }

        public MapRoomNode CurrentRoom
        {
            get
            {
                var layer = CurrentLayer;
                return layer != null ? layer.GetRoom(currentRoomIndex) : null;
            }
        }

        public void BuildFromLayers(List<MapLayerData> generatedLayers)
        {
            layers = generatedLayers ?? new List<MapLayerData>();
            currentLayerIndex = 0;
            currentRoomIndex = -1;
        }

        public void ResetToStart()
        {
            currentLayerIndex = 0;
            currentRoomIndex = -1;
        }

        public void MoveToRoom(int layerIndex, int roomIndex)
        {
            if (layerIndex < 0 || layerIndex >= layers.Count) return;
            var layer = layers[layerIndex];
            if (roomIndex < 0 || roomIndex >= layer.RoomCount) return;
            currentLayerIndex = layerIndex;
            currentRoomIndex = roomIndex;
        }

        public List<MapRoomNode> GetAvailableNextRooms()
        {
            var result = new List<MapRoomNode>();
            var currentRoom = CurrentRoom;
            if (currentRoom == null) return result;

            int nextLayerIndex = currentLayerIndex + 1;
            if (nextLayerIndex >= layers.Count) return result;

            var nextLayer = layers[nextLayerIndex];
            foreach (int nextRoomIndex in currentRoom.ConnectedToRoomIndices)
            {
                var room = nextLayer.GetRoom(nextRoomIndex);
                if (room != null) result.Add(room);
            }
            return result;
        }

        public bool HasReachedBoss
        {
            get { return CurrentRoom != null && CurrentRoom.IsBossRoom; }
        }

        public bool IsComplete
        {
            get { return currentLayerIndex >= layers.Count - 1; }
        }
    }
}