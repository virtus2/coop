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

## Phase 3. 핵심 멀티플레이어 게임플레이 동기화 (약 6 ~ 10주)
- [ ] 플레이어 조작 & 위치 동기화 (`NetworkTransform` 또는 커스텀 보정 로직)
- [ ] 상태 동기화 (체력, 인벤토리 등 `NetworkVariable` 기반 동기화)
- [ ] 상호작용 및 1회성 이벤트 동기화 (공격, 스킬 등 `ServerRPC` / `ClientRPC` 설계)
- [ ] 동적 오브젝트(아이템, 문, 트랩 등) Spawn/Despawn 및 소유권(Ownership) 동기화
- [ ] Co-op AI / 몬스터 동기화 (Host 주도 NavMeshAgent 타겟팅 및 애니메이션 동기화)

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
