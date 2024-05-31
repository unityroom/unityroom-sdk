using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unityroom.Client
{
    readonly struct AwaitableAsyncOperation
    {
        readonly AsyncOperation operation;

        public AwaitableAsyncOperation(AsyncOperation operation)
        {
            this.operation = operation;
        }

        public AsyncOperationAwaiter GetAwaiter() => new AsyncOperationAwaiter(operation);
    }

    struct AsyncOperationAwaiter : ICriticalNotifyCompletion
    {
        AsyncOperation asyncOperation;
        Action<AsyncOperation> continuationAction;

        public AsyncOperationAwaiter(AsyncOperation asyncOperation)
        {
            this.asyncOperation = asyncOperation;
            this.continuationAction = null;
        }

        public bool IsCompleted => asyncOperation.isDone;

        public void GetResult()
        {
            if (continuationAction != null)
            {
                asyncOperation.completed -= continuationAction;
                continuationAction = null;
                asyncOperation = null;
            }
            else
            {
                asyncOperation = null;
            }
        }

        public void OnCompleted(Action continuation)
        {
            UnsafeOnCompleted(continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            if (continuationAction != null) throw new InvalidOperationException("continuation is already registered.");

            continuationAction = _ => continuation();
            asyncOperation.completed += continuationAction;
        }
    }
}