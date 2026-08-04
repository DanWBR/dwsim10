// Compares the public surface of two builds of the same assembly.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

class Program
{
    static int Main(string[] args)
    {
        var probe = args.Skip(2).ToArray();

        var left = Surface(args[0], probe);
        var right = Surface(args[1], probe);

        var removed = left.Except(right).OrderBy(x => x).ToList();
        var added = right.Except(left).OrderBy(x => x).ToList();

        Console.WriteLine($"esquerda: {left.Count} membros  direita: {right.Count} membros");

        foreach (var item in removed) Console.WriteLine("- " + item);
        foreach (var item in added) Console.WriteLine("+ " + item);

        Console.WriteLine($"{removed.Count} removido(s), {added.Count} adicionado(s)");

        return removed.Count == 0 ? 0 : 1;
    }

    static HashSet<string> Surface(string path, string[] probe)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        var paths = new List<string>(Directory.GetFiles(folder, "*.dll"));
        paths.AddRange(Directory.GetFiles(
            Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll"));

        foreach (var extra in probe)
        {
            if (Directory.Exists(extra)) paths.AddRange(Directory.GetFiles(extra, "*.dll", SearchOption.AllDirectories));
        }

        paths = paths.GroupBy(Path.GetFileName).Select(g => g.First()).ToList();

        var resolver = new PathAssemblyResolver(paths);
        using var context = new MetadataLoadContext(resolver);

        var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
        var surface = new HashSet<string>();

        foreach (var type in assembly.GetExportedTypes())
        {
            surface.Add("type " + type.FullName);

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance |
                                                   BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                surface.Add(type.FullName + "::" + Describe(member));
            }
        }

        return surface;
    }

    // Names only: resolving a signature would need every referenced assembly of both builds.
    static string Describe(MemberInfo member)
    {
        return member.MemberType + " " + member.Name;
    }
}
