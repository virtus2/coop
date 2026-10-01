using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using Coop.MapGeneration;


namespace CoopGame.MapGeneration
{
    public class MapGenerator : MonoBehaviour
    {
        public static MapGenerator Instance;

        [Header("Map Settings")]
        public int mapSeed = 0;
        public int gridSizeX = 10;
        public int gridSizeY = 10;
        public float cellSize = 10f; // 방 프리팹 하나의 실제 크기 (예: 10x10 미터)

        public Vector2Int mapSize => new Vector2Int(gridSizeX, gridSizeY);
        public event System.Action<List<PlacedRoomInfo>> OnMapGenerated;
        
        public struct PlacedRoomInfo
        {
            public RoomPrefabData roomData;
            public Vector2Int originCoord;
            public int rotationAngle;
        }

        [Header("Prefabs")]
        public GameObject coreChamberPrefab;
        public List<GameObject> optionalRoomPrefabs;

        [Header("Navigation")]
        public NavMeshSurface navMeshSurface;

        // 맵 그리드 점유 상태 관리
        private bool[,] _gridOccupied;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void GenerateMap(int seed)
        {
            mapSeed = seed;
            Random.InitState(mapSeed);

            _gridOccupied = new bool[gridSizeX, gridSizeY];

            // 1. 중앙 코어 챔버 배치
            int centerX = gridSizeX / 2;
            int centerY = gridSizeY / 2;
            PlaceRoom(coreChamberPrefab, centerX, centerY, "CoreChamber");

            // 2. 선택 방 무작위 배치 (예시로 5개 배치)
            int roomsToPlace = 5;
            for (int i = 0; i < roomsToPlace; i++)
            {
                int randomX = Random.Range(0, gridSizeX);
                int randomY = Random.Range(0, gridSizeY);

                // 빈 공간을 찾을 때까지 반복 (간단한 구현)
                int safety = 0;
                while (_gridOccupied[randomX, randomY] && safety < 100)
                {
                    randomX = Random.Range(0, gridSizeX);
                    randomY = Random.Range(0, gridSizeY);
                    safety++;
                }

                if (!_gridOccupied[randomX, randomY])
                {
                    GameObject randomPrefab = optionalRoomPrefabs[Random.Range(0, optionalRoomPrefabs.Count)];
                    PlaceRoom(randomPrefab, randomX, randomY, $"OptionalRoom_{i}");
                }
            }

            // 3. 네비메시 굽기 (몬스터 AI용)
            Debug.Log("[MapGenerator] 맵 생성 완료. 네비메시를 굽습니다...");
            if (navMeshSurface != null)
            {
                navMeshSurface.BuildNavMesh();
            }
            
            // 4. (중요) 맵 생성이 모두 끝난 후, 세이브 매니저에게 파괴된 프롭들을 지우라고 알림
            // SaveLoadManager.Instance.ApplyMapDelta(); 
        }

        private void PlaceRoom(GameObject prefab, int gridX, int gridY, string roomName)
        {
            Vector3 worldPos = new Vector3(gridX * cellSize, 0, gridY * cellSize);
            GameObject roomInstance = Instantiate(prefab, worldPos, Quaternion.identity, this.transform);
            roomInstance.name = roomName;
            _gridOccupied[gridX, gridY] = true;

            // 방 안의 모든 프롭들에게 고유 ID 발급 (세이브/로드 연동용)
            AssignPropIds(roomInstance, gridX, gridY);
        }

        private void AssignPropIds(GameObject room, int gridX, int gridY)
        {
            // 방 하위에 있는 모든 파괴 가능한 프롭들을 찾습니다.
            var props = room.GetComponentsInChildren<DestructibleProp>();
            for (int i = 0; i < props.Length; i++)
            {
                // "GridX_GridY_순번" 형태로 절대 겹치지 않는 ID 발급
                props[i].SaveId = $"Room_{gridX}_{gridY}_Prop_{i}";
            }
        }
    }
}
