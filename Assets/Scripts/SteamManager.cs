// The SteamManager is designed to work with Steamworks.NET
// This file is provided as a foundational script for Unity projects.

using UnityEngine;
using Steamworks;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class SteamManager : MonoBehaviour
{
    protected static SteamManager _instance;
    public static SteamManager Instance
    {
        get
        {
            if (_instance == null)
            {
                return new GameObject("SteamManager").AddComponent<SteamManager>();
            }
            return _instance;
        }
    }

    protected static bool _everInitialized = false;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic()
    {
        _instance = null;
        _everInitialized = false;
    }

    protected bool _isInitialized = false;
    public static bool Initialized
    {
        get
        {
            if (_instance == null)
            {
                return false;
            }
            return _instance._isInitialized;
        }
    }

    protected virtual void Awake()
    {
        if (_instance != null)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (!_everInitialized)
        {
            // This is almost always an error.
            if (!Packsize.Test())
            {
                Debug.LogError("[Steamworks.NET] Packsize Test returned false, the wrong version of Steamworks.NET is being run in this platform.", this);
            }

            if (!DllCheck.Test())
            {
                Debug.LogError("[Steamworks.NET] DllCheck Test returned false, One or more of the Steamworks binaries seems to be the wrong version.", this);
            }
        }

        try
        {
            // NOTE: 개발/테스트(Spacewar 480) 단계에서는 RestartAppIfNecessary가 게임을 종료(Application.Quit)시키는 현상을 방지하기 위해 비활성화합니다.
            // 스팀 스토어 정식 출시 및 DRM 적용 시 필요에 따라 활성화하십시오.
            /*
            if (SteamAPI.RestartAppIfNecessary((AppId_t)480))
            {
                Application.Quit();
                return;
            }
            */
        }
        catch (System.DllNotFoundException e)
        { // We catch this exception here, as it will be the first occurrence of it.
            Debug.LogError("[Steamworks.NET] Could not load [lib]steam_api.dll/so/dylib. It's likely not in the correct location. Refer to the readme for more details.\n" + e, this);
            Application.Quit();
            return;
        }

        // Initializes the Steamworks API.
        _isInitialized = SteamAPI.Init();
        if (!_isInitialized)
        {
            Debug.LogError("[Steamworks.NET] SteamAPI_Init() failed. Refer to Valve's documentation or the comment above this line for more information.", this);
            return;
        }

        Debug.Log("[Steamworks.NET] SteamAPI_Init successful.");
        _everInitialized = true;
    }

    protected virtual void OnEnable()
    {
        if (_instance == null)
        {
            _instance = this;
        }

        if (!_isInitialized)
        {
            return;
        }

        // Hook up any warning messages from Steam if needed here.
    }

    protected virtual void OnDestroy()
    {
        if (_instance != this)
        {
            return;
        }

        _instance = null;

        if (!_isInitialized)
        {
            return;
        }

        SteamAPI.Shutdown();
    }

    protected virtual void Update()
    {
        if (!_isInitialized)
        {
            return;
        }

        // Run Steam client callbacks
        SteamAPI.RunCallbacks();
    }
}
