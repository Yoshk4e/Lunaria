namespace Lunaria.Silver;


public static class Dh
{
    public static readonly UInt128 P = UInt128.MaxValue - 158;

    public static readonly UInt128 G = 5;

    public static UInt128 AddMod(UInt128 a, UInt128 b)
    {
        var sum = unchecked(a + b);

        if (sum < a)
            return unchecked(sum + 159);

        return sum >= P ? sum - P : sum;
    }

    private static (UInt128 Lo, UInt128 Hi) WideMultiply(UInt128 a, UInt128 b)
    {
        var a0 = (ulong)(a >> 0);
        var a1 = (ulong)(a >> 64);
        var b0 = (ulong)(b >> 0);
        var b1 = (ulong)(b >> 64);

        var ll = (UInt128)a0 * b0;
        var lh = (UInt128)a0 * b1;
        var hl = (UInt128)a1 * b0;
        var hh = (UInt128)a1 * b1;

        var mid = unchecked(lh + hl);
        var midCarry = mid < lh;

        var lo = unchecked(ll + (mid << 64));
        var loCarry = lo < ll;

        var hi = unchecked(hh + (mid >> 64) + ((UInt128)(midCarry ? 1UL : 0UL) << 64)
                           + (loCarry ? (UInt128)1 : 0));
        return (lo, hi);
    }

    public static UInt128 MulMod(UInt128 a, UInt128 b)
    {
        var (lo, hi) = WideMultiply(a, b);

        while (hi != 0)
        {
            var (mLo, mHi) = WideMultiply(hi, b: 159);
            var t = unchecked(lo + mLo);
            var carry = t < lo;
            lo = t;
            hi = unchecked(mHi + (carry ? (UInt128)1 : 0));
        }
        return lo >= P ? lo - P : lo;
    }

    public static UInt128 PowMod(UInt128 basis, UInt128 exponent)
    {
        UInt128 result = 1;
        basis %= P;

        for (var i = 127; i >= 0; i--)
        {
            result = MulMod(result, result);

            if ((exponent >> i & 1) == 1)
                result = MulMod(result, basis);
        }
        return result;
    }
}
