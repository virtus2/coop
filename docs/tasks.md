# Netcode for GameObjects + Steamworks.NET Co-op Game Development Roadmap

## Phase 1. 개발 환경 구축 & 네트워크 프로토타이핑 (약 2 ~ 3주) - [현재 진행 중]
- [x] Netcode for GameObjects (NGO) 패키지 설치
- [x] Steamworks.NET 패키지 설치 및 `steam_appid.txt` (480) 설정
- [x] Unity Multiplayer Play Mode 패키지 설치 (로컬 멀티플레이어 테스트 환경 구축)
- [x] `NetworkManager` 세팅 및 기본 통신 테스트 (Host / Client 로컬 접속 테스트)
- [x] NGO용 Steam Transport 연동 (SteamSockets 기반 트랜스포트로 교체 완료)
- [x] 에디터(Localhost) / 빌드(Steam) 환경에 따른 Transport 자동 분기 처리 완료
- [x] 플레이어 동적 스폰(Spawn) 및 4인 접속/해제 기본 흐름 구현

## Phase 2. Steam 로비 & 매치메이킹 시스템 (약 2 ~ 3주) - [완료]
- [x] `SteamManager` 생명주기 관리 및 API 에러 핸들링 고도화 (스크립트 완료)
- [x] Steam P2P Lobby 생성 (Friends Only / 4인) 및 콜백 처리
- [x] Steam In-Game Overlay를 통한 친구 초대 연동 (Join Request 처리 완료)
- [x] 대기실(Lobby Room) UI & Ready 시스템 동기화 (LobbyManager 및 Canvas 생성 완료)
- [x] 방장(Host)의 게임 시작 제어 및 씬 전환 (`NetworkSceneManager` 연동)

## Phase 3. 핵심 멀티플레이어 게임플레이 & 코어 디펜스 루프 (약 6 ~ 10주)
- [ ] **플레이어 기본 동기화**
  - [ ] 플레이어 조작 & 위치 동기화 (`NetworkTransform` 또는 커스텀 보정 로직)
  - [ ] 상태 동기화 (체력, 스태미나 등 `NetworkVariable` 기반 동기화)
  - [ ] 인벤토리 및 아이템 홀더 동기화 (`PlayerInventory`, `PlayerItemHolder` 연동 완료 후 고도화)
  - [ ] 상호작용 및 전투 이벤트 동기화 (공격, 채취, 사격 등 `ServerRPC` / `ClientRPC` 설계)
- [ ] **코어(Core) 수호 오브젝트 시스템**
  - [ ] 맵 중앙 코어 `NetworkObject` 생성 및 체력(`NetworkVariable`) 관리
  - [ ] 코어 피격 이펙트, 체력 HUD 동기화 및 파괴 시 패배(Game Over) 처리
- [ ] **웨이브 관리자 (WaveManager) & 몬스터 AI**
  - [ ] 웨이브 상태 머신 구현 (`준비` ➔ `시작` ➔ `스폰` ➔ `종료` ➔ `재정비`)
  - [ ] 웨이브별 몬스터 종류/수량/스폰 주기 제어 데이터 테이블 설계
  - [ ] Host 주도 몬스터 스폰, NavMeshAgent 타겟팅 (코어 우선/플레이어 어그로) 및 공격 로직
  - [ ] 몬스터 피격/사망 판정 및 네트워크 동기화 (`NetworkTransform`, `NetworkAnimator`)
- [ ] **자원 채취 & 크래프팅 & 건물(방어시설) 배치**
  - [ ] 환경 자원 오브젝트(나무, 돌, 광물) 채취 및 드롭 아이템 연동
  - [ ] 제작 UI (자원 소모 ➔ 무기, 도구, 건물 제작)
  - [ ] 방어 건물(벽, 바리케이드, 터렛 등) 프리팹 제작 및 호스트 권한 설치 동기화
- [ ] **보스전 & 월드 진화 프로그레션**
  - [ ] 3단계 중간 보스 스폰 및 보스 전용 기믹/패턴 구현
  - [ ] 보스 처치 시 월드 상태 변화 (몬스터 스케일링, 신규 광물/NPC 스폰 트리거)
  - [ ] 최종 라스트 보스(Final Boss) 소환 및 클리어 시 승리(Victory) 시퀀스 구현
- [ ] **절차적 맵 생성 (Procedural Map) 기초**
  - [ ] 중앙 코어 거점 및 주변 지형/자원 절차적 배치 알고리즘
  - [ ] 동물 NPC(강아지 등) 스폰 및 상호작용 프레임워크 구축

## Phase 4. Steam 전용 기능 연동 (약 3 ~ 4주)
- [ ] Steam 도전 과제 (Achievements) & 통계 (Stats) 연동
- [ ] Steam Cloud Save (클라우드 저장) 동기화
- [ ] Steam Rich Presence (친구 목록 텍스트 상태 표시) 연동
- [ ] (선택) 4인 인게임 음성 채팅 (Voice Chat) 통합 (Unity Vivox 또는 Steam Voice)

## Phase 5. 네트워크 최적화 & 예외 처리 (약 3 ~ 4주)
- [ ] 호스트 이탈 처리 (Host Migration / Disconnect Handling) 및 로비 복귀 UI
- [ ] 대역폭 및 패킷 최적화 (`NetworkVariable` 갱신 주기 조정)
- [ ] 네트워크 지연(Lag/Ping) 및 패킷 손실 환경에서의 동기화 오차 보정 검증

## Phase 6. QA 테스트, 빌드 및 스팀 파이프라인 (약 3 ~ 4주)
- [ ] Steamworks 대시보드 세팅, 실제 AppID 등록 및 상점 페이지 설정
- [ ] SteamPipe를 활용한 빌드 자동 업로드(Depot) 환경 구축
- [ ] Closed Beta / Steam Playtest 기능을 활용한 4인 네트워크 QA 테스트
