using UnityEngine;

namespace Coop.MapGeneration
{
    [CreateAssetMenu(fileName = "NewRoomData", menuName = "Map Generation/Room Data")]
    public class RoomPrefabData : ScriptableObject
    {
        [Tooltip("방 프리팹 (WorldGridManager의 그리드 스냅 기준에 맞춘 것)")]
        public GameObject prefab;

        [Tooltip("그리드 상의 방 가로/세로 셀 크기")]
        public Vector2Int size = new Vector2Int(5, 5);

        [Tooltip("맵 배치 시 방의 회전 허용 여부")]
        public bool allowRotation = true;
        
        [Tooltip("관리 및 구분용 방 타입")]
        public string roomType = "Room";

        [Header("Minimap Settings")]
        [Tooltip("미니맵에 표시될 방의 이름")]
        public string mapRoomName = "Room";

        [Tooltip("미니맵에 표시될 방의 배경 색상")]
        public Color mapColor = new Color(0.2f, 0.4f, 0.8f, 0.8f);
    }
}
