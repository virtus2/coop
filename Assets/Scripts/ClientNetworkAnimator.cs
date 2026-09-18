using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// 소유 클라이언트 권한(Client-Authoritative)으로 애니메이션 상태와 파라미터를 동기화하는 컴포넌트입니다.
/// Netcode for GameObjects의 NetworkAnimator 기본 동작(Server-Authoritative)을 오버라이드하여
/// 로컬 플레이어(소유자)가 애니메이터 상태를 주도적으로 브로드캐스트할 수 있도록 합니다.
/// </summary>
[DisallowMultipleComponent]
public class ClientNetworkAnimator : NetworkAnimator
{
    /// <summary>
    /// 애니메이터의 권한 주체를 결정합니다.
    /// false를 반환하여 소유 클라이언트(Owner)가 애니메이션 상태를 전송할 수 있도록 설정합니다.
    /// </summary>
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
