using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using MIR.LevelEditor.Objects;
using MIR.Test.GameTests;
using Newtonsoft.Json;
using Walgelijk;
using Walgelijk.AssetManager;
using Xunit;

namespace MIR.Test.Data;

/// <summary>
/// Every <see cref="LevelScript"/> in a shipped level compiles with the compiler the game uses (<see cref="BuiltLevelScript.Build"/>,
/// which <c>LevelScriptSystem</c> runs). A script that fails only logs an error in game, so nothing else would catch it (BUG-64).
/// </summary>
[Collection("Registry collection")]
public class LevelScriptCompileTests
{
    private const string LevelScriptTypeName = "MIR.LevelEditor.Objects.LevelScript, MIR";

    /// <summary>Asset paths of the shipped levels whose JSON names the <see cref="LevelScript"/> type.</summary>
    public static IEnumerable<object[]> LevelsWithScripts()
        => ShippedData.Files("data/levels", ".json")
            .Where(p => ShippedData.ReadText(p).Contains(LevelScriptTypeName, StringComparison.Ordinal))
            .Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(LevelsWithScripts))]
    public void Every_script_in_the_level_compiles(string levelPath)
    {
        var logger = new RecordingLogger();
        var game = TestGameUtil.Create(logger);
        try
        {
            var level = Assets.LoadNoCache<Level>(levelPath);
            var scripts = level.Objects.OfType<LevelScript>().ToArray();
            scripts.Should().NotBeEmpty("because the level file names the LevelScript type");

            var failures = new List<string>();
            foreach (var script in scripts)
            {
                logger.Clear();
                using var built = new BuiltLevelScript(script.Code);
                built.Build().GetAwaiter().GetResult();

                var errors = logger.Errors.ToArray();
                if (built.Script == null || errors.Length > 0)
                    failures.Add($"script \"{script.Name}\" did not compile:\n{string.Join("\n", errors)}");
            }

            failures.Should().BeEmpty();
        }
        finally
        {
            game.Stop();
        }
    }

    [Fact]
    public void The_theory_covers_every_level_that_has_a_script()
    {
        // Reads the objects with the game's level JSON settings but without LevelDeserialiser's validity checks, so a level
        // that is broken in some other way (AllBaseDataLoadsTests reports those) is still searched for scripts.
        var expected = LevelsWithScripts().Select(o => (string)o[0]).ToHashSet();
        expected.Should().NotBeEmpty();

        foreach (var path in ShippedData.Files("data/levels", ".json"))
        {
            var level = JsonConvert.DeserializeObject<Level>(ShippedData.ReadText(path), LevelDeserialiser.SerializerSettings);
            level.Should().NotBeNull(path);
            if (level!.Objects.OfType<LevelScript>().Any())
                expected.Should().Contain(path, "because every level with a script must be compiled by the theory above");
        }
    }
}
