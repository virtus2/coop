# Coding Conventions (Naming & Style)

Adhere to standard C# and Unity naming conventions as outlined in the [Unity C# Scripting Guide](https://unity.com/how-to/naming-and-code-style-tips-c-scripting-unity#casing-terminology):
- **Classes, Structs, Enums, Methods, Properties, Events:** `PascalCase` (e.g., `PlayerController`, `TakeDamage()`)
- **Interfaces:** `PascalCase` starting with 'I' (e.g., `IDamageable`)
- **Private/Protected Fields:** `_camelCase` (e.g., `_currentHealth`, `_moveSpeed`). **Do NOT** use the `m_` prefix.
- **Local Variables & Parameters:** `camelCase` (e.g., `targetPosition`, `deltaTime`).
- **Booleans:** **Do NOT** use the `b` prefix for boolean values (e.g., use `isDead` instead of `bIsDead` or `bDead`).
- **Constants & Static Readonly:** `PascalCase` or `UPPER_SNAKE_CASE` (e.g., `MaxPlayers`, `MAX_PLAYERS`).

## Code Formatting

- **Braces `{}`:** Always place opening and closing braces on a new line (Allman style).

## Unity Best Practices

- **Performance (Caching):** Avoid using `GetComponent()`, `GameObject.Find()`, or `FindObjectOfType()` in `Update()` or `FixedUpdate()`. Cache references in `Awake()` or `Start()`.
- **Encapsulation:** Keep variables private or protected by default. Use `[SerializeField]` to expose them in the Unity Inspector instead of making them `public`.
- **Garbage Collection (GC):** Minimize memory allocations in hot paths (like `Update()`). Avoid frequent string concatenations, LINQ, or object instantiations within loops. Use Object Pooling for frequently spawned/destroyed items.
- **Event-Driven Architecture:** Avoid using `SendMessage()` or `BroadcastMessage()`. Prefer standard C# `event` / `Action` or `UnityEvent` for decoupled communication between components.
- **Physics:** Apply forces and handle Rigidbody updates inside `FixedUpdate()`, not `Update()`. Ensure framerate-independent movement by utilizing `Time.deltaTime` (in `Update`).
