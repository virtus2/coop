// The SteamManager is designed to work with Steamworks.NET
// This file is provided as a foundational script for Unity projects.

using UnityEngine;
using Steamworks;

[DisallowMultipleComponent]
public class SteamManager : MonoBehaviour {
    protected static SteamManager s_instance;
    public static SteamManager Instance {
        get {
            if (s_instance == null) {
                return new GameObject("SteamManager").AddComponent<SteamManager>();
            }
            return s_instance;
        }
    }

    protected static bool s_EverInitialized = false;

    protected bool m_bInitialized = false;
    public static bool Initialized {
        get {
            return Instance.m_bInitialized;
        }
    }

    protected virtual void Awake() {
        if (s_instance != null) {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        DontDestroyOnLoad(gameObject);

        if (!s_EverInitialized) {
#if UNITY_EDITOR
            Debug.Log("[Steamworks.NET] Disabled in Editor to prevent Steam from locking the app in a 'Playing' state.");
            return;
#endif
            // This is almost always an error.
            if (!Packsize.Test()) {
                Debug.LogError("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this);
            }

            if (!DllCheck.Test()) {
                Debug.LogError("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this);
            }
        }

        try {
            // If Steam is not running or the game wasn't started through Steam, SteamAPI_RestartAppIfNecessary starts the
            // Steam client and also launches this game again if the User owns it. This can act as a rudimentary form of DRM.
            if (SteamAPI.RestartAppIfNecessary((AppId_t)480)) {
                Application.Quit();
                return;
            }
        }
        catch (System.DllNotFoundException e) { // We catch this exception here, as it will be the first occurrence of it.
            Debug.LogError("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the readme for more details.\n" + e, this);
            Application.Quit();
            return;
        }

        // Initializes the Steamworks API.
        m_bInitialized = SteamAPI.Init();
        if (!m_bInitialized) {
            Debug.LogError("[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", this);
            return;
        }

        s_EverInitialized = true;
    }

    protected virtual void OnEnable() {
        if (s_instance == null) {
            s_instance = this;
        }

        if (!m_bInitialized) {
            return;
        }

        // Hook up any warning messages from Steam if needed here.
    }

    protected virtual void OnDestroy() {
        if (s_instance != this) {
            return;
        }

        s_instance = null;

        if (!m_bInitialized) {
            return;
        }

        SteamAPI.Shutdown();
    }

    protected virtual void Update() {
        if (!m_bInitialized) {
            return;
        }

        // Run Steam client callbacks
        SteamAPI.RunCallbacks();
    }
}
