using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 인벤토리 시스템(ItemData 에셋, 절차적 아이콘 스프라이트, UI 프리팹, PlayerPrefab 컴포넌트 및 GameScene UI)을
/// 원클릭으로 완벽하게 구성하는 에디터 도구입니다.
/// </summary>
public static class SetupInventorySystem
{
    private const string ITEM_DATA_FOLDER = "Assets/Resources/ItemData";
    private const string ICONS_FOLDER = "Assets/Sprites/Icons";
    private const string PREFABS_FOLDER = "Assets/Prefabs";
    private const string SLOT_PREFAB_PATH = "Assets/Prefabs/InventorySlotPrefab.prefab";
    private const string INVENTORY_CANVAS_PREFAB_PATH = "Assets/Prefabs/InventoryCanvas.prefab";
    private const string PLAYER_PREFAB_PATH = "Assets/Prefabs/PlayerPrefab.prefab";
    private const string GAME_SCENE_PATH = "Assets/Scenes/GameScene.unity";

    [MenuItem("Tools/Setup Complete Inventory System")]
    public static void ExecuteCompleteSetup()
    {
        EnsureFolders();

        // 1. 아이콘 텍스처 및 스프라이트 생성
        var gunIcon = GetOrCreateIcon("Icon_Gun.png", new Color(0.95f, 0.35f, 0.25f, 1f), "GUN");
        var medkitIcon = GetOrCreateIcon("Icon_Medkit.png", new Color(0.25f, 0.85f, 0.45f, 1f), "MED");
        var chargedIcon = GetOrCreateIcon("Icon_Charged.png", new Color(0.75f, 0.3f, 0.95f, 1f), "CHG");
        var boxIcon = GetOrCreateIcon("Icon_Box.png", new Color(0.95f, 0.65f, 0.2f, 1f), "BOX");

        // 2. ItemData ScriptableObject 에셋 생성
        var gunData = GetOrCreateItemData("SampleGunItemData.asset", "item_gun", "샘플 권총", gunIcon, "단발 발사가 가능한 권총입니다.", 1, "Assets/Prefabs/SampleGun.prefab");
        var medkitData = GetOrCreateItemData("SampleMedkitItemData.asset", "item_medkit", "샘플 구급키트", medkitIcon, "좌클릭을 1.5초 홀드하여 체력을 회복합니다.", 5, "Assets/Prefabs/SampleMedkit.prefab");
        var chargedData = GetOrCreateItemData("SampleChargedWeaponItemData.asset", "item_charged_weapon", "샘플 차지 라이플", chargedIcon, "탭 클릭으로 기본 빔을 쏘고, 2초 홀드로 메가 레이저를 발사합니다.", 1, "Assets/Prefabs/SampleChargedWeapon.prefab");
        var boxData = GetOrCreateItemData("PickableBoxItemData.asset", "item_box", "보관 상자", boxIcon, "바닥에 내려놓거나 들 수 있는 상자입니다.", 10, "Assets/Prefabs/PickableBox.prefab");

        var initialItems = new List<ItemData> { gunData, medkitData, chargedData, boxData };

        // 3. 슬롯 UI 프리팹 생성
        var slotPrefab = CreateOrUpdateSlotPrefab();

        // 4. 인벤토리 캔버스 생성 및 프리팹화
        var canvasPrefab = CreateOrUpdateInventoryCanvasPrefab(slotPrefab);

        // 5. PlayerPrefab에 PlayerInventory 및 PlayerItemHolder 부착
        UpdatePlayerPrefab(initialItems);

        // 6. GameScene에 인벤토리 캔버스 배치
        SetupGameScene(canvasPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("<color=green>[SetupInventorySystem]</color> 인벤토리 및 툴바 시스템 전체 셋업 완료!");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }
        if (!AssetDatabase.IsValidFolder(ITEM_DATA_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Resources", "ItemData");
        }
        if (!AssetDatabase.IsValidFolder("Assets/Sprites"))
        {
            AssetDatabase.CreateFolder("Assets", "Sprites");
        }
        if (!AssetDatabase.IsValidFolder(ICONS_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets/Sprites", "Icons");
        }
        if (!AssetDatabase.IsValidFolder(PREFABS_FOLDER))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
    }

    /// <summary>
    /// 지정된 색상과 심볼 텍스처를 절차적으로 생성하여 Sprite로 반환합니다.
    /// </summary>
    private static Sprite GetOrCreateIcon(string fileName, Color baseColor, string label)
    {
        string fullPath = $"{ICONS_FOLDER}/{fileName}";
        var existingSprite = AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
        if (existingSprite != null)
        {
            return existingSprite;
        }

        int width = 64;
        int height = 64;
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        Color bgColor = new Color(baseColor.r * 0.4f, baseColor.g * 0.4f, baseColor.b * 0.4f, 0.95f);
        Color borderColor = baseColor;
        Color centerColor = Color.white;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 테두리
                if (x < 3 || x >= width - 3 || y < 3 || y >= height - 3)
                {
                    tex.SetPixel(x, y, borderColor);
                }
                // 중앙 포인트
                else if (x >= 20 && x <= 43 && y >= 20 && y <= 43)
                {
                    tex.SetPixel(x, y, baseColor);
                }
                else
                {
                    tex.SetPixel(x, y, bgColor);
                }
            }
        }
        tex.Apply();

        byte[] pngBytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);

        File.WriteAllBytes(fullPath, pngBytes);
        AssetDatabase.ImportAsset(fullPath, ImportAssetOptions.ForceUpdate);

        // TextureImporter 설정: Sprite (2D and UI)
        var importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Sprite>(fullPath);
    }

    private static ItemData GetOrCreateItemData(string assetName, string id, string name, Sprite icon, string desc, int maxStack, string prefabPath)
    {
        string fullPath = $"{ITEM_DATA_FOLDER}/{assetName}";
        var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(fullPath);
        if (itemData == null)
        {
            itemData = ScriptableObject.CreateInstance<ItemData>();
            AssetDatabase.CreateAsset(itemData, fullPath);
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        itemData.Initialize(id, name, icon, desc, maxStack, prefab, prefab);
        EditorUtility.SetDirty(itemData);

        return itemData;
    }

    /// <summary>
    /// 개별 슬롯 UI 프리팹 생성
    /// </summary>
    public static GameObject CreateOrUpdateSlotPrefab()
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var slotGO = new GameObject("InventorySlot");
        var slotRect = slotGO.AddComponent<RectTransform>();
        slotRect.sizeDelta = new Vector2(64f, 64f);

        // 1. Background
        var bgImage = slotGO.AddComponent<Image>();
        bgImage.color = new Color(0.12f, 0.14f, 0.18f, 0.92f);

        // 2. Icon
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(slotGO.transform, false);
        var iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.1f);
        iconRect.anchorMax = new Vector2(0.9f, 0.9f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var iconImage = iconGO.AddComponent<Image>();
        iconImage.raycastTarget = false;
        iconGO.SetActive(false);

        // 3. Highlight Outline (선택 테두리)
        var outlineGO = new GameObject("HighlightOutline");
        outlineGO.transform.SetParent(slotGO.transform, false);
        var outlineRect = outlineGO.AddComponent<RectTransform>();
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = new Vector2(-2f, -2f);
        outlineRect.offsetMax = new Vector2(2f, 2f);
        var outlineImage = outlineGO.AddComponent<Image>();
        outlineImage.color = new Color(1f, 0.85f, 0.2f, 0.9f); // 금색 테두리
        outlineImage.raycastTarget = false;

        // Outline 내부 투명 마스크(테두리 느낌을 주기 위해 약간 작은 배경 덮기)
        var innerGO = new GameObject("InnerMask");
        innerGO.transform.SetParent(outlineGO.transform, false);
        var innerRect = innerGO.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(3f, 3f);
        innerRect.offsetMax = new Vector2(-3f, -3f);
        var innerImg = innerGO.AddComponent<Image>();
        innerImg.color = new Color(0f, 0f, 0f, 0.01f);
        innerImg.raycastTarget = false;
        outlineGO.SetActive(false);

        // 4. Key Number Text (좌상단 1~0 번호)
        var keyTextGO = new GameObject("KeyText");
        keyTextGO.transform.SetParent(slotGO.transform, false);
        var keyRect = keyTextGO.AddComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 0.65f);
        keyRect.anchorMax = new Vector2(0.45f, 1f);
        keyRect.offsetMin = new Vector2(4f, 0f);
        keyRect.offsetMax = Vector2.zero;
        var keyText = keyTextGO.AddComponent<TextMeshProUGUI>();
        // keyText.font = font; /* TMP Font */
        keyText.text = "1";
        keyText.fontSize = 14;
        keyText.fontStyle = FontStyles.Bold;
        keyText.color = new Color(0.9f, 0.9f, 0.9f, 0.9f);
        keyText.alignment = TextAlignmentOptions.TopLeft;
        keyText.raycastTarget = false;

        // 5. Quantity Text (우하단 수량)
        var qtyTextGO = new GameObject("QuantityText");
        qtyTextGO.transform.SetParent(slotGO.transform, false);
        var qtyRect = qtyTextGO.AddComponent<RectTransform>();
        qtyRect.anchorMin = new Vector2(0.4f, 0f);
        qtyRect.anchorMax = new Vector2(1f, 0.45f);
        qtyRect.offsetMin = Vector2.zero;
        qtyRect.offsetMax = new Vector2(-4f, 0f);
        var qtyText = qtyTextGO.AddComponent<TextMeshProUGUI>();
        // qtyText.font = font; /* TMP Font */
        qtyText.text = "99";
        qtyText.fontSize = 14;
        qtyText.fontStyle = FontStyles.Bold;
        qtyText.color = Color.white;
        qtyText.alignment = TextAlignmentOptions.BottomRight;
        qtyText.raycastTarget = false;
        qtyTextGO.SetActive(false);

        // 6. Component 부착 및 필드 할당
        var slotUI = slotGO.AddComponent<InventorySlotUI>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(InventorySlotUI).GetField("_backgroundImage", flags)?.SetValue(slotUI, bgImage);
        typeof(InventorySlotUI).GetField("_iconImage", flags)?.SetValue(slotUI, iconImage);
        typeof(InventorySlotUI).GetField("_quantityText", flags)?.SetValue(slotUI, qtyText);
        typeof(InventorySlotUI).GetField("_keyNumberText", flags)?.SetValue(slotUI, keyText);
        typeof(InventorySlotUI).GetField("_highlightOutline", flags)?.SetValue(slotUI, outlineImage);

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(slotGO, SLOT_PREFAB_PATH);
        Object.DestroyImmediate(slotGO);

        return savedPrefab;
    }

    /// <summary>
    /// 인벤토리 Canvas 및 HUD 툴바, 팝업 창 프리팹 생성
    /// </summary>
    public static GameObject CreateOrUpdateInventoryCanvasPrefab(GameObject slotPrefab)
    {
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("InventoryCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60; // InteractionHUD(50)보다 위, PauseMenu(100)보다 아래

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasGO.AddComponent<GraphicRaycaster>();

        // ----------------------------------------------------
        // 1. HUD Toolbar (화면 하단 중앙 상시 노출)
        // ----------------------------------------------------
        var hudToolbarGO = new GameObject("HUD_Toolbar");
        hudToolbarGO.transform.SetParent(canvasGO.transform, false);
        var hudToolbarRect = hudToolbarGO.AddComponent<RectTransform>();
        hudToolbarRect.anchorMin = new Vector2(0.5f, 0f);
        hudToolbarRect.anchorMax = new Vector2(0.5f, 0f);
        hudToolbarRect.pivot = new Vector2(0.5f, 0f);
        hudToolbarRect.sizeDelta = new Vector2(740f, 80f);
        hudToolbarRect.anchoredPosition = new Vector2(0f, 30f);

        var hudToolbarBg = hudToolbarGO.AddComponent<Image>();
        hudToolbarBg.color = new Color(0.06f, 0.08f, 0.1f, 0.8f);

        var hudLayout = hudToolbarGO.AddComponent<HorizontalLayoutGroup>();
        hudLayout.spacing = 8f;
        hudLayout.padding = new RectOffset(10, 10, 8, 8);
        hudLayout.childAlignment = TextAnchor.MiddleCenter;
        hudLayout.childControlWidth = false;
        hudLayout.childControlHeight = false;
        hudLayout.childForceExpandWidth = false;
        hudLayout.childForceExpandHeight = false;

        // HUD 툴바 슬롯 10개 생성
        for (int i = 0; i < PlayerInventory.TOOLBAR_SIZE; i++)
        {
            var slotInstance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, hudToolbarGO.transform);
            slotInstance.name = $"HUD_ToolbarSlot_{i}";
            var slotUI = slotInstance.GetComponent<InventorySlotUI>();
            slotUI.Setup(SlotType.Toolbar, i);
        }

        // ----------------------------------------------------
        // 2. Inventory Window (Tab 키 토글 창)
        // ----------------------------------------------------
        var windowGO = new GameObject("InventoryWindow");
        windowGO.transform.SetParent(canvasGO.transform, false);
        var windowRect = windowGO.AddComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(760f, 540f);

        var windowBg = windowGO.AddComponent<Image>();
        windowBg.color = new Color(0.08f, 0.09f, 0.12f, 0.94f);

        // Window Title Text
        var titleGO = new GameObject("TitleText");
        titleGO.transform.SetParent(windowGO.transform, false);
        var titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.05f, 0.90f);
        titleRect.anchorMax = new Vector2(0.85f, 0.98f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        var titleText = titleGO.AddComponent<TextMeshProUGUI>();
        // titleText.font = font; /* TMP Font */
        titleText.text = "INVENTORY";
        titleText.fontSize = 26;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Left;

        // Instructions Hint Text
        var hintGO = new GameObject("HintText");
        hintGO.transform.SetParent(windowGO.transform, false);
        var hintRect = hintGO.AddComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.28f, 0.90f);
        hintRect.anchorMax = new Vector2(0.85f, 0.98f);
        hintRect.offsetMin = Vector2.zero;
        hintRect.offsetMax = Vector2.zero;
        var hintText = hintGO.AddComponent<TextMeshProUGUI>();
        // hintText.font = font; /* TMP Font */
        hintText.text = "[Tab / ESC] 닫기 | 마우스 클릭/드래그로 아이템 이동";
        hintText.fontSize = 14;
        hintText.color = new Color(0.7f, 0.8f, 0.9f, 0.8f);
        hintText.alignment = TextAlignmentOptions.Right;

        // Close Button (X)
        var closeBtnGO = new GameObject("CloseButton");
        closeBtnGO.transform.SetParent(windowGO.transform, false);
        var closeBtnRect = closeBtnGO.AddComponent<RectTransform>();
        closeBtnRect.anchorMin = new Vector2(0.93f, 0.91f);
        closeBtnRect.anchorMax = new Vector2(0.98f, 0.97f);
        closeBtnRect.offsetMin = Vector2.zero;
        closeBtnRect.offsetMax = Vector2.zero;
        var closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.6f, 0.2f, 0.2f, 1f);
        var closeBtn = closeBtnGO.AddComponent<Button>();

        var closeTxtGO = new GameObject("XText");
        closeTxtGO.transform.SetParent(closeBtnGO.transform, false);
        var closeTxtRect = closeTxtGO.AddComponent<RectTransform>();
        closeTxtRect.anchorMin = Vector2.zero;
        closeTxtRect.anchorMax = Vector2.one;
        closeTxtRect.offsetMin = Vector2.zero;
        closeTxtRect.offsetMax = Vector2.zero;
        var closeTxt = closeTxtGO.AddComponent<TextMeshProUGUI>();
        // closeTxt.font = font; /* TMP Font */
        closeTxt.text = "X";
        closeTxt.fontSize = 18;
        closeTxt.fontStyle = FontStyles.Bold;
        closeTxt.color = Color.white;
        closeTxt.alignment = TextAlignmentOptions.Center;

        // ----------------------------------------------------
        // 2-1. Grid Area (5x4)
        // ----------------------------------------------------
        var gridLabelGO = new GameObject("GridLabel");
        gridLabelGO.transform.SetParent(windowGO.transform, false);
        var gridLabelRect = gridLabelGO.AddComponent<RectTransform>();
        gridLabelRect.anchorMin = new Vector2(0.05f, 0.84f);
        gridLabelRect.anchorMax = new Vector2(0.95f, 0.89f);
        gridLabelRect.offsetMin = Vector2.zero;
        gridLabelRect.offsetMax = Vector2.zero;
        var gridLabelText = gridLabelGO.AddComponent<TextMeshProUGUI>();
        // gridLabelText.font = font; /* TMP Font */
        gridLabelText.text = "소지품 (그리드 인벤토리)";
        gridLabelText.fontSize = 16;
        gridLabelText.fontStyle = FontStyles.Bold;
        gridLabelText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        gridLabelText.alignment = TextAlignmentOptions.Left;

        var gridContainerGO = new GameObject("GridContainer");
        gridContainerGO.transform.SetParent(windowGO.transform, false);
        var gridContainerRect = gridContainerGO.AddComponent<RectTransform>();
        gridContainerRect.anchorMin = new Vector2(0.05f, 0.27f);
        gridContainerRect.anchorMax = new Vector2(0.95f, 0.83f);
        gridContainerRect.offsetMin = Vector2.zero;
        gridContainerRect.offsetMax = Vector2.zero;

        var gridLayout = gridContainerGO.AddComponent<GridLayoutGroup>();
        gridLayout.cellSize = new Vector2(64f, 64f);
        gridLayout.spacing = new Vector2(8f, 8f);
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        gridLayout.childAlignment = TextAnchor.UpperLeft;
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 5; // 가로 5칸 기본

        // 20개 기본 그리드 슬롯 생성
        for (int i = 0; i < 20; i++)
        {
            var slotInstance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, gridContainerGO.transform);
            slotInstance.name = $"GridSlot_{i}";
            var slotUI = slotInstance.GetComponent<InventorySlotUI>();
            slotUI.Setup(SlotType.Grid, i);
        }

        // ----------------------------------------------------
        // 2-2. Window Toolbar Area (창 내부 툴바 10칸)
        // ----------------------------------------------------
        var winTbLabelGO = new GameObject("WindowToolbarLabel");
        winTbLabelGO.transform.SetParent(windowGO.transform, false);
        var winTbLabelRect = winTbLabelGO.AddComponent<RectTransform>();
        winTbLabelRect.anchorMin = new Vector2(0.05f, 0.18f);
        winTbLabelRect.anchorMax = new Vector2(0.95f, 0.24f);
        winTbLabelRect.offsetMin = Vector2.zero;
        winTbLabelRect.offsetMax = Vector2.zero;
        var winTbLabelText = winTbLabelGO.AddComponent<TextMeshProUGUI>();
        // winTbLabelText.font = font; /* TMP Font */
        winTbLabelText.text = "단축키 툴바 (1~0 키 연동)";
        winTbLabelText.fontSize = 16;
        winTbLabelText.fontStyle = FontStyles.Bold;
        winTbLabelText.color = new Color(0.85f, 0.85f, 0.9f, 1f);
        winTbLabelText.alignment = TextAlignmentOptions.Left;

        var winTbContainerGO = new GameObject("WindowToolbarContainer");
        winTbContainerGO.transform.SetParent(windowGO.transform, false);
        var winTbContainerRect = winTbContainerGO.AddComponent<RectTransform>();
        winTbContainerRect.anchorMin = new Vector2(0.05f, 0.04f);
        winTbContainerRect.anchorMax = new Vector2(0.95f, 0.17f);
        winTbContainerRect.offsetMin = Vector2.zero;
        winTbContainerRect.offsetMax = Vector2.zero;

        var winTbLayout = winTbContainerGO.AddComponent<HorizontalLayoutGroup>();
        winTbLayout.spacing = 8f;
        winTbLayout.childAlignment = TextAnchor.MiddleLeft;
        winTbLayout.childControlWidth = false;
        winTbLayout.childControlHeight = false;
        winTbLayout.childForceExpandWidth = false;
        winTbLayout.childForceExpandHeight = false;

        for (int i = 0; i < PlayerInventory.TOOLBAR_SIZE; i++)
        {
            var slotInstance = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, winTbContainerGO.transform);
            slotInstance.name = $"Window_ToolbarSlot_{i}";
            var slotUI = slotInstance.GetComponent<InventorySlotUI>();
            slotUI.Setup(SlotType.Toolbar, i);
        }

        // ----------------------------------------------------
        // 3. Drag Ghost Object
        // ----------------------------------------------------
        var ghostGO = new GameObject("DragGhost");
        ghostGO.transform.SetParent(canvasGO.transform, false);
        var ghostRect = ghostGO.AddComponent<RectTransform>();
        ghostRect.sizeDelta = new Vector2(56f, 56f);
        var ghostImg = ghostGO.AddComponent<Image>();
        ghostImg.raycastTarget = false;
        ghostImg.color = new Color(1f, 1f, 1f, 0.8f);
        ghostGO.SetActive(false);

        // ----------------------------------------------------
        // 4. InventoryUIController 부착 및 연결
        // ----------------------------------------------------
        var controller = canvasGO.AddComponent<InventoryUIController>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(InventoryUIController).GetField("_inventoryWindow", flags)?.SetValue(controller, windowGO);
        typeof(InventoryUIController).GetField("_toolbarPanel", flags)?.SetValue(controller, hudToolbarGO);
        typeof(InventoryUIController).GetField("_closeButton", flags)?.SetValue(controller, closeBtn);
        typeof(InventoryUIController).GetField("_gridSlotsParent", flags)?.SetValue(controller, gridContainerGO.transform);
        typeof(InventoryUIController).GetField("_windowToolbarSlotsParent", flags)?.SetValue(controller, winTbContainerGO.transform);
        typeof(InventoryUIController).GetField("_hudToolbarSlotsParent", flags)?.SetValue(controller, hudToolbarGO.transform);
        typeof(InventoryUIController).GetField("_dragGhostObject", flags)?.SetValue(controller, ghostGO);
        typeof(InventoryUIController).GetField("_dragGhostImage", flags)?.SetValue(controller, ghostImg);
        typeof(InventoryUIController).GetField("_slotPrefab", flags)?.SetValue(controller, slotPrefab);

        windowGO.SetActive(false); // 기본 상태에서 창 닫힘

        var savedPrefab = PrefabUtility.SaveAsPrefabAsset(canvasGO, INVENTORY_CANVAS_PREFAB_PATH);
        Object.DestroyImmediate(canvasGO);

        return savedPrefab;
    }

    /// <summary>
    /// PlayerPrefab에 PlayerInventory 및 PlayerItemHolder 부착
    /// </summary>
    public static void UpdatePlayerPrefab(List<ItemData> initialItems)
    {
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_PREFAB_PATH);
        if (playerPrefab == null)
        {
            Debug.LogError($"[SetupInventorySystem] {PLAYER_PREFAB_PATH}을 찾을 수 없습니다.");
            return;
        }

        string assetPath = AssetDatabase.GetAssetPath(playerPrefab);
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);

        try
        {
            var inventory = prefabRoot.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                inventory = prefabRoot.AddComponent<PlayerInventory>();
            }

            var itemHolder = prefabRoot.GetComponent<PlayerItemHolder>();
            if (itemHolder == null)
            {
                itemHolder = prefabRoot.AddComponent<PlayerItemHolder>();
            }

            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(PlayerInventory).GetField("_gridWidth", flags)?.SetValue(inventory, 5);
            typeof(PlayerInventory).GetField("_gridHeight", flags)?.SetValue(inventory, 4);
            typeof(PlayerInventory).GetField("_selectedToolbarIndex", flags)?.SetValue(inventory, 0);

            if (initialItems != null)
            {
                inventory.SetInitialItems(initialItems);
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
            Debug.Log("[SetupInventorySystem] PlayerPrefab에 PlayerInventory 및 PlayerItemHolder 설정 완료.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    /// <summary>
    /// GameScene에 인벤토리 캔버스 배치
    /// </summary>
    public static void SetupGameScene(GameObject canvasPrefab)
    {
        Scene activeScene = SceneManager.GetActiveScene();
        bool opened = false;
        if (activeScene.path != GAME_SCENE_PATH)
        {
            activeScene = EditorSceneManager.OpenScene(GAME_SCENE_PATH, OpenSceneMode.Single);
            opened = true;
        }

        var existing = GameObject.Find("InventoryCanvas");
        if (existing != null)
        {
            Undo.DestroyObjectImmediate(existing);
        }

        var canvasInstance = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, activeScene);
        canvasInstance.name = "InventoryCanvas";
        Undo.RegisterCreatedObjectUndo(canvasInstance, "Create Inventory Canvas");

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);

        if (opened)
        {
            Debug.Log($"[SetupInventorySystem] {GAME_SCENE_PATH}에 InventoryCanvas 배치 완료.");
        }
    }
}

