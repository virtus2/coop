var nmGO = UnityEngine.GameObject.Find("NetworkManager");
if (nmGO == null) return "ERROR: NetworkManager GameObject not found";
var syncType = System.Type.GetType("LobbyNetworkSync, Assembly-CSharp");
if (syncType != null) {
    var existing = nmGO.GetComponent(syncType);
    if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
    nmGO.AddComponent(syncType);
    var existingNetObj = nmGO.GetComponent<Unity.Netcode.NetworkObject>();
    if (existingNetObj == null) nmGO.AddComponent<Unity.Netcode.NetworkObject>();
    return "LobbyNetworkSync added to NetworkManager";
} else {
    return "ERROR: LobbyNetworkSync type not found - scripts may not be compiled yet";
}