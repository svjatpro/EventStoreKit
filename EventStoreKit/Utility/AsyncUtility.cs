using System;
using System.Threading.Tasks;

namespace EventStoreKit.Utility
{
    public static class AsyncUtility
    {
        public static Task ContinueWithEx<TResult>( this Task<TResult> task, Action<Task<TResult>> continuation )
        {
            return task.ContinueWith( t =>
            {
                if ( t.IsFaulted )
                    throw t.Exception;
                continuation( t );
            } );
        }

        public static Task<TResult> ContinueWithEx<TResult>( this Task task, Func<Task,TResult> continuation )
        {
            return task.ContinueWith( t =>
            {
                if ( t.IsFaulted )
                    throw t.Exception;
                return continuation( t );
            } );
        }
    }
}