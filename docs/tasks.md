# Project: Containment Breach - 개발 작업 체크리스트 (Tasks Roadmap)

본 문서는 기획서([`product-spec.md`](product-spec.md), [`gameplay-loop-and-mechanics.md`](gameplay-loop-and-mechanics.md), [`inventory-item-system.md`](inventory-item-system.md), [`gun-combat-system-spec.md`](gun-combat-system-spec.md), [`melee-combat-system-spec.md`](melee-combat-system-spec.md), [`room-archetypes-and-gimmicks.md`](room-archetypes-and-gimmicks.md)) 및 현재까지 구현된 코드베이스를 기준으로 **완료된 작업(Completed)**과 **앞으로 구현해야 할 작업(Upcoming)**을 체계적으로 정리한 프로젝트 공식 로드맵 체크리스트입니다.

---

## 📌 전체 진행 현황 요약
- **Phase 1. 개발 환경 & 네트워크 프로토타이핑:** 100% 완료 ✅
- **Phase 2. Steam 로비 & 매치메이킹:** 100% 완료 ✅
- **Phase 3. 플레이어 컨트롤러 & 인벤토리 & 뷰모델:** 100% 완료 ✅
- **Phase 4. 전투 시스템 (총기/산탄총/근접 무기/히트스캔):** 100% 완료 ✅
- **Phase 5. 그리드 건축 시스템 & 레트로 렌더링 파이프라인:** 100% 완료 ✅
- **Phase 6. 중앙 격리 코어(Containment Core) & 방어 오브젝트:** 0% (진행 예정) ⏳
- **Phase 7. 웨이브 시스템 (WaveManager) & 게임 라이프사이클:** 0% (진행 예정) ⏳
- **Phase 8. 몬스터 AI 고도화 & 변이체 아키타입:** 30% (기본 AI 완료 / 코어 타겟팅 및 변종 예정) ⏳
- **Phase 9. 사물 해체(Scrapping) & 크래프팅 작업대:** 20% (파괴 객체/UI 완료 / 자원 드롭 및 제작 UI 예정) ⏳
- **Phase 10. 방어 시설(바리케이드/터렛/트랩) 실전화:** 20% (배치 시스템 완료 / 실전 프리팹 및 기믹 예정) ⏳
- **Phase 11. 보스전 & 구역 해금(Sector Unlock) 프로그레션:** 0% (진행 예정) ⏳
- **Phase 12. 절차적 격리 시설 맵 생성 (Procedural Facility):** 0% (진행 예정) ⏳
- **Phase 13. Steam 전용 기능 연동 (도전과제/클라우드/Rich Presence):** 0% (진행 예정) ⏳
- **Phase 14. 네트워크 최적화, 예외 처리 & Steam 릴리즈 파이프라인:** 0% (진행 예정) ⏳

---

## [Phase 1] 개발 환경 구축 & 멀티플레이어 기본 기반 ✅
- [x] **NGO 및 기본 패키지 환경 구축**
  - [x] Netcode for GameObjects (NGO) 패키지 설치 및 세팅
  - [x] Steamworks.NET 패키지 설치 및 `steam_appid.txt` (480) 설정
  - [x] ParrelSync 패키지 설치 (에디터 클론 기반 로컬 멀티플레이어 테스트 환경)
  - [x] Universal Render Pipeline (URP) 환경 세팅 및 프로젝트 컨벤션 수립
- [x] **네트워크 트랜스포트 분기 처리**
  - [x] NGO용 SteamSockets 기반 트랜스포트 연동
  - [x] 에디터(Localhost / UnityTransport)와 빌드(SteamSocketsTransport) 자동 분기 (`NetworkBootstrap.cs`, `SteamManager.cs`)
- [x] **플레이어 스폰 & 생명주기**
  - [x] 호스트/클라이언트 4인 접속 및 동적 플레이어 프리팹 스폰 (`PlayerSpawner.cs`)
  - [x] 플레이어 머리 위 닉네임 플레이트 동기화 (`PlayerNamePlate.cs`)
  - [x] 씬 전환 시 화면 페이드 트랜지션 연출 (`ScreenFader.cs`)

---

## [Phase 2] Steam 로비 & 매치메이킹 시스템 ✅
- [x] **Steam 로비 생성 및 초대**
  - [x] `SteamManager` 싱글톤 생명주기 관리 및 에디터 비활성화(더미 모드) 예외 처리
  - [x] Steam P2P Lobby 생성 (Friends Only / 4인) 및 로비 콜백 처리 (`SteamLobbyManager.cs`)
  - [x] Steam In-Game Overlay를 통한 친구 초대 및 초대 수락(Join Request) 연동
  - [x] Lobby ID 직접 입력을 통한 세션 참가 기능
- [x] **대기실(Lobby Room) UI & 동기화**
  - [x] 로비 플레이어 리스트 및 슬롯 UI (`LobbyUIController.cs`)
  - [x] 플레이어 준비(Ready) 상태 네트워크 동기화 (`LobbyNetworkSync.cs`)
  - [x] 방장(Host) 전용 게임 시작 권한 제어 및 `NetworkSceneManager` 기반 씬 전환

---

## [Phase 3] 플레이어 조작 & 인벤토리 & 뷰모델 시스템 ✅
- [x] **1인칭 플레이어 컨트롤러 (`PlayerController.cs`)**
  - [x] New Input System 기반 이동, 점프, 전력질주(스프린트), 웅크리기(Crouch)
  - [x] 스태미나 소비 및 자동 회복 메커니즘
  - [x] 1인칭 카메라 피치 각도 상하 제한 및 네트워크 동기화 (`ClientNetworkTransform` / `NetworkVariable`)
  - [x] 1인칭 손 뷰모델(그림자 끄기) 및 3인칭 외형 렌더링 분리
  - [x] 클라이언트 접속 시 위치, 회전, 카메라 피치 저장 및 복원 (`SaveLoadManager.cs`)
  - [x] 표면 재질(SurfaceType) 판별 기반 동적 발소리 SFX 재생 시스템 (`CharacterFootsteps.cs`, `SoundCue.cs`)
- [x] **인벤토리 & 아이템 데이터 허브 (`PlayerInventory.cs`, `ItemDatabase.cs`)**
  - [x] 30칸 보관함 + 9칸 핫바 슬롯 관리 및 스택 병합, 스왑 로직 (`InventorySlot.cs`)
  - [x] ScriptableObject 기반 아이템 데이터(`ItemData.cs`) 및 Resources 자동 캐싱/조회
  - [x] 인벤토리 uGUI 시스템: 드래그 앤 드롭, 아이템 나누기, 툴팁 (`InventoryUIController.cs`, `InventorySlotUI.cs`)
  - [x] 옵션 창 및 사운드 설정 UI (`OptionWindowUI.cs`, `SettingsManager.cs`, `InGameMenuController.cs`)
- [x] **단일 소켓 메쉬 교체 뷰모델 (`PlayerItemHolder.cs`)**
  - [x] 프리팹 생성/파괴 없는 단일 `HeldItemVisual` 메쉬/머티리얼 교체 방식 (GC Alloc 0)
  - [x] `_networkHeldItemId`를 통한 3인칭 원격 플레이어 소지품 외형 동기화
  - [x] 인스펙터 실시간 손 오프셋 튜닝 에디터 툴 제작 (`ItemHoldOffsetTweaker.cs`)
- [x] **플레이어 세션 & 캐릭터 분리 아키텍처 (`NetworkPlayer.cs`, `PlayerCharacter.cs`, `PlayerCameraController.cs`)**
  - [x] 접속자 세션 객체(`NetworkPlayer`, `PlayerSessionPrefab`)와 월드 조종 캐릭터(`PlayerCharacter`, `PlayerDummyPrefab`)의 역할 및 생명주기 분리
  - [x] `PlayerInventory`를 세션 객체(`NetworkPlayer`)에 귀속하여 캐릭터 사망/디스폰 시에도 인벤토리 데이터 영구 보존
  - [x] `NetworkVariable<NetworkObjectReference>` 기반 안전한 빙의(Possess) 및 언포제스(Unpossess) 파이프라인 구축
  - [x] 단일 카메라 매니저(`PlayerCameraController.cs`): 캐릭터 조종 시 1인칭 머리 추적 ↔ 캐릭터 부재(사망/스폰 대기) 시 기지 코어(`Containment Core`) 고정 시점 자동 전환
  - [x] 캐릭터 부재 시 `Tab` 키 인벤토리 열기 및 툴바 숫자키(1~0) 조작 자동 차단
  - [x] Dummy FBX 완성형 모델(`PlayerDummyPrefab.prefab`)을 메인 캐릭터로 전격 연동 및 구형 프리팹 정리
- [x] **월드 물리 아이템 상호작용 (`PickableItem.cs`, `PlayerInteraction.cs`)**
  - [x] E키 시선 레이캐스트 아이템 줍기 (서버 Despawn) 및 손에 든 임시 상태 처리
  - [x] 번호키(1~9) 입력 시 지정 핫바 슬롯 보관 (핫바 가득 찰 시 자동 드롭)
  - [x] G키 물리 투척 (시선 벡터 기준 물리력 전달, 서버 Spawn)
  - [x] 월드 물리 최적화: Sleep & Freeze 판정, 충돌 레이어 격리, 거리 기반 컬링 (`ItemCullingManager.cs`)

---

## [Phase 4] 전투 시스템 (총기/산탄총/근접 무기/히트스캔) ✅
- [x] **서버 권한(Server-Authoritative) 총기 사격 (`PlayerGunCombat.cs`, `GunItemData.cs`)**
  - [x] 3가지 발사 모드: 단발(Semi-Auto), 연사(Full-Auto), 차지 샷(Charge Shot - 우클릭 취소 지원)
  - [x] 샷건(산탄총) 특화: 8발 펠릿 탄도 분산, 거리별 데미지 감쇄, 1회 ServerRpc 통합 대역폭 최적화
  - [x] 2가지 재장전 모드: 탄창 교체(Magazine Reload) 및 샷건 쉘 바이 쉘(Shell-by-Shell 1발씩 장전 루프)
  - [x] 장전 중 즉시 격발 인터럽트(0초 반응 사격 캔슬) 및 달리기/스왑/버리기 중단 처리
  - [x] 인벤토리 탄약 연동(`Ammo_Rifle`, `Ammo_Pistol`, `Ammo_Shotgun`) 및 슬롯 인스턴스 잔탄 보존
  - [x] 0ms 조작 반응성: 클라이언트 탄약 차감 예측, 반동(Recoil), 화면 흔들림, 총구 화염, 탄피 배출
  - [x] 실시간 잔탄/예비 탄약 HUD (`AmmoHUD.cs`)
- [x] **근접 무기 전투 (`PlayerMeleeCombat.cs`, `MeleeItemData.cs`)**
  - [x] 좌클릭 자동 연속 공격(Auto-Swing) 및 공격 주기 제어
  - [x] 3단계 상태 머신: 선딜레이(Windup) ➔ 타격(HitCheck) ➔ 후딜레이(Recovery)
  - [x] 공격 중 스프린트 강제 해제 및 이동 속도 50% 감속 적용
  - [x] SphereCast 단일 대상 타격 및 벽 차단 검사(Wall Occlusion Check)
  - [x] 프로시저럴 뷰모델 스윙 모션 연출
- [x] **피격 판정 & 데미지 인터페이스**
  - [x] `IDamageable.cs` 인터페이스 및 `DamageInfo` 구조체 정의
  - [x] 부위별 피격 판정 컴포넌트(`Hitbox.cs`, 헤드샷 1.5배 배수 적용)
  - [x] 피격 넉백 물리 전달 및 몬스터 경직(Stagger) 트리거 연동
  - [x] 파괴 가능 오브젝트 컴포넌트 (`DestructibleObject.cs`)
  - [x] 타격 표면(SurfaceType)별 피격 파티클/스파크 풀링 시스템 (`VfxPoolManager.cs`, `SurfaceIdentifier.cs`)

---

## [Phase 5] 그리드 건축 시스템 & 레트로 렌더링 파이프라인 ✅
- [x] **그리드 기반 건축 시스템**
  - [x] 전역 그리드 좌표 변환 및 스냅 관리자 (`WorldGridManager.cs`)
  - [x] 건축 프리뷰 반투명 렌더링, 충돌 검증 및 R키 회전 제어 (`GridPlacementPreview.cs`)
  - [x] LineRenderer 및 셰이더 기반 바닥 그리드 시각화 (`LineRendererGridVisualizer.cs`, `ShaderGridVisualizer.cs`)
  - [x] 건축 불가 구역 컴포넌트 (`NoBuildArea.cs`)
  - [x] 블록 배치 및 F키 철거 진행도 UI (`GridBuildingController.cs`, `DismantleProgressUI.cs`, `PlaceableObject.cs`)
- [x] **90s 레트로 그래픽 파이프라인**
  - [x] 픽셀화 다운스케일링 렌더링 및 디더링 카메라 (`RetroPixelCamera.cs`)
  - [x] 로우폴리 3D 및 픽셀 텍스처 스타일 가이드 수립 (`art-style-reference.md`)

---

## [Phase 6] 중앙 격리 코어 (Containment Core) 시스템 ⏳
- [ ] **코어 챔버 및 코어 오브젝트 세팅**
  - [ ] 맵 중앙 코어 챔버 구역 구성 및 중앙 `ContainmentCore` 프리팹 제작
  - [ ] 코어 `NetworkObject` 등록 및 격리 필드 내구도 동기화 (`NetworkVariable<float> _coreHealth`)
- [ ] **코어 피격 & 비상 연출**
  - [ ] 몬스터의 코어 타격 시 전기 아크, 스파크 방전 및 시설 경보 사이렌 연동
  - [ ] 코어 체력 HUD: 화면 상단 중앙 게이지 및 코어 챔버 월드 LED 전광판 동기화
- [ ] **게임 오버(Defeat) 시퀀스**
  - [ ] 코어 내구도 0 도달 시 초자연 격리 대폭발 시퀀스 재생
  - [ ] 패배 화면 UI(Game Over Canvas) 팝업 및 로비 복귀/재도전 흐름 처리

---

## [Phase 7] 웨이브 관리자 (WaveManager) & 게임 라이프사이클 ⏳
- [ ] **호스트 권한 웨이브 상태 머신 (FSM) 구현**
  - [ ] `Preparation(정비)` ➔ `WaveAlert(경보)` ➔ `Spawning & Combat(전투)` ➔ `Stabilization(안정화)` 라이프사이클 관리
  - [ ] 웨이브 진행 상태 및 남은 시간 `NetworkVariable` 동기화
- [ ] **웨이브 데이터 테이블 설계**
  - [ ] ScriptableObject 기반 웨이브별 스폰 설정 (웨이브 번호, 스폰 간격, 몬스터 타입 조합, 동시 스폰 상한)
  - [ ] 참가 플레이어 인원수(1~4인)에 따른 난이도/적 수량 동적 스케일링
- [ ] **환경 연출 및 인게임 HUD**
  - [ ] 비상 경보 발령 시 형광등 점멸 소등 + 붉은색 회전 경광등 점등 시스템
  - [ ] 비상 사이렌 오디오 재생 및 안내 방송 SFX
  - [ ] 현재 웨이브 번호, 정비 카운트다운 타이머, 잔여 변이체 수 표시 HUD
- [ ] **웨이브 클리어 보급품**
  - [ ] 웨이브 종료 시 정규 보급 캡슐/상자 드롭 (탄약, 고급 부품, 구급약)

---

## [Phase 8] 몬스터 AI 고도화 & 변이체 아키타입 ⏳
- [ ] **코어 디펜스 타겟팅 AI 개선 (`MonsterController.cs`)**
  - [ ] 타겟 우선순위 가중치 로직: 1순위 중앙 코어 vs 2순위 플레이어(어그로/피격 시)
  - [ ] NavMesh 상의 바리케이드/방어벽 감지 시 우회 불가 시 방어벽 공격 및 파괴 행동
- [ ] **변이체 종류별 아키타입 프리팹 및 패턴 제작**
  - [ ] **일반 변이체 (Mutant Worker):** 빠른 이동속도로 코어로 쇄도하는 기본 근접 개체
  - [ ] **산성 분사체 (Acid Spitter):** 원거리에서 부식성 화학탄을 곡사/직사로 투척하는 개체
  - [ ] **돌진 파괴체 (Brute Breaker):** 높은 체력과 바리케이드 돌파 특화, 넉백 저항을 지닌 중형 개체
- [ ] **네트워크 애니메이션 & 전리품**
  - [ ] 공격, 피격, 사망 모션 `ClientNetworkAnimator` 동기화
  - [ ] 몬스터 처치 시 자원 드롭 (고철, 변이 조직, 배터리 등)

---

## [Phase 9] 사물 해체 (Scrapping) & 크래프팅 작업대 ⏳
- [ ] **연구소 프롭 해체(Dismantle) 상호작용**
  - [ ] 사무실/복도 배치 오브젝트에 `DestructibleObject` 및 해체 전용 데이터 연결:
    - 오피스 파티션, 책상 ➔ 합성 합판, 경량 플라스틱
    - 서버 랙, 모니터, 배전반 ➔ 전자 회로, 마이크로칩, 구리 배선
    - 음료수 자판기, 캐비닛 ➔ 고철, 배터리
    - 의무실 약품함 ➔ 구급 키트, 소독제
  - [ ] 무기(빠루, 총기) 타격 및 F키 도구 분해 시 물리 자원 아이템 월드 드롭
- [ ] **크래프팅 작업대 (Workbench) 시스템**
  - [ ] 시설 내 고정 작업대 또는 휴대용 제작 키트 UI 구현
  - [ ] 인벤토리 자원을 소모하여 무기, 도구, 탄약, 방어 시설 프리팹을 제작하는 레시피 시스템
  - [ ] 구역 전력 복구에 따른 상위 티어(Tier 1~3) 제작법 잠금 해제 체계

---

## [Phase 10] 방어 시설(바리케이드/터렛/트랩) 실전화 ⏳
- [ ] **실전 방어 시설 프리팹 구축 (Placeable Item & Object)**
  - [ ] **접이식 바리케이드 (Wooden/Metal Barricade):** 내구도를 가지며 몬스터 진로 차단, 플레이어 수리(Repair) 상호작용
  - [ ] **자동 경비 터렛 (Auto Turret):** 사거리 내 변이체 자동 감지 및 히트스캔 사격, 탄약/배터리 소모 메커니즘
  - [ ] **레이저/전기 트랩 (Electric Floor Trap):** 통과하는 적에게 이동속도 저하(Slow) 및 감전 도트 데미지
- [ ] **방어 시설 유지보수**
  - [ ] 방어벽 내구도 손상도에 따른 시각적 파손 연출 (스파크, 연기)
  - [ ] 빠루/용접기를 소지하고 상호작용하여 자원을 소모해 수리하는 기능

---

## [Phase 11] 보스전 & 구역 해금(Sector Unlock) 프로그레션 ⏳
- [ ] **3단계 특급 격리 개체 (중간 보스)**
  - [ ] **Tier 1 보스 (변이 경비반장):** 돌진 기믹 및 지면 강타 충격파
  - [ ] **Tier 2 보스 (생화학 융합체):** 광범위 산성 바닥 살포 및 소형 자폭 슬라임 생성
  - [ ] **Tier 3 보스 (차원 왜곡체):** 공간 왜곡 순간이동 및 주기적 전자기 펄스 방출
- [ ] **월드 진화 & 구역 해금 (Sector Unlock)**
  - [ ] 보스 처치 시 시설 전력 복구 시퀀스 연출
  - [ ] 차단된 격리 보안 셔터가 열리며 상위 섹터(생화학 랩, 양자물리동) 개방
  - [ ] 신규 티어 자원 출현 및 생존 과학자/엔지니어 NPC 구출 (고급 청사진 획득)
- [ ] **최종 탈출 보스 (Final Boss: Alpha Anomaly) & 승리 시퀀스**
  - [ ] 3마리 중간 보스 격파 후 중앙 코어 챔버에 강림하는 대형 라스트 보스
  - [ ] 다단계 페이즈 전투 및 최종 처치 시 연구소 완전 정상화/승리(Victory) 시퀀스

---

## [Phase 12] 절차적 연구소 맵 생성 (Procedural Facility) ⏳
- [ ] **그리드 기반 룸 모듈러 생성기**
  - [ ] 중앙 격리 코어 챔버를 중심으로 사방으로 뻗어 나가는 타일 그리드 배치 알고리즘
  - [ ] [`room-archetypes-and-gimmicks.md`](room-archetypes-and-gimmicks.md) 기반 모듈러 방 프리팹 제작:
    - 서버실 & 데이터 허브
    - 오픈 오피스 & 행정 구역
    - 자재 물류 창고
    - 직원 탕비실 & 휴게실
    - 외곽 특수 방 (보안 무기고, 의무실 등)
- [ ] **복도 연결 & 런타임 NavMesh 빌드**
  - [ ] 방 사이를 잇는 복도 통로, 환기구, 절차적 방화문 배치
  - [ ] 맵 생성 완료 후 몬스터 이동을 위한 Unity AI NavMesh 런타임 베이킹 (`NavMeshSurface`)
  - [ ] 우호적 NPC(방호 헬멧을 쓴 강아지, 청소 드론 등) 배치

---

## [Phase 13] Steam 전용 기능 연동 ⏳
- [ ] **Steam 도전 과제 (Achievements)**
  - [ ] 최초 웨이브 방어, 보스 처치, 특수 무기 제작 등 업적 트리거 연동
- [ ] **Steam Cloud Save**
  - [ ] 플레이어 커스터마이징, 세팅 및 누적 게임 데이터 클라우드 저장
- [ ] **Steam Rich Presence**
  - [ ] 친구 목록 상태 표시 ("로비 대기 중", "웨이브 5 방어 중 (3/4인)", "보스전 교전 중")
- [ ] **(선택) 인게임 3D 공간 음성 채팅 (Voice Chat)**
  - [ ] Unity Vivox 또는 Steam Voice 연동 (근접 무전기 거리 감쇄 효과)

---

## [Phase 14] 네트워크 최적화, 예외 처리 & 릴리즈 파이프라인 ⏳
- [ ] **호스트 이탈 및 예외 처리 (Host Migration / Disconnect)**
  - [ ] 호스트 접속 종료 시 남은 클라이언트 안전한 로비 강제 퇴장 및 에러 팝업
  - [ ] 네트워크 지연(Ping) 및 패킷 손실 시 동기화 보정 검증
- [ ] **대역폭 및 성능 최적화**
  - [ ] 수백 개 드롭 아이템 및 다수 몬스터 스폰 시 `NetworkVariable` 갱신 빈도 및 컬링 최적화
- [ ] **스팀 빌드 & 배포 파이프라인**
  - [ ] Steamworks 대시보드 정식 AppID 등록 및 빌드 설정
  - [ ] SteamPipe 활용 자동 업로드(Depot) 파이프라인 구축
  - [ ] 4인 네트워크 QA 및 스팀 플레이테스트(Steam Playtest) 진행
