using UnityEngine;
using Unity.Netcode;

/// <summary>
/// GameScene을 MainScene(로비)을 거치지 않고 에디터에서 바로 재생할 때,
/// 로컬 플레이어가 없으면 자동으로 PlayerPrefab을 스폰해주는 테스트 보조 컴포넌트입니다.
/// 멀티플레이 환경(NetworkManager 활성화 시)에서는 스폰하지 않고 호스트/클라이언트 스폰에 양보합니다.
/// </summary>
public class GameSceneBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject _playerPrefab;

    private void Start()
    {
        // NetworkManager가 실행 중이지 않은 오프라인 단독 플레이 테스트인 경우
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            if (PlayerController.LocalInstance != null)
            {
                return;
            }

            if (_playerPrefab == null)
            {
                _playerPrefab = Resources.Load<GameObject>("PlayerDummyPrefab");
                if (_playerPrefab == null) _playerPrefab = Resources.Load<GameObject>("PlayerCharacterPrefab");
                if (_playerPrefab == null) _playerPrefab = Resources.Load<GameObject>("PlayerPrefab");
#if UNITY_EDITOR
                if (_playerPrefab == null)
                {
                    _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerDummyPrefab.prefab");
                    if (_playerPrefab == null)
                    {
                        _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerCharacterPrefab.prefab");
                    }
                    if (_playerPrefab == null)
                    {
                        _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PlayerPrefab.prefab");
                    }
                }
#endif
            }

            if (_playerPrefab != null)
            {
                var player = Instantiate(_playerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity);
                player.name = "Player (Direct Play)";
                Debug.Log("[GameSceneBootstrap] 에디터 단독 실행 환경에서 테스트용 플레이어를 생성했습니다.");
            }
            else
            {
                Debug.LogWarning("[GameSceneBootstrap] PlayerPrefab을 찾을 수 없습니다.");
            }
        }
    }
}
