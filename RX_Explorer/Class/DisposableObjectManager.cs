using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RX_Explorer.Class
{
    public static class DisposableObjectManager
    {
        private static event EventHandler<long> OnInnerDataDisposed;
        private static readonly List<object> Collection = new List<object>();
        private static readonly List<InnerCallbackData> CallbackTokenTable = new List<InnerCallbackData>();
        private static readonly Random RandomSource = new Random();
        private static readonly object Locker = new object();

        public static void RegisterCallbackOnObjectDisposed(Action OnDisposeCompleted, params long[] Tokens)
        {
            if (Tokens.Length > 0)
            {
                lock (Locker)
                {
                    CallbackTokenTable.Add(new InnerCallbackData(OnDisposeCompleted, Tokens));
                }
            }
        }

        public static long DisposeObjectOnConditionSatisfied<T>(T Object, int DetectInEveryMilliseconds, Func<T, bool> Condition) where T : IDisposable
        {
            long Token = ((long)RandomSource.Next(0, int.MaxValue)) << 32 + RandomSource.Next(0, int.MaxValue);

            if (Object != null)
            {
                lock (Locker)
                {
                    Collection.Add(new InnerDisposableManageData<T>(Object, DetectInEveryMilliseconds, Token, Condition));
                }
            }

            return Token;
        }

        static DisposableObjectManager()
        {
            OnInnerDataDisposed += DisposableObjectManager_OnInnerDataDisposed;
        }

        private static void DisposableObjectManager_OnInnerDataDisposed(object sender, long token)
        {
            lock (Locker)
            {
                Collection.Remove(sender);

                if (CallbackTokenTable.FirstOrDefault((Item) => Item.Tokens.Remove(token)) is InnerCallbackData Data)
                {
                    if (Data.Tokens.Count == 0)
                    {
                        CallbackTokenTable.Remove(Data);
                        Data.Callback();
                    }
                }
            }
        }

        private sealed class InnerCallbackData
        {
            public List<long> Tokens { get; }

            public Action Callback { get; }

            public InnerCallbackData(Action Callback, params long[] Tokens)
            {
                this.Callback = Callback;
                this.Tokens = new List<long>(Tokens);
            }
        }

        private sealed class InnerDisposableManageData<T> : IDisposable where T : IDisposable
        {
            private bool IsDisposed;
            private readonly long Token;
            private readonly Timer CallbackTimer;

            public InnerDisposableManageData(T Object, int DetectInEveryMilliseconds, long Token, Func<T, bool> Condition)
            {
                this.Token = Token;

                CallbackTimer = new Timer((Input) =>
                {
                    if (!IsDisposed
                        && Input is T DisposableObject
                        && Condition(DisposableObject))
                    {
                        DisposableObject.Dispose();
                        Dispose();
                    }
                }, Object, 0, DetectInEveryMilliseconds);
            }

            public void Dispose()
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;
                    CallbackTimer.Dispose();
                    OnInnerDataDisposed?.Invoke(this, Token);
                    GC.SuppressFinalize(this);
                }
            }

            ~InnerDisposableManageData()
            {
                Dispose();
            }
        }
    }
}
