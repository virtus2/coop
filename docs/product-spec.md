# Product Specification

## Game Overview
- **Title:** [Game Title / 가칭: Containment Breach / Project: Overflow]
- **Genre:** 1인칭 협동 PvE, 기지 방어(Base Defense), 90s 아날로그 SF 서바이벌 액션
- **Target Audience:** [Abiotic Factor](https://store.steampowered.com/app/427410/Abiotic_Factor/), [Lethal Company](https://store.steampowered.com/app/1966720/Lethal_Company/), [Muck](https://store.steampowered.com/app/1625450/Muck/)을 즐기며, 90년대 레트로 연구소 감성에서 친구들과 가구를 부숴 방어선을 쌓고 몰려오는 초자연 괴물 웨이브를 쓸어버리는 캐주얼한 코옵 액션을 원하는 유저층
- **Core Loop:** 격리 시설 스폰 및 오피스/연구실 자원 해체 ➔ 즉석 무기/도구/바리케이드 제작 ➔ 폭주하는 격리 코어(Core) 사수 웨이브 전투 ➔ 셧다운 정비 및 신규 연구 구역 확장 (세부 내용은 [docs/gameplay-loop-and-mechanics.md](file:///C:/unity-projects/coop/docs/gameplay-loop-and-mechanics.md) 참고)

## Game Mechanics
- **게임 목표 (Core Defense):** 연구소 중앙의 폭주 직전인 **초자연 격리 코어(Containment Core)**를 몰려오는 변이 생명체들로부터 사수. 코어 폭주 파괴 시 패배, 최종 탈출 라스트 보스 격파 시 승리.
- **웨이브 시스템 (Wave System):** `정비/해체` ➔ `격리 경보(사이렌)` ➔ `변이체 스폰 및 요격` ➔ `격리 안정화` 순환. 데이터 테이블 기반으로 변이체 타입, 수량, 주기별 스폰 제어.
- **크래프팅 & 빌딩 (Scrap Crafting & Barricade):** 연구소 내 사물(오피스 파티션, 캐비닛, 자판기, 서버 랙 등)을 해체하여 얻은 자원(고철, 합성목재, 전자부품, 화학물질)으로 무기, 도구, 방어벽(방화 셔터, 레이저 철조망, 자동 경비 터렛 등) 제작 및 배치.
- **보스 & 월드 진화 프로그레션 (Bosses & Sector Unlock):** 일정 웨이브마다 탈출하는 3단계의 특급 격리 개체(중간 보스)를 격파할 때마다 격리 구역 전력이 복구되며, 상위 연구 섹터(Tier 2 화학 랩, Tier 3 양자물리 연구동) 및 신규 자원, 생존 연구원 NPC가 해금되어 성장 요소 확장. 이후 등장하는 최종 탈출 보스를 처치하면 연구소 완전 정상화(클리어).
- **절차적 맵 생성 (Procedural Facility):** 중앙 격리 코어 챔버를 중심으로 주변 연구실 복도, 사무 구역, 창고, 환기구가 절차적으로 생성되며 해체 가능한 프롭과 우호적 시설 로봇/실험 동물이 분산 배치.

## Art and Audio
- **Visual Style:** 90s 아날로그 레트로 로우폴리 3D + 픽셀 텍스처 ([Abiotic Factor](https://store.steampowered.com/app/427410/Abiotic_Factor/), [Lethal Company](https://store.steampowered.com/app/1966720/Lethal_Company/), [Barony](https://store.steampowered.com/app/371970/Barony/), [Stonewards](https://store.steampowered.com/app/4502710/Stonewards/) 레퍼런스 기반, 세부 내용은 [docs/art-style-reference.md](file:///C:/unity-projects/coop/docs/art-style-reference.md) 참고)
- **Audio Style:** 90년대 산업용 신디사이저, 긴박한 격리 경보 사이렌, 기계 작동음, 형광등 웅웅거림(Humming), 익살스럽고 둔탁한 타격 SFX

## Technical Requirements
- **Platform:** PC 최우선(Steam), 게임패드 지원 및 스팀덱(Steam Deck) 호환성 고려
- **Engine:** Unity
- **Render Pipeline:** URP (Point Filtering, Retro Downscaling, CRT Dithering)
- **Input System:** Unity Input System

## Monetization
- 스팀 플랫폼을 통한 패키지 유료 판매 (스팀 세일 및 4-Pack/번들 전략 최적화)
