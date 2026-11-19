using FluentAssertions;
using Xunit;

namespace MIR.Test.Data;

/// <summary>
/// <see cref="Registries.LoadFactions"/> clears <see cref="FactionRelationships"/> and gets the factions from the asset cache,
/// so the relationships must be put back on every call, not only the first one (a second load in one process, e.g. a mod
/// reload or another test).
/// </summary>
[Collection("Registry collection")]
public class FactionReloadTests
{
    [Fact]
    public void RelationshipsSurviveASecondLoad()
    {
        ImprobabilityDisks.IsEnabled("everyone_evil").Should().BeFalse("because that disk makes every faction an enemy and would hide the bug");

        Registries.LoadFactions();
        var aahw = Registries.Factions.Get("aahw");
        var player = Registries.Factions.Get("player");
        aahw.IsEnemiesWith(player).Should().BeTrue("because aahw.json lists player as an enemy");

        Registries.LoadFactions();

        var aahwAgain = Registries.Factions.Get("aahw");
        var playerAgain = Registries.Factions.Get("player");
        aahwAgain.IsEnemiesWith(playerAgain).Should().BeTrue("because a second LoadFactions must rebuild the relationships");
        playerAgain.IsEnemiesWith(aahwAgain).Should().BeTrue();
        aahwAgain.IsAlliedTo(aahwAgain).Should().BeTrue("because aahw.json lists aahw as its own ally");
        FactionRelationships.Enemies["aahw"].Should().BeEquivalentTo(["player", "rogue", "zed"], "because the relationships must not pile up or go missing");

        // The cached objects come back, so the collision layers stay the same and are not used up by reloading.
        aahwAgain.Should().BeSameAs(aahw);
        aahwAgain.CollisionLayer.Should().Be(aahw.CollisionLayer);
    }
}
