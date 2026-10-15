using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
class VerifyMagic
{
    static void Need(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Main(string[] args)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(args[1]); resolver.AddSearchDirectory("rustbuddy/bin");
        var rp = new ReaderParameters { AssemblyResolver = resolver };
        using (var plugin = AssemblyDefinition.ReadAssembly(args[0], rp))
        using (var game = AssemblyDefinition.ReadAssembly(Path.Combine(args[1], "Assembly-CSharp.dll"), rp))
        {
            int count = 0;
            foreach (var type in plugin.MainModule.Types)
            foreach (var attribute in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
            {
                var args2 = attribute.ConstructorArguments;
                if (args2.Count < 2 || !(args2[0].Value is TypeReference) || !(args2[1].Value is string)) continue;
                var target = ((TypeReference)args2[0].Value).Resolve(); string name = (string)args2[1].Value;
                var candidates = target.Methods.Where(m => m.Name == name).ToList();
                if (args2.Count >= 3 && args2[2].Value is CustomAttributeArgument[])
                {
                    var pars = (CustomAttributeArgument[])args2[2].Value;
                    candidates = candidates.Where(m => m.Parameters.Count == pars.Length && m.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(pars.Select(p => ((TypeReference)p.Value).FullName))).ToList();
                }
                Need(candidates.Count == 1, "Harmony target missing/ambiguous: " + target.FullName + "::" + name);
                count++;
            }
            Need(plugin.MainModule.Resources.Any(r => r.Name == "RustPG.frost.png"), "missing frost texture resource");
            var modifiers = game.MainModule.Types.First(t => t.Name == "BaseModifiers`1");
            Need(modifiers.Methods.Any(m => m.Name == "Add" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "Modifier"), "native modifier Add unavailable");
            Need(modifiers.Methods.Any(m => m.Name == "Remove" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.Name == "Modifier"), "native modifier Remove unavailable");
            foreach (string name in new [] { "All", "totalValues" }) Need(modifiers.Fields.Any(f => f.Name == name), "native modifier field missing: " + name);
            foreach (string name in new [] { "AdjustSourceCount", "ApplyModifierValue", "SetDirty" }) Need(modifiers.Methods.Count(m => m.Name == name) == 1, "native modifier method missing/ambiguous: " + name);
            Need(game.MainModule.Types.First(t => t.Name == "SmokeGrenade").Fields.Any(f => f.Name == "activeGrenades" && f.IsStatic), "smoke registry unavailable");
            var methods = plugin.MainModule.Types.SelectMany(t => t.Methods).Where(m => m.HasBody);
            Need(!methods.SelectMany(m => m.Body.Instructions).Any(i => i.Operand as string == "assets/bundled/prefabs/fx/player/onfire.prefab"), "unmanaged repeated fire effect remains");
            Need(game.MainModule.Types.First(t => t.Name == "Item").Methods.Where(m => m.Name == "Save" && m.HasBody).SelectMany(m => m.Body.Instructions).Any(i => i.Operand is FieldReference && ((FieldReference)i.Operand).FullName == "System.String Item::text"), "item loadout text not serialized by game");
            Console.WriteLine("PASS: " + count + " Harmony targets resolve uniquely; native modifier access, frost resource, item text serialization and fire cleanup migration verified");
        }
    }
}
