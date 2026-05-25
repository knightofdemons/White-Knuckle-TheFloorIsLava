using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Rocks;

namespace ApiDump;

internal static class HandholdAnalyze
{
    public static void Run(string gameDir)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameDir);
        var asm = AssemblyDefinition.ReadAssembly(
            Path.Combine(gameDir, "Assembly-CSharp.dll"),
            new ReaderParameters { AssemblyResolver = resolver });

        foreach (var t in asm.MainModule.Types.Where(x => x.Name.StartsWith("CL_Handhold", StringComparison.Ordinal)))
        {
            Console.WriteLine($"=== {t.FullName} (base {t.BaseType?.Name}) ===");
            foreach (var f in t.Fields.OrderBy(f => f.Name))
                Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
        }

        var hm = asm.MainModule.GetType("HandholdManager");
        if (hm != null)
        {
            Console.WriteLine($"=== {hm.FullName} ===");
            foreach (var f in hm.Fields.OrderBy(f => f.Name))
                Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
            foreach (var nt in hm.NestedTypes)
            {
                Console.WriteLine($"  nested {nt.Name}");
                foreach (var f in nt.Fields.OrderBy(f => f.Name))
                    Console.WriteLine($"    field {f.FieldType.Name} {f.Name}");
            }
        }

        foreach (var name in new[]
                 {
                     "HandholdModule_LimitDirection", "HandholdModule_HandPlacement",
                     "HandholdModule_JumpBoost", "MonoBehaviourGizmos"
                 })
        {
            var t = asm.MainModule.GetType(name);
            if (t == null)
                continue;
            Console.WriteLine($"=== {t.FullName} ===");
            foreach (var f in t.Fields.OrderBy(f => f.Name))
                Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
        }
    }
}
