using System;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>Child list that is filled on first access. Used to load models read from a file on demand.</summary>
    internal sealed class LazyList<T>
    {
        private Lazy<List<T>> _items = new Lazy<List<T>>(() => new List<T>());

        public List<T> Value => _items.Value;

        /// <summary>True once the list has been accessed; unloaded lists are unchanged by definition.</summary>
        public bool IsLoaded => _items.IsValueCreated;

        public void SetLoader(Func<IEnumerable<T>> loader) => _items = new Lazy<List<T>>(() => loader().ToList());

        /// <summary>A list holding the same items as <paramref name="original"/>, copied (and loaded) on first access.</summary>
        public static LazyList<T> CopyOf(LazyList<T> original)
        {
            var copy = new LazyList<T>();
            copy.SetLoader(() => original.Value);
            return copy;
        }
    }
}
