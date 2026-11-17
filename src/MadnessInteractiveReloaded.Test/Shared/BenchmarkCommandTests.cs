using System.Linq;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Walgelijk;
using Xunit;

namespace MIR.Test.Shared;

/// <summary>
/// The <c>benchmark [ai] [seconds]</c> console command. Checked by hand in a test game: the engine's command parser fills in
/// an omitted optional argument (<c>benchmark 0</c> answers with the AI-count error).
/// </summary>
public class BenchmarkCommandTests
{
    [Fact]
    public void BenchmarkCommandTakesOptionalAiAndSecondsWithTheDefaults()
    {
        var method = typeof(MadnessCommands).GetMethod(nameof(MadnessCommands.Benchmark), BindingFlags.Public | BindingFlags.Static);
        method.Should().NotBeNull();
        method!.GetCustomAttribute<CommandAttribute>().Should().NotBeNull();
        var p = method.GetParameters();
        p.Select(x => x.ParameterType).Should().Equal(typeof(int), typeof(int));
        p.Should().OnlyContain(x => x.HasDefaultValue);
        p[0].DefaultValue.Should().Be(BenchmarkOptions.DefaultAi);
        p[1].DefaultValue.Should().Be(BenchmarkOptions.DefaultSeconds);
    }

    [Fact]
    public void BenchmarkCommandRejectsOutOfRangeArgumentsWithoutStartingAnything()
    {
        // No game exists in this test: an out-of-range call must answer before it touches the game.
        var result = MadnessCommands.Benchmark(0, 60);
        result.Type.Should().Be(LogLevel.Error);
        result = MadnessCommands.Benchmark(10, 100000);
        result.Type.Should().Be(LogLevel.Error);
    }
}
