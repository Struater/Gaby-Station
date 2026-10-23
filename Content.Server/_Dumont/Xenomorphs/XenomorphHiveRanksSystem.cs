using Content.Shared._CMU14.Xenomorphs;
using Content.Server._CMU14.Xenomorphs;
using Content.Shared._Dumont.Xenomorphs;
using Content.Shared._White.Xenomorphs.Caste;
using Content.Shared._White.Xenomorphs.Xenomorph;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server._Dumont.Xenomorphs;

public sealed partial class XenomorphHiveRanksSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private MobStateSystem _mobs = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private XenomorphHiveCaptureSystem _hive = default!;
    private bool _extraGuardUnlocked;
    private EntityUid? _queen;
    public bool QueenHasExisted { get; private set; }
    public bool QueenFallen { get; private set; }
    public int RoyalGuardLimit => _extraGuardUnlocked ? 2 : 1;

    public override void Initialize()
    {
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRestart);
        SubscribeLocalEvent<XenomorphLineageComponent, MapInitEvent>(OnSpawned);
        SubscribeLocalEvent<XenomorphLineageComponent, MobStateChangedEvent>(OnState);
        SubscribeLocalEvent<XenomorphLineageComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<XenomorphLineageComponent, ComponentShutdown>(OnRemoved);
    }

    private void OnRestart(RoundRestartCleanupEvent args)
    {
        _extraGuardUnlocked = false;
        _queen = null;
        QueenHasExisted = false;
        QueenFallen = false;
    }

    private void OnSpawned(Entity<XenomorphLineageComponent> ent, ref MapInitEvent args)
    {
        if (!HasComp<XenomorphRoyalComponent>(ent) || QueenHasExisted) return;
        QueenHasExisted = true;
        _queen = ent;
    }

    private void OnState(Entity<XenomorphLineageComponent> ent, ref MobStateChangedEvent args)
    {
        if (ent.Owner != _queen || QueenFallen || args.NewMobState != MobState.Dead) return;
        QueenFallen = true;
        _hive.AnnounceHive(Loc.GetString("xenomorph-queen-fallen"),
            new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_queen_died.ogg", AudioParams.Default.WithVolume(-6)));
    }

    public bool UnlockRoyalGuardSlot()
    {
        if (_extraGuardUnlocked) return false;
        _extraGuardUnlocked = true;
        return true;
    }

    private void OnRemoved(Entity<XenomorphLineageComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Owner != _queen || QueenFallen) return;
        QueenFallen = true;
        _hive.AnnounceHive(Loc.GetString("xenomorph-queen-fallen"),
            new SoundPathSpecifier("/Audio/_RMC14/Xeno/alien_queen_died.ogg", AudioParams.Default.WithVolume(-6)));
    }

    public int Tier(ProtoId<XenomorphCastePrototype> caste) => _prototypes.Index(caste).Tier;

    public bool CanEvolve(EntityUid user, ProtoId<XenomorphCastePrototype> caste, bool popup = true)
    {
        if (!HasComp<XenomorphLineageComponent>(user)) return true;
        string? reason = null;
        if (caste == "Queen" && QueenHasExisted)
            reason = QueenFallen ? "xenomorph-queen-fallen-no-evolution" : "xenomorph-queen-only-once";
        else if (Tier(caste) == 3)
        {
            var count = 0;
            var query = EntityQueryEnumerator<XenomorphLineageComponent, XenomorphComponent>();
            while (query.MoveNext(out var uid, out _, out var xeno))
                if (uid != user && !TerminatingOrDeleted(uid) && !_mobs.IsDead(uid) && Tier(xeno.Caste) == 3) count++;
            if (count >= RoyalGuardLimit) reason = "xenomorph-royal-guard-limit";
        }
        if (reason == null) return true;
        if (popup) _popup.PopupEntity(Loc.GetString(reason, ("limit", RoyalGuardLimit)), user, user);
        return false;
    }

    private void OnExamined(Entity<XenomorphLineageComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<XenomorphComponent>(ent, out var xeno)) return;
        var tier = Tier(xeno.Caste);
        if (tier is >= 1 and <= 3) args.PushMarkup(Loc.GetString("xenomorph-rank-examine", ("rank", Loc.GetString($"xenomorph-rank-{tier}"))));
    }
}
