using UnityEngine;

/// <summary>
/// 휴머노이드 캐릭터의 시선 추적(Aim Offset LookAt)과 양손 무기 왼손 파지(Two-Bone IK)를 OnAnimatorIK로 제어하는 컴포넌트입니다.
/// Animator의 UpperBody 또는 해당 레이어에서 'IK Pass'가 켜져 있어야 동작합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class PlayerCharacterIK : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerItemHolder _playerItemHolder;
    [SerializeField] private Animator _animator;

    [Header("Aim Offset (LookAt) Settings")]
    [Tooltip("상체 및 머리가 조준점(Pitch)을 향해 자연스럽게 회전하도록 하는 LookAt 활성화 여부")]
    [SerializeField] private bool _enableLookAt = true;
    [Tooltip("전체 LookAt 가중치 (0~1)")]
    [Range(0f, 1f)] [SerializeField] private float _lookAtWeight = 1.0f;
    [Tooltip("몸통(Spine) 가중치 (0~1, 기본 0.3)")]
    [Range(0f, 1f)] [SerializeField] private float _bodyWeight = 0.3f;
    [Tooltip("머리(Head) 가중치 (0~1, 기본 0.7)")]
    [Range(0f, 1f)] [SerializeField] private float _headWeight = 0.7f;
    [Tooltip("시선 회전 한계 제한 (0: 자유, 1: 완전 제한)")]
    [Range(0f, 1f)] [SerializeField] private float _clampWeight = 0.5f;

    [Header("Left Hand Two-Bone IK Settings")]
    [Tooltip("양손 총기 파지 시 왼손을 핸드가드에 고정하는 Two-Bone IK 활성화 여부")]
    [SerializeField] private bool _enableLeftHandIK = true;
    [Tooltip("무기 장착/해제 시 왼손 IK 가중치 전환 속도")]
    [SerializeField] private float _ikTransitionSpeed = 8f;

    private float _currentLeftHandWeight = 0f;

    private void Awake()
    {
        if (_animator == null) _animator = GetComponent<Animator>();
        if (_playerController == null) _playerController = GetComponent<PlayerController>();
        if (_playerItemHolder == null) _playerItemHolder = GetComponent<PlayerItemHolder>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (_animator == null || !_animator.isHuman)
        {
            return;
        }

        HandleLookAtIK();
        HandleLeftHandIK();
    }

    /// <summary>
    /// 플레이어의 카메라 Pitch(상하 각도)를 바탕으로 언리얼 엔진의 Aim Offset처럼
    /// 상체와 머리를 부드럽게 조준 방향으로 회전시킵니다.
    /// </summary>
    private void HandleLookAtIK()
    {
        if (!_enableLookAt || _playerController == null)
        {
            _animator.SetLookAtWeight(0f);
            return;
        }

        float pitch = _playerController.CameraPitch;

        // 머리 본 또는 가슴 높이 기준
        Transform headBone = _animator.GetBoneTransform(HumanBodyBones.Head);
        Vector3 headPos = headBone != null ? headBone.position : (transform.position + Vector3.up * 1.6f);

        // 캐릭터가 바라보는 전방 수평 벡터에서 상하 Pitch 회전 적용
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Quaternion pitchRotation = Quaternion.AngleAxis(pitch, -right);
        Vector3 lookDirection = pitchRotation * forward;
        Vector3 targetLookPosition = headPos + lookDirection * 15f;

        _animator.SetLookAtWeight(_lookAtWeight, _bodyWeight, _headWeight, 0.0f, _clampWeight);
        _animator.SetLookAtPosition(targetLookPosition);
    }

    /// <summary>
    /// 현재 든 아이템이 양손 무기인 경우, 왼손을 총기 핸드가드(그립) 위치로 스냅합니다.
    /// 빈손이거나 한손 무기인 경우 가중치를 부드럽게 0으로 낮춥니다.
    /// </summary>
    private void HandleLeftHandIK()
    {
        if (!_enableLeftHandIK || _playerItemHolder == null)
        {
            _currentLeftHandWeight = 0f;
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            return;
        }

        ItemData heldItem = _playerItemHolder.CurrentHeldItemData;
        GameObject heldInstance = _playerItemHolder.ThirdPersonHeldInstance ?? _playerItemHolder.CurrentHeldInstance;

        bool shouldUseIK = heldItem != null && heldItem.UseLeftHandIK && heldInstance != null && heldInstance.activeInHierarchy;

        float targetWeight = shouldUseIK ? 1.0f : 0.0f;
        _currentLeftHandWeight = Mathf.MoveTowards(_currentLeftHandWeight, targetWeight, Time.deltaTime * _ikTransitionSpeed);

        if (_currentLeftHandWeight > 0.001f && heldInstance != null && heldItem != null)
        {
            Transform gunTransform = heldInstance.transform;
            Vector3 targetIKPos = gunTransform.TransformPoint(heldItem.LeftHandIKLocalPosition);
            Quaternion targetIKRot = gunTransform.rotation * Quaternion.Euler(heldItem.LeftHandIKLocalRotation);

            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, _currentLeftHandWeight);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, _currentLeftHandWeight);
            _animator.SetIKPosition(AvatarIKGoal.LeftHand, targetIKPos);
            _animator.SetIKRotation(AvatarIKGoal.LeftHand, targetIKRot);
        }
        else
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
        }
    }
}
