using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CoopGame.MapGeneration;
using UnityEngine.InputSystem;

namespace Coop.UI
{
    public class MinimapUIManager : MonoBehaviour
    {
        [Header("References")]
        public MapGenerator mapGenerator;
        public RectTransform minimapWindow; // M키 누를때 켜고 꺼질 전체 미니맵 창
        public RectTransform mapContentContainer; // 실제 방 UI들이 배치되고, 줌/드래그 되는 부모 패널

        [Header("UI Prefabs (Optional)")]
        [Tooltip("방을 표시할 UI 프리팹 (없으면 코드로 동적 생성)")]
        public GameObject roomUIPrefab;
        [Tooltip("플레이어 마커를 표시할 UI 프리팹 (없으면 코드로 동적 생성)")]
        public GameObject playerMarkerPrefab;

        [Header("Minimap Settings")]
        [Tooltip("월드 좌표 1유닛(그리드 1칸) 당 미니맵 픽셀 수")]
        public float pixelsPerCell = 10f;
        public float zoomSpeed = 0.1f;
        public float minZoom = 0.5f;
        public float maxZoom = 3f;

        private bool _isMinimapOpen = false;
        private Vector2 _dragStartMousePos;
        private Vector2 _dragStartContentPos;
        private bool _isDragging = false;

        private List<Transform> _playerTransforms = new List<Transform>();
        private List<RectTransform> _playerMarkers = new List<RectTransform>();

        private void OnEnable()
        {
            if (mapGenerator != null)
                mapGenerator.OnMapGenerated += BuildMinimap;
        }

        private void OnDisable()
        {
            if (mapGenerator != null)
                mapGenerator.OnMapGenerated -= BuildMinimap;
        }

        private void Start()
        {
            if (minimapWindow != null)
                minimapWindow.gameObject.SetActive(false);
        }

        private void Update()
        {
            HandleInput();

            if (_isMinimapOpen)
            {
                HandleZoomAndPan();
                UpdatePlayerMarkers();
            }
        }

        private void HandleInput()
        {
            // 키보드 M 키로 토글 (New Input System)
            if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame)
            {
                ToggleMinimap();
            }
        }

        private void ToggleMinimap()
        {
            _isMinimapOpen = !_isMinimapOpen;
            if (minimapWindow != null)
            {
                minimapWindow.gameObject.SetActive(_isMinimapOpen);
            }
            
            if (_isMinimapOpen)
            {
                RefreshPlayerList();
            }
        }

        private void HandleZoomAndPan()
        {
            if (Mouse.current == null || mapContentContainer == null) return;

            // Zoom (마우스 휠)
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                float zoomDelta = (scroll > 0) ? zoomSpeed : -zoomSpeed;
                Vector3 currentScale = mapContentContainer.localScale;
                float newScale = Mathf.Clamp(currentScale.x + zoomDelta, minZoom, maxZoom);
                mapContentContainer.localScale = new Vector3(newScale, newScale, 1f);
            }

            // Pan (마우스 좌클릭 또는 휠클릭 드래그)
            if (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame)
            {
                _isDragging = true;
                _dragStartMousePos = Mouse.current.position.ReadValue();
                _dragStartContentPos = mapContentContainer.anchoredPosition;
            }
            else if (Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.middleButton.wasReleasedThisFrame)
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Vector2 currentMousePos = Mouse.current.position.ReadValue();
                Vector2 delta = currentMousePos - _dragStartMousePos;
                mapContentContainer.anchoredPosition = _dragStartContentPos + delta;
            }
        }

        private void BuildMinimap(List<MapGenerator.PlacedRoomInfo> placedRooms)
        {
            // 기존 방 UI 클리어
            foreach (Transform child in mapContentContainer)
            {
                Destroy(child.gameObject);
            }
            _playerMarkers.Clear();

            // 맵 전체 크기 기반으로 중심점 정렬을 위해 오프셋 계산
            float offsetX = mapGenerator.mapSize.x * pixelsPerCell * 0.5f;
            float offsetY = mapGenerator.mapSize.y * pixelsPerCell * 0.5f;

            foreach (var info in placedRooms)
            {
                Vector2Int effectiveSize = (info.rotationAngle == 90 || info.rotationAngle == 270) 
                                        ? new Vector2Int(info.roomData.size.y, info.roomData.size.x) 
                                        : info.roomData.size;

                GameObject roomUI;
                if (roomUIPrefab != null)
                {
                    roomUI = Instantiate(roomUIPrefab, mapContentContainer);
                }
                else
                {
                    // 코드로 기본 방 UI 생성
                    roomUI = new GameObject($"Room_{info.roomData.mapRoomName}");
                    roomUI.transform.SetParent(mapContentContainer, false);
                    var img = roomUI.AddComponent<Image>();
                    img.color = info.roomData.mapColor;
                    
                    // 텍스트 생성
                    GameObject textObj = new GameObject("RoomName");
                    textObj.transform.SetParent(roomUI.transform, false);
                    var txt = textObj.AddComponent<TextMeshProUGUI>();
                    txt.text = info.roomData.mapRoomName;
                    txt.color = Color.white;
                    txt.fontSize = 14;
                    txt.alignment = TextAlignmentOptions.Center;
                    
                    RectTransform textRt = txt.rectTransform;
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.sizeDelta = Vector2.zero;
                }

                RectTransform rt = roomUI.GetComponent<RectTransform>();
                // 왼쪽 아래(0,0) 기준 앵커 셋업
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0, 0); // 방도 좌하단 피벗으로 UI 배치

                rt.sizeDelta = new Vector2(effectiveSize.x * pixelsPerCell, effectiveSize.y * pixelsPerCell);
                
                // 중심점을 (0,0)으로 만들기 위해 맵 절반 크기만큼 빼줌
                float uiX = (info.originCoord.x * pixelsPerCell) - offsetX;
                float uiY = (info.originCoord.y * pixelsPerCell) - offsetY;
                rt.anchoredPosition = new Vector2(uiX, uiY);
            }
        }

        private void RefreshPlayerList()
        {
            _playerTransforms.Clear();
            
            // Player 태그를 가진 모든 오브젝트를 찾음 (자신 + 아군)
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                _playerTransforms.Add(p.transform);
            }

            // 마커 갯수 맞추기
            while (_playerMarkers.Count < _playerTransforms.Count)
            {
                GameObject marker;
                if (playerMarkerPrefab != null)
                {
                    marker = Instantiate(playerMarkerPrefab, mapContentContainer);
                }
                else
                {
                    marker = new GameObject("PlayerMarker");
                    marker.transform.SetParent(mapContentContainer, false);
                    var img = marker.AddComponent<Image>();
                    img.color = Color.green; // 아군 및 자신 마커 색상
                    
                    RectTransform rt = marker.GetComponent<RectTransform>();
                    rt.sizeDelta = new Vector2(8, 8); // 마커 크기
                }
                
                // 마커들은 UI 계층 가장 위에 보이도록 설정
                marker.transform.SetAsLastSibling();
                _playerMarkers.Add(marker.GetComponent<RectTransform>());
            }
        }

        private void UpdatePlayerMarkers()
        {
            if (mapGenerator == null) return;
            
            // WorldGridManager가 있다면 셀 사이즈와 기준점 활용
            float cellSize = 1.0f;
            Vector3 gridOrigin = Vector3.zero;
            
            if (WorldGridManager.Instance != null)
            {
                cellSize = WorldGridManager.Instance.CellSize;
                gridOrigin = WorldGridManager.Instance.GridOrigin;
            }

            float offsetX = mapGenerator.mapSize.x * pixelsPerCell * 0.5f;
            float offsetY = mapGenerator.mapSize.y * pixelsPerCell * 0.5f;

            for (int i = 0; i < _playerTransforms.Count; i++)
            {
                if (_playerTransforms[i] == null) continue;

                Vector3 worldPos = _playerTransforms[i].position;
                
                // 월드 좌표를 미니맵 UI 픽셀 좌표로 변환
                float gridX = (worldPos.x - gridOrigin.x) / cellSize;
                float gridZ = (worldPos.z - gridOrigin.z) / cellSize;

                float uiX = (gridX * pixelsPerCell) - offsetX;
                float uiY = (gridZ * pixelsPerCell) - offsetY;

                if (i < _playerMarkers.Count && _playerMarkers[i] != null)
                {
                    _playerMarkers[i].anchoredPosition = new Vector2(uiX, uiY);
                }
            }
        }
    }
}
