using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI.common.result
{
    public interface IResultData
    {
        string DataToString(string format = "0.00");
    }

    /// <summary>
    /// Model inference result class.
    /// </summary>
    /// <typeparam name="T">Model inference result type.</typeparam>
    public class Result<T> where T : class // Added "where T : class" constraint to fix CS8627
    {
        public List<T> datas;

        public Result()
        {
            datas = new List<T>();
        }

        public Result(List<T> datas)
        {
            this.datas = datas;
        }

        public Result(Result<T> values)
        {
            datas = new List<T>(values.datas);
        }

        public T this[int index]
        {
            get { return datas[index]; }
            set { datas[index] = value; }
        }

        public int count
        {
            get { return datas.Count; }
        }

        public int capacity
        {
            get { return datas.Count; }
            set { datas.Capacity = value; }
        }

        public void Add(T item) => datas.Add(item);

        public void AddRange(List<T> collection) => datas.AddRange(collection);

        public void Clear() => datas.Clear();

        public bool Contains(T item) => datas.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => datas.CopyTo(array, arrayIndex);

        public void CopyTo(T[] array) => datas.CopyTo(array);

        public void CopyTo(int index, T[] array, int arrayIndex, int count)
            => datas.CopyTo(index, array, arrayIndex, count);

        public T Find(Predicate<T> match) => datas.Find(match); // Removed nullable annotation to fix CS8370

        public List<T> FindAll(Predicate<T> match) => datas.FindAll(match);

        public int FindIndex(int startIndex, int count, Predicate<T> match)
            => datas.FindIndex(startIndex, count, match);

        public int FindIndex(int startIndex, Predicate<T> match)
            => datas.FindIndex(startIndex, match);

        public int FindIndex(Predicate<T> match) => datas.FindIndex(match);

        public T FindLast(Predicate<T> match) => datas.FindLast(match); // Removed nullable annotation to fix CS8370

        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
            => datas.FindLastIndex(startIndex, count, match);

        public int FindLastIndex(int startIndex, Predicate<T> match)
            => datas.FindLastIndex(startIndex, match);

        public int FindLastIndex(Predicate<T> match)
            => datas.FindLastIndex(match);

        public void ForEach(Action<T> action)
        {
            datas.ForEach(action);
        }

        public List<T>.Enumerator GetEnumerator()
            => datas.GetEnumerator();

        public List<T> GetRange(int index, int count)
            => datas.GetRange(index, count);

        public int IndexOf(T item, int index, int count)
            => datas.IndexOf(item, index, count);

        public int IndexOf(T item, int index)
            => datas.IndexOf(item, index);

        public int IndexOf(T item)
            => datas.IndexOf(item);

        public void Insert(int index, T item) => datas.Insert(index, item);

        public void InsertRange(int index, IEnumerable<T> collection)
            => datas.InsertRange(index, collection);

        public int LastIndexOf(T item) => datas.LastIndexOf(item);

        public int LastIndexOf(T item, int index)
            => datas.LastIndexOf(item, index);

        public int LastIndexOf(T item, int index, int count)
            => datas.LastIndexOf(item, index, count);

        public bool Remove(T item) => datas.Remove(item);

        public int RemoveAll(Predicate<T> match) => datas.RemoveAll(match);

        public void RemoveAt(int index) => datas.RemoveAt(index);

        public void RemoveRange(int index, int count) => datas.RemoveRange(index, count);

        public void Reverse(int index, int count) => datas.Reverse(index, count);

        public void Reverse() => datas.Reverse();

        public void Sort(IComparer<T> comparer) => datas.Sort(comparer);

        public void Sort(Comparison<T> comparison) => datas.Sort(comparison);

        public void Sort(int index, int count, IComparer<T> comparer)
            => datas.Sort(index, count, comparer);

        public void Sort() => datas.Sort();

        public T[] ToArray() => datas.ToArray();

        public virtual void Print(string format = "0.00") { }

        public static void INFO(string msg) => Console.WriteLine("[INFO]  " + msg);
    }
}
