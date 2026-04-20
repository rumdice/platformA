using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlatformA.Library.Helper
{
    public class SnowflakeGenerator
    {
        // [ 타임스탬프 (41bit) ] + [ 서버번호 (10bit) ] + [ 순번 (12bit) ]

        // 1. 기준 시간 (2024년 1월 1일) - 이 날짜 이후로 흐른 시간을 씁니다.
        private const long Twepoch = 1704067200000L;

        // 2. 비트 할당 설정
        private const int WorkerIdBits = 5;
        private const int DatacenterIdBits = 5;
        private const int SequenceBits = 12;

        // 3. 최대값 계산 (비트 연산)
        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);
        private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);

        // 4. 비트 이동(Shift) 위치 계산
        private const int WorkerIdShift = SequenceBits;
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits;

        private const long SequenceMask = -1L ^ (-1L << SequenceBits);

        private readonly long _workerId;
        private readonly long _datacenterId;
        private long _sequence = 0L;
        private long _lastTimestamp = -1L;

        // 동시성 제어를 위한 락 (Lock)
        private static readonly object _lock = new object();

        public SnowflakeGenerator(long workerId, long datacenterId)
        {
            if (workerId > MaxWorkerId || workerId < 0)
                throw new ArgumentException($"Worker Id cannot be greater than {MaxWorkerId} or less than 0");

            if (datacenterId > MaxDatacenterId || datacenterId < 0)
                throw new ArgumentException($"Datacenter Id cannot be greater than {MaxDatacenterId} or less than 0");

            _workerId = workerId;
            _datacenterId = datacenterId;
        }

        public long NextId()
        {
            lock (_lock)
            {
                var timestamp = TimeGen();

                //if (timestamp < _lastTimestamp)
                //    throw new Exception("Clock moved backwards. Refusing to generate id");

                if (timestamp < _lastTimestamp)
                {
                    long offset = _lastTimestamp - timestamp;
                    if (offset <= 5)
                    {
                        // aws ec2 환경의 미세한 time diff 부분.
                        // 5ms 이하의 역행이라면, 시간이 원래대로 돌아올 때까지 스레드를 잠재움
                        Thread.Sleep((int)offset);
                        timestamp = TimeGen();
                    }
                    else
                    {
                        // 심각한 시간 역행 (서버 시간이 크게 변경됨)
                        throw new Exception($"Clock moved backwards by {offset}ms. Refusing to generate id");
                    }
                }

                if (_lastTimestamp == timestamp)
                {
                    // 같은 밀리초 안에 들어온 경우 순번 증가
                    _sequence = (_sequence + 1) & SequenceMask;
                    if (_sequence == 0) timestamp = TilNextMillis(_lastTimestamp);
                }
                else
                {
                    // 시간이 바뀌었으면 순번 리셋
                    _sequence = 0;
                }

                _lastTimestamp = timestamp;

                // 🔥 핵심: 비트 연산으로 3가지 정보를 합쳐서 하나의 long 숫자로 만듦
                return ((timestamp - Twepoch) << TimestampLeftShift) |
                       (_datacenterId << DatacenterIdShift) |
                       (_workerId << WorkerIdShift) |
                       _sequence;
            }
        }

        private long TilNextMillis(long lastTimestamp)
        {
            var timestamp = TimeGen();
            while (timestamp <= lastTimestamp) timestamp = TimeGen();
            return timestamp;
        }

        private long TimeGen() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
