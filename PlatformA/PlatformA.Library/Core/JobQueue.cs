namespace PlatformA.Library.Core
{
    public class JobQueue
    {
        // 할 일(Action)들을 담아둘 큐
        private Queue<Action> _jobQueue = new Queue<Action>();

        // 다수의 요청이 들어오므로 쓰레드 안전하게 잠금장치 필요.
        private object _lock = new object();

        // 현재 누군가가 큐의 일거리를 처리하고 있는지 여부
        private bool _isExecuting = false;


        public void Push(Action job)
        {
            bool isFirst = false;

            lock (_lock)
            {
                _jobQueue.Enqueue(job);

                // 아무도 실행하고 있지 않다면, 내가 총대를 멘다!
                if (!_isExecuting)
                {
                    isFirst = true;
                    _isExecuting = true;
                }
            }

            // 내가 총대를 멨다면 쌓인 일거리를 모두 처리하러 출발
            if (isFirst)
            {
                Flush();
            }
        }

        // push가 아닌 flush인 이유.
        // push : 하나씩 뺀다.
        // flush : 한번에 싹 비운다.
        private void Flush()
        {
            while (true)
            {
                Action? action = null;

                lock (_lock)
                {
                    // 더 이상 할 일이 없으면 실행 상태를 끄고 퇴근
                    if (_jobQueue.Count == 0)
                    {
                        _isExecuting = false;
                        return;
                    }
                    action = _jobQueue.Dequeue();
                }

                // 락(lock)을 푼 상태에서 일을 실행! (다른 스레드가 Push할 수 있게 끔)
                try
                {
                    action.Invoke(); // lock 바깥 예외처리로 _isExecuting 플래그(false) 보장. 탈출.
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[JobQueue] 작업 실행 중 예외 발생: {ex}");
                }
            }
        }
    }
}
