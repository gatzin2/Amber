using System;
using System.Collections.Generic;
using UnityEngine;

namespace Amber.StarRailDustChronicle
{
    public enum MapFlowState
    {
        MapViewing,
        RoomRunning,
        RoomComplete,
        RunVictory,
        RunFailure
    }

    public sealed class DustChronicleMapController : MonoBehaviour
    {
        [Header("Map Configuration")]
        [SerializeField] private MapGenerationConfig generationConfig;
        [SerializeField] private GeneratedMapData mapData;
        [SerializeField] private bool regenerateOnAwake = true;

        [Header("Sub-Controllers")]
        [SerializeField] private DustChronicleCombatController combatController;
        [SerializeField] private DustChronicleShopController shopController;
        [SerializeField] private DustChronicleMysteriousEventController eventController;

        [Header("Visualization")]
        [SerializeField] private DustChronicleMapVisualizer mapVisualizer;
        [SerializeField] private TextMesh statusBoard;
        [SerializeField] private bool showGui = true;

        [Header("Readonly Runtime State")]
        [SerializeField] private MapFlowState flowState = MapFlowState.MapViewing;
        [SerializeField] private List<string> completedRoomIds = new List<string>();
        [SerializeField] private string activeRoomId;

        public MapFlowState FlowState => flowState;
        public GeneratedMapData MapData => mapData;
        public IReadOnlyList<string> CompletedRoomIds => completedRoomIds;

        private void Awake()
        {
            ValidateReferences();
            
            if (generationConfig != null && (regenerateOnAwake || mapData == null))
            {
                RegenerateMap();
            }
        }

        private void Start()
        {

            if (mapVisualizer != null)
            {
                mapVisualizer.Initialize(this);
            }

            DeactivateAllSubControllers();
            flowState = MapFlowState.MapViewing;
        }

        private void Update()
        {
            if (flowState == MapFlowState.RoomRunning)
            {
                PollActiveRoom();
            }
        }

        private void ValidateReferences()
        {
            if (combatController == null)
                combatController = GetComponent<DustChronicleCombatController>();
            if (shopController == null)
                shopController = GetComponent<DustChronicleShopController>();
            if (eventController == null)
                eventController = GetComponent<DustChronicleMysteriousEventController>();
            if (eventController == null)
                eventController = GetComponentInChildren<DustChronicleMysteriousEventController>(true);
            if (mapVisualizer == null)
                mapVisualizer = GetComponent<DustChronicleMapVisualizer>();
        }

        public void RegenerateMap()
        {
            if (generationConfig == null)
            {
                Debug.LogWarning("generationConfig is null, using default settings.");
                generationConfig = ScriptableObject.CreateInstance<MapGenerationConfig>();
            }

            if (mapData != null && Application.isPlaying)
            {
                Destroy(mapData);
            }

            mapData = DustChronicleMapGenerator.Generate(generationConfig);
            completedRoomIds.Clear();
            activeRoomId = null;
            flowState = MapFlowState.MapViewing;

            if (mapData != null)
            {
                mapData.ResetToStart();
            }

            RefreshStatusBoard();
        }

        public List<MapRoomNode> GetAvailableNextRooms()
        {
            if (mapData == null) return new List<MapRoomNode>();

            if (mapData.CurrentRoom == null)
            {
                // Run not started: all layer-0 rooms are available
                var result = new List<MapRoomNode>();
                if (mapData.Layers.Count > 0)
                {
                    foreach (var room in mapData.Layers[0].Rooms)
                        result.Add(room);
                }
                return result;
            }

            return mapData.GetAvailableNextRooms();
        }

        public bool CanMoveToRoom(MapRoomNode targetRoom)
        {
            if (mapData == null || targetRoom == null) return false;

            // First selection: any layer-0 room
            if (mapData.CurrentRoom == null && targetRoom.LayerIndex == 0)
                return true;

            var available = mapData.GetAvailableNextRooms();
            return available.Exists(r => r.RoomId == targetRoom.RoomId);
        }

        public void SelectRoom(MapRoomNode targetRoom)
        {
            if (targetRoom == null) return;
            if (mapData == null) return;

            bool isFirstSelection = mapData.CurrentRoom == null && targetRoom.LayerIndex == 0;
            bool isNextRoom = CanMoveToRoom(targetRoom);
            if (!isFirstSelection && !isNextRoom) return;

            // Complete current room if one is active
            var current = mapData.CurrentRoom;
            if (current != null)
            {
                completedRoomIds.Add(current.RoomId);
            }

            // Move to target
            mapData.MoveToRoom(targetRoom.LayerIndex, targetRoom.RoomIndex);
            activeRoomId = targetRoom.RoomId;

            // Activate the room
            ActivateRoom(targetRoom);
        }

        public void ContinueAfterRoom()
        {
            if (flowState != MapFlowState.RoomComplete) return;

            DeactivateAllSubControllers();

            if (mapData == null) return;

            if (mapData.HasReachedBoss || mapData.IsComplete)
            {
                flowState = MapFlowState.RunVictory;
            }
            else
            {
                flowState = MapFlowState.MapViewing;
            }

            RefreshStatusBoard();
        }

        private void ActivateRoom(MapRoomNode room)
        {
            DeactivateAllSubControllers();

            switch (room.NodeType)
            {
                case DustChronicleRouteNodeType.Battle:
                case DustChronicleRouteNodeType.EliteBattle:
                case DustChronicleRouteNodeType.Boss:
                    ActivateCombatRoom(room);
                    break;

                case DustChronicleRouteNodeType.Shop:
                    ActivateShopRoom(room);
                    break;

                case DustChronicleRouteNodeType.Event:
                    ActivateEventRoom(room);
                    break;

                case DustChronicleRouteNodeType.Rest:
                case DustChronicleRouteNodeType.Treasure:
                    CompleteRoom();
                    break;

                default:
                    CompleteRoom();
                    break;
            }
        }

        private void ActivateCombatRoom(MapRoomNode room)
        {
            if (combatController == null) { CompleteRoom(); return; }

            // Restore the battle units that were hidden when the previous room was
            // left. Elite enemies stay inactive - they are not part of map battles.
            var allUnitsIncludingInactive = GetComponentsInChildren<DustChronicleUnit>(true);
            foreach (var unit in allUnitsIncludingInactive)
            {
                if (unit == null) continue;
                if (unit.name.Contains("精英")) continue;
                unit.gameObject.SetActive(true);
            }

            // Suppress auto-rebuild to avoid pulling inactive/ghost units into battle
            combatController.SetSuppressAutoRebuild(true);

            // Manually collect only active player and enemy units
            var players = new List<DustChronicleUnit>();
            var enemies = new List<DustChronicleUnit>();
            var allUnits = GetComponentsInChildren<DustChronicleUnit>(false);

            foreach (var unit in allUnits)
            {
                if (unit == null || !unit.gameObject.activeInHierarchy) continue;
                if (unit.Team == DustTeam.Player)
                    players.Add(unit);
                else
                    enemies.Add(unit);
            }

            if (players.Count == 0 || enemies.Count == 0)
            {
                Debug.LogError("[DustChronicleMapController] ActivateCombatRoom found no active units (players=" + players.Count + ", enemies=" + enemies.Count + "); skipping battle and completing room.");
                CompleteRoom();
                return;
            }
            combatController.Configure(players, enemies, statusBoard);
            combatController.StartBattle();
            flowState = MapFlowState.RoomRunning;
            RefreshStatusBoard();
        }

        private void ActivateShopRoom(MapRoomNode room)
        {
            if (shopController == null)
            {
                shopController = GetComponent<DustChronicleShopController>();
            }

            if (shopController == null)
            {
                shopController = gameObject.AddComponent<DustChronicleShopController>();
            }

            shopController.ResetShop();
            shopController.SetGuiVisible(true);
            shopController.SetMerchantVisible(true);
            shopController.gameObject.SetActive(true);
            flowState = MapFlowState.RoomRunning;
            RefreshStatusBoard();
        }

        private void ActivateEventRoom(MapRoomNode room)
        {
            if (eventController == null)
            {
                eventController = GetComponent<DustChronicleMysteriousEventController>();
            }

            if (eventController == null)
            {
                eventController = gameObject.AddComponent<DustChronicleMysteriousEventController>();
            }

            eventController.ResetAndStart();
            eventController.SetGuiVisible(true);
            eventController.gameObject.SetActive(true);
            flowState = MapFlowState.RoomRunning;
            RefreshStatusBoard();
        }

        private void PollActiveRoom()
        {
            if (mapData == null || mapData.CurrentRoom == null) return;

            var room = mapData.CurrentRoom;
            bool isComplete = false;

            switch (room.NodeType)
            {
                case DustChronicleRouteNodeType.Battle:
                case DustChronicleRouteNodeType.EliteBattle:
                case DustChronicleRouteNodeType.Boss:
                    if (combatController != null && combatController.IsBattleFinished)
                        isComplete = true;
                    break;

                case DustChronicleRouteNodeType.Event:
                    if (eventController != null && eventController.IsEventComplete)
                        isComplete = true;
                    break;

                case DustChronicleRouteNodeType.Shop:
                    if (shopController != null && shopController.IsShopComplete)
                        isComplete = true;
                    break;
            }

            if (isComplete)
                CompleteRoom();
        }

        public void CompleteRoom()
        {
            flowState = MapFlowState.RoomComplete;
            RefreshStatusBoard();
        }

        public void FailRun(string reason)
        {
            flowState = MapFlowState.RunFailure;
            DeactivateAllSubControllers();
            RefreshStatusBoard();
        }

        private void DeactivateAllSubControllers()
        {
            if (combatController != null)
                combatController.ConfigureFlowMode(false, false);

            if (shopController != null)
            {
                shopController.SetGuiVisible(false);
                shopController.SetMerchantVisible(false);
            }

            if (eventController != null)
            {
                eventController.SetGuiVisible(false);
                eventController.DeactivateScene();
            }

            HideBattleUnits();
        }

        private void HideBattleUnits()
        {
            var units = GetComponentsInChildren<DustChronicleUnit>(true);
            foreach (var unit in units)
            {
                if (unit != null)
                {
                    unit.gameObject.SetActive(false);
                }
            }
        }

        private void RefreshStatusBoard()
        {
            if (statusBoard == null) return;

            var room = mapData != null ? mapData.CurrentRoom : null;
            string roomInfo = room != null
                ? "Current: " + room.NodeType + " @ Layer " + (room.LayerIndex + 1) + " Room " + (room.RoomIndex + 1)
                : "No room selected - pick a start room";

            statusBoard.text = "State: " + flowState + "\n" + roomInfo + "\nCompleted: " + completedRoomIds.Count + " rooms";
        }

        private void OnGUI()
        {
            if (!showGui) return;
            DrawFlowGui();
        }

        private void DrawFlowGui()
        {
            float panelX = 16f;
            float panelY = Screen.height - 160f;
            float panelW = 320f;
            float panelH = 140f;

            GUI.Box(new Rect(panelX, panelY, panelW, panelH), "Map Control");

            float y = panelY + 24f;
            GUI.Label(new Rect(panelX + 10, y, panelW - 20, 20), "State: " + flowState);

            y += 22f;
            if (mapData != null && mapData.CurrentRoom != null)
            {
                var room = mapData.CurrentRoom;
                GUI.Label(new Rect(panelX + 10, y, panelW - 20, 20),
                    "Room: L" + (room.LayerIndex + 1) + " R" + (room.RoomIndex + 1) + " [" + room.NodeType + "]");
            }
            else
            {
                GUI.Label(new Rect(panelX + 10, y, panelW - 20, 20), "Click a green start room to begin");
            }

            y += 22f;
            GUI.Label(new Rect(panelX + 10, y, panelW - 20, 20),
                "Completed: " + completedRoomIds.Count + " | Layers: " + (mapData != null ? mapData.TotalLayerCount : 0));

            y += 28f;
            if (flowState == MapFlowState.RoomComplete)
            {
                if (GUI.Button(new Rect(panelX + 10, y, 140, 30), "Continue"))
                {
                    ContinueAfterRoom();
                }
            }

            if (flowState == MapFlowState.MapViewing || flowState == MapFlowState.RoomComplete)
            {
                if (GUI.Button(new Rect(panelX + 160, y, 140, 30), "Regenerate Map"))
                {
                    RegenerateMap();
                }
            }
        }
    }
}
