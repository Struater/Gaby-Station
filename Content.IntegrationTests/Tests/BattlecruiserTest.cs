// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Antag.Components;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.GameTicking.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class BattlecruiserTest
{
    [Test]
    public async Task LoadsBattlecruiser()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true, DummyTicker = false });
        EntityUid rule = default;
        EntityUid shuttle = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var ticker = pair.Server.System<GameTicker>();
            var uid = ticker.AddGameRule("SyndicateBattlecruiserRule");
            Assert.That(entities.HasComponent<EndedGameRuleComponent>(uid), Is.False);
            var grids = entities.GetComponent<RuleGridsComponent>(uid);
            Assert.That(grids.MapGrids, Has.Count.EqualTo(1));
            shuttle = grids.MapGrids.Single();
            Assert.That(entities.HasComponent<MapGridComponent>(shuttle), Is.True);
            rule = uid;
            Assert.That(ticker.StartGameRule(uid), Is.True);
        });
        await pair.RunTicksSync(3600);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var spawners = entities.EntityQuery<GhostRoleAntagSpawnerComponent, TransformComponent>(true)
                .Where(pair => pair.Item1.Rule == rule).ToArray();
            Assert.That(spawners, Has.Length.EqualTo(20));
            Assert.That(spawners.All(pair => pair.Item2.GridUid == shuttle), Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
