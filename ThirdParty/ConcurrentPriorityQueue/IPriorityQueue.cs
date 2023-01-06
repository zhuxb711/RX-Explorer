using CSharpFunctionalExtensions;

namespace ThirdParty.ConcurrentPriorityQueue
{
    public interface IPriorityQueue<T>
    {
        Result Enqueue(T item);

        Result<T> Dequeue();

        Result<T> Peek();
    }
}
