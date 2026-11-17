using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace MIR.Test.Branding;

[Collection("Registry collection")]
public class BrandingTests
{
    private const string OldName = "Madness Interactive Reloaded";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "src", "MIR.sln")))
            dir = dir.Parent;
        dir.Should().NotBeNull("the repo root must be reachable from the test output folder");
        return dir!.FullName;
    }

    private static string GameDir() => Path.Combine(RepoRoot(), "src", "MadnessInteractiveReloaded");

    private static bool IsBuildOutput(string path)
    {
        var p = path.Replace('\\', '/');
        return p.Contains("/bin/") || p.Contains("/obj/");
    }

    [Fact]
    public void No_user_facing_madness_name()
    {
        var game = GameDir();
        var offenders = new System.Collections.Generic.List<string>();
        var literal = new Regex(@"""(?:[^""\\\r\n]|\\.)*""");

        foreach (var file in Directory.EnumerateFiles(game, "*.cs", SearchOption.AllDirectories).Where(f => !IsBuildOutput(f)))
        {
            var isBranding = Path.GetFileName(file) == "Branding.cs";
            foreach (var line in File.ReadLines(file))
            {
                var code = line.TrimStart();
                if (code.StartsWith("//") || code.StartsWith("*") || code.StartsWith("/*"))
                    continue;
                foreach (Match m in literal.Matches(line))
                {
                    if (!m.Value.Contains(OldName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (isBranding && m.Value.Contains("Based on " + OldName))
                        continue;
                    offenders.Add(file + ": " + line.Trim());
                }
            }
        }

        var textFiles = Directory.EnumerateFiles(Path.Combine(game, "base", "locale"), "*.json")
            .Concat(Directory.EnumerateFiles(Path.Combine(game, "base", "data"), "*.txt"));
        foreach (var file in textFiles)
        {
            var isCredits = Path.GetFileName(file) == "credits.txt";
            foreach (var line in File.ReadLines(file))
            {
                if (!line.Contains(OldName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (isCredits && line.TrimStart().StartsWith("Based on"))
                    continue;
                offenders.Add(file + ": " + line.Trim());
            }
        }

        offenders.Should().BeEmpty();
    }

    [Fact]
    public void Credits_keep_original_creators()
    {
        var credits = File.ReadAllText(Path.Combine(GameDir(), "base", "data", "credits.txt"));
        foreach (var name in new[] { "zooi", "duston", "Dikkiedik", "Koof", "Lothyde", "Flecko", "Krinkels", "Orsoniks", "Studio Minus", "Zeph" })
            credits.Should().Contain(name);
    }

    [Fact]
    public void Notice_leads_with_project_license()
    {
        var text = File.ReadAllText(Path.Combine(GameDir(), "NOTICE.txt")).TrimStart('﻿');
        var head = string.Join("\n", text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Take(6));
        head.Should().Contain("Insanity Interactive Reloaded");
        head.Should().Contain("Zeph");
        head.Should().Contain("Studio Minus");
        head.Should().Contain("GNU General Public License v3");
        text.Should().Contain("### OpenTK");
    }

    [Fact]
    public void Based_on_credit_names_the_MIR_base_version_not_the_assembly_version()
    {
        MIR.Branding.MirBaseVersion.Should().Be("0.51.1");
        MIR.Branding.BasedOn.Should().Be("Based on " + OldName + " v0.51.1 by Studio Minus");
        MIR.Branding.DisplayVersion.Should().NotBe(MIR.Branding.MirBaseVersion, "IIR has its own version");
    }

    [Fact]
    public void Main_menu_links()
    {
        var links = MainMenuSystem.VisibleLinks();
        links.Should().NotBeEmpty();
        links.Should().NotContain(u => u.Contains("studiominus") || u.Contains("studio-minus"));
        links.Should().Contain(MIR.Branding.RepoUrl);
        MIR.Branding.DiscordUrl.Should().BeNull();
        links.Should().NotContain(u => u.Contains("discord"));
    }
}
