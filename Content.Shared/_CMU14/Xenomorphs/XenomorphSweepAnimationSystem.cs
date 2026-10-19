using Content.Shared._Dumont.Xenomorphs;
using Content.Shared.Interaction;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._CMU14.Xenomorphs;

public sealed class XenomorphSweepAnimationSystem : EntitySystem
{
    [Dependency] private readonly RotateToFaceSystem _rotate = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Update(float frameTime)
    {
        if (_net.IsClient)
            return;
        var query = EntityQueryEnumerator<XenomorphSweepingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sweeping, out var xform))
        {
            if (sweeping.NextRotation > _timing.CurTime)
                continue;
            if (sweeping.TotalRotations >= 4)
            {
                RemCompDeferred<XenomorphSweepingComponent>(uid);
                continue;
            }
            sweeping.TotalRotations++;
            sweeping.NextRotation = _timing.CurTime + sweeping.Delay;
            sweeping.LastDirection ??= _transform.GetWorldRotation(xform).GetDir();
            var angle = sweeping.LastDirection.Value.ToAngle() + Angle.FromDegrees(90);
            sweeping.LastDirection = angle.GetDir();
            Dirty(uid, sweeping);
            _rotate.TryFaceAngle(uid, angle, xform);
        }
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<XenomorphSweepingComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var sweeping, out var xform))
        {
            if (sweeping.LastDirection is { } direction)
                _rotate.TryFaceAngle(uid, direction.ToAngle(), xform);
        }
    }
}
