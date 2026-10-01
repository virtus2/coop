# Unity Game Development Rules

When working on this project, please adhere to the following rules and guidelines:

## Documentation Map
- `docs/coding-convention.md`: C# and Unity naming conventions and coding style guidelines.
- `docs/workflow.md`: 에이전트가 작업을 수행할 때 따라야 하는 표준 절차 및 워크플로우 명시.
- `docs/product-spec.md`: 게임 전반의 개요, 메커니즘, 기술 요구사항 명세.
- `docs/gameplay-loop-and-mechanics.md`: 코어 방어 목표, 게임플레이 루프, 웨이브 시스템, 보스/월드 프로그레션 상세 명세.
- `docs/inventory-item-system.md`: 인벤토리, 아이템 데이터, 손 뷰모델 및 월드 물리 동기화 시스템 명세.
- `docs/gun-combat-system-spec.md`: 총기 사격 및 히트스캔 전투 시스템 기본 명세서 (샷건 포함).
- `docs/art-style-reference.md`: 프로젝트 아트 스타일, 레퍼런스 게임 분석 및 시각적 가이드라인.

## Project Settings
- **UI Text:** Always use `TextMeshPro` (`TMP_Text`, `TextMeshProUGUI`) instead of the legacy Unity `Text` component.
- **Input System:** Always use the new Unity `InputSystem`. Do not use the legacy `Input` manager.
- **Render Pipeline:** This project uses the Universal Render Pipeline (**URP**). Ensure all materials, shaders, and rendering features are compatible with URP.
- **Enter Play Mode Settings (Domain Reload):** This project disables **Domain Reload** to speed up Play Mode iteration. You MUST ensure that all `static` variables, events, and singletons are properly reset. Use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset static state, or handle cleanup manually in `OnDestroy()` / `OnDisable()`.

## Steamworks & Multiplayer
- **Steamworks in Editor:** NEVER initialize or use Steam APIs (`Steamworks.NET`) in the Unity Editor. Use `#if !UNITY_EDITOR` to disable actual Steam calls. This prevents the Steam client from locking the app in a "Playing" state when testing in the Editor.
- **Editor Testing:** When testing features that use Steamworks in the Editor, DO NOT call Steam APIs. Instead, use `Debug.Log` to simulate and verify the API calls (e.g., `Debug.Log("Simulating Steam API Call: XYZ")`).
