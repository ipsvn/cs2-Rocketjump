using System.Drawing;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

using CSSTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

namespace Rocketjump;
public static class Helper
{
    public static CBeam SpawnBeam(Vector3 a, Vector3 b, Color color, float killAfter = 10.0f)
    {
        var beam = Utilities.CreateEntityByName<CBeam>("beam")!;
        beam.EndPos.Overwrite(b);
        unsafe
        {
            beam.Teleport(new Vector((nint)(&a)), QAngle.Zero, Vector.Zero);
        }
        beam.Render = color;

        beam.StartFrame = 0;
        beam.FrameRate = 0;
        beam.LifeState = 1;
        beam.Width = 1;
        beam.EndWidth = 1;
        beam.Amplitude = 0;
        beam.Speed = 50;
        beam.Flags = 0;
        beam.BeamType = BeamType_t.BEAM_HOSE;
        beam.FadeLength = 10.0f;

        beam.DispatchSpawn();

        new CSSTimer(killAfter, () =>
        {
            if (beam.IsValid)
            {
                beam.Remove();
            }
        });

        return beam;
    }
    
    public static Vector3 GetEyeOrigin(CCSPlayerPawn pawn)
    {
        var origin = pawn.AbsOrigin;
        if (origin == null)
        {
            return Vector3.Zero;
        }

        return new Vector3(
            origin.X, 
            origin.Y, 
            origin.Z + pawn.CameraServices?.OldPlayerViewOffsetZ ?? 0.0f
        );
    }

    public static void AngleVectors(Vector3 input, out Vector3 forward, out Vector3 right, out Vector3 up)
    {
        Vector3 tmpForward, tmpRight, tmpUp;

        unsafe
        {
            NativeAPI.AngleVectors(
                (nint)(&input), 
                (nint)(&tmpForward), 
                (nint)(&tmpRight), 
                (nint)(&tmpUp)
            );
        }
        
        forward = tmpForward;
        right = tmpRight;
        up = tmpUp;
    }

    public static CCSPlayerController? GetSpectatorTarget(CCSPlayerController controller)
    {
        var observerPawn = controller?.ObserverPawn.Value;
        if (observerPawn == null || observerPawn.ObserverServices == null)
        {
            return null;
        }

        var os = observerPawn.ObserverServices;

        var observerMode = (ObserverMode_t)os.ObserverMode;
        if (observerMode != ObserverMode_t.OBS_MODE_IN_EYE && observerMode != ObserverMode_t.OBS_MODE_CHASE)
        {
            return null;
        }

        var targetPawn = os.ObserverTarget.Value?.As<CCSPlayerPawn>();
        if (targetPawn == null || targetPawn.DesignerName != "player")
        {
            return null;
        }

        var targetController = targetPawn.Controller.Value?.As<CCSPlayerController>();
        if (targetController == null || targetController.DesignerName != "cs_player_controller")
        {
            return null;
        }

        return targetController;
    }

}