namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Portable deterministic pseudo‑random number generator using the xorshift128+ algorithm.
/// Guarantees identical sequences across .NET versions and platforms.
/// </summary>
public class ChronosRandom
{
    private ulong _s0, _s1;

    /// <summary>Creates a new generator from a 64‑bit seed.</summary>
    public ChronosRandom(ulong seed)
    {
        _s0 = SplitMix64(seed);
        _s1 = SplitMix64(_s0);
    }

    /// <summary>Creates a new generator from a 32‑bit seed.</summary>
    public ChronosRandom(int seed) : this((ulong)seed) { }

    private static ulong SplitMix64(ulong state)
    {
        state += 0x9E3779B97F4A7C15UL;
        ulong z = state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Returns a uniformly distributed 64‑bit integer.</summary>
    public ulong NextUInt64()
    {
        ulong s1 = _s0;
        ulong s0 = _s1;
        ulong result = s0 + s1;
        _s0 = s0;
        s1 ^= s1 << 23;
        _s1 = s1 ^ s0 ^ (s1 >> 18) ^ (s0 >> 5);
        return result;
    }

    /// <summary>Returns a uniform double in [0.0, 1.0).</summary>
    public double NextDouble() => (NextUInt64() >> 11) * (1.0 / (1UL << 53));

    /// <summary>Returns a non‑negative random integer less than <paramref name="maxValue"/>.</summary>
    public int Next(int maxValue) => (int)(NextUInt64() % (ulong)maxValue);

    /// <summary>Returns a random integer within the specified range.</summary>
    public int Next(int minValue, int maxValue) => minValue + Next(maxValue - minValue);
}
