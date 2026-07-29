using System;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesActionLifecycleFacade
    {
        private readonly ParalivesActionCompletionDispatcher _completionDispatcher;

        internal ParalivesActionLifecycleFacade(ParalivesActionCompletionDispatcher completionDispatcher)
        {
            if (completionDispatcher == null)
                throw new ArgumentNullException("completionDispatcher");

            _completionDispatcher = completionDispatcher;
        }

        public event ParalivesActionCompletedEventHandler Completed
        {
            add { _completionDispatcher.ActionCompleted += value; }
            remove { _completionDispatcher.ActionCompleted -= value; }
        }
    }
}
