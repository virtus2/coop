# Unity Game Development Rules

When working on this project, please adhere to the following rules and guidelines:

## Documentation Map
- `docs/coding-convention.md`: C# and Unity naming conventions and coding style guidelines.
- `docs/workflow.md`: 에이전트가 작업을 수행할 때 따라야 하는 표준 절차 및 워크플로우 명시.

## Project Settings
- **Input System:** Always use the new Unity `InputSystem`. Do not use the legacy `Input` manager.
- **Render Pipeline:** This project uses the Universal Render Pipeline (**URP**). Ensure all materials, shaders, and rendering features are compatible with URP.
- **Enter Play Mode Settings (Domain Reload):** This project disables **Domain Reload** to speed up Play Mode iteration. You MUST ensure that all `static` variables, events, and singletons are properly reset. Use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset static state, or handle cleanup manually in `OnDestroy()` / `OnDisable()`.

## Steamworks & Multiplayer
- **Steamworks in Editor:** NEVER initialize or use Steam APIs (`Steamworks.NET`) in the Unity Editor. Use `#if !UNITY_EDITOR` to disable actual Steam calls. This prevents the Steam client from locking the app in a "Playing" state when testing in the Editor.
- **Editor Testing:** When testing features that use Steamworks in the Editor, DO NOT call Steam APIs. Instead, use `Debug.Log` to simulate and verify the API calls (e.g., `Debug.Log("Simulating Steam API Call: XYZ")`).
