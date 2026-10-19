using Content.Shared._CMU14.Xenomorphs;
using Content.Server._Dumont.Xenomorphs;
using System.Numerics;
using Content.Shared._Dumont.Xenomorphs;
using Content.Shared._White.Xenomorphs.Xenomorph;
using Content.Shared.ActionBlocker;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._CMU14.Xenomorphs;

public sealed class XenomorphLungeSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly ThrownItemSystem _thrown = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<XenomorphLungeComponent, XenomorphLungeActionEvent>(OnLunge);
        SubscribeLocalEvent<XenomorphLungeComponent, ThrowDoHitEvent>(OnHit);
        SubscribeLocalEvent<XenomorphLungeComponent, StopThrowEvent>(OnStop);
        SubscribeLocalEvent<XenomorphLungeStunnedComponent, PullStoppedMessage>(OnPullStopped);
    }

    private bool IsPrey(EntityUid target) => HasComp<MobStateComponent>(target)
        && !HasComp<XenomorphComponent>(target) && !_mobs.IsDead(target) && !Transform(target).Anchored;

    private void OnLunge(Entity<XenomorphLungeComponent> ent, ref XenomorphLungeActionEvent args)
    {
        if (args.Handled || ent.Comp.Target != null || !_mobs.IsAlive(ent) || !_blocker.CanAttack(ent)
            || !_blocker.CanMove(ent) || _containers.IsEntityInContainer(ent) || Transform(ent).Anchored
            || !IsPrey(args.Target) || !_interaction.InRangeUnobstructed(ent.Owner, args.Target, ent.Comp.Range))
            return;
        var diff = _transform.GetWorldPosition(args.Target) - _transform.GetWorldPosition(ent);
        _pulling.StopAllPulls(ent);
        ent.Comp.Target = args.Target;
        args.Handled = true;
        if (diff.LengthSquared() <= 1.25f * 1.25f)
        {
            Grab(ent, args.Target);
            return;
        }
        _throwing.TryThrow(ent, Vector2.Normalize(diff) * ent.Comp.Range, ent.Comp.Speed,
            ent, pushbackRatio: 0, doSpin: false);
        if (!HasComp<ThrownItemComponent>(ent))
            ent.Comp.Target = null;
    }

    private void OnHit(Entity<XenomorphLungeComponent> ent, ref ThrowDoHitEvent args)
    {
        if (args.Handled || ent.Comp.Target == null)
            return;
        args.Handled = true;
        if (!IsPrey(args.Target) || !_mobs.IsAlive(ent) || !_blocker.CanAttack(ent))
        {
            ent.Comp.Target = null;
            _thrown.StopThrow(ent, args.Component);
            return;
        }
        Grab(ent, args.Target);
        _thrown.StopThrow(ent, args.Component);
    }

    private void Grab(Entity<XenomorphLungeComponent> ent, EntityUid target)
    {
        ent.Comp.Target = null;
        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            melee.NextAttack = _timing.CurTime;
            Dirty(ent, melee);
        }
        _pulling.StopAllPulls(target);
        if (!_pulling.TryStartPull(ent, target, force: true))
            return;
        _stun.TryUpdateParalyzeDuration(target, ent.Comp.StunTime);
        var stunned = EnsureComp<XenomorphLungeStunnedComponent>(target);
        stunned.Stunner = ent;
        stunned.ExpiresAt = _timing.CurTime + ent.Comp.StunTime;
    }

    private void OnStop(Entity<XenomorphLungeComponent> ent, ref StopThrowEvent args)
    {
        if (ent.Comp.Target is { } target && IsPrey(target)
            && _interaction.InRangeUnobstructed(ent.Owner, target, 1.5f))
            Grab(ent, target);
        ent.Comp.Target = null;
    }

    private void OnPullStopped(Entity<XenomorphLungeStunnedComponent> ent, ref PullStoppedMessage args)
    {
        if (args.PulledUid != ent.Owner || args.PullerUid != ent.Comp.Stunner)
            return;
        if (_status.TryGetTime(ent, SharedStunSystem.StunId, out var time) && time.EndEffectTime <= ent.Comp.ExpiresAt)
            _status.TryRemoveStatusEffect(ent, SharedStunSystem.StunId);
        RemCompDeferred<XenomorphLungeStunnedComponent>(ent);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<XenomorphLungeStunnedComponent>();
        while (query.MoveNext(out var uid, out var comp))
            if (_timing.CurTime >= comp.ExpiresAt)
                RemCompDeferred<XenomorphLungeStunnedComponent>(uid);
    }
}
