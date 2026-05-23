using System;
using System.Runtime.InteropServices;
using Menace.SDK.Internal;

namespace Menace.SDK;

/// <summary>
/// Safe wrapper for IL2CPP List&lt;T&gt; objects. Reads the internal _items array
/// and _size field directly via cached offsets.
/// </summary>
public readonly struct GameList
{
    private static readonly int ArrayHeader = IntPtr.Size * 4;

    private readonly IntPtr _itemsArray;
    public readonly int Count;

    public bool IsValid => _itemsArray != IntPtr.Zero;

    public GameList(IntPtr listPointer)
    {
        _itemsArray = IntPtr.Zero;
        Count = 0;

        if (listPointer == IntPtr.Zero) return;
        if (OffsetCache.ListItemsOffset == 0 || OffsetCache.ListSizeOffset == 0) return;

        _itemsArray = Marshal.ReadIntPtr(listPointer + (int)OffsetCache.ListItemsOffset);
        Count = Marshal.ReadInt32(listPointer + (int)OffsetCache.ListSizeOffset);
    }

    public GameList(GameObj listObj) : this(listObj.Pointer) { }

    public GameObj this[int index]
    {
        get
        {
            if (_itemsArray == IntPtr.Zero || index < 0 || index >= Count) return GameObj.Null;
            var elementPtr = Marshal.ReadIntPtr(_itemsArray + ArrayHeader + index * IntPtr.Size);
            return new GameObj(elementPtr);
        }
    }

    public Enumerator GetEnumerator() => new(this);

    public struct Enumerator
    {
        private readonly GameList _list;
        private int _index;

        internal Enumerator(GameList list) { _list = list; _index = -1; }

        public GameObj Current => _list[_index];
        public bool MoveNext() => ++_index < _list.Count;
    }
}
