// 현재 편집기 컴파일 오류 메시지 확인
var errors = UnityEditor.Compilation.CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Assembly-CSharp");
return errors ?? "null";