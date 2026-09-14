var asm = System.Reflection.Assembly.Load("Assembly-CSharp");
var allTypes = asm.GetTypes();
var results = new System.Collections.Generic.List<string>();
results.Add("Total types in Assembly-CSharp: " + allTypes.Length);
foreach (var t in allTypes) {
    results.Add(t.Name);
}
return string.Join(", ", results);