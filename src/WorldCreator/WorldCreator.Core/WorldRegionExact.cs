using System.Globalization;
using System.Numerics;
using WorldCreator.Core.Geometry;

namespace WorldCreator.Core.Spatial;

/// <summary>Exact predicates on the finite binary64 values already produced by
/// the arrangement. This does not replace the surface owner's floating geometry.</summary>
internal static class WorldRegionExact
{
    private readonly record struct Binary(BigInteger Value,int Exponent)
    {
        internal static Binary From(double value)
        {
            long bits=BitConverter.DoubleToInt64Bits(value);
            int exponent=(int)((bits>>52)&2047);long coefficient=bits&0xfffffffffffffL;
            if(exponent!=0)coefficient|=1L<<52;
            if(coefficient==0)return new(BigInteger.Zero,0);
            int shift=BitOperations.TrailingZeroCount((ulong)coefficient);coefficient>>=shift;
            if(bits<0)coefficient=-coefficient;
            return new(new(coefficient),(exponent==0?-1074:exponent-1075)+shift);
        }
        private static Binary Normalize(BigInteger value,int exponent)
        {
            if(value.IsZero)return new(BigInteger.Zero,0);
            while(value.IsEven){value>>=1;exponent++;}
            return new(value,exponent);
        }
        public static Binary operator -(Binary a,Binary b)
        {
            int exponent=Math.Min(a.Exponent,b.Exponent);
            return Normalize((a.Value<<(a.Exponent-exponent))-(b.Value<<(b.Exponent-exponent)),exponent);
        }
        public static Binary operator *(Binary a,Binary b)
            =>a.Value.IsZero||b.Value.IsZero?new(BigInteger.Zero,0):new(a.Value*b.Value,a.Exponent+b.Exponent);
        internal string Key=>Value.ToString(CultureInfo.InvariantCulture)+"@"+Exponent.ToString(CultureInfo.InvariantCulture);
    }

    internal static string Constant(double value,double x,double y,WorldPoint origin)
        =>(Binary.From(value)-Binary.From(x)*Binary.From(origin.X)-Binary.From(y)*Binary.From(origin.Y)).Key;

    internal static int Orientation(WorldPoint a,WorldPoint b,WorldPoint c)
    {
        // Axis comparisons are already exact and cover most sweep-generated edges.
        if(a.Y==b.Y)return b.X.CompareTo(a.X)*c.Y.CompareTo(b.Y);
        if(a.X==b.X)return -b.Y.CompareTo(a.Y)*c.X.CompareTo(b.X);
        if(b.Y==c.Y)return -b.Y.CompareTo(a.Y)*c.X.CompareTo(b.X);
        if(b.X==c.X)return b.X.CompareTo(a.X)*c.Y.CompareTo(b.Y);
        var dx=Binary.From(b.X)-Binary.From(a.X);var dy=Binary.From(b.Y)-Binary.From(a.Y);
        var ex=Binary.From(c.X)-Binary.From(b.X);var ey=Binary.From(c.Y)-Binary.From(b.Y);
        return (dx*ey-dy*ex).Value.Sign;
    }
}
