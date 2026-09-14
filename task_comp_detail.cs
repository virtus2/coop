// Unity 6 CompilationPipeline을 통해 어셈블리 소스 파일 확인 및 컴파일 오류 체크
var sb = new System.Text.StringBuilder();
var assemblies = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(
    UnityEditor.Compilation.AssembliesType.Editor | UnityEditor.Compilation.AssembliesType.Player);
sb.AppendLine("Assembly count: " + assemblies.Length);

foreach (var asm in assemblies) {
    if (asm.name == "Assembly-CSharp") {
        sb.AppendLine("Name: " + asm.name);
        sb.AppendLine("Flags: " + asm.flags);
        sb.AppendLine("SourceFiles count: " + asm.sourceFiles.Length);
        foreach (var src in asm.sourceFiles) {
            sb.AppendLine("  " + src);
        }
    }
}
return sb.ToString();