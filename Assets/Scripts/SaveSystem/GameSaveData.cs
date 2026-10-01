using System;
using System.Collections.Generic;
using UnityEngine;

namespace CoopGame.SaveSystem
{
    /// <summary>
    /// 세이브 파일에 기록되는 최상위 데이터 클래스
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        // 1. 메타데이터
        public string version = "1.0.0";
        public string timestamp;
        public float playTime;
        public string slotName; // "SaveSlot_1", "MyRoom" 등

        // 2. 월드 및 게임 진행 상태
        public int currentWave;
        public int inGameDays = 1;
        public float coreHealth;
        public List<string> defeatedBosses = new List<string>();
        public List<string> unlockedSectors = new List<string>();

        // 3. 맵 및 오브젝트 상태 (Delta State)
        public int mapSeed;
        public List<string> destroyedProps = new List<string>(); // 파괴된 프롭의 고유 Hash ID
        public List<BuiltStructureData> builtStructures = new List<BuiltStructureData>();
        public List<DroppedItemData> droppedItems = new List<DroppedItemData>();
        public List<string> openedDoors = new List<string>();

        // 4. 플레이어 데이터 (Dictionary 대신 직렬화 가능한 List 사용)
        public List<PlayerSaveData> players = new List<PlayerSaveData>();
    }

    [Serializable]
    public class PlayerSaveData
    {
        public string playerId;         // SteamID (오프라인/에디터의 경우 GUID)
        public Vector3 position;
        public Vector3 rotation;
        public float cameraPitch;
        public float health;
        public bool isDead;             // 로드 시 true면 관전 모드로 시작
        public List<InventorySlotData> inventory = new List<InventorySlotData>();
    }

    [Serializable]
    public class InventorySlotData
    {
        public int slotIndex;
        public string itemId;
        public int amount;
        public float currentDurability;
    }

    [Serializable]
    public class BuiltStructureData
    {
        public string structureId;      // 바리케이드/터렛 종류 (Prefab ID)
        public Vector3 position;
        public Vector3 rotation;
        public float health;
        public int ammo;
    }

    [Serializable]
    public class DroppedItemData
    {
        public string itemId;
        public Vector3 position;
        public Vector3 rotation;
        public int amount;
    }
}
