using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;

// 用法：
//   dotnet run -- <Managed目录> <类型名片段...>            列出匹配类型的字段+方法签名
//   dotnet run -- <Managed目录> <类型片段> --il <方法片段>  再打印这些方法（及.ctor）的 IL
// 反射只读，不执行任何游戏代码。
internal static class Program
{
    static string _managed;

    static Assembly Resolve(AssemblyLoadContext ctx, AssemblyName name)
    {
        var p = Path.Combine(_managed, name.Name + ".dll");
        return File.Exists(p) ? ctx.LoadFromAssemblyPath(p) : null;
    }

    static string Safe(Func<string> f) { try { return f(); } catch { return "?"; } }

    static readonly Dictionary<short, OpCode> Ops;
    static Program()
    {
        Ops = new Dictionary<short, OpCode>();
        foreach (var f in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
            if (f.GetValue(null) is OpCode op && op.Name != null) Ops[op.Value] = op;
    }

    static void PrintIL(MethodBase m)
    {
        var body = m.GetMethodBody();
        if (body == null) { Console.WriteLine("    (no body)"); return; }
        var il = body.GetILAsByteArray();
        var mod = m.Module;
        int pos = 0;
        while (pos < il.Length)
        {
            int offset = pos;
            short v = il[pos++];
            OpCode op;
            if (v == 0xFE)
            {
                v = (short)(0xFE00 | il[pos++]);
                if (!Ops.TryGetValue(v, out op)) { Console.WriteLine($"    IL_{offset:X4}: ??? 0x{v:X2}"); continue; }
            }
            else if (!Ops.TryGetValue(v, out op)) { Console.WriteLine($"    IL_{offset:X4}: ??? 0x{v:X2}"); continue; }

            string operand = "";
            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    operand = il[pos].ToString(); pos += 1; break;
                case OperandType.InlineVar:
                    operand = BitConverter.ToUInt16(il, pos).ToString(); pos += 2; break;
                case OperandType.InlineBrTarget:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                case OperandType.InlineType:
                case OperandType.InlineSwitch:
                {
                    int tok = BitConverter.ToInt32(il, pos); pos += 4;
                    if (op.OperandType == OperandType.InlineBrTarget)
                        operand = "IL_" + (pos + tok).ToString("X4");
                    else operand = Safe(() =>
                        (MemberInfo)mod.ResolveMember(tok, m.DeclaringType?.GetGenericArguments(), m.GetGenericArguments()) is var mi && mi != null ? mi.ToString()
                        : (string)mod.ResolveString(tok));
                    break;
                }
                case OperandType.InlineString:
                    operand = "\"" + mod.ResolveString(BitConverter.ToInt32(il, pos)) + "\""; pos += 4; break;
                case OperandType.InlineI8:
                    operand = BitConverter.ToInt64(il, pos).ToString(); pos += 8; break;
                case OperandType.InlineR:
                    operand = BitConverter.ToDouble(il, pos).ToString(); pos += 8; break;
                case OperandType.InlineI:
                    operand = BitConverter.ToInt32(il, pos).ToString(); pos += 4; break;
                case OperandType.ShortInlineR:
                    operand = BitConverter.ToSingle(il, pos).ToString(); pos += 4; break;
            }
            Console.WriteLine($"    IL_{offset:X4}: {op.Name,-12} {operand}");
        }
    }

    static void Main(string[] args)
    {
        _managed = args[0];
        var rest = args.Skip(1).ToList();
        bool wantIl = rest.Contains("--il");
        if (wantIl) rest.RemoveAt(rest.IndexOf("--il"));
        string methodPattern = null;
        if (rest.Count > 1 && wantIl) { methodPattern = rest[^1].ToLowerInvariant(); rest.RemoveAt(rest.Count - 1); }
        var patterns = rest.Select(a => a.ToLowerInvariant()).ToArray();

        AssemblyLoadContext.Default.Resolving += Resolve;
        var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(_managed, "Assembly-CSharp.dll"));

        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (var t in types)
        {
            var full = (t.FullName ?? "").ToLowerInvariant();
            if (patterns.Length > 0 && !patterns.Any(full.Contains)) continue;

            Console.WriteLine($"=== {t.FullName}  (base: {Safe(() => t.BaseType?.Name ?? "")})");
            const string Pub = "+", Priv = "-";
            foreach (var f in t.GetFields(All))
                Console.WriteLine($"  field {(f.IsPublic ? Pub : Priv)} {Safe(() => f.FieldType.Name)} {f.Name}{(f.IsStatic ? " [static]" : "")}");
            foreach (var p in t.GetProperties(All))
            {
                bool pubProp = false;
                try { pubProp = p.GetGetMethod(true)?.IsPublic ?? false; } catch { }
                Console.WriteLine($"  prop  {(pubProp ? Pub : Priv)} {Safe(() => p.PropertyType.Name)} {p.Name}");
            }
            foreach (var m in t.GetMethods(All))
            {
                if (m.IsSpecialName) continue;
                var ps = string.Join(", ", m.GetParameters().Select(pp => Safe(() => pp.ParameterType.Name) + " " + pp.Name));
                Console.WriteLine($"  method {(m.IsPublic ? Pub : Priv)} {Safe(() => m.ReturnType.Name)} {m.Name}({ps})");
                if (wantIl && methodPattern != null && m.Name.ToLowerInvariant().Contains(methodPattern))
                {
                    Console.WriteLine("    --- IL ---");
                    PrintIL(m);
                }
            }
        }
    }
}
