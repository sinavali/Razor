namespace Chronos.Core.Abstractions.Shared;

/// <summary>
/// Portable deterministic pseudo‑random number generator using the xorshift128+ algorithm.
/// Guarantees identical sequences across .NET versions and platforms.
/// <para><b>This class is not thread‑safe.</b> For concurrent usage, create one instance per thread.</para>
/// </summary>
public sealed class ChronosRandom
{
    private ulong _s0, _s1;

    /// <summary>Creates a new generator from a 64‑bit seed.</summary>
    public ChronosRandom(ulong seed)
    {
        _s0 = SplitMix64(seed);
        _s1 = SplitMix64(_s0);
    }

    /// <summary>
    /// Creates a new generator from a 32‑bit seed.
    /// </summary>
    /// <param name="seed">
    /// A non‑negative seed value. Negative values are rejected because their direct
    /// conversion to <see cref="ulong"/> would produce unpredictable sequences.
    /// Use the <see cref="ChronosRandom(ulong)"/> constructor if a full 64‑bit seed is needed.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="seed"/> is negative.</exception>
    public ChronosRandom(int seed) : this((ulong)seed)
    {
        // Validate after the call to ensure clarity: the cast itself would work,
        // but we want to catch negative seeds early with a meaningful message.
        if (seed < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), seed,
                "Seed must be non‑negative. Use the ChronosRandom(ulong) constructor for arbitrary 64‑bit seeds.");
        }
    }

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

    /// <summary>Returns a uniform double in [0.0, maxValue).</summary>
    public double NextDouble(double maxValue) => NextDouble() * maxValue;

    /// <summary>Returns a uniform double in [minValue, maxValue).</summary>
    public double NextDouble(double minValue, double maxValue) =>
        minValue + NextDouble() * (maxValue - minValue);

    /// <summary>Returns a non‑negative random integer less than <paramref name="maxValue"/>.</summary>
    public int Next(int maxValue)
    {
        if (maxValue <= 0)
        {
            return 0;
        }

        return (int)(NextUInt64() % (ulong)maxValue);
    }

    /// <summary>Returns a random integer within the specified range.</summary>
    public int Next(int minValue, int maxValue)
    {
        if (minValue >= maxValue)
        {
            return minValue;
        }

        return minValue + Next(maxValue - minValue);
    }

    /// <summary>Fills a byte array with random values.</summary>
    public void NextBytes(byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(NextUInt64() & 0xFF);
        }
    }
}
