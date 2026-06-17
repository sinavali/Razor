using System.Buffers;

namespace Chronos.Core.Abstractions.Shared;

/// <summary>Base class for all technical indicators.</summary>
public abstract class Indicator : IDisposable, IWindowAwareIndicator
{
    /// <summary>Unique signature used for chaining and caching.</summary>
    public string Signature { get; internal set; } = string.Empty;

    /// <summary>Gives access to the underlying tick stream aggregate window.</summary>
    protected TickWindow Window { get; private set; } = null!;

    private double[] _buffer = [];
    private int _count;
    private bool _isRented;

    /// <summary>Indexer. The index is absolute and wraps around the circular buffer.</summary>
    public double this[long index]
    {
        get => _count == 0 ? 0 : _buffer[(int)(index % _count)];
        set => _buffer[(int)(index % _count)] = value;
    }

    /// <summary>Dependency injection from the Indicator Registry.</summary>
    public void SetWindow(TickWindow window) => Window = window;

    /// <summary>Initialises the indicator with a fixed buffer size.
    /// Must be called with a positive capacity.</summary>
    public virtual void Initialize(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentException("Indicator capacity must be positive.", nameof(capacity));
        }

        if (_isRented)
        {
            ArrayPool<double>.Shared.Return(_buffer);
            _isRented = false;
        }

        _buffer = ArrayPool<double>.Shared.Rent(capacity);
        _count = capacity;
        _isRented = true;
        Array.Clear(_buffer, 0, capacity);
    }

    /// <summary>Called for every new tick to update the indicator.</summary>
    public abstract void Calculate(long index);

    /// <summary>Releases the rented buffer.</summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Dispose pattern implementation.</summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_isRented)
            {
                ArrayPool<double>.Shared.Return(_buffer);
                _buffer = [];
                _count = 0;
                _isRented = false;
            }
        }
    }
}
