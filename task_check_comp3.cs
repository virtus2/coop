// HasCompileErrors 및 각 어셈블리 컴파일 결과 확인
bool hasErrors = UnityEditor.EditorUtility.scriptCompilationFailed;
var results = new System.Collections.Generic.List<string>();
results.Add("ScriptCompilationFailed: " + hasErrors);

// 더 직접적인 방법 - 오류 메시지 가져오기
var messages = new System.Collections.Generic.List<string>();
foreach (UnityEditor.Compilation.AssemblyDefinitionAsset asset in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEditor.Compilation.AssemblyDefinitionAsset>()) {
    messages.Add("AsmDef: " + asset.name);
}
results.Add("AsmDefs: " + string.Join(", ", messages));
return string.Join("\n", results);