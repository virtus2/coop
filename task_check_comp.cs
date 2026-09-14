// 현재 프로젝트 컴파일 오류 확인
var assembly = UnityEditor.Compilation.CompilationPipeline.GetAssemblies();
var results = new System.Collections.Generic.List<string>();
foreach (var asm in assembly) {
    if (asm.name == "Assembly-CSharp") {
        results.Add("Assembly-CSharp found");
        results.Add("Source files count: " + asm.sourceFiles.Length);
        foreach (var src in asm.sourceFiles) {
            if (src.Contains("Lobby") || src.Contains("Network")) {
                results.Add("  - " + src);
            }
        }
    }
}
return string.Join("\n", results);