using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Memory.DynamicFunctions;
using CounterStrikeSharp.API.Modules.UserMessages;
using CounterStrikeSharp.API.Modules.Utils;
using System.Drawing;
using System.Numerics;

using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;
using QAngle = CounterStrikeSharp.API.Modules.Utils.QAngle;
using Microsoft.Extensions.Logging;

namespace Rocketjump;
public class Rocketjump : BasePlugin, IPluginConfig<RocketjumpConfig>
{
    public override string ModuleName => "Rocketjump";
    public override string ModuleVersion => "0.0.1";
    public override string ModuleAuthor => "svn (https://github.com/ipsvn)";

    public RocketjumpConfig Config { get; set; } = null!;

    public const int GE_FireBulletsId = 452;

    public FakeConVar<bool> EnableBeams = new("rj_enable_beams", "Enable beams", false);

    public MemoryFunctionVoid<IntPtr, IntPtr> Touch
        = new("55 48 89 E5 41 54 49 89 F4 53 48 8B 87");

    private class DecoyShot
    {
        public required Vector3 Position { get; set; }
        public required CCSPlayerController Controller { get; set; }
    }
    private Dictionary<CDecoyProjectile, DecoyShot> _shots = new();

    public override void Load(bool hotReload)
    {
        HookUserMessage(GE_FireBulletsId, FireBulletsUMHook, HookMode.Pre);

        RegisterListener<Listeners.CheckTransmit>(OnCheckTransmit);

        Touch.Hook(CBaseEntity_Touch_Hook, HookMode.Pre);
    }

    public override void Unload(bool hotReload)
    {
        Touch.Unhook(CBaseEntity_Touch_Hook, HookMode.Pre);
    }

    public void OnConfigParsed(RocketjumpConfig config)
    {
        Config = config;
    }

    public HookResult FireBulletsUMHook(UserMessage um)
    {
        var playerIndex = um.ReadInt("player") & 0x3FFF;

        var pawn = Utilities.GetEntityFromIndex<CCSPlayerPawn>(playerIndex);
        if (pawn == null || pawn.DesignerName != "player")
        {
            return HookResult.Continue;
        }

        var recipients = um.Recipients.ToList();
        foreach (var recipient in recipients)
        {
            if (recipient.Team == CsTeam.Spectator
                && Helper.GetSpectatorTarget(recipient) == pawn.Controller.Value)
            {
                continue;
            }

            um.Recipients.Remove(recipient);
        }
        return HookResult.Continue;
    }

    public void OnCheckTransmit(CCheckTransmitInfoList c)
    {
        foreach (var (info, controller) in c)
        {
            if (controller == null)
            {
                continue;
            }

            foreach (var (decoy, shot) in _shots)
            {
                if (shot.Controller == controller)
                {
                    continue;
                }

                if (controller.Team == CsTeam.Spectator
                    && Helper.GetSpectatorTarget(controller) == shot.Controller)
                {
                    continue;
                }
                info.TransmitEntities.Remove(decoy);
            }
        }
    }

    public HookResult CBaseEntity_Touch_Hook(DynamicHook hook)
    {
        var decoy = hook.GetParam<CDecoyProjectile>(0);

        if (decoy.DesignerName != "decoy_projectile")
        {
            return HookResult.Continue;
        }

        var owner = decoy.OwnerEntity.Value?.As<CCSPlayerPawn>();
        if (owner == null || owner.DesignerName != "player")
        {
            return HookResult.Continue;
        }

        var bulletOrigin = decoy.AbsOrigin;
        var pawnOrigin = owner.AbsOrigin;
        if (bulletOrigin == null || pawnOrigin == null)
        {
            return HookResult.Continue;
        }

        var eyeOrigin = Helper.GetEyeOrigin(owner);

        float distance = Vector3.Distance(bulletOrigin.Into(), pawnOrigin.Into());

        _shots.Remove(decoy, out var shot);

        if (EnableBeams.Value && shot != null)
        {
            Helper.SpawnBeam(bulletOrigin.Into(), shot.Position, Color.Red);
        }

        decoy.Remove();

        // csgo plugin has these arguments round the wrong way, confusing :/
        DoJump(owner, distance, bulletOrigin.Into(), eyeOrigin);

        return HookResult.Handled;
    }

    [GameEventHandler]
    public HookResult OnWeaponFire(EventWeaponFire @event, GameEventInfo info)
    {
        var controller = @event.Userid;
        if (controller == null)
        {
            return HookResult.Continue;
        }

        var weapon = @event.Weapon;
        if (weapon != "weapon_nova")
        {
            return HookResult.Continue;
        }

        var pawn = controller.PlayerPawn.Value;
        var origin = pawn?.AbsOrigin;
        if (pawn == null || origin == null)
        {
            return HookResult.Continue;
        }

        var eyeAngles = pawn.EyeAngles;

        var eyeOrigin = Helper.GetEyeOrigin(pawn);

        var eyeAnglesAsManagedVector = new Vector3(eyeAngles.X, eyeAngles.Y, eyeAngles.Z);

        Helper.AngleVectors(eyeAnglesAsManagedVector, out var forward, out var _, out var _);
        for (int i = 0; i < 3; i++)
        {
            forward[i] = eyeOrigin[i] + (forward[i] * 10.0f);
        }

        if (EnableBeams.Value)
        {
            Helper.SpawnBeam(eyeOrigin, forward, Color.Green);
        }

        var velocity = pawn.AbsVelocity.Into();
        Helper.AngleVectors(eyeAnglesAsManagedVector, out var bulletVelocity, out var _, out var _);

        var realBulletVelocity = Vector3.Normalize(bulletVelocity) * Config.BulletSpeed;

        var addedBulletVelocity = velocity + realBulletVelocity;

        Logger.LogInformation($"Shoot bullet {forward} {addedBulletVelocity}");

        ShootBullet(controller, forward, addedBulletVelocity, eyeAnglesAsManagedVector);

        return HookResult.Continue;
    }


    private void ShootBullet(CCSPlayerController controller, Vector3 origin, Vector3 velocity, Vector3 angle)
    {
        var pawn = controller.PlayerPawn.Value!;

        var decoy = Utilities.CreateEntityByName<CDecoyProjectile>("decoy_projectile");
        if (decoy == null)
        {
            return;
        }

        decoy.OwnerEntity.Raw = pawn.EntityHandle.Raw;

        decoy.Collision.SolidType = SolidType_t.SOLID_VPHYSICS;
        decoy.Collision.CollisionGroup = (byte)CollisionGroup.COLLISION_GROUP_DEBRIS;

        decoy.GravityScale = 0.001f;

        decoy.DispatchSpawn();

        unsafe
        {
            decoy.Teleport(
                new Vector((nint)(&origin)), 
                new QAngle((nint)(&angle)), 
                new Vector((nint)(&velocity))
            );
        }

        _shots[decoy] = new()
        {
            Position = origin,
            Controller = controller
        };

    }

    private void DoJump(CCSPlayerPawn pawn, float distance, Vector3 bulletOrigin, Vector3 pawnOrigin)
    {
        if (distance >= Config.MaxDistance)
        {
            return;
        }

        bool down = false;

        var velocity = pawnOrigin - bulletOrigin;
        if (velocity.Z < 0)
        {
            down = true;
        }

        velocity = Vector3.Normalize(velocity);

        if (EnableBeams.Value)
        {
            Helper.SpawnBeam(bulletOrigin, pawnOrigin, Color.Blue);
        }

        Logger.LogInformation($"DoJump {bulletOrigin} {pawnOrigin}");

        var pawnVelocity = pawn.AbsVelocity;
        if (pawnVelocity == null)
        {
            return;
        }

        velocity = velocity * Config.JumpForceMain;
        var totalVelocity = pawnVelocity.Into() + velocity;

        pawnVelocity.Z = 0.0f;
        totalVelocity.Z = 0.0f;

        if (pawnVelocity.X < 0.0f)
        {
            if (bulletOrigin.X > pawnOrigin.X)
            {
                totalVelocity = totalVelocity * Config.JumpForceForward;
            }
            else
            {
                totalVelocity = totalVelocity * Config.JumpForceBackward;
            }
        }
        else
        {
            if (bulletOrigin.X < pawnOrigin.X)
            {
                totalVelocity = totalVelocity * Config.JumpForceForward;
            }
            else
            {
                totalVelocity = totalVelocity * Config.JumpForceBackward;
            }
        }

        if (pawnVelocity.Y < 0.0f)
        {
            if (bulletOrigin.Y > pawnOrigin.Y)
            {
                totalVelocity = totalVelocity * Config.JumpForceForward;
            }
            else
            {
                totalVelocity = totalVelocity * Config.JumpForceBackward;
            }
        }
        else
        {
            if (bulletOrigin.Y < pawnOrigin.Y)
            {
                totalVelocity = totalVelocity * Config.JumpForceForward;
            }
            else
            {
                totalVelocity = totalVelocity * Config.JumpForceBackward;
            }
        }

        if (pawn.OnGroundLastTick)
        {
            totalVelocity = totalVelocity * Config.RunForceMain;
        }

        var forceUp = Config.JumpForceUp * (Config.MaxDistance - distance);
        if (distance > 37.0f)
        {
            if (totalVelocity.Z > 0.0f)
            {
                totalVelocity.Z = 1000.0f + forceUp;
            }
            else
            {
                totalVelocity.Z = totalVelocity.Z + forceUp;
            }
        }
        else
        {
            totalVelocity.Z = totalVelocity.Z + forceUp / 1.37f;
        }

        if (down)
        {
            velocity.Z *= -1.0f;
        }

        unsafe
        {
            pawn.Teleport(
                null,
                null,
                new Vector((nint)(&totalVelocity))
            );
        }

    }

}
