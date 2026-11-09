// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.GameTicking;
using Content.Trauma.Shared.JobListings;
using Content.Shared.Mind;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Damage.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System.Numerics;
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
        var map = await pair.CreateTestMap();
        EntityUid rule = default;
        EntityUid shuttle = default;
        EntityUid user = default;
        EntityUid console = default;
        EntityUid transmitter = default;
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            user = entities.SpawnEntity("MobHuman", map.GridCoords);
            entities.AddComponent<GodmodeComponent>(user);
            var minds = pair.Server.System<SharedMindSystem>();
            var mind = minds.CreateMind(null);
            minds.TransferTo(mind, user);
            console = entities.SpawnEntity("ComputerComms", map.GridCoords);
            pair.Server.System<SharedPowerReceiverSystem>().SetNeedsPower(console, false);
            transmitter = entities.SpawnEntity("SyndicateBroadcastDevice", map.GridCoords);
            Assert.That(pair.Server.System<SharedHandsSystem>().TryPickupAnyHand(user, transmitter), Is.True);
        });
        await pair.RunTicksSync(10);
        await pair.Server.WaitAssertion(() =>
        {
            var interaction = new InteractUsingEvent(user, transmitter, console, map.GridCoords);
            pair.Server.EntMan.EventBus.RaiseLocalEvent(console, interaction);
            Assert.That(interaction.Handled, Is.True);
        });
        await pair.RunTicksSync(1200);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var uid = entities.EntityQuery<MetaDataComponent>(true)
                .Single(meta => meta.EntityPrototype?.ID == "SyndicateBattlecruiserRule").Owner;
            Assert.That(entities.HasComponent<EndedGameRuleComponent>(uid), Is.False);
            var grids = entities.GetComponent<RuleGridsComponent>(uid);
            Assert.That(grids.MapGrids, Has.Count.EqualTo(1));
            shuttle = grids.MapGrids.Single();
            Assert.That(entities.HasComponent<MapGridComponent>(shuttle), Is.True);
            rule = uid;
            Assert.That(entities.HasComponent<ActiveGameRuleComponent>(uid) ||
                        entities.HasComponent<DelayedStartRuleComponent>(uid), Is.True);
        });
        await pair.RunTicksSync(3600);
        await pair.Server.WaitAssertion(() =>
        {
            var entities = pair.Server.EntMan;
            var spawners = entities.EntityQuery<GhostRoleAntagSpawnerComponent, TransformComponent>(true)
                .Where(pair => pair.Item1.Rule == rule).ToArray();
            Assert.That(spawners, Has.Length.EqualTo(20));
            Assert.That(spawners.All(pair => pair.Item2.GridUid == shuttle), Is.True);
            var turrets = entities.EntityQuery<TransformComponent, MetaDataComponent>(true)
                .Where(pair => pair.Item2.EntityPrototype?.ID == "WeaponTurretSyndicate" &&
                               pair.Item1.GridUid == shuttle).ToArray();
            Assert.That(turrets, Has.Length.EqualTo(15));
            Assert.That(turrets.All(pair => pair.Item1.Anchored), Is.True);
            var crates = entities.EntityQuery<TransformComponent, MetaDataComponent>(true)
                .Count(pair => pair.Item2.EntityPrototype?.ID == "CrateSyndicateSuperSurplusBundle" &&
                               pair.Item1.GridUid == shuttle);
            Assert.That(crates, Is.EqualTo(3));
            var mass = entities.GetComponent<Robust.Shared.Physics.Components.PhysicsComponent>(shuttle).Mass;
            var drive = entities.GetComponent<Content.Shared._NF.Shuttles.FTLDriveComponent>(shuttle);
            var shuttleSystem = pair.Server.System<ShuttleSystem>();
            Assert.That(shuttleSystem.CanFTL(shuttle, out var reason), Is.True, $"{reason}, mass={mass}, drive={drive.Data}");
            Assert.That(drive.Data.FTLToSameMap, Is.True);
            var transform = entities.GetComponent<TransformComponent>(shuttle);
            var target = new EntityCoordinates(transform.MapUid!.Value, new Vector2(300, 0));
            Assert.That(shuttleSystem.FTLFree(shuttle, target, Angle.Zero, null), Is.True);
            shuttleSystem.FTLToCoordinates(shuttle, entities.GetComponent<ShuttleComponent>(shuttle), target, Angle.Zero);
            Assert.That(entities.HasComponent<FTLComponent>(shuttle), Is.True);
        });
        await pair.RunTicksSync(3600);
        await pair.Server.WaitAssertion(() =>
        {
            var position = pair.Server.System<SharedTransformSystem>().GetWorldPosition(shuttle);
            Assert.That(Vector2.Distance(position, new Vector2(300, 0)), Is.LessThan(5));
        });
        await pair.CleanReturnAsync();
    }
}
