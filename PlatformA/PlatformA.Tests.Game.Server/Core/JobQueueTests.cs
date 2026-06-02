using PlatformA.Library.Core;

namespace PlatformA.Tests.Game.Server.Core
{
    public class JobQueueTests
    {
        [Fact]
        public void Push_SingleJob_ExecutesJob()
        {
            var queue = new JobQueue();
            bool executed = false;

            queue.Push(() => executed = true);

            // Push is synchronous up to the point the flush loop completes on the calling thread
            // when it is the "first" executor. Give a small settle time for safety.
            Thread.Sleep(10);
            Assert.True(executed);
        }

        [Fact]
        public void Push_MultipleJobs_AllExecuteInOrder()
        {
            var queue = new JobQueue();
            var results = new List<int>();

            // Push one at a time from a single thread — flush runs inline for the first push
            // and the remaining jobs are picked up by the same flush loop.
            for (int i = 0; i < 5; i++)
            {
                int value = i;
                queue.Push(() => results.Add(value));
            }

            Thread.Sleep(20);
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, results);
        }

        [Fact]
        public void Push_ConcurrentPush_AllJobsExecuteExactlyOnce()
        {
            const int threadCount = 10;
            const int jobsPerThread = 10;
            var queue = new JobQueue();
            var counter = 0;

            var threads = new Thread[threadCount];
            for (int i = 0; i < threadCount; i++)
            {
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < jobsPerThread; j++)
                    {
                        queue.Push(() => Interlocked.Increment(ref counter));
                    }
                });
            }

            foreach (var t in threads)
                t.Start();
            foreach (var t in threads)
                t.Join();

            // Wait for any in-flight flush to finish
            Thread.Sleep(50);

            Assert.Equal(threadCount * jobsPerThread, counter);
        }

        [Fact]
        public void Push_JobThrowsException_RemainingJobsStillExecute()
        {
            var queue = new JobQueue();
            bool afterExceptionExecuted = false;

            queue.Push(() => throw new InvalidOperationException("intentional test exception"));
            queue.Push(() => afterExceptionExecuted = true);

            Thread.Sleep(30);
            Assert.True(afterExceptionExecuted);
        }
    }
}
