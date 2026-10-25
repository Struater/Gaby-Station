using Content.Shared.Shuttles.Components;
using Content.Shared._CMU14.Xenomorphs;
using System.Numerics;
using System.Linq;
using Content.Server.Explosion.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Dumont.Xenomorphs;
using Content.Shared._NF.Shuttles;
using Content.Shared.ActionBlocker;
using Content.Shared.Audio;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Dumont.Xenomorphs;

public sealed partial class XenomorphShuttleHijackSystem
{
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ShuttleSystem _shuttles = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private ExplosionSystem _explosions = default!;

    private readonly List<(TimeSpan At, EntityCoordinates Position)> _landingEffects = new();

    private void InitializeInteraction()
    {
        SubscribeLocalEvent<XenomorphRoyalComponent, AttackAttemptEvent>(OnConsoleAttack);
        SubscribeLocalEvent<ShuttleConsoleComponent, InteractHandEvent>(OnConsoleHand);
        SubscribeLocalEvent<ShuttleConsoleComponent, ActivateInWorldEvent>(OnConsoleActivate, before: new[] { typeof(ActivatableUISystem) });
        SubscribeLocalEvent<ShuttleConsoleComponent, GetVerbsEvent<Verb>>(OnConsoleVerbs);
    }

    private bool IsQueen(EntityUid uid) => HasComp<XenomorphRoyalComponent>(uid)
        && HasComp<XenomorphLineageComponent>(uid);

    private void OnConsoleAttack(Entity<XenomorphRoyalComponent> ent, ref AttackAttemptEvent args)
    {
        if (args.Cancelled || args.Target is not { } target || !TryComp<ShuttleConsoleComponent>(target, out var console)
            || !TryComp<XenomorphOvipositorComponent>(ent, out var ovi) || !ovi.Attached) return;
        // A console click must not become a claw strike when combat mode is selected.
        args.Cancel();
        TryHijack(ent, (target, console));
    }

    private void OnConsoleHand(Entity<ShuttleConsoleComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !IsQueen(args.User)) return;
        args.Handled = true;
        TryHijack(args.User, ent);
    }

    private void OnConsoleActivate(Entity<ShuttleConsoleComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !IsQueen(args.User)) return;
        args.Handled = true;
        TryHijack(args.User, ent);
    }

    private void OnConsoleVerbs(Entity<ShuttleConsoleComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanInteract || !IsQueen(args.User) || !CommsLocked) return;
        var user = args.User;
        args.Verbs.Add(new Verb { Text = Loc.GetString("xenomorph-shuttle-hijack-verb"), Act = () => TryHijack(user, ent) });
    }

    public bool TryHijack(EntityUid queen, Entity<ShuttleConsoleComponent> console)
    {
        string? failure = null;
        var grid = Transform(console).GridUid;
        if (WindowClosed) failure = "xenomorph-shuttle-hijack-window-lost";
        else if (!CommsLocked || _objectives.CommsCorrupted) failure = "xenomorph-shuttle-hijack-unavailable";
        else if (HijackedShuttle != null) failure = "xenomorph-shuttle-hijack-already-started";
        else if (!IsQueen(queen) || !_mobs.IsAlive(queen) || !_blocker.CanConsciouslyPerformAction(queen)
            || !TryComp<XenomorphOvipositorComponent>(queen, out var ovi) || !ovi.Attached
            || !_interaction.InRangeUnobstructed(queen, console.Owner, 3.5f)
            || Transform(queen).GridUid != grid || !Transform(console).Anchored
            || grid == null || !HasComp<ShuttleComponent>(grid) || !HasComp<MapGridComponent>(grid)
            || HasComp<FTLComponent>(grid) || grid == _evac
            || !Exists(_centcomm) || !Exists(_centcommMap)
            || Transform(grid!.Value).MapUid == _centcommMap)
            failure = "xenomorph-shuttle-hijack-requirements";
        if (failure != null)
        {
            _popup.PopupEntity(Loc.GetString(failure), queen, queen);
            return false;
        }

        var shuttle = grid!.Value;
        // Hijacking supplies the navigation link even for local shuttles without an FTL drive.
        EnsureComp<FTLDriveComponent>(shuttle);
        EnsureComp<FTLSmashImmuneComponent>(_centcomm!.Value);
        var landing = GetCrashLanding(shuttle, _centcomm.Value);
        _shuttles.FTLToCoordinates(shuttle, Comp<ShuttleComponent>(shuttle), landing.Coordinates, landing.Rotation,
            startupTime: 0, hyperspaceTime: 180);
        if (!HasComp<FTLComponent>(shuttle)) return false;
        HijackedShuttle = shuttle;
        EnsureComp<XenomorphHijackedShuttleComponent>(shuttle);
        _hive.AnnounceHive(Loc.GetString("xenomorph-shuttle-hijack-departing"));
        return true;
    }

    private void OnInvasionArrived(Entity<XenomorphHijackedShuttleComponent> ent, ref FTLCompletedEvent args)
    {
        if (HijackedShuttle != ent.Owner || Invaded || args.MapUid != _centcommMap) return;
        // Reaching the destination with an empty shuttle is not an invasion.
        var xenos = EntityQueryEnumerator<Content.Shared._White.Xenomorphs.Xenomorph.XenomorphComponent>();
        var occupants = false;
        while (xenos.MoveNext(out var uid, out _))
        {
            if (!_mobs.IsDead(uid) && Transform(uid).GridUid == ent.Owner) { occupants = true; break; }
        }
        if (!occupants) return;
        _shuttles.Disable(ent);
        ClearCrashFootprint(ent);
        Invaded = true;
        ExtendFinale(_timing.CurTime + TimeSpan.FromMinutes(5));
        _rules.CompleteShuttleInvasion();
        _hive.AnnounceHive(Loc.GetString("xenomorph-shuttle-hijack-arrived"));
        var sound = new SoundPathSpecifier("/Audio/_RMC14/Hijack/GigaXO_we_lose.ogg", AudioParams.Default.WithVolume(-4));
        RaiseNetworkEvent(new StopStationEventMusic(StationEventMusicType.Xenomorph), Filter.Broadcast());
        RaiseNetworkEvent(new StationEventMusicEvent(_audio.ResolveSound(sound), StationEventMusicType.Xenomorph, sound.Params), Filter.Broadcast());
        PrepareLandingEffects(ent);
    }

    private (EntityCoordinates Coordinates, Angle Rotation) GetCrashLanding(EntityUid shuttle, EntityUid centcomm)
    {
        var ccGrid = Comp<MapGridComponent>(centcomm);
        var shipGrid = Comp<MapGridComponent>(shuttle);
        var center = ccGrid.LocalAABB.Center;
        var doors = new List<Vector2>();
        var docks = EntityQueryEnumerator<DockingComponent, TransformComponent>();
        while (docks.MoveNext(out var uid, out _, out var transform))
        {
            if (transform.GridUid != centcomm) continue;
            if (TryComp<PriorityDockComponent>(uid, out var priority) && priority.Tag == "DockEmergency")
                doors.Add(transform.LocalPosition);
        }
        Vector2 entrance;
        if (doors.Count > 0)
            entrance = doors.Aggregate(Vector2.Zero, (sum, pos) => sum + pos) / doors.Count;
        else
            entrance = new Vector2(ccGrid.LocalAABB.Right, center.Y);
        var delta = center - entrance;
        var inward = Math.Abs(delta.X) >= Math.Abs(delta.Y)
            ? new Vector2(Math.Sign(delta.X), 0)
            : new Vector2(0, Math.Sign(delta.Y));
        var halfSize = Math.Abs(inward.X) > 0 ? shipGrid.LocalAABB.Width / 2 : shipGrid.LocalAABB.Height / 2;
        var landingCenter = entrance + inward * (Math.Min(halfSize, 8) + 3);
        var mapPosition = _transform.ToMapCoordinates(new EntityCoordinates(centcomm, landingCenter - shipGrid.LocalAABB.Center));
        // Target the map, not the grid: FTL to a grid would automatically choose a dock.
        return (new EntityCoordinates(Transform(centcomm).MapUid!.Value, mapPosition.Position), _transform.GetWorldRotation(centcomm));
    }

    private void ClearCrashFootprint(EntityUid shuttle)
    {
        var grid = Comp<MapGridComponent>(shuttle);
        var query = EntityQueryEnumerator<TransformComponent, Robust.Shared.Physics.Components.PhysicsComponent>();
        while (query.MoveNext(out var uid, out var transform, out _))
        {
            if (uid == _centcomm || transform.GridUid != _centcomm || HasComp<Content.Shared.Mobs.Components.MobStateComponent>(uid)) continue;
            var local = _transform.ToCoordinates(shuttle, _transform.GetMapCoordinates(uid));
            if (!_map.GetTileRef(shuttle, grid, local).Tile.IsEmpty) QueueDel(uid);
        }
    }

    private void PrepareLandingEffects(EntityUid shuttle)
    {
        if (!TryComp<MapGridComponent>(_centcomm, out var grid)) return;
        var origin = _transform.GetWorldPosition(shuttle);
        var tiles = _map.GetAllTiles(_centcomm!.Value, grid);
        foreach (var tile in tiles)
        {
            if (_turf.IsSpace(tile)) continue;
            var pos = _map.GridTileToLocal(_centcomm.Value, grid, tile.GridIndices);
            var distance = Vector2.DistanceSquared(_transform.ToMapCoordinates(pos).Position, origin);
            if (distance < 36 || distance > 324) continue;
            if (_landingEffects.Any(effect => Vector2.DistanceSquared(effect.Position.Position, pos.Position) < 16)) continue;
            _landingEffects.Add((_timing.CurTime + TimeSpan.FromSeconds(2 + _landingEffects.Count * 3), pos));
            if (_landingEffects.Count >= 6) break;
        }
    }

    private void UpdateLandingEffects()
    {
        for (var i = _landingEffects.Count - 1; i >= 0; i--)
        {
            var effect = _landingEffects[i];
            if (_timing.CurTime < effect.At) continue;
            _landingEffects.RemoveAt(i);
            if (!Exists(effect.Position.EntityId)) continue;
            var position = _transform.ToMapCoordinates(effect.Position);
            _explosions.QueueExplosion(position, "FireBombWeak", 20, 3, 4, cause: null, maxTileBreak: 3);
            Spawn("MolotovFire", effect.Position);
        }
    }
}
