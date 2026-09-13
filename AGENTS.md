# Unity Game Development Rules

When working on this project, please adhere to the following rules and guidelines:

- **Input System:** Always use the new Unity `InputSystem`. Do not use the legacy `Input` manager.
- **Render Pipeline:** This project uses the Universal Render Pipeline (**URP**). Ensure all materials, shaders, and rendering features are compatible with URP.

## Unity Best Practices

- **Performance (Caching):** Avoid using `GetComponent()`, `GameObject.Find()`, or `FindObjectOfType()` in `Update()` or `FixedUpdate()`. Cache references in `Awake()` or `Start()`.
- **Encapsulation:** Keep variables private or protected by default. Use `[SerializeField]` to expose them in the Unity Inspector instead of making them `public`.
- **Garbage Collection (GC):** Minimize memory allocations in hot paths (like `Update()`). Avoid frequent string concatenations, LINQ, or object instantiations within loops. Use Object Pooling for frequently spawned/destroyed items.
- **Event-Driven Architecture:** Avoid using `SendMessage()` or `BroadcastMessage()`. Prefer standard C# `event` / `Action` or `UnityEvent` for decoupled communication between components.
- **Physics:** Apply forces and handle Rigidbody updates inside `FixedUpdate()`, not `Update()`. Ensure framerate-independent movement by utilizing `Time.deltaTime` (in `Update`).
- **Enter Play Mode Settings (Domain Reload):** This project disables **Domain Reload** to speed up Play Mode iteration. You MUST ensure that all `static` variables, events, and singletons are properly reset. Use `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` to reset static state, or handle cleanup manually in `OnDestroy()` / `OnDisable()`.
