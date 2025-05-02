using System.Numerics;

using Vector = CounterStrikeSharp.API.Modules.Utils.Vector;

namespace Rocketjump;

public static class VectorExtensions
{
    public static void Overwrite(this Vector vector, Vector3 other)
    {
        vector.X = other.X;
        vector.Y = other.Y;
        vector.Z = other.Z;
    }

    public static Vector3 Into(this Vector vector)
    {
        return new Vector3(vector.X, vector.Y, vector.Z);
    }
}