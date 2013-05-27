using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TSVCEO.CloudPrint.Util
{
    public static class TaskHelpers
    {
        private static class CancelCache<TResult>
        {
            public static readonly Task<TResult> Cancelled = GetCancelled();

            private static Task<TResult> GetCancelled()
            {
                var tcs = new TaskCompletionSource<TResult>();
                tcs.SetCanceled();
                return tcs.Task;
            }
        }

        private struct Void { }

        private static readonly Task<object> CompletedTaskReturningNull = GetCompleted<Object>(null);
        private static readonly Task CompletedTaskReturningVoid = GetCompleted(default(Void));

        public static Task GetCancelled()
        {
            return CancelCache<Void>.Cancelled;
        }

        public static Task<TResult> GetCancelled<TResult>()
        {
            return CancelCache<TResult>.Cancelled;
        }

        public static Task GetCompleted()
        {
            return CompletedTaskReturningVoid;
        }

        public static Task<TResult> GetCompleted<TResult>(TResult result)
        {
            var tcs = new TaskCompletionSource<TResult>();
            tcs.SetResult(result);
            return tcs.Task;
        }

        public static Task<object> GetCompletedNull()
        {
            return CompletedTaskReturningNull;
        }

        public static Task GetFaulted(Exception ex)
        {
            return GetFaulted<Void>(ex);
        }

        public static Task<TResult> GetFaulted<TResult>(Exception ex)
        {
            var tcs = new TaskCompletionSource<TResult>();
            tcs.SetException(ex);
            return tcs.Task;
        }

        public static Task GetFaulted(IEnumerable<Exception> ex)
        {
            return GetFaulted(ex);
        }
        
        public static Task<TResult> GetFaulted<TResult>(IEnumerable<Exception> ex)
        {
            var tcs = new TaskCompletionSource<TResult>();
            tcs.SetException(ex);
            return tcs.Task;
        }

        public static Task RunSynchronously(Action action, CancellationToken token = default(CancellationToken))
        {
            if (token.IsCancellationRequested)
            {
                return GetCancelled();
            }
            else
            {
                try
                {
                    action();
                    return GetCompleted();
                }
                catch (Exception ex)
                {
                    return GetFaulted(ex);
                }
            }
        }

        public static Task<TResult> RunSynchronously<TResult>(Func<TResult> func, CancellationToken token = default(CancellationToken))
        {
            if (token.IsCancellationRequested)
            {
                return GetCancelled<TResult>();
            }
            else
            {
                try
                {
                    return GetCompleted(func());
                }
                catch (Exception ex)
                {
                    return GetFaulted<TResult>(ex);
                }
            }
        }

        public static Task<TResult> RunSynchronously<TResult>(Func<Task<TResult>> func, CancellationToken token = default(CancellationToken))
        {
            if (token.IsCancellationRequested)
            {
                return GetCancelled<TResult>();
            }
            else
            {
                try
                {
                    return func();
                }
                catch (Exception ex)
                {
                    return GetFaulted<TResult>(ex);
                }
            }
        }

        public static bool TrySet<TResult>(this TaskCompletionSource<TResult> tcs, Task source)
        {
            var task = source as Task<TResult>;

            switch (source.Status)
            {
                case TaskStatus.Canceled: return tcs.TrySetCanceled();
                case TaskStatus.Faulted: return tcs.TrySetException(source.Exception);
                case TaskStatus.RanToCompletion: return tcs.TrySetResult(task == null ? default(TResult) : task.Result);
                default: return false;
            }
        }

        public static bool TrySet<TResult>(this TaskCompletionSource<Task<TResult>> tcs, Task source)
        {
            var tasktask = source as Task<Task<TResult>>;
            var task = source as Task<TResult>;

            switch (source.Status)
            {
                case TaskStatus.Canceled: return tcs.TrySetCanceled();
                case TaskStatus.Faulted: return tcs.TrySetException(source.Exception);
                case TaskStatus.RanToCompletion: return tcs.TrySetResult(tasktask == null ? task == null ? GetCompleted(default(TResult)) : task : tasktask.Result);
                default: return false;
            }
        }

        public static bool TrySetIfFailed<TResult>(this TaskCompletionSource<TResult> tcs, Task source)
        {
            if (source.Status == TaskStatus.Canceled || source.Status == TaskStatus.Faulted)
            {
                return TrySet<TResult>(tcs, source);
            }
            else
            {
                return false;
            }
        }

        public static bool TrySetIfFailed<TResult>(this TaskCompletionSource<Task<TResult>> tcs, Task source)
        {
            if (source.Status == TaskStatus.Canceled || source.Status == TaskStatus.Faulted)
            {
                return TrySet<TResult>(tcs, source);
            }
            else
            {
                return false;
            }
        }
    }
}
