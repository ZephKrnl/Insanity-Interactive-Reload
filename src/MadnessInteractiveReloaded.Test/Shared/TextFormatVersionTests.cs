using FluentAssertions;
using MIR.Serialisation;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace MIR.Test.Shared;

/// <summary>
/// The text formats' version line is a format version of its own, not the assembly version (BUG-10), and it is compared on
/// load (BUG-04).
/// </summary>
[Collection("Registry collection")]
public class TextFormatVersionTests : IDisposable
{
    private readonly TempUserData userData = new();
    private readonly SaveTestLooks looks = new();

    public void Dispose()
    {
        looks.Dispose();
        userData.Dispose();
    }

    private static string FirstLine(string path) => File.ReadLines(path).First();

    [Fact]
    public void Text_format_version_stays_at_the_version_MIR_0_51_1_wrote()
    {
        SaveFormat.TextFormatVersion.Should().Be(new Version(0, 51, 1), "unchanged since MIR 0.51.1, so files stay readable by both");
    }

    [Fact]
    public void Text_formats_write_the_format_version_not_the_assembly_version()
    {
        var kv = userData.PathOf("kv.txt");
        var look = userData.PathOf("a.look");
        var stats = userData.PathOf("a.stats");
        var preset = userData.PathOf("a.preset");

        BaseDeserialiser.Write(kv, ["key value"]);
        CharacterLookDeserialiser.Save(looks.Look(), look);
        CharacterStatsDeserialiser.Save(new CharacterStats { Abilities = [] }, stats);
        CharacterPresetDeserialiser.Save("p", looks.Look(), new CharacterStats { Abilities = [] }, preset);

        foreach (var file in new[] { kv, look, stats, preset })
            FirstLine(file).Should().Be(SaveFormat.TextFormatVersion.ToString(), file);
    }

    [Theory]
    [InlineData("0.1.18")]
    [InlineData("0.51.1")]
    [InlineData("99.0.0")]
    public void Text_file_of_any_version_loads(string version)
    {
        var path = userData.PathOf("v.look");
        File.WriteAllText(path, SaveTestLooks.LookFile(version));

        CharacterLookDeserialiser.Load(path).HeadLayer1.Should().BeSameAs(looks.HeadAccessory);
    }

    [Fact]
    public void Each_line_carries_the_file_version_for_format_dependent_parsing()
    {
        using var reader = new StreamReader(new MemoryStream("0.36.0\n\nkey value\n"u8.ToArray()));

        BaseDeserialiser.Read(reader, "test").Single().Version.Should().Be(new Version(0, 36, 0));
    }

    [Fact]
    public void Text_file_without_a_version_line_is_rejected_as_before()
    {
        using var reader = new StreamReader(new MemoryStream("key value\n"u8.ToArray()));

        var read = () => BaseDeserialiser.Read(reader, "test").ToArray();

        read.Should().Throw<Exceptions.SerialisationException>();
    }
}
