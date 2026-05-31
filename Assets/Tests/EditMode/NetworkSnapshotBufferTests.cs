using NUnit.Framework;
using UnityEngine;

public class NetworkSnapshotBufferTests
{
    [Test]
    public void TrySample_InterpolatesPositionsAtRenderTime()
    {
        var buffer = new NetworkSnapshotBuffer();
        buffer.Add(new BattleSnapshot
        {
            Sequence = 1,
            RemoteTime = 10.0,
            CharacterPosition = new Vector2(0f, 0f),
            BirdiePosition = new Vector2(2f, 4f),
            Facing = 1,
            Flags = BattleSnapshotFlags.BirdieInPlay
        });
        buffer.Add(new BattleSnapshot
        {
            Sequence = 2,
            RemoteTime = 10.2,
            CharacterPosition = new Vector2(10f, 4f),
            BirdiePosition = new Vector2(6f, 8f),
            Facing = -1,
            Flags = BattleSnapshotFlags.Swinging | BattleSnapshotFlags.Walking | BattleSnapshotFlags.BirdieInPlay
        });

        Assert.That(buffer.TrySample(10.1, out var sample), Is.True);
        Assert.That(sample.CharacterPosition.x, Is.EqualTo(5f).Within(0.001f));
        Assert.That(sample.CharacterPosition.y, Is.EqualTo(2f).Within(0.001f));
        Assert.That(sample.BirdiePosition.x, Is.EqualTo(4f).Within(0.001f));
        Assert.That(sample.BirdiePosition.y, Is.EqualTo(6f).Within(0.001f));
        Assert.That(sample.Facing, Is.EqualTo(-1));
        Assert.That(sample.IsWalking, Is.True);
    }

    [Test]
    public void Add_DropsOutOfOrderSnapshots()
    {
        var buffer = new NetworkSnapshotBuffer();
        buffer.Add(new BattleSnapshot { Sequence = 10, RemoteTime = 1.0, CharacterPosition = Vector2.one });
        buffer.Add(new BattleSnapshot { Sequence = 9, RemoteTime = 1.1, CharacterPosition = Vector2.one * 99f });

        Assert.That(buffer.Count, Is.EqualTo(1));
        Assert.That(buffer.TrySample(1.2, out var sample), Is.True);
        Assert.That(sample.CharacterPosition, Is.EqualTo(Vector2.one));
    }
}

