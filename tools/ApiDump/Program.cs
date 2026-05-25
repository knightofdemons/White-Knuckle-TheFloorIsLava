using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Rocks;

if (args.Length > 0 && args[0] == "handholds")
{
    ApiDump.HandholdAnalyze.Run(args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed");
    return;
}

if (args.Length > 0 && args[0] == "nest")
{
    var d = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var rr = new DefaultAssemblyResolver();
    rr.AddSearchDirectory(d);
    var aa = AssemblyDefinition.ReadAssembly(Path.Combine(d, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = rr });
    var parent = aa.MainModule.GetType("ENT_Player");
    if (parent == null) { Console.WriteLine("missing ENT_Player"); return; }
    foreach (var t in parent.NestedTypes)
    {
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var f in t.Fields.OrderBy(f => f.Name).Take(60))
            Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
        foreach (var m in t.Methods.Where(x => x.HasBody && !x.IsSpecialName).OrderBy(m => m.Name).Take(60))
            Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
    }
    return;
}

if (args.Length > 0 && args[0] == "findtype")
{
    var d = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var search = args.Length > 2 ? args[2] : "Hand";
    var rr = new DefaultAssemblyResolver();
    rr.AddSearchDirectory(d);
    foreach (var dll in Directory.GetFiles(d, "*.dll"))
    {
        try
        {
            var aa = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { AssemblyResolver = rr });
            foreach (var t in aa.MainModule.GetTypes().Where(x => x.Name == search))
                Console.WriteLine($"Found {t.FullName} in {Path.GetFileName(dll)}");
        }
        catch { }
    }
    return;
}

if (args.Length > 0 && args[0] == "grab")
{
    var d = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var rr = new DefaultAssemblyResolver();
    rr.AddSearchDirectory(d);
    var aa = AssemblyDefinition.ReadAssembly(Path.Combine(d, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = rr });
    foreach (var t in aa.MainModule.Types.Where(x => x.Name.Contains("Hand") || x.Name.Contains("Interaction") || x.Name == "InteractionInfo"))
    {
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var f in t.Fields.OrderBy(f => f.Name))
            Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
        foreach (var m in t.Methods.Where(m => m.HasBody && !m.IsSpecialName).OrderBy(m => m.Name).Take(40))
            Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
    }
    return;
}

if (args.Length > 0 && args[0] == "body")
{
    var d = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var typeName = args.Length > 2 ? args[2] : "CL_Prop";
    var methodFilter = args.Length > 3 ? args[3] : "Start|Initialize|Pickup|Interact|CanInteract";
    var rr = new DefaultAssemblyResolver();
    rr.AddSearchDirectory(d);
    var aa = AssemblyDefinition.ReadAssembly(Path.Combine(d, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = rr });
    var t = aa.MainModule.GetType(typeName) ?? aa.MainModule.Types.FirstOrDefault(x => x.Name == typeName);
    if (t == null) { Console.WriteLine($"MISSING {typeName}"); return; }
    var filter = new System.Text.RegularExpressions.Regex(methodFilter);
    foreach (var m in t.Methods.Where(m => m.HasBody && filter.IsMatch(m.Name)))
    {
        Console.WriteLine($"=== {t.Name}.{m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))}) ===");
        foreach (var ins in m.Body.Instructions)
            Console.WriteLine($"  {ins}");
    }
    return;
}

if (args.Length > 0 && args[0] == "prop")
{
    var d = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var rr = new DefaultAssemblyResolver();
    rr.AddSearchDirectory(d);
    var aa = AssemblyDefinition.ReadAssembly(Path.Combine(d, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = rr });
    string[] types = { "CL_Prop", "GameEntity", "Damageable", "ENT_Player" };
    foreach (var n in types)
    {
        var t = aa.MainModule.GetType(n) ?? aa.MainModule.Types.FirstOrDefault(x => x.Name == n);
        if (t == null) { Console.WriteLine($"MISSING {n}"); continue; }
        Console.WriteLine($"=== {t.FullName} (base={t.BaseType?.Name}) ===");
        foreach (var f in t.Fields.OrderBy(f => f.Name))
            Console.WriteLine($"  field {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");
        foreach (var m in t.Methods.Where(m => m.HasBody && !m.IsSpecialName).OrderBy(m => m.Name))
            Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
        if (!string.IsNullOrEmpty(t.BaseType?.FullName) && t.BaseType.FullName != "UnityEngine.MonoBehaviour")
        {
            var bt = t.BaseType.Resolve();
            if (bt != null)
            {
                Console.WriteLine($"-- base {bt.FullName} fields --");
                foreach (var f in bt.Fields.OrderBy(f => f.Name).Take(40))
                    Console.WriteLine($"    {f.FieldType.Name} {f.Name}");
            }
        }
    }
    return;
}

if (args.Length > 0 && args[0] == "hand")
{
    var d = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var rr = new DefaultAssemblyResolver();
    rr.AddSearchDirectory(d);
    var aa = AssemblyDefinition.ReadAssembly(Path.Combine(d, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = rr });
    var ent = aa.MainModule.GetType("ENT_Player");
    var hnd = ent.NestedTypes.FirstOrDefault(x => x.Name == "Hand");
    if (hnd != null)
    {
        Console.WriteLine("=== Hand ALL fields ===");
        foreach (var f in hnd.Fields.OrderBy(f => f.Name))
            Console.WriteLine($"  {f.FieldType.Name} {f.Name}");
        Console.WriteLine("=== Hand ALL methods ===");
        foreach (var m in hnd.Methods.Where(m => m.HasBody).OrderBy(m => m.Name))
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
    }
    return;
}

if (args.Length > 0 && args[0] == "probe")
{
    var dir = args.Length > 1 ? args[1] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
    var r = new DefaultAssemblyResolver();
    r.AddSearchDirectory(dir);
    var a = AssemblyDefinition.ReadAssembly(Path.Combine(dir, "Assembly-CSharp.dll"), new ReaderParameters { AssemblyResolver = r });
    var pattern = args.Length > 2 ? args[2] : "crouch|view|look|pitch|input|down|prop";
    string[] types = { "ENT_Player", "CL_CameraControl", "CL_Prop", "ViewSway" };
    foreach (var n in types)
    {
        var t = a.MainModule.GetType(n) ?? a.MainModule.Types.FirstOrDefault(x => x.Name == n);
        if (t == null) { Console.WriteLine($"MISSING {n}"); continue; }
        Console.WriteLine($"=== {t.FullName} ===");
        foreach (var f in t.Fields.OrderBy(f => f.Name))
            if (Regex.IsMatch(f.Name, pattern, RegexOptions.IgnoreCase))
                Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
        foreach (var m in t.Methods.Where(m => m.HasBody).OrderBy(m => m.Name))
            if (Regex.IsMatch(m.Name, pattern, RegexOptions.IgnoreCase))
                Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
    }
    return;
}

var gameDir = args.Length > 0 ? args[0] : @"c:\Users\TFS\Documents\projects\White Knuckle\White Knuckle_Data\Managed";
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(gameDir);
var reader = new ReaderParameters { AssemblyResolver = resolver };
var asm = AssemblyDefinition.ReadAssembly(Path.Combine(gameDir, "Assembly-CSharp.dll"), reader);
string[] names = ["ENT_Player", "Damageable", "GameEntity", "UT_DamagePlayer", "CL_Handhold_Damage", "HandholdManager", "OS_GameManager", "SessionManager"];
foreach (var n in names)
{
    var t = asm.MainModule.GetType(n) ?? asm.MainModule.Types.FirstOrDefault(x => x.FullName == n || x.Name == n);
    if (t == null) { Console.WriteLine($"MISSING: {n}"); continue; }
    Console.WriteLine($"=== {t.FullName} ===");
    foreach (var f in t.Fields.OrderBy(f => f.Name))
        if (Regex.IsMatch(f.Name, "health|stamina|damage|ground|grab|Hand|Health|Stamina|Damage|Ground", RegexOptions.IgnoreCase))
            Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
    foreach (var m in t.Methods.Where(m => !m.IsSpecialName && m.HasBody).OrderBy(m => m.Name))
        if (Regex.IsMatch(m.Name, "health|stamina|damage|ground|grab|Hand|Health|Stamina|Damage|Ground|Scene|Load|Start|Update", RegexOptions.IgnoreCase))
            Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
}

var player = asm.MainModule.GetType("ENT_Player");
if (player != null)
{
    var hand = player.NestedTypes.FirstOrDefault(x => x.Name == "Hand");
    if (hand != null)
    {
        Console.WriteLine($"=== {hand.FullName} ===");
        foreach (var f in hand.Fields.OrderBy(f => f.Name))
            Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
    }
}

foreach (var t in asm.MainModule.Types.Where(x => x.Name.Contains("Spike", StringComparison.OrdinalIgnoreCase) || x.Name.Contains("Hazard", StringComparison.OrdinalIgnoreCase)).Take(20))
    Console.WriteLine($"TYPE: {t.FullName}");

foreach (var t in asm.MainModule.Types.Where(x => x.Name == "OS_GameManager" || x.Name.Contains("GameManager") || x.Name.Contains("RoomManager") || x.Name.Contains("LevelManager")).Take(15))
{
    Console.WriteLine($"=== {t.FullName} ===");
    foreach (var m in t.Methods.Where(m => Regex.IsMatch(m.Name, "Load|Scene|Room|Start|Init", RegexOptions.IgnoreCase) && m.HasBody).Take(12))
        Console.WriteLine($"  method {m.Name}");
}

var p2 = asm.MainModule.GetType("ENT_Player");
Console.WriteLine("Base: " + p2.BaseType.FullName);
foreach (var m in p2.Methods.Where(m => m.Name.Contains("Instance") || m.Name == "Damage").Take(15))
    Console.WriteLine($"  {m.ReturnType.Name} {m.Name}");
var ut2 = asm.MainModule.GetType("UT_GetPlayer");
if (ut2 != null)
    foreach (var m in ut2.Methods.Where(x => x.HasBody))
        Console.WriteLine($"  UT_GetPlayer {m.ReturnType.Name} {m.Name}");

var hh = asm.MainModule.GetType("CL_Handhold");
if (hh != null)
{
    Console.WriteLine($"=== CL_Handhold base:{hh.BaseType?.Name} ===");
    foreach (var f in hh.Fields.OrderBy(f => f.Name).Take(40))
        Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
}
foreach (var n in new[] { "CL_GameManager", "SessionManager", "RunController", "GameSession", "LevelManager" })
{
    var t = asm.MainModule.Types.FirstOrDefault(x => x.Name == n);
    if (t == null) { Console.WriteLine($"MISSING {n}"); continue; }
    Console.WriteLine($"=== {t.Name} ===");
    foreach (var f in t.Fields.OrderBy(f => f.Name))
        if (Regex.IsMatch(f.Name, "run|session|play|start|active|menu|level|game|InGame|end|die|death", RegexOptions.IgnoreCase))
            Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
    foreach (var m in t.Methods.Where(m => m.HasBody && Regex.IsMatch(m.Name, "run|session|play|start|active|menu|level|InGame|IsPlay|End|Die|Death|GameOver|Lose", RegexOptions.IgnoreCase)))
        Console.WriteLine($"  method {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
}

var ep = asm.MainModule.GetType("ENT_Player");
if (ep != null)
{
    var handT = ep.NestedTypes.FirstOrDefault(x => x.Name == "Hand");
    if (handT != null)
    {
        Console.WriteLine("=== ENT_Player/Hand methods (grip) ===");
        foreach (var m in handT.Methods.Where(m => m.HasBody && Regex.IsMatch(m.Name, "Grip|Stamina|Strain|Damage", RegexOptions.IgnoreCase)))
            Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
    }
    foreach (var mn in new[] { "HandStamina", "SetGripStrength", "DamageGripStrength" })
    {
        var m = ep.Methods.FirstOrDefault(x => x.Name == mn);
        if (m == null || !m.HasBody) continue;
        Console.WriteLine($"=== {mn} params: {string.Join(",", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name))} ===");
        foreach (var ins in m.Body.Instructions)
        {
            if (ins.OpCode.Code != Mono.Cecil.Cil.Code.Call && ins.OpCode.Code != Mono.Cecil.Cil.Code.Callvirt) continue;
            var mr = ins.Operand as MethodReference;
            if (mr != null && Regex.IsMatch(mr.Name, "rip|rain|ealth|train|amage|tamina", RegexOptions.IgnoreCase))
                Console.WriteLine($"  -> {mr.DeclaringType.Name}.{mr.Name}");
        }
    }
}

var di = asm.MainModule.GetTypes().FirstOrDefault(t => t.Name == "DamageInfo");
if (di != null)
{
    Console.WriteLine($"=== {di.FullName} ===");
    foreach (var f in di.Fields) Console.WriteLine($"  field {f.FieldType.Name} {f.Name}");
    foreach (var m in di.Methods.Where(m => m.IsConstructor)) Console.WriteLine($"  ctor({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
}

// UT_DamagePlayer deep dive
var utdp = asm.MainModule.GetType("UT_DamagePlayer");
if (utdp != null)
{
    Console.WriteLine($"=== UT_DamagePlayer (base={utdp.BaseType?.Name}) ===");
    foreach (var f in utdp.Fields) Console.WriteLine($"  field {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");
    foreach (var m in utdp.Methods.Where(x => x.HasBody))
    {
        Console.WriteLine($"  method {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name + " " + p.Name))})");
        foreach (var ins in m.Body.Instructions)
        {
            if (ins.OpCode.Code != Mono.Cecil.Cil.Code.Call && ins.OpCode.Code != Mono.Cecil.Cil.Code.Callvirt) continue;
            var mr = ins.Operand as MethodReference;
            if (mr != null)
                Console.WriteLine($"    -> {mr.DeclaringType.Name}.{mr.Name}");
        }
    }
}

// All methods on ENT_Player that mention Kill/Die/Death
var p3 = asm.MainModule.GetType("ENT_Player");
if (p3 != null)
{
    Console.WriteLine("=== ENT_Player kill/die methods ===");
    foreach (var m in p3.Methods.Where(x => x.HasBody && Regex.IsMatch(x.Name, "Kill|Die|Death|GameOver|Lose|Damage", RegexOptions.IgnoreCase)))
        Console.WriteLine($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");

    // also walk base
    var baseType = p3.BaseType?.Resolve();
    while (baseType != null && baseType.Name != "MonoBehaviour" && baseType.Name != "Object")
    {
        Console.WriteLine($"--- base: {baseType.Name} ---");
        foreach (var m in baseType.Methods.Where(x => x.HasBody && Regex.IsMatch(x.Name, "Kill|Die|Death|GameOver|Lose|Damage", RegexOptions.IgnoreCase)))
            Console.WriteLine($"  {(m.IsStatic ? "static " : "")}{m.ReturnType.Name} {m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})");
        foreach (var f in baseType.Fields.Where(x => Regex.IsMatch(x.Name, "health|dead|die|kill|alive", RegexOptions.IgnoreCase)))
            Console.WriteLine($"  field {(f.IsStatic ? "static " : "")}{f.FieldType.Name} {f.Name}");
        baseType = baseType.BaseType?.Resolve();
    }
}
