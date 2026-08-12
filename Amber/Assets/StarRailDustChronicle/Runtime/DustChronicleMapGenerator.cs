using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Amber.StarRailDustChronicle
{
    /// <summary>
    /// Generates a layered, branching map similar to Slay the Spire''s map system.
    /// Algorithm based on: nearest-neighbor connection + dead-end bridging.
    /// </summary>
    public static class DustChronicleMapGenerator
    {
        public static GeneratedMapData Generate(MapGenerationConfig config)
        {
            if (config == null)
            {
                Debug.LogError("MapGenerationConfig is null. Cannot generate map.");
                return null;
            }

            config.Sanitize();

            var layers = new List<MapLayerData>();
            int layerCount = config.TotalLayerCount;
            int startRoomCount = config.StartLayerRoomCount;

            // Phase 1: Create layers and rooms
            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                var layer = new MapLayerData(layerIndex);
                int roomCount = CalculateRoomCount(layerIndex, layerCount, startRoomCount, config.MidLayerMaxRoomCount);

                for (int roomIndex = 0; roomIndex < roomCount; roomIndex++)
                {
                    var nodeType = DetermineNodeType(layerIndex, layerCount, config);
                    var contentKey = GenerateContentKey(layerIndex, roomIndex, nodeType);
                    var position = CalculateRoomPosition(
                        roomIndex, roomCount, layerIndex,
                        config.MaxHorizontalSpread, config.LayerVerticalSpacing,
                        config.RandomHorizontalOffset, config.RandomVerticalOffset);

                    var room = new MapRoomNode(layerIndex, roomIndex, nodeType, contentKey, position);
                    layer.AddRoom(room);
                }

                layers.Add(layer);
            }

            // Phase 2: Generate connections (nearest-neighbor + dead-end bridging)
            GenerateConnections(layers);

            // Phase 3: Build the map data
            var mapData = ScriptableObject.CreateInstance<GeneratedMapData>();
            mapData.BuildFromLayers(layers);
            return mapData;
        }

        private static int CalculateRoomCount(int layerIndex, int totalLayers, int startRoomCount, int maxMidRooms)
        {
            if (layerIndex == 0)
            {
                // Start layer
                return startRoomCount;
            }

            if (layerIndex == totalLayers - 1)
            {
                // Boss layer: always 1 room
                return 1;
            }

            // Middle layers: random between 2 and startCount*2-1, capped by maxMidRooms
            int minRooms = 2;
            int maxRooms = Mathf.Min(startRoomCount * 2 - 1, maxMidRooms);
            if (maxRooms < minRooms) maxRooms = minRooms;
            return Random.Range(minRooms, maxRooms + 1);
        }

        private static DustChronicleRouteNodeType DetermineNodeType(
            int layerIndex, int totalLayers, MapGenerationConfig config)
        {
            // Start layer: always Battle
            if (layerIndex == 0)
                return DustChronicleRouteNodeType.Battle;

            // Last layer: always Boss
            if (layerIndex == totalLayers - 1)
                return DustChronicleRouteNodeType.Boss;

            // Middle layers: weight-based random
            float roll = Random.value;
            float battleW = config.BattleWeight;
            float eventW = battleW + config.EventWeight;
            float shopW = eventW + config.ShopWeight;
            float restW = shopW + config.RestWeight;

            if (roll < battleW) return DustChronicleRouteNodeType.Battle;
            if (roll < eventW) return DustChronicleRouteNodeType.Event;
            if (roll < shopW) return DustChronicleRouteNodeType.Shop;
            if (roll < restW) return DustChronicleRouteNodeType.Rest;
            return DustChronicleRouteNodeType.Treasure;
        }

        private static string GenerateContentKey(int layerIndex, int roomIndex, DustChronicleRouteNodeType nodeType)
        {
            string typePrefix = nodeType switch
            {
                DustChronicleRouteNodeType.Battle => "combat",
                DustChronicleRouteNodeType.EliteBattle => "elite",
                DustChronicleRouteNodeType.Boss => "boss",
                DustChronicleRouteNodeType.Shop => "shop",
                DustChronicleRouteNodeType.Event => "event",
                DustChronicleRouteNodeType.Rest => "rest",
                DustChronicleRouteNodeType.Treasure => "treasure",
                _ => "unknown"
            };
            return $"{typePrefix}/layer-{layerIndex + 1}/room-{roomIndex + 1}";
        }

        private static Vector2 CalculateRoomPosition(
            int roomIndex, int roomCount, int layerIndex,
            float maxHorizontalSpread, float layerVerticalSpacing,
            float randomHorizontalOffset, float randomVerticalOffset)
        {
            // Horizontal: spread rooms evenly across the available width
            float totalWidth = maxHorizontalSpread;
            float xStep = roomCount <= 1 ? 0f : totalWidth / (roomCount - 1);
            float xBase = roomCount <= 1 ? 0f : -totalWidth / 2f + roomIndex * xStep;
            float xRandom = Random.Range(-randomHorizontalOffset, randomHorizontalOffset);

            // Vertical: position by layer
            float yBase = layerIndex * layerVerticalSpacing;
            float yRandom = layerIndex == 0 ? 0f : Random.Range(-randomVerticalOffset, randomVerticalOffset);

            return new Vector2(xBase + xRandom, yBase + yRandom);
        }

        /// <summary>
        /// Phase 2a: For each room on layer i, find the nearest room on layer i+1 and connect.
        /// Phase 2b: Dead-end bridging - ensure every room on layer i+1 has at least one incoming connection.
        /// </summary>
        private static void GenerateConnections(List<MapLayerData> layers)
        {
            int layerCount = layers.Count;

            // Phase 2a: Nearest-neighbor upward connections
            for (int i = 0; i < layerCount - 1; i++)
            {
                var currentLayer = layers[i];
                var upperLayer = layers[i + 1];

                foreach (var room in currentLayer.Rooms)
                {
                    var nearestRoom = FindNearestRoom(room, upperLayer.Rooms);
                    if (nearestRoom != null)
                    {
                        room.AddConnectionTo(nearestRoom.RoomIndex);
                        nearestRoom.AddConnectionFrom(room.RoomIndex);
                    }
                }
            }

            // Phase 2b: Dead-end bridging
            // For each layer (starting from layer 1), check if any room has no incoming connection.
            // If so, find the nearest room on the layer below and bridge them.
            for (int i = 1; i < layerCount; i++)
            {
                var currentLayer = layers[i];
                var lowerLayer = layers[i - 1];

                foreach (var room in currentLayer.Rooms)
                {
                    if (room.ConnectedFromRoomIndices.Count == 0)
                    {
                        // This room is a dead-end from below — bridge it
                        var nearestRoom = FindNearestRoom(room, lowerLayer.Rooms);
                        if (nearestRoom != null)
                        {
                            nearestRoom.AddConnectionTo(room.RoomIndex);
                            room.AddConnectionFrom(nearestRoom.RoomIndex);
                        }
                    }
                }
            }
        }

        private static MapRoomNode FindNearestRoom(MapRoomNode source, IReadOnlyList<MapRoomNode> candidates)
        {
            MapRoomNode nearest = null;
            float nearestDistSqr = float.MaxValue;

            foreach (var candidate in candidates)
            {
                float distSqr = (source.Position - candidate.Position).sqrMagnitude;
                if (distSqr < nearestDistSqr)
                {
                    nearestDistSqr = distSqr;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>
        /// Generates a map using default settings. Useful for quick testing.
        /// </summary>
        public static GeneratedMapData GenerateDefault()
        {
            var config = ScriptableObject.CreateInstance<MapGenerationConfig>();
            return Generate(config);
        }
    }
}
