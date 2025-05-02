using CounterStrikeSharp.API.Core;

namespace Rocketjump;
public class RocketjumpConfig : BasePluginConfig
{
    public float BulletSpeed { get; set; } = 1000.0f;
    public float MaxDistance { get; set; } = 160.0f;

    public float JumpForceMain { get; set; } = 270.0f;
    public float JumpForceUp { get; set; } = 8.0f;
    public float JumpForceForward { get; set; } = 1.20f;
    public float JumpForceBackward { get; set; } = 1.25f;
    public float RunForceMain { get; set; } = 0.8f;
}