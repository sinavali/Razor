# Chronos.Core.Shared

**Version:** 1.0.0 LTS  
**Status:** Internal – closed source  
**Last Updated:** 2026-07-09  

---

## Overview

`Chronos.Core.Shared` is a private utility library that provides high‑performance, low‑level I/O primitives for the Chronos Engine. It is not part of the public SDK and is intended for internal use only.

The primary responsibility of this project is to enable **zero‑copy, memory‑mapped access** to large historical tick data files, reducing memory pressure and improving backtest and optimisation performance.

---

## Key Components

### `BinaryDataMapper`

A static class that handles reading and writing binary tick files in the Chronos format (`.chrs`).

- **File format:** 8‑byte header (`magic = 0x53524843` "CHRS", `version = 1`) followed by raw `Tick` structs.
- **Write:** `WriteTicksToBinary(string filePath, Tick[] ticks)` – writes a sorted array of ticks to disk.
- **Map:** `MapTicks(string filePath)` – returns a `MemoryMappedTickList` for read‑only access.

**Usage (internal):**
```csharp
var ticks = new Tick[] { ... };
BinaryDataMapper.WriteTicksToBinary("data.chrs", ticks);
using var mmList = BinaryDataMapper.MapTicks("data.chrs");
Tick first = mmList[0];
```

---

### `MemoryMappedTickList`

An `IReadOnlyList<Tick>` implementation that uses memory‑mapped I/O for zero‑copy, read‑only access to binary tick files.

- **Zero‑copy:** The file is mapped into virtual memory; data is paged in on demand.
- **Thread‑safe:** Multiple readers can access the same file concurrently.
- **Dispose:** Releases the memory‑mapped view and file handles.

**Performance characteristics:**
- O(1) element access via pointer arithmetic.
- Supports files up to `int.MaxValue` ticks (≈ 2.1 billion ticks).
- Header detection: skips the 8‑byte header if present; otherwise reads raw ticks.

**Implementation details:**
- Uses `MemoryMappedFile` and `MemoryMappedViewAccessor` from `System.IO.MemoryMappedFiles`.
- Raw pointer access via `Unsafe.Read<T>` for blittable `Tick` structs.
- Finalizer releases unmanaged resources if `Dispose()` is not called.

---

### `BorrowedTickData`

A disposable wrapper that groups memory‑mapped tick streams and their file paths, coordinating cleanup and adapter notification.

- **Purpose:** Encapsulates the full lifecycle of tick data files from adapter fetch to disposal.
- **Reference counting:** Tracks file usage across multiple consumers to avoid premature deletion.
- **Disposal:** Calls `adapter.NotifyFileSafeToDeleteAsync()` and optionally deletes the file based on `DataActionPolicy`.

**Usage (internal):**
```csharp
await using var borrowed = new BorrowedTickData(
    streams, symbols, mappedLists, filePaths, adapter, policy);
// Access borrowed.Streams and borrowed.Symbols
// After disposal, files are safely cleaned up.
```

---

### `Helpers`

A collection of static utility classes:

- **`MathHelpers`** – Clamping, percentage change calculations.
- **`ValidationHelpers`** – Argument validation (positive, range, not null).
- **`TimeHelpers`** – Unix timestamp conversion utilities.

---

## Dependencies

| Dependency | Purpose |
|------------|---------|
| `Chronos.Core.Sdk` | Provides `Tick`, `DataActionPolicy`, `IAdapterCapability`, and domain types. |

---

## Build & Integration

This project is compiled as a `net10.0` class library and is referenced by:

- `Chronos.Core.Kernel` (for backtesting and optimisation)
- `Chronos.Core.Engine` (indirectly via Kernel)

It has **no public API** – all types are internal to the `Chronos.Core` namespace.

---

## Testing

Unit and integration tests are located in `tests/Chronos.Core.Sdk.IntegrationTests/Shared/` (the test project name is misleading; it covers both Sdk and Shared). Key test coverage includes:

- `BinaryDataMapper_IntegrationTests` – round‑trip read/write, unsorted data detection, file versioning.
- `BorrowedTickData_IntegrationTests` – reference counting, notification to adapter, disposal.
- `MemoryMappedTickListEnumerableTests` – enumeration and indexing.

---

## Performance Considerations

- **Memory consumption:** Memory‑mapped files do not load the entire file into the managed heap; physical memory is paged in as needed. This allows backtesting of multi‑year tick archives without exceeding memory limits.
- **Allocation:** The `Tick` struct is blittable and accessed directly via pointer, avoiding boxing and copying.
- **Concurrency:** Multiple `MemoryMappedTickList` instances can open the same file simultaneously with `FileShare.Read`.

---

## Security

- All file paths are resolved relative to the engine base directory.
- File access is read‑only; no modifications are made to the original adapter‑provided files.
- File deletion is delegated to the adapter after `NotifyFileSafeToDeleteAsync` is called, ensuring that the adapter controls its own data lifecycle.

---

## Future Enhancements

- **Compressed tick storage:** Support for LZ4 or Zstd compression to reduce disk footprint.
- **Network streaming:** Ability to read tick data directly from cloud storage (S3, Azure Blob) without local files.
- **Indexed access:** Support for time‑based binary search to skip irrelevant data ranges.

---

*This README is intended for Chronos core developers. For extension development, see the [Chronos.Core.Sdk README](src/Chronos.Core.Sdk/README.md).*