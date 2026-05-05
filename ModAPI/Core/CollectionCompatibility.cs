using System;
using System.Collections;
using System.Collections.Generic;

// We keep the interfaces in System.Collections.Generic so mod code 
// looks like standard C# and remains forward-compatible.
namespace System.Collections.Generic
{
    /// <summary>
    /// Minimal .NET 3.5-compatible read-only collection contract.
    /// This lives in the standard namespace so mod code can move to newer framework targets without API churn.
    /// </summary>
    public interface IReadOnlyCollection<T> : IEnumerable<T>, IEnumerable
    {
        int Count { get; }
    }

    /// <summary>
    /// Minimal .NET 3.5-compatible read-only list contract.
    /// Use this for API returns that should be enumerable and indexable but not caller-mutated.
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
    /// Read-only adapter over an existing <see cref="IList{T}"/>.
    /// The wrapper does not copy the list, so changes by the owner are visible to readers.
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

    /// <summary>
    /// Helpers for exposing collections through the compatibility read-only interfaces.
    /// </summary>
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
