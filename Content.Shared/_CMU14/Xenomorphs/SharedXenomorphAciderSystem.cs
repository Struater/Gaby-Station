using Content.Shared._Dumont.Xenomorphs;
using Content.Shared.Movement.Systems;

namespace Content.Shared._CMU14.Xenomorphs;

public sealed class SharedXenomorphAciderSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<XenomorphAciderComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<XenomorphAciderComponent, AfterAutoHandleStateEvent>(OnState);
    }
    private void OnSpeed(Entity<XenomorphAciderComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Primed) args.ModifySpeed(0.55f, 0.55f);
    }
    private void OnState(Entity<XenomorphAciderComponent> ent, ref AfterAutoHandleStateEvent args)
        => _movement.RefreshMovementSpeedModifiers(ent);
}
