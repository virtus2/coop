// 컴파일 오류 확인 - 현재 편집기의 컴파일러 오류 메시지
var messages = new System.Collections.Generic.List<string>();
foreach (var msg in UnityEditor.EditorUtility.compilationLog ?? new string[0]) {
    messages.Add(msg);
}
return "Count: " + messages.Count + " - " + string.Join("; ", messages);