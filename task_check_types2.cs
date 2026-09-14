var asm = System.Reflection.Assembly.Load("Assembly-CSharp");
var results = new System.Collections.Generic.List<string>();
// 전체 타입 목록 (UI, Lobby 관련)
foreach (var t in asm.GetTypes()) {
    if (t.Name.Contains("Lobby") || t.Name.Contains("UI") || t.Name.Contains("Network")) {
        results.Add(t.Name);
    }
}
return string.Join(", ", results);