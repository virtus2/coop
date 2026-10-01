using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// PlayerPrefab에 World Space uGUI 이름표(NamePlate)와 PlayerNamePlate 컴포넌트를 구성하는 에디터 유틸리티입니다.
/// </summary>
public static class SetupPlayerNamePlate
{
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";

    [MenuItem("Tools/Coop/Setup Player NamePlate on Prefab", false, 30)]
    public static void SetupOnPrefab()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (playerPrefab == null)
        {
            Debug.LogError($"[SetupPlayerNamePlate] {PLAYER_PREFAB_PATH}을 찾을 수 없습니다.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(playerPrefab);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            // 1. PlayerNamePlate 컴포넌트 추가/가져오기
            var playerNamePlate = prefabRoot.GetComponent<PlayerNamePlate>();
            if (playerNamePlate == null)
            {
                playerNamePlate = prefabRoot.AddComponent<PlayerNamePlate>();
            }

            // 2. NamePlateCanvas 탐색 또는 생성
            Transform canvasTransform = prefabRoot.transform.Find("NamePlateCanvas");
            GameObject canvasGO;
            if (canvasTransform == null)
            {
                canvasGO = new GameObject("NamePlateCanvas");
                canvasGO.transform.SetParent(prefabRoot.transform, false);
            }
            else
            {
                canvasGO = canvasTransform.gameObject;
            }

            canvasGO.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            canvasGO.transform.localRotation = Quaternion.identity;
            canvasGO.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var canvas = canvasGO.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = canvasGO.AddComponent<Canvas>();
            }
            canvas.renderMode = RenderMode.WorldSpace;

            var rectTransform = canvasGO.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                rectTransform.sizeDelta = new Vector2(250f, 60f);
            }

            // 3. NameText 텍스트 컴포넌트 구성
            Transform textTransform = canvasGO.transform.Find("NameText");
            GameObject textGO;
            if (textTransform == null)
            {
                textGO = new GameObject("NameText");
                textGO.transform.SetParent(canvasGO.transform, false);
            }
            else
            {
                textGO = textTransform.gameObject;
            }

            var textRect = textGO.GetComponent<RectTransform>();
            if (textRect == null)
            {
                textRect = textGO.AddComponent<RectTransform>();
            }
            textRect.sizeDelta = new Vector2(250f, 60f);
            textRect.anchoredPosition = Vector2.zero;

            var nameText = textGO.GetComponent<TMP_Text>();
            if (nameText == null)
            {
                nameText = textGO.AddComponent<TextMeshProUGUI>();
            }
            // // nameText.font = null; /* TMP Font */ /* TMP font */
            nameText.fontSize = 24;
            nameText.fontStyle = FontStyles.Bold;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = Color.white;
            nameText.text = "Player";

            var outline = textGO.GetComponent<Outline>();
            if (outline == null)
            {
                outline = textGO.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // 4. 필드 참조 연결 (Reflection 사용)
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(PlayerNamePlate).GetField("_canvas", flags)?.SetValue(playerNamePlate, canvas);
            typeof(PlayerNamePlate).GetField("_nameText", flags)?.SetValue(playerNamePlate, nameText);
            typeof(PlayerNamePlate).GetField("_offset", flags)?.SetValue(playerNamePlate, new Vector3(0f, 2.2f, 0f));
            typeof(PlayerNamePlate).GetField("_showLocalPlayerName", flags)?.SetValue(playerNamePlate, false);

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[SetupPlayerNamePlate] PlayerPrefab에 NamePlate Canvas, Text 및 PlayerNamePlate 설정이 성공적으로 저장되었습니다.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }
}

