var canvasGO = UnityEngine.GameObject.Find("LobbyCanvas");
if (canvasGO != null) {
    var oldNetObj = canvasGO.GetComponent<Unity.Netcode.NetworkObject>();
    if (oldNetObj != null) UnityEngine.Object.DestroyImmediate(oldNetObj);
    var mgrType = System.Type.GetType("LobbyManager, Assembly-CSharp");
    if (mgrType != null) { var c = canvasGO.GetComponent(mgrType); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
    var syncType = System.Type.GetType("LobbyNetworkSync, Assembly-CSharp");
    if (syncType != null) { var c = canvasGO.GetComponent(syncType); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
    var uiType = System.Type.GetType("LobbyUIController, Assembly-CSharp");
    if (uiType != null) { var c = canvasGO.GetComponent(uiType); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
}
var panelGO = canvasGO != null ? canvasGO.transform.Find("LobbyPanel")?.gameObject : null;
if (panelGO != null) {
    var oldNetObj2 = panelGO.GetComponent<Unity.Netcode.NetworkObject>();
    if (oldNetObj2 != null) UnityEngine.Object.DestroyImmediate(oldNetObj2);
    var mgrType2 = System.Type.GetType("LobbyManager, Assembly-CSharp");
    if (mgrType2 != null) { var c = panelGO.GetComponent(mgrType2); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
}
return "Cleanup done";