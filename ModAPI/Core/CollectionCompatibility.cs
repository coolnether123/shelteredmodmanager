using System;
using System.Collections;
using System.Collections.Generic;

// We keep the interfaces in System.Collections.Generic so mod code 
// looks like standard C# and remains forward-compatible.
namespace System.Collections.Generic
{
    /// <summary>
    /// Compatibility polyfill for IReadOnlyCollection for .NET 3.5
    /// </summary>
    public interface IReadOnlyCollection<T> : IEnumerable<T>, IEnumerable
    {
        int Count { get; }
    }

    /// <summary>
    /// Compatibility polyfill for IReadOnlyList for .NET 3.5
    /// </summary>
    public interface IReadOnlyList<T> : IReadOnlyCollection<T>, IEnumerable<T>, IEnumerable
    {
        T this[int index] { get; }
    }
}

namespace ModAPI.Util
{
    using System.Collections.Generic;

    /// <summary>
    /// A simple wrapper to provide IReadOnlyList functionality for an existing IList.
    /// </summary>
    public class ReadOnlyListWrapper<T> : IReadOnlyList<T>
    {
        private readonly IList<T> _list;

        public ReadOnlyListWrapper(IList<T> list)
        {
            _list = list;
        }

        public T this[int index]
        {
            get { return _list[index]; }
        }

        public int Count
        {
            get { return _list.Count; }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    public static class CollectionExtensions
    {
        public static IReadOnlyList<T> ToReadOnlyList<T>(this IList<T> list)
        {
            return new ReadOnlyListWrapper<T>(list);
        }

        public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> enumerable)
        {
            return new ReadOnlyListWrapper<T>(new List<T>(enumerable));
        }
    }
}
