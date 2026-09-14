var assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
var asmNames = new System.Collections.Generic.List<string>();
foreach (var a in assemblies) {
    var name = a.GetName().Name;
    if (name.Contains("Assembly") || name.Contains("Lobby") || name.Contains("Script")) {
        asmNames.Add(name);
    }
}
return string.Join(", ", asmNames);