using PlatformA.Library.Helper;

namespace PlatformA.Tests.Utils.API;

public class SnowflakeGeneratorTests
{
    [Fact]
    public void Constructor_InvalidWorkerId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SnowflakeGenerator(32, 1));
    }

    [Fact]
    public void Constructor_NegativeWorkerId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SnowflakeGenerator(-1, 1));
    }

    [Fact]
    public void Constructor_InvalidDatacenterId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SnowflakeGenerator(1, 32));
    }

    [Fact]
    public void Constructor_NegativeDatacenterId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new SnowflakeGenerator(1, -1));
    }

    [Fact]
    public void NextId_ReturnsPositiveNumber()
    {
        var gen = new SnowflakeGenerator(1, 1);
        var id = gen.NextId();
        Assert.True(id > 0);
    }

    [Fact]
    public void NextId_SequentialCalls_AreMonotonicallyIncreasing()
    {
        var gen = new SnowflakeGenerator(1, 1);
        var ids = Enumerable.Range(0, 100).Select(_ => gen.NextId()).ToList();

        for (int i = 1; i < ids.Count; i++)
            Assert.True(ids[i] >= ids[i - 1], $"ID[{i}]={ids[i]} < ID[{i-1}]={ids[i-1]}");
    }

    [Fact]
    public void NextId_MultipleCallsAreUnique()
    {
        var gen = new SnowflakeGenerator(1, 1);
        var ids = Enumerable.Range(0, 1000).Select(_ => gen.NextId()).ToHashSet();
        Assert.Equal(1000, ids.Count);
    }

    [Fact]
    public void NextId_ConcurrentCalls_AreUnique()
    {
        var gen = new SnowflakeGenerator(1, 1);
        var bag = new System.Collections.Concurrent.ConcurrentBag<long>();

        Parallel.For(0, 100, _ =>
        {
            for (int i = 0; i < 10; i++)
                bag.Add(gen.NextId());
        });

        var unique = bag.ToHashSet();
        Assert.Equal(1000, unique.Count);
    }

    [Fact]
    public void NextId_DifferentWorkerIds_ProduceDifferentIds()
    {
        var gen1 = new SnowflakeGenerator(1, 1);
        var gen2 = new SnowflakeGenerator(2, 1);

        // 같은 시각에 생성해도 workerId가 달라 ID가 달라야 함
        var id1 = gen1.NextId();
        var id2 = gen2.NextId();
        Assert.NotEqual(id1, id2);
    }
}
