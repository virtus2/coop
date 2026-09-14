using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using Unity.Netcode;

public class CreateLobbyUI
{
    [MenuItem("Tools/Create Lobby UI")]
    public static void CreateUI()
    {
        var canvasGO = new GameObject("LobbyCanvas");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        var panelGO = new GameObject("LobbyPanel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelImg = panelGO.AddComponent<Image>();
        panelImg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        var panelRect = panelGO.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.2f, 0.2f);
        panelRect.anchorMax = new Vector2(0.8f, 0.8f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Text[] nameTexts = new Text[4];
        Text[] readyTexts = new Text[4];

        for (int i = 0; i < 4; i++) {
            var slot = new GameObject("Slot_" + i);
            slot.transform.SetParent(panelGO.transform, false);
            var slotRect = slot.AddComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0.1f, 0.7f - (i * 0.15f));
            slotRect.anchorMax = new Vector2(0.9f, 0.8f - (i * 0.15f));
            slotRect.offsetMin = Vector2.zero;
            slotRect.offsetMax = Vector2.zero;

            var nameGO = new GameObject("NameText");
            nameGO.transform.SetParent(slot.transform, false);
            var nameText = nameGO.AddComponent<Text>();
            nameText.font = font;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;
            nameText.fontSize = 24;
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 0);
            nameRect.anchorMax = new Vector2(0.5f, 1);
            nameRect.offsetMin = Vector2.zero;
            nameRect.offsetMax = Vector2.zero;
            nameTexts[i] = nameText;

            var readyGO = new GameObject("ReadyText");
            readyGO.transform.SetParent(slot.transform, false);
            var readyText = readyGO.AddComponent<Text>();
            readyText.font = font;
            readyText.color = Color.white;
            readyText.alignment = TextAnchor.MiddleRight;
            readyText.fontSize = 24;
            var readyRect = readyGO.GetComponent<RectTransform>();
            readyRect.anchorMin = new Vector2(0.5f, 0);
            readyRect.anchorMax = new Vector2(1f, 1);
            readyRect.offsetMin = Vector2.zero;
            readyRect.offsetMax = Vector2.zero;
            readyTexts[i] = readyText;
        }

        var readyBtnGO = new GameObject("ReadyButton");
        readyBtnGO.transform.SetParent(panelGO.transform, false);
        var readyBtnImg = readyBtnGO.AddComponent<Image>();
        var readyBtn = readyBtnGO.AddComponent<Button>();
        var readyBtnRect = readyBtnGO.GetComponent<RectTransform>();
        readyBtnRect.anchorMin = new Vector2(0.1f, 0.05f);
        readyBtnRect.anchorMax = new Vector2(0.45f, 0.15f);
        readyBtnRect.offsetMin = Vector2.zero;
        readyBtnRect.offsetMax = Vector2.zero;
        var readyTxtGO = new GameObject("Text");
        readyTxtGO.transform.SetParent(readyBtnGO.transform, false);
        var readyBtnText = readyTxtGO.AddComponent<Text>();
        readyBtnText.font = font;
        readyBtnText.text = "Toggle Ready";
        readyBtnText.color = Color.black;
        readyBtnText.alignment = TextAnchor.MiddleCenter;
        var rbtRect = readyTxtGO.GetComponent<RectTransform>();
        rbtRect.anchorMin = Vector2.zero; rbtRect.anchorMax = Vector2.one;
        rbtRect.offsetMin = Vector2.zero; rbtRect.offsetMax = Vector2.zero;

        var startBtnGO = new GameObject("StartButton");
        startBtnGO.transform.SetParent(panelGO.transform, false);
        var startBtnImg = startBtnGO.AddComponent<Image>();
        var startBtn = startBtnGO.AddComponent<Button>();
        var startBtnRect = startBtnGO.GetComponent<RectTransform>();
        startBtnRect.anchorMin = new Vector2(0.55f, 0.05f);
        startBtnRect.anchorMax = new Vector2(0.9f, 0.15f);
        startBtnRect.offsetMin = Vector2.zero;
        startBtnRect.offsetMax = Vector2.zero;
        var startTxtGO = new GameObject("Text");
        startTxtGO.transform.SetParent(startBtnGO.transform, false);
        var startBtnText = startTxtGO.AddComponent<Text>();
        startBtnText.font = font;
        startBtnText.text = "Start Game";
        startBtnText.color = Color.black;
        startBtnText.alignment = TextAnchor.MiddleCenter;
        var sbtRect = startTxtGO.GetComponent<RectTransform>();
        sbtRect.anchorMin = Vector2.zero; sbtRect.anchorMax = Vector2.one;
        sbtRect.offsetMin = Vector2.zero; sbtRect.offsetMax = Vector2.zero;

        var netObj = panelGO.AddComponent<NetworkObject>();
        var manager = panelGO.AddComponent<LobbyManager>();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        manager.GetType().GetField("_playerNameTexts", flags).SetValue(manager, nameTexts);
        manager.GetType().GetField("_playerReadyTexts", flags).SetValue(manager, readyTexts);
        manager.GetType().GetField("_readyButton", flags).SetValue(manager, readyBtn);
        manager.GetType().GetField("_startGameButton", flags).SetValue(manager, startBtn);
        manager.GetType().GetField("_lobbyUIPanel", flags).SetValue(manager, panelGO);
        
        Selection.activeGameObject = canvasGO;
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
}
