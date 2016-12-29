using System;
using System.Collections;
using System.Collections.Generic;

namespace Utilities
{
    public class SortedList<T> : ICollection<T>
    {
        private List<T> m_innerList;
        private Comparer<T> m_comparer;

        public SortedList() : this(Comparer<T>.Default)
        {
        }

        public SortedList(Comparer<T> comparer)
        {
            m_innerList = new List<T>();
            m_comparer = comparer;
        }

        public void Add(T item)
        {
            int insertIndex = FindIndexForSortedInsert(m_innerList, m_comparer, item);
            m_innerList.Insert(insertIndex, item);
        }

        public bool Contains(T item)
        {
            return IndexOf(item) != -1;
        }

        /// <summary>
        /// Searches for the specified object and returns the zero-based index of the first occurrence within the entire SortedList<T>
        /// </summary>
        public int IndexOf(T item)
        {
            return FirstIndexOf(m_innerList, m_comparer, item);
        }

        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index >= 0)
            {
                m_innerList.RemoveAt(index);
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            m_innerList.RemoveAt(index);
        }

        public void CopyTo(T[] array)
        {
            m_innerList.CopyTo(array);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            m_innerList.CopyTo(array, arrayIndex);
        }

        public void Clear()
        {
            m_innerList.Clear();
        }

        public T this[int index]
        {
            get
            {
                return m_innerList[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return m_innerList.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_innerList.GetEnumerator();
        }

        public int Count
        {
            get
            {
                return m_innerList.Count;
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public static int FirstIndexOf(List<T> list, Comparer<T> comparer, T item)
        {
            int insertIndex = FindIndexForSortedInsert(list, comparer, item);
            if (insertIndex == list.Count)
            {
                return -1;
            }
            if (comparer.Compare(item, list[insertIndex]) == 0)
            {
                int index = insertIndex;
                while (index > 0 && comparer.Compare(item, list[index - 1]) == 0)
                {
                    index--;
                }
                return index;
            }
            return -1;
        }

        public static int FindIndexForSortedInsert(List<T> list, Comparer<T> comparer, T item)
        {
            if (list.Count == 0)
            {
                return 0;
            }

            int lowerIndex = 0;
            int upperIndex = list.Count - 1;
            int comparisonResult;
            while (lowerIndex < upperIndex)
            {
                int middleIndex = (lowerIndex + upperIndex) / 2;
                T middle = list[middleIndex];
                comparisonResult = comparer.Compare(middle, item);
                if (comparisonResult == 0)
                {
                    return middleIndex;
                }
                else if (comparisonResult > 0) // middle > item
                {
                    upperIndex = middleIndex - 1;
                }
                else // middle < item
                {
                    lowerIndex = middleIndex + 1;
                }
            }

            // At this point any entry following 'middle' is greater than 'item',
            // and any entry preceding 'middle' is lesser than 'item'.
            // So we either put 'item' before or after 'middle'.
            comparisonResult = comparer.Compare(list[lowerIndex], item);
            if (comparisonResult < 0) // middle < item
            {
                return lowerIndex + 1;
            }
            else
            {
                return lowerIndex;
            }
        }
    }
}
