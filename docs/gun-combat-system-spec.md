# 총기 사격 및 히트스캔 전투 시스템 명세서 (Gun Combat & Hitscan System Spec)

본 문서는 프로젝트의 **총기 발사(일반 총기 및 산탄총), 히트스캔 데미지 판정, 탄약/재장전(탄창 교체 및 쉘 바이 쉘), 적 경직 메커니즘, 서버 권한 동기화** 전반의 기능 명세와 예외 케이스 처리 규칙을 정리한 기술/기획 통합 명세서입니다.

---

## 1. 시스템 개요

- **네트워크 아키텍처:** **Server-Authoritative (완전 서버 권한 판정)**
  - 레이캐스트 충돌 판정, 데미지 적용, 탄약 확정, 적 경직 계산은 모두 **서버**에서 실행합니다.
  - 조작 반응성(0ms 체감)을 위해 **사격음, 총구 화염, 화면 반동, 클라이언트 탄약 차감 예측**은 로컬 클라이언트에서 즉각 선반영(Client Prediction)됩니다.
  - 샷건의 8발 다중 펠릿 레이캐스트 역시 대역폭 최적화를 위해 **1회의 ServerRpc**에 통합되어 서버에서 연산됩니다.
- **주요 구성 요소:**
  - `GunItemData`: 무기 스탯(데미지, 사격 주기, 탄창, 반동, 발사 모드, 샷건 산탄/감쇄, 쉘 바이 쉘 장전 설정 등)을 정의하는 ScriptableObject.
  - `PlayerGunCombat`: 사격 입력, 차징, 연사, 쉘 바이 쉘 장전, 반동 제어 및 ServerRpc 통신.
  - `InventorySlot` 런타임 데이터 확장: 무기 인스턴스별 잔탄 보존.
  - `IDamageable`: 몬스터 및 파괴 가능 오브젝트 피격 인터페이스.
  - `Hitbox` / `HeadHitbox`: 부위별(헤드샷 1.5배) 피격 판정 컴포넌트.
  - `AmmoHUD`: 현재 탄창 잔탄 및 인벤토리 예비 탄약 표시 UI.

---

## 2. 기능 상세 명세

### 1) 발사 모드 (Fire Modes)
총기 데이터에 따라 3가지 발사 모드를 지원합니다.
1. **단발 (Semi-Auto):**
   - 마우스 좌클릭 1회당 1발 발사. (권총, 샷건 등)
   - 마우스를 누르고 있어도 연속 발사되지 않으며, 손을 뗐다가 다시 클릭해야 재발사.
   - 정해진 사격 주기(Fire Rate Cooldown) 동안은 재클릭해도 발사 불가.
2. **연사 (Full-Auto):**
   - 마우스 좌클릭을 누르고 있는 동안 사격 주기 간격으로 연속 발사. (돌격소총 등)
   - 탄창이 0발이 되거나 클릭을 해제하면 발사 중단.
3. **차지 발사 (Charge Shot):**
   - 좌클릭을 홀드하여 차징 게이지를 모으고, **버튼을 떼는 순간(Release) 발사**. (레이저 건 등)
   - **완전 충전(Full Charge):** 최소 차징 시간(예: 1.0초) 이상 충전 후 해제 시 강화탄 발사 (기본 2.0배 데미지).
   - **부분 충전/미달(Partial/Under Charge):** 최소 차징 시간 전에 해제 시 일반 약한 탄환 발사 (기본 1.0배 데미지).
   - **소모 탄약:** 완전 충전과 미달 발사 모두 동일하게 **1발** 소모.
   - **차징 취소:** 차징 도중 **마우스 우클릭**을 누르면 차징이 즉시 취소되며 탄약 소모 없음.

---

### 2) 탄약 및 재장전 (Ammo & Reload)

#### A. 인벤토리 탄약 연동
- 탄약은 인벤토리 슬롯에 보관되는 별도 아이템입니다.
  - 소총/권총: `Ammo_Rifle`, `Ammo_Pistol`
  - 샷건(산탄총): **`Ammo_Shotgun`** (슬롯당 최대 24발 스택, F1 디버그 시 24발 지급)
- 총기 데이터(`GunItemData`)에 해당 총기가 사용하는 `RequiredAmmoItemId`를 지정합니다.

#### B. 재장전 방식 (2가지 모드)
총기 데이터(`_useShellByShellReload`)에 따라 재장전 방식이 결정됩니다.

1. **표준 탄창 통교체 방식 (Magazine Reload):**
   - 소총, 권총 등에서 사용.
   - `R` 키 입력 시 `ReloadDuration` 초 후 탄창이 한 번에 채워집니다.
   - 필요한 탄약 수만큼 인벤토리에서 차감되며, 인벤토리 탄약이 부족하면 잔여 수량만큼만 부분 충전.

2. **쉘 바이 쉘 1발씩 장전 방식 (Shell-by-Shell Reload - 샷건 전용):**
   - 펌프액션 샷건 등에서 사용하며, 3단계 상태 머신으로 동작:
     - **Step 1. [장전 시작 선딜레이 (`ReloadStartDelay`)]**: 약실 개방 모션 (기본 0.35초) 및 `ReloadStartSound` 재생.
     - **Step 2. [1발 삽입 루프 (`ReloadInsertInterval`)]**: 쉘을 1발씩 밀어 넣는 주기 (기본 0.5초).
       - 주기 완료 시마다 `인벤토리 탄약 -1`, `총기 탄창 +1` 즉시 갱신 및 `ReloadInsertSound` 재생.
       - 탄창이 가득 차거나 인벤토리 탄약이 0개가 될 때까지 자동 반복.
     - **Step 3. [장전 종료 후딜레이 (`ReloadEndDelay`)]**: 장전 끝마침 모션 (기본 0.3초) 및 `ReloadEndSound` 재생 후 사격 가능 복귀.
   - **즉시 사격 캔슬 (인터럽트) 규칙:**
     - **0발(완전 빈 총) 상태에서 첫 1발 들어가기 전 좌클릭:** 장전 즉시 캔슬 + 빈총 소리(`Dry Fire`).
     - **1발 이상 들어간 상태에서 장전 도중 좌클릭:** **딜레이 0초로 즉시 장전을 끊고 바로 탕! 격발.**
     - **달리기(`Shift`), 무기 스왑, 버리기(`G`):** 현재까지 들어간 탄약 수는 그대로 보존되고 장전 루프만 중단.
     - **인벤토리 탄약 부족 시:** 남은 탄약만큼만 넣고 자동으로 장전 종료 단계로 전환.

---

### 3) 히트스캔 및 데미지 판정 (Hitscan & Damage)

#### A. 일반 총기 (단일 탄환)
- 탄 퍼짐 0%, 크로스헤어 정중앙으로 1개의 레이캐스트 방출.
- 최대 사거리(100m) 내 거리 감쇄 없음.

#### B. 샷건 (다탄자 산탄 - 8발 펠릿)
1. **산탄 및 확산 (Spread):**
   - 1회 격발 시 **8발의 펠릿(Pellet)** 방출.
   - 카메라 뷰포트 원점 기준 **7도 랜덤 콘(Random Cone)** 형태로 균등 분산.
   - 이동/점프/달리기 무관하게 동일한 7도 확산 각도 유지 (이동 페널티 없음).
2. **사거리별 데미지 감쇄 (Falloff):**
   - **`0m ~ 3m` (초근접):** 100% 풀 데미지 구간.
   - **`3m ~ 10m` (유효 사거리):** 거리에 비례한 선형 감쇄 (Linear Falloff).
   - **`10m ~ 15m` (원거리):** **펠릿당 최소 데미지 1** 보장.
   - **`15m 초과 (MaxRange)`:** 완전 빗맞음 / 소멸 (적중 안 됨).
3. **데미지 계산:**
   - `BaseDamage`는 8발 전탄 명중 시의 총 데미지.
   - 펠릿 1발당 기본 데미지 = `Mathf.FloorToInt(BaseDamage / 8)` (내림 계산).
4. **부위별 분할 묶음 피격 호출 (다탄 적중 최적화):**
   - 1마리의 대상에게 여러 발이 적중할 경우, 피격 사운드 중첩 폭음 및 성능 저하를 방지하기 위해 부위별로 묶어 최대 2회 `TakeDamage` 호출:
     - `TakeDamage(헤드 적중분 합산 데미지, HitboxType.Head)` (헤드 맞은 탄이 있을 때 1회)
     - `TakeDamage(몸통 적중분 합산 데미지, HitboxType.Body)` (몸통 맞은 탄이 있을 때 1회)
5. **피격 파티클 & 궤적:**
   - 데미지 호출은 묶어서 최적화하되, **피격 파티클(Impact Effect)과 탄 궤적(Tracer Line)은 8개 탄착 지점 각각에 100% 개별 생성**.

#### C. 공통 규칙
- **서버 완전 권한:** 클라이언트는 카메라 Ray와 발사 트리거를 전송하고, 충돌 판정 및 데미지는 서버에서 연산.
- **팀킬 방지 (Friendly Fire OFF):** 아군 플레이어 피격 시 데미지는 0이며 파티클만 발생. 관통되지 않음.
- **관통 없음 (No Penetration):** 각 레이는 처음 충돌한 단일 콜라이더에서 즉시 소멸.
- **헤드샷 배율:** `HeadHitbox` 피격 시 해당 탄환 데미지의 **1.5배** 적용.

---

### 4) 적 경직 시스템 (Stagger & Diminishing Returns)
1. **기본 경직:**
   - 데미지를 입은 적은 짧은 시간(`BaseStaggerDuration`, 기본 약 0.35초) 동안 이동 및 공격 행동 정지(Stun).
2. **무한 경직 방지 (점감 법칙):**
   - 연사 총기 등으로 단시간 내에 연속 타격당할 경우 경직 지속 시간이 점진적 감소 (100% -> 60% -> 30% -> 0%).
   - 샷건의 경우 8발이 동시에 적중해도 기존 경직 로직 1회만 발동 (펠릿 수 비례 가산 없음).

---

### 5) 화면 반동 및 에임 복구 (Recoil & Recovery)
1. **화면 튕김 및 부드러운 자동 복구:**
   - 격발 시 카메라 피치(상하)가 순간적으로 위로 튕김 (`RecoilPitch`).
   - 미세 좌우 랜덤 흔들림 (`RecoilYaw`).
   - 발사 후 마우스 조작이 멈추면 원래 조준점 높이로 부드럽게 복구 (`RecoilRecoverySpeed`).
   - 샷건은 수직 피치 3.5 ~ 4.5도의 묵직한 반동 세팅.

---

### 6) 연출 및 피드백 (VFX, SFX, HUD)
1. **비주얼 & 사운드:**
   - **발사자(1인칭):** 즉각적인 격발음, 총구 화염(Muzzle Flash), 탄 궤적(Tracer Line), 화면 반동.
   - **피격 지점:** 타격 지점 표면 재질(살점, 금속, 콘크리트 등)에 맞춘 파티클 풀링 스폰.
   - **샷건 전용 세분화 사운드:** `ReloadStartSound`, `ReloadInsertSound`, `ReloadEndSound`.
2. **HUD 표시:**
   - 화면 우측 하단: `[ 현재 탄창 잔탄 / 인벤토리 소지 탄약수 ]` (예: `8 / 24`).
   - 크로스헤어는 표준 십자선 HUD 유지.

---

### 7) 달리기(Sprint) ↔ 사격/장전 상호작용 규칙 (CoD 스타일)
1. **사격 중 달리기(`Shift`):** 사격 즉시 중단 및 달리기 전환. 마우스를 뗐다가 다시 클릭해야 사격 가능.
2. **장전 중 달리기(`Shift`):** 장전 즉시 취소 후 달리기 시작. (샷건의 경우 그때까지 들어간 탄약은 보존)
3. **달리기 중 사격(좌클릭):** 달리기 즉시 해제 및 `_sprintToFireDelay`(약 0.18초) 후 첫 발 격발.
4. **달리기 중 재장전(`R`):** 달리기 즉시 해제 및 재장전 프로세스 시작.
5. **사격 중 이동 속도 페널티:** 사격 중에는 걷기 속도가 약 25% 감소 (기본 속도의 75%로 이동).

---

## 3. 데이터 구조 설계 (ScriptableObject)

```csharp
[CreateAssetMenu(fileName = "NewGunData", menuName = "Inventory/Gun Data")]
public class GunItemData : ItemData
{
    [Header("Gun Base Stats")]
    [SerializeField] private int _baseDamage = 25;
    [SerializeField] private float _fireInterval = 0.15f;
    [SerializeField] private float _maxRange = 100f;
    [SerializeField] private GunFireMode _fireMode = GunFireMode.FullAuto;

    [Header("Shotgun Spread & Pellet Settings")]
    [Tooltip("샷건 여부 (체크 시 다중 펠릿 및 사거리 감쇄 활성화)")]
    [SerializeField] private bool _isShotgun = false;
    [Tooltip("1회 발사 시 발사되는 펠릿 수")]
    [SerializeField] private int _pelletCount = 8;
    [Tooltip("확산 원뿔 각도 (도, 기본: 7도)")]
    [SerializeField] private float _spreadAngle = 7f;
    [Tooltip("최대 데미지가 유지되는 근거리 한계 (미터, 기본: 3m)")]
    [SerializeField] private float _damageFalloffStartRange = 3f;
    [Tooltip("최소 데미지 구간이 시작되는 원거리 한계 (미터, 기본: 10m)")]
    [SerializeField] private float _damageFalloffEndRange = 10f;
    [Tooltip("최소 사거리 도달 시 보장되는 펠릿당 최소 데미지 (기본: 1)")]
    [SerializeField] private int _minDamagePerPellet = 1;

    [Header("Ammo & Reload")]
    [SerializeField] private int _magazineCapacity = 30;
    [SerializeField] private float _reloadDuration = 2.0f;
    [SerializeField] private string _requiredAmmoItemId = "Ammo_Rifle";

    [Header("Shell-by-Shell Reload Settings (Shotgun)")]
    [Tooltip("쉘 바이 쉘(1발씩) 장전 사용 여부")]
    [SerializeField] private bool _useShellByShellReload = false;
    [Tooltip("장전 시작 선딜레이 (초)")]
    [SerializeField] private float _reloadStartDelay = 0.35f;
    [Tooltip("1발 삽입 주기 (초)")]
    [SerializeField] private float _reloadInsertInterval = 0.5f;
    [Tooltip("장전 완료 후딜레이 (초)")]
    [SerializeField] private float _reloadEndDelay = 0.3f;

    [Header("Recoil")]
    [SerializeField] private float _recoilPitch = 1.8f;
    [SerializeField] private float _recoilYaw = 0.4f;
    [SerializeField] private float _recoilRecoverySpeed = 8.0f;

    [Header("Audio & Visuals")]
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private AudioClip _dryFireSound;
    [SerializeField] private AudioClip _reloadStartSound;
    [SerializeField] private AudioClip _reloadInsertSound;
    [SerializeField] private AudioClip _reloadEndSound;
    [SerializeField] private GameObject _muzzleFlashPrefab;
    [SerializeField] private GameObject _impactEffectPrefab;
    [SerializeField] private GameObject _bulletTracerPrefab;
}
```

---

## 4. 예외 케이스 및 엣지 케이스 종합 처리 규칙 (Edge Cases)

| 번호 | 상황 (Scenario) | 처리 및 동작 규칙 (Expected Behavior) |
| :---: | :--- | :--- |
| **E-01** | **잔탄 0발 상태에서 발사 시도** | - 발사되지 않으며 빈 총 격발음(`Dry Fire`) 1회 재생. 자동 재장전 안 됨. |
| **E-02** | **재장전 도중 무기 교체 (스왑)** | - 재장전 즉시 취소. 기존 탄약 유지 (샷건은 들어간 발수까지 보존). |
| **E-03** | **일반 총기 재장전 도중 발사 클릭** | - 발사 입력 무시. 재장전 타이머 계속 진행. |
| **E-04** | **샷건 0발(빈 총) 상태에서 장전 직후(1발 삽입 전) 좌클릭** | - 장전을 즉시 캔슬하고 빈 총 격발음(`Dry Fire`) 재생. 잔탄 0발 유지. |
| **E-05** | **샷건 1발 이상 들어간 상태에서 장전 도중 좌클릭** | - **딜레이 0초로 즉시 장전을 끊고 바로 탕! 격발.** 즉시 사격 처리 및 반동 적용. |
| **E-06** | **샷건 장전 도중 달리기(`Shift`), 스왑, 버리기(`G`)** | - 장전 즉시 중단. **중단 직전까지 들어간 탄약 수는 그대로 보존**되어 동기화 유지. |
| **E-07** | **인벤토리 탄약이 부족한 상태에서 장전 시작 (예: 2발만 보유)** | - 보유한 수량만큼만 넣은 뒤 **자동으로 장전 종료 단계로 넘어가 완료 처리**. |
| **E-08** | **장전 루프 도중 인벤토리 탄약이 외부 요인으로 버려짐/소모됨** | - 다음 발 삽입 주기 검사 시 인벤토리 잔여량이 0이면 즉시 안전하게 장전 종료 단계로 진입. |
| **E-09** | **차징 도중 마우스 우클릭 / 무기 교체 / 피격 / 드롭** | - 차징 즉시 취소/리셋. 탄약 소모 0. |
| **E-10** | **인벤토리에 탄약이 0개일 때 R키 입력** | - 재장전 시작되지 않음. 탄약 부족 알림 피드백. |
| **E-11** | **벽/몬스터에 바짝 붙은 영거리 사격 (Zero-Distance)** | - 원점을 카메라 뷰포트(`MainCamera.ViewportPointToRay`)로 설정하여 총구가 벽 속에 파묻혀 발생하는 관통 버그 원천 방지. |
| **E-12** | **아군 플레이어를 조준하고 사격 (Friendly Fire)** | - 데미지 및 경직 없음. 관통되지 않으므로 아군 피격 지점에서 파티클 발생 후 소멸. |
| **E-13** | **샷건 최대 사거리(15m) 초과 타겟 사격** | - 15m를 초과하는 적/오브젝트에는 충돌 판정이 발생하지 않음 (허공 소멸). |
| **E-14** | **샷건 8발 다중 펠릿의 네트워크 동기화** | - 1회의 ServerRpc로 [카메라 시선 Ray + Random Seed]를 전송하여 서버에서 8발 레이를 계산. 8개 피격 지점 파티클은 개별 브로드캐스트. |
| **E-15** | **단일 몬스터에게 8발 중 다수의 펠릿 동시 적중** | - `TakeDamage` 폭음 및 연산 부하를 막기 위해 [헤드샷 합산 1회] + [몸통 합산 1회]로 부위별 묶음 호출. |
| **E-16** | **인벤토리에서 총기를 바닥에 버렸다가 다시 주웠을 때** | - 버리기 직전의 탄창 잔탄 수가 그대로 유지되어 인벤토리에 보관됨. |
| **E-17** | **사격 도중 Shift (달리기) 입력** | - 사격 즉시 캔슬 후 달리기 전환. 마우스를 뗐다가 다시 클릭해야 사격 가능. |
| **E-18** | **달리는 도중 마우스 좌클릭 (사격) 입력** | - 달리기 즉시 해제 및 `_sprintToFireDelay` 선딜레이 후 첫 발 격발. |
