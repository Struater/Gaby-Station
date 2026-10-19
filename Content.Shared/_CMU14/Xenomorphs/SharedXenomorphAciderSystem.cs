using Content.Shared._Dumont.Xenomorphs;
using Content.Shared.Movement.Systems;
using Robust.Shared.Physics.Events;
using Content.Shared.Physics;

namespace Content.Shared._CMU14.Xenomorphs;

public sealed partial class SharedXenomorphAciderSystem : EntitySystem
{
    [Dependency] private Robust.Shared.Physics.Systems.SharedPhysicsSystem _physics = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<XenomorphAciderComponent, PreventCollideEvent>(OnCollide);
        SubscribeLocalEvent<XenomorphAciderComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
        SubscribeLocalEvent<XenomorphAciderComponent, AfterAutoHandleStateEvent>(OnState);
    }
    private void OnCollide(Entity<XenomorphAciderComponent> ent, ref PreventCollideEvent args)
    {
        if (ent.Comp.Hiding && (args.OtherFixture.CollisionLayer & (int)CollisionGroup.TableLayer) != 0
            && (args.OtherFixture.CollisionLayer & (int)CollisionGroup.Impassable) == 0) args.Cancelled = true;
    }
    private void OnSpeed(Entity<XenomorphAciderComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Primed) args.ModifySpeed(0.55f, 0.55f);
    }
    private void OnState(Entity<XenomorphAciderComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        _movement.RefreshMovementSpeedModifiers(ent);
        _physics.RegenerateContacts(ent.Owner);
    }
}
