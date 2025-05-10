// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Random.CompatImpl.cs#L264

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

public class LegacyRandom
{
    private CompatPrng _prng;

    public LegacyRandom(int seed) =>
        _prng.EnsureInitialized(seed);

    public double Sample() => _prng.Sample();

    public int Next() => _prng.InternalSample();

    public int Next(int maxValue)
    {
        // We can use ConvertToIntegerNative here since we know the result of
        // scaling the sample is in the range [0, int.MaxValue) and therefore
        // the integer portion is exactly representable since it's < 2^52.
        return double.ConvertToIntegerNative<int>(_prng.Sample() * maxValue);
    }

    public int Next(int minValue, int maxValue)
    {
        // We can use ConvertToIntegerNative here since we know the result of
        // scaling the sample is in the range [0, uint.MaxValue) and therefore
        // the integer portion is exactly representable since it's < 2^52.
        long range = (long)maxValue - minValue;
        return range <= int.MaxValue ?
            double.ConvertToIntegerNative<int>(_prng.Sample() * range) + minValue :
            (int)(double.ConvertToIntegerNative<long>(_prng.GetSampleForLargeRange() * range) + minValue);
    }

    public long NextInt64()
    {
        while (true)
        {
            // Get top 63 bits to get a value in the range [0, long.MaxValue], but try again
            // if the value is actually long.MaxValue, as the method is defined to return a value
            // in the range [0, long.MaxValue).
            ulong result = NextUInt64() >> 1;
            if (result != long.MaxValue)
            {
                return (long)result;
            }
        }
    }

    public long NextInt64(long maxValue) => NextInt64(0, maxValue);

    public long NextInt64(long minValue, long maxValue)
    {
        ulong exclusiveRange = (ulong)(maxValue - minValue);

        if (exclusiveRange > 1)
        {
            // Narrow down to the smallest range [0, 2^bits] that contains maxValue - minValue
            // Then repeatedly generate a value in that outer range until we get one within the inner range.
            int bits = Log2Ceiling(exclusiveRange);
            while (true)
            {
                ulong result = NextUInt64() >> (sizeof(long) * 8 - bits);
                if (result < exclusiveRange)
                {
                    return (long)result + minValue;
                }
            }
        }

        Debug.Assert(minValue == maxValue || minValue + 1 == maxValue);
        return minValue;
    }


    /// <summary>Returns the integer (ceiling) log of the specified value, base 2.</summary>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Log2Ceiling(ulong value)
    {
        int result = BitOperations.Log2(value);
        if (BitOperations.PopCount(value) != 1)
        {
            result++;
        }
        return result;
    }

    /// <summary>Produces a value in the range [0, ulong.MaxValue].</summary>
    private ulong NextUInt64() =>
            ((ulong)(uint)Next(1 << 22)) |
        (((ulong)(uint)Next(1 << 22)) << 22) |
        (((ulong)(uint)Next(1 << 20)) << 44);

    public double NextDouble() => _prng.Sample();

    public float NextSingle()
    {
        while (true)
        {
            float f = (float)_prng.Sample();
            if (f < 1.0f) // reject 1.0f, which is rare but possible due to rounding
            {
                return f;
            }
        }
    }

    public void NextBytes(byte[] buffer) => _prng.NextBytes(buffer);

    public void NextBytes(Span<byte> buffer) => _prng.NextBytes(buffer);

    private struct CompatPrng
    {
        private int[]? _seedArray;
        private int _inext;
        private int _inextp;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [MemberNotNull(nameof(_seedArray))]
        internal void EnsureInitialized(int seed)
        {
            if (_seedArray is null)
            {
                Initialize(seed);
            }
        }

        [MemberNotNull(nameof(_seedArray))]
        private void Initialize(int seed)
        {
            Debug.Assert(_seedArray is null);

            int[] seedArray = new int[56];

            int subtraction = (seed == int.MinValue) ? int.MaxValue : Math.Abs(seed);
            int mj = 161803398 - subtraction; // magic number based on Phi (golden ratio)
            seedArray[55] = mj;
            int mk = 1;

            int ii = 0;
            for (int i = 1; i < 55; i++)
            {
                // The range [1..55] is special (Knuth) and so we're wasting the 0'th position.
                if ((ii += 21) >= 55)
                {
                    ii -= 55;
                }

                seedArray[ii] = mk;
                mk = mj - mk;
                if (mk < 0)
                {
                    mk += int.MaxValue;
                }

                mj = seedArray[ii];
            }

            for (int k = 1; k < 5; k++)
            {
                for (int i = 1; i < 56; i++)
                {
                    int n = i + 30;
                    if (n >= 55)
                    {
                        n -= 55;
                    }

                    seedArray[i] -= seedArray[1 + n];
                    if (seedArray[i] < 0)
                    {
                        seedArray[i] += int.MaxValue;
                    }
                }
            }

            _seedArray = seedArray;
            _inext = 0;
            _inextp = 21;
        }

        internal double Sample() =>
            // Including the division at the end gives us significantly improved random number distribution.
            InternalSample() * (1.0 / int.MaxValue);

        internal void NextBytes(Span<byte> buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)InternalSample();
            }
        }


        internal int InternalSample()
        {
            Debug.Assert(_seedArray is not null);

            int locINext = _inext;
            if (++locINext >= 56)
            {
                locINext = 1;
            }

            int locINextp = _inextp;
            if (++locINextp >= 56)
            {
                locINextp = 1;
            }

            int[] seedArray = _seedArray;
            int retVal = seedArray[locINext] - seedArray[locINextp];

            if (retVal == int.MaxValue)
            {
                retVal--;
            }
            if (retVal < 0)
            {
                retVal += int.MaxValue;
            }

            seedArray[locINext] = retVal;
            _inext = locINext;
            _inextp = locINextp;

            return retVal;
        }

        internal double GetSampleForLargeRange()
        {
            // The distribution of the double returned by Sample is not good enough for a large range.
            // If we use Sample for a range [int.MinValue..int.MaxValue), we will end up getting even numbers only.
            int result = InternalSample();

            // We can't use addition here: the distribution will be bad if we do that.
            if (InternalSample() % 2 == 0) // decide the sign based on second sample
            {
                result = -result;
            }

            double d = result;
            d += int.MaxValue - 1; // get a number in range [0..2*int.MaxValue-1)
            d /= 2u * int.MaxValue - 1;
            return d;
        }
    }
}
