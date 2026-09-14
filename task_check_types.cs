// Assembly.GetType()으로 직접 검색
var asm = System.Reflection.Assembly.Load("Assembly-CSharp");
var lobbyType = asm.GetType("LobbyNetworkSync");
var results = new System.Collections.Generic.List<string>();
results.Add("Assembly found: " + (asm != null ? "yes" : "no"));
results.Add("LobbyNetworkSync type: " + (lobbyType != null ? "found" : "null"));

// 모든 타입 중 Lobby 포함 타입 검색
foreach (var t in asm.GetTypes()) {
    if (t.Name.Contains("Lobby")) {
        results.Add("Found type: " + t.FullName);
    }
}
return string.Join("\n", results);