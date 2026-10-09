using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace IfcHopper.Core.Model
{
    /// <summary>
    /// Live view of the children of one type, e.g. the facilities of a site. Adding appends to <see cref="ModelObject.Children"/>;
    /// removing and clearing only affect children of this type. Enumeration works on a snapshot, so the view may be edited meanwhile.
    /// </summary>
    public sealed class ChildView<T> : IList<T> where T : ModelObject
    {
        private readonly ModelObject _owner;

        internal ChildView(ModelObject owner)
        {
            _owner = owner;
        }

        private List<ModelObject> All => _owner.Children;
        private List<T> Items => All.OfType<T>().ToList();

        public int Count => All.Count(c => c is T);
        public bool IsReadOnly => false;

        public T this[int index]
        {
            get => Items[index];
            set => All[All.IndexOf(Items[index])] = value ?? throw new ArgumentNullException(nameof(value));
        }

        public void Add(T item) => All.Add(item ?? throw new ArgumentNullException(nameof(item)));

        public void AddRange(IEnumerable<T> items)
        {
            foreach (var item in items) Add(item);
        }

        /// <summary>Inserts before the child of this type at <paramref name="index"/>, or at the end.</summary>
        public void Insert(int index, T item)
        {
            var items = Items;
            if (index == items.Count) Add(item);
            else All.Insert(All.IndexOf(items[index]), item ?? throw new ArgumentNullException(nameof(item)));
        }

        public bool Remove(T item) => All.Remove(item);
        public void RemoveAt(int index) => All.Remove(Items[index]);
        public void Clear() => All.RemoveAll(c => c is T);
        public int RemoveAll(Predicate<T> match) => All.RemoveAll(c => c is T item && match(item));
        public bool Contains(T item) => All.Contains(item);
        public int IndexOf(T item) => Items.IndexOf(item);
        public void CopyTo(T[] array, int arrayIndex) => Items.CopyTo(array, arrayIndex);
        public IEnumerator<T> GetEnumerator() => Items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
