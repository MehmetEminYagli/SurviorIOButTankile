using System.Threading.Tasks;

namespace Network
{
    public static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
            if (task.IsCompleted)
            {
                _ = task.Exception;
            }
            else
            {
                task.ContinueWith(t => _ = t.Exception,
                    TaskContinuationOptions.OnlyOnFaulted |
                    TaskContinuationOptions.ExecuteSynchronously);
            }
        }
    }
} 