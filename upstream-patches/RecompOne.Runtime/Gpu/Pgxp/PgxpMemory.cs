using System.Runtime.CompilerServices;
using RecompOne.Runtime.Memory;

namespace RecompOne.Runtime.Pgxp;

/// <summary>Precision metadata keyed to real RAM/scratchpad words, never MMIO aliases.</summary>
public static class PgxpMemory
{
    // Private shadow flags: each surviving coordinate half can retain its origin
    // while the other half is overwritten. Valid2 is set only when BOTH halves
    // have the same origin. No extra full-size metadata arrays are needed.
    private const uint DepthLow = 1u << 4, DepthHigh = 1u << 12;
    private static PgxpValue[] _shadow = [];
    private static uint _ramMask, _ramWords, _hostBytes;

    public static void Init(uint ramSize)
    {
        if (ramSize < MemoryMap.RetailRamSize || ramSize > MemoryMap.RamWindow || (ramSize & (ramSize - 1)) != 0)
            throw new ArgumentOutOfRangeException(nameof(ramSize));
        _hostBytes = 0;
        _ramMask = ramSize - 1u;
        _ramWords = ramSize >> 2;
        _shadow = new PgxpValue[_ramWords + (MemoryMap.ScratchpadWindow >> 2)];
    }
    public static void Free() { _shadow = []; _hostBytes = 0; }

    public static void ConfigureHostScratch(uint size)
    {
        if (size == 0 || size > MemoryMap.HostScratchMaxSize || (size & 3) != 0)
            throw new ArgumentOutOfRangeException(nameof(size));
        if (_shadow.Length == 0) throw new InvalidOperationException("Precision memory is not initialized.");
        if (_hostBytes == size) return;
        if (_hostBytes != 0) throw new InvalidOperationException("Precision workspace cannot be silently resized.");
        Array.Resize(ref _shadow, checked((int)(_ramWords + (MemoryMap.ScratchpadWindow >> 2) + (size >> 2))));
        _hostBytes = size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryIndex(uint address, out int index)
    {
        index = 0;
        if (_shadow.Length == 0) return false;
        uint physical = address & MemoryMap.PhysicalMask;
        if (physical < MemoryMap.RamWindow)
        {
            index = (int)((physical & _ramMask) >> 2);
            return true;
        }
        uint scratch = physical - MemoryMap.ScratchpadBase;
        if (scratch < MemoryMap.ScratchpadWindow)
        {
            index = (int)(_ramWords + (scratch >> 2));
            return true;
        }
        uint host = physical - MemoryMap.HostScratchBase;
        if (host >= _hostBytes) return false;
        index = (int)(_ramWords + (MemoryMap.ScratchpadWindow >> 2) + (host >> 2));
        return true;
    }

    public static void Store(uint address, in PgxpValue value, uint written)
    {
        if (!Pgxp.MemoryTracking || (address & 3) != 0 || !TryIndex(address, out int i)) return;
        _shadow[i] = value;
        _shadow[i].Value = written;
        _shadow[i].Flags &= ~(DepthLow | DepthHigh);
        if ((value.Flags & PgxpFlags.Valid2) != 0)
        {
            if ((value.Flags & PgxpFlags.Valid0) != 0) _shadow[i].Flags |= DepthLow;
            if ((value.Flags & PgxpFlags.Valid1) != 0) _shadow[i].Flags |= DepthHigh;
        }
    }

    public static void StoreHalf(uint address, in PgxpValue src, ushort written)
    {
        if (!Pgxp.MemoryTracking || (address & 1) != 0 || !TryIndex(address, out int i)) return;
        ref var slot = ref _shadow[i];
        bool high = (address & 2) != 0;
        uint own = high ? PgxpFlags.Valid1 : PgxpFlags.Valid0;
        uint ownDepth = high ? DepthHigh : DepthLow, otherDepth = high ? DepthLow : DepthHigh;
        bool srcValid = (src.Flags & PgxpFlags.Valid0) != 0;
        bool srcDepth = srcValid && (src.Flags & PgxpFlags.Valid2) != 0;
        bool sameOrigin = srcDepth && (slot.Flags & otherDepth) != 0 &&
            slot.Count == src.Count && slot.Z == src.Z && slot.Transform == src.Transform;
        if (high)
        {
            slot.Y = src.X;
            slot.Value = (slot.Value & 0xFFFF) | ((uint)written << 16);
        }
        else
        {
            slot.X = src.X;
            slot.Value = (slot.Value & 0xFFFF0000) | written;
        }
        slot.Flags = (slot.Flags & ~(own | ownDepth | PgxpFlags.Valid2)) | (srcValid ? own : 0u);
        if (srcDepth)
        {
            // When replacing last frame's coordinates one half at a time, keep
            // the new half's origin even though its old partner does not match.
            // The second new half can then complete the coherent pair.
            if (!sameOrigin) slot.Flags &= ~otherDepth;
            slot.Z = src.Z;
            slot.Count = src.Count;
            slot.Transform = src.Transform;
            slot.Flags |= ownDepth;
        }
        if ((slot.Flags & (DepthLow | DepthHigh | PgxpFlags.ValidLow)) ==
            (DepthLow | DepthHigh | PgxpFlags.ValidLow)) slot.Flags |= PgxpFlags.Valid2;
    }

    public static void LoadHalf(uint address, uint value, ref PgxpValue dest)
    {
        dest = default;
        dest.Value = value;
        if (!Pgxp.MemoryTracking || (address & 1) != 0 || !TryIndex(address, out int i)) return;
        ref var slot = ref _shadow[i];
        bool high = (address & 2) != 0;
        uint flag = high ? PgxpFlags.Valid1 : PgxpFlags.Valid0;
        uint stored = high ? slot.Value >> 16 : slot.Value & 0xFFFF;
        if ((slot.Flags & flag) == 0 || stored != (value & 0xFFFF)) return;
        dest.X = high ? slot.Y : slot.X;
        dest.Y = (short)(value >> 16); // signed LH and unsigned LHU differ here
        dest.Z = slot.Z;
        dest.Count = slot.Count;
        dest.Transform = slot.Transform;
        dest.Flags = PgxpFlags.ValidLow | ((slot.Flags & (high ? DepthHigh : DepthLow)) != 0 ? PgxpFlags.Valid2 : 0u);
    }

    public static void LoadInto(uint address, uint value, ref PgxpValue dest)
    {
        dest = default;
        if (Pgxp.MemoryTracking && (address & 3) == 0 && TryIndex(address, out int i))
        {
            dest = _shadow[i];
            if (dest.Value != value) dest.Flags = PgxpFlags.None;
        }
        dest.Value = value;
    }

    public static void Invalidate(uint address, uint written)
    {
        if (!Pgxp.MemoryTracking || !TryIndex(address, out int i)) return;
        _shadow[i] = new PgxpValue { Value = written };
    }

    /// <summary>
    /// Called before every ordinary guest write, including HLE/bulk writes. The
    /// generated SW/SH/SWC2 hook then attaches new metadata, if any. Invalidation
    /// must happen even when the new integer happens to equal an old coordinate.
    /// </summary>
    public static void InvalidateBytes(uint address, int length)
    {
        if (!Pgxp.MemoryTracking || _shadow.Length == 0) return;
        for (int k = 0; k < length;)
        {
            uint a = address + (uint)k;
            int part = Math.Min(length - k, 4 - (int)(a & 3));
            if (TryIndex(a, out int i))
            {
                ref var slot = ref _shadow[i];
                int first = (int)(a & 3), last = first + part - 1;
                if (first < 2) slot.Flags &= ~(PgxpFlags.Valid0 | DepthLow | PgxpFlags.Valid2);
                if (last >= 2) slot.Flags &= ~(PgxpFlags.Valid1 | DepthHigh | PgxpFlags.Valid2);
                if ((slot.Flags & PgxpFlags.ValidLow) == 0) slot = default;
            }
            k += part;
        }
    }

    public static bool TryLoad(uint address, uint packed, out float x, out float y, out float w,
        out bool validW, out uint seq, out int transform)
    {
        x = y = 0f; w = 1f; validW = false; seq = 0; transform = 0;
        if (!Pgxp.MemoryTracking || (address & 3) != 0 || !TryIndex(address, out int i)) return false;
        ref var slot = ref _shadow[i];
        if (!PgxpFlags.Matches(in slot, packed)) return false;
        if (!float.IsFinite(slot.X) || !float.IsFinite(slot.Y)) return false;
        validW = (slot.Flags & PgxpFlags.Valid2) != 0 && float.IsFinite(slot.Z) && slot.Z > 0f;
        x = slot.X; y = slot.Y; w = slot.Z; seq = slot.Count; transform = slot.Transform;
        return true;
    }
}
