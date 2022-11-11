using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class Execution
    {
        private static readonly object Locker = new object();
        private static readonly HashSet<ExecutionPackage> ReferenceMapping = new HashSet<ExecutionPackage>();

        public static bool CheckAlreadyExecuted<T>(T Instance, [CallerFilePath] string CallerFilePath = null, [CallerMemberName] string CallerMethodName = null, [CallerLineNumber] int CallerLineNumber = 0) where T : class
        {
            return !VerifyReference(Instance, false, $"{CallerFilePath}|{CallerLineNumber}|{CallerMethodName}");
        }

        public static void ExecuteOnce<T>(T Instance, Action Action, [CallerFilePath] string CallerFilePath = null, [CallerMemberName] string CallerMethodName = null, [CallerLineNumber] int CallerLineNumber = 0) where T : class
        {
            ThrowIfNull(Instance);

            if (VerifyReference(Instance, true, $"{CallerFilePath}|{CallerLineNumber}|{CallerMethodName}"))
            {
                Action();
            }
        }

        public static V ExecuteOnce<T, V>(T Instance, Func<V> Action, [CallerFilePath] string CallerFilePath = null, [CallerMemberName] string CallerMethodName = null, [CallerLineNumber] int CallerLineNumber = 0) where T : class
        {
            ThrowIfNull(Instance);

            if (VerifyReference(Instance, true, $"{CallerFilePath}|{CallerLineNumber}|{CallerMethodName}"))
            {
                return Action();
            }

            return default;
        }

        public static Task ExecuteOnceAsync<T>(T Instance, Func<Task> Action, [CallerFilePath] string CallerFilePath = null, [CallerMemberName] string CallerMethodName = null, [CallerLineNumber] int CallerLineNumber = 0) where T : class
        {
            ThrowIfNull(Instance);

            if (VerifyReference(Instance, true, $"{CallerFilePath}|{CallerLineNumber}|{CallerMethodName}"))
            {
                return Action();
            }

            return Task.CompletedTask;
        }

        public static Task<V> ExecuteOnceAsync<T, V>(T Instance, Func<Task<V>> Action, [CallerFilePath] string CallerFilePath = null, [CallerMemberName] string CallerMethodName = null, [CallerLineNumber] int CallerLineNumber = 0) where T : class
        {
            ThrowIfNull(Instance);

            if (VerifyReference(Instance, true, $"{CallerFilePath}|{CallerLineNumber}|{CallerMethodName}"))
            {
                return Action();
            }

            return Task.FromResult(default(V));
        }

        private static void ThrowIfNull(object Instance)
        {
            if (Instance is null)
            {
                throw new ArgumentNullException();
            }
        }

        private static bool VerifyReference<T>(T Instance, bool AddReferenceOnSuccess = true, string CallerName = null) where T : class
        {
            lock (Locker)
            {
                ReferenceMapping.RemoveRange(ReferenceMapping.Where((Ref) => !Ref.Reference.IsAlive));

                if (ReferenceMapping.All((Ref) => Ref.MethodName != CallerName || !ReferenceEquals(Ref.Reference.Target, Instance)))
                {
                    if (AddReferenceOnSuccess)
                    {
                        ReferenceMapping.Add(new ExecutionPackage(new WeakReference(Instance), CallerName));
                    }

                    return true;
                }

                return false;
            }
        }

        private class ExecutionPackage
        {
            public string MethodName { get; }

            public WeakReference Reference { get; }

            public ExecutionPackage(WeakReference Reference, string MethodName)
            {
                this.MethodName = MethodName;
                this.Reference = Reference;
            }
        }
    }
}
