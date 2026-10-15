using System;
using Mono.Cecil;
class Inspect {
 static void Main(string[] args) {
 var asm=AssemblyDefinition.ReadAssembly(args[0]);
 foreach(var t in asm.MainModule.GetTypes()) {
 if(!System.Text.RegularExpressions.Regex.IsMatch(t.FullName,args[1])) continue;
 Console.WriteLine("TYPE "+t.FullName+" : "+t.BaseType);
 foreach(var f in t.Fields) Console.WriteLine(" FIELD "+f.FieldType+" "+f.Name+(f.HasConstant?" = "+f.Constant:""));
 foreach(var m in t.Methods) Console.WriteLine(" "+m.FullName);
 }
 }
}
