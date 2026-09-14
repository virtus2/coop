# Unity Game Development Rules

When working on this project, please adhere to the following rules and guidelines:

## Documentation Map
- `docs/coding-convention.md`: C# and Unity naming conventions and coding style guidelines.

## Project Settings
- **Input System:** Always use the new Unity `InputSystem`. Do not use the legacy `Input` manager.
- **Render Pipeline:** This project uses the Universal Render Pipeline (**URP**). Ensure all materials, shaders, and rendering features are compatible with URP.
- **Enter Play Mode Settings (Domain Reload):** This project disables **Domain Reload** to speed up Play Mode iteration. You MUST ensure that all `static` variables, events, and singletons are properly reset. Use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset static state, or handle cleanup manually in `OnDestroy()` / `OnDisable()`.
