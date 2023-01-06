using CSharpFunctionalExtensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ThirdParty.ConcurrentPriorityQueue
{
    public class ConcurrentPriorityQueue<T, TP> : IConcurrentPriorityQueue<T, TP> where T : IHavePriority<TP> where TP : IEquatable<TP>, IComparable<TP>
    {
        private readonly int MaxCapacity;
        private readonly Dictionary<TP, Queue<T>> InternalQueues;

        public int Count => InternalQueues.Values.Sum((Queue) => Queue.Count);

        public bool IsSynchronized => true;

        public object SyncRoot { get; } = new object();

        public void CopyTo(T[] array, int index)
        {
            ToArray().CopyTo(array, index);
        }

        public void CopyTo(Array array, int index)
        {
            CopyTo((T[])array, index);
        }

        public T[] ToArray()
        {
            lock (SyncRoot)
            {
                return InternalQueues.OrderBy((Queue) => Queue.Key).SelectMany((Queue) => Queue.Value).ToArray();
            }
        }

        public bool TryAdd(T Item)
        {
            return Enqueue(Item).IsSuccess;
        }

        public bool TryTake(out T Item)
        {
            Result<T> DeResult = Dequeue();

            if (DeResult.IsSuccess)
            {
                Item = DeResult.Value;
                return true;
            }
            else
            {
                Item = default;
                return false;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)ToArray()).GetEnumerator();
        }

        public Result Enqueue(T Item)
        {
            lock (SyncRoot)
            {
                return AddOrUpdate(Item);
            }
        }

        public Result<T> Dequeue()
        {
            lock (SyncRoot)
            {
                return GetNextQueue().Map((Queue) => Queue.Dequeue()).TapError((Error) => Result.Failure(Error));
            }
        }

        public Result<T> Peek()
        {
            lock (SyncRoot)
            {
                return GetNextQueue().Map((Queue) => Queue.Peek()).TapError((Error) => Result.Failure(Error));
            }
        }

        private Result<Queue<T>> GetNextQueue()
        {
            if (InternalQueues.OrderBy((ValuePair) => ValuePair.Key).Select((ValuePair) => ValuePair.Value).FirstOrDefault((Queue) => Queue.Count > 0) is Queue<T> NextQueue)
            {
                return Result.Success(NextQueue);
            }

            return Result.Failure<Queue<T>>("Could not find a queue with items.");
        }

        private Result AddOrUpdate(T Item)
        {
            if (InternalQueues.TryGetValue(Item.Priority, out Queue<T> Queue))
            {
                Queue.Enqueue(Item);
                return Result.Success();
            }
            else if (MaxCapacity == 0 || Count != MaxCapacity)
            {
                Queue<T> NewQueue = new Queue<T>();
                NewQueue.Enqueue(Item);
                InternalQueues.Add(Item.Priority, NewQueue);
                return Result.Success();
            }
            else
            {
                return Result.Failure("Reached max capacity.");
            }
        }

        public ConcurrentPriorityQueue(int MaxCapacity) : this()
        {
            this.MaxCapacity = MaxCapacity;
        }

        public ConcurrentPriorityQueue()
        {
            InternalQueues = new Dictionary<TP, Queue<T>>();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
