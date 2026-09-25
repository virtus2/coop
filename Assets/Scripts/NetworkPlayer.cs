using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크에 접속한 플레이어(세션) 인스턴스 역할을 담당하는 컴포넌트입니다.
/// NetworkManager에 의해 클라이언트 접속 시 SpawnAsPlayerObject로 생성되며, 씬 전환이나 캐릭터 사망 시에도 영구히 유지됩니다.
/// 플레이어 고유 식별자, 닉네임, 플레이어 귀속 영구 인벤토리(PlayerInventory)를 관리하고,
/// 현재 월드에 스폰되어 조종 중인 캐릭터(PlayerCharacter)에 대한 빙의(Possess) 및 카메라/입력을 위임합니다.
/// </summary>
[RequireComponent(typeof(PlayerInventory))]
public class NetworkPlayer : NetworkBehaviour
{
    public static NetworkPlayer LocalInstance { get; private set; }
    public static readonly Dictionary<ulong, NetworkPlayer> ConnectedPlayers = new Dictionary<ulong, NetworkPlayer>();

    [Header("Player Metadata")]
    private readonly NetworkVariable<FixedString64Bytes> _playerName = new NetworkVariable<FixedString64Bytes>(
        "Player",
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    [Header("Controlled Character Reference")]
    private readonly NetworkVariable<NetworkObjectReference> _currentCharacterRef = new NetworkVariable<NetworkObjectReference>(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private PlayerInventory _inventory;
    private PlayerCharacter _currentCharacter;

    public string PlayerName => _playerName.Value.ToString();
    public PlayerInventory Inventory => _inventory;
    public PlayerCharacter CurrentCharacter => _currentCharacter;
    public bool HasCharacter => _currentCharacter != null;

    public event Action<PlayerCharacter> OnCharacterPossessed;
    public event Action OnCharacterUnpossessed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        LocalInstance = null;
        ConnectedPlayers.Clear();
    }

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        DontDestroyOnLoad(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        ConnectedPlayers[OwnerClientId] = this;

        if (IsOwner)
        {
            LocalInstance = this;

            // 로컬 플레이어 이름 초기화 (SteamPersonaName 또는 기본값)
            string initialName = GetInitialPlayerName();
            _playerName.Value = initialName;
        }

        _currentCharacterRef.OnValueChanged += HandleCharacterRefChanged;

        // 이미 스폰된 캐릭터가 네트워크 변수에 들어있는 경우 즉시 바인딩
        UpdateCharacterReference(_currentCharacterRef.Value);
    }

    public override void OnNetworkDespawn()
    {
        ConnectedPlayers.Remove(OwnerClientId);
        _currentCharacterRef.OnValueChanged -= HandleCharacterRefChanged;

        if (IsOwner && LocalInstance == this)
        {
            LocalInstance = null;
        }
    }

    private string GetInitialPlayerName()
    {
#if !UNITY_EDITOR
        if (Steamworks.SteamManager.Initialized)
        {
            return Steamworks.SteamFriends.GetPersonaName();
        }
#endif
        return $"Player {OwnerClientId + 1}";
    }

    private void HandleCharacterRefChanged(NetworkObjectReference oldRef, NetworkObjectReference newRef)
    {
        UpdateCharacterReference(newRef);
    }

    private void UpdateCharacterReference(NetworkObjectReference charRef)
    {
        if (charRef.TryGet(out NetworkObject charNetObj))
        {
            _currentCharacter = charNetObj.GetComponent<PlayerCharacter>();
            if (_currentCharacter != null)
            {
                _currentCharacter.SetOwningPlayer(this);

                if (IsOwner)
                {
                    if (PlayerCameraController.Instance != null && _currentCharacter.CameraTarget != null)
                    {
                        PlayerCameraController.Instance.SetCharacterTarget(_currentCharacter.CameraTarget);
                    }
                }

                OnCharacterPossessed?.Invoke(_currentCharacter);
                Debug.Log($"[NetworkPlayer] 클라이언트 {OwnerClientId}가 캐릭터 '{_currentCharacter.name}'에 빙의했습니다.");
                return;
            }
        }

        // 캐릭터가 해제되었거나 디스폰된 경우
        if (_currentCharacter != null)
        {
            _currentCharacter = null;
            if (IsOwner)
            {
                if (PlayerCameraController.Instance != null)
                {
                    PlayerCameraController.Instance.SetCoreTarget();
                }
            }
            OnCharacterUnpossessed?.Invoke();
            Debug.Log($"[NetworkPlayer] 클라이언트 {OwnerClientId}의 캐릭터가 해제되었습니다 (관전/코어 시점 전환).");
        }
    }

    #region Server Authority - Possess / Unpossess

    /// <summary>
    /// [서버 전용] 스폰된 캐릭터를 해당 플레이어 세션에 빙의(Possess)시킵니다.
    /// </summary>
    public void PossessCharacter(PlayerCharacter character)
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkPlayer] PossessCharacter는 서버에서만 호출할 수 있습니다.");
            return;
        }

        if (character == null)
        {
            UnpossessCharacter();
            return;
        }

        if (character.NetworkObject.OwnerClientId != OwnerClientId)
        {
            character.NetworkObject.ChangeOwnership(OwnerClientId);
        }

        _currentCharacter = character;
        character.SetOwningPlayer(this);
        _currentCharacterRef.Value = character.NetworkObject;
    }

    /// <summary>
    /// [서버 전용] 현재 조종 중인 캐릭터를 해제(Unpossess)합니다.
    /// </summary>
    public void UnpossessCharacter()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[NetworkPlayer] UnpossessCharacter는 서버에서만 호출할 수 있습니다.");
            return;
        }

        _currentCharacter = null;
        _currentCharacterRef.Value = default;
    }

    #endregion

    public void SetInputEnabled(bool enabled)
    {
        if (_currentCharacter != null)
        {
            _currentCharacter.SetInputEnabled(enabled);
        }
    }

    public static NetworkPlayer GetPlayer(ulong clientId)
    {
        if (ConnectedPlayers.TryGetValue(clientId, out var player))
        {
            return player;
        }
        return null;
    }
}
