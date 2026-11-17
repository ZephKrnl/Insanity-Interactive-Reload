using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Walgelijk.AssetManager;

namespace MIR.Test.Data;

/// <summary>Helpers for tests over the data shipped in <c>base</c>.</summary>
internal static class ShippedData
{
    /// <summary>The <c>base</c> folder the test build copies next to the test assembly. It is also what <c>base.waa</c> is packed from.</summary>
    public static string BaseDirectory => Path.Combine(AppContext.BaseDirectory, "base");

    /// <summary>Asset paths (like <c>data/levels/lvl_bakery.json</c>) of every file under <paramref name="folder"/> with one of the extensions.</summary>
    public static IEnumerable<string> Files(string folder, params string[] extensions)
        => Directory.EnumerateFiles(Path.Combine(BaseDirectory, folder), "*", SearchOption.AllDirectories)
            .Where(f => extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
            .Select(f => Path.GetRelativePath(BaseDirectory, f).Replace('\\', '/'))
            .Order(StringComparer.Ordinal);

    /// <summary>Fails with every problem listed, one per line (a plain collection assertion only shows the first few).</summary>
    public static void ShouldHaveNoProblems(this IEnumerable<string> problems)
    {
        var all = problems.ToArray();
        if (all.Length > 0)
            throw new Xunit.Sdk.XunitException($"{all.Length} problem(s):\n" + string.Join("\n", all));
    }

    public static string ReadText(string assetPath) => File.ReadAllText(Path.Combine(BaseDirectory, assetPath));

    public static bool Exists(string assetPath) => File.Exists(Path.Combine(BaseDirectory, assetPath));

    private static readonly Assembly gameAssembly = typeof(Level).Assembly;

    /// <summary>
    /// Walks the fields of each object (and of the game objects and collections it holds) and returns a line for every
    /// <see cref="AssetRef{T}"/> or <see cref="GlobalAssetId"/> that is set but names an asset no registered package has.
    /// </summary>
    public static List<string> MissingAssetReferences(IEnumerable<(string Name, object Value)> roots)
    {
        var missing = new List<string>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var (name, value) in roots)
            Walk(value, name, seen, missing, 0);
        return missing;
    }

    private static void Walk(object? o, string path, HashSet<object> seen, List<string> missing, int depth)
    {
        if (o == null || depth > 40)
            return;

        var t = o.GetType();
        if (t.IsPrimitive || t.IsEnum || o is string || o is Delegate)
            return;

        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AssetRef<>))
        {
            if ((bool)t.GetProperty("IsValid")!.GetValue(o)!)
            {
                var id = (GlobalAssetId)(t.GetProperty("Id")?.GetValue(o) ?? t.GetField("Id")!.GetValue(o)!);
                if (!Assets.HasAsset(id))
                    missing.Add($"{path}: {id} does not exist");
            }
            return;
        }

        if (o is GlobalAssetId gid)
        {
            if (!gid.Equals(default(GlobalAssetId)) && !Assets.HasAsset(gid))
                missing.Add($"{path}: {gid} does not exist");
            return;
        }

        if (!t.IsValueType && !seen.Add(o))
            return;

        if (o is IEnumerable enumerable)
        {
            var elementType = t.IsArray ? t.GetElementType() : t.GetGenericArguments().LastOrDefault();
            if (elementType != null && !MightHoldReferences(elementType))
                return;
            int i = 0;
            foreach (var item in enumerable)
                Walk(item, $"{path}[{i++}]", seen, missing, depth + 1);
            return;
        }

        if (!IsWalked(t))
            return;

        for (var type = t; type != null && type != typeof(object); type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (MightHoldReferences(field.FieldType))
                    Walk(field.GetValue(o), $"{path}.{field.Name}", seen, missing, depth + 1);
    }

    /// <summary>Game types, and the generic types from the engine that wrap them (like <c>DoubleSided&lt;T&gt;</c> or a nullable).</summary>
    private static bool IsWalked(Type t)
        => t.Assembly == gameAssembly || (t.IsGenericType && t.GetGenericArguments().Any(IsWalked));

    private static bool MightHoldReferences(Type t)
    {
        if (t.IsPrimitive || t.IsEnum || t == typeof(string) || typeof(Delegate).IsAssignableFrom(t))
            return false;
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Lazy<>))
            return false;
        if (t == typeof(GlobalAssetId) || (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(AssetRef<>)))
            return true;
        if (t.IsArray)
            return MightHoldReferences(t.GetElementType()!);
        if (t.IsInterface || t == typeof(object) || t.IsAbstract)
            return true;
        if (Nullable.GetUnderlyingType(t) is { } u)
            return MightHoldReferences(u);
        if (typeof(IEnumerable).IsAssignableFrom(t))
            return t.GetGenericArguments().Any(MightHoldReferences);
        return IsWalked(t);
    }
}
