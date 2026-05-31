using System.Collections.Generic;
using UnityEngine;

[System.Flags]
public enum BattleSnapshotFlags : byte
{
    None = 0,
    Swinging = 1 << 0,
    Serving = 1 << 1,
    Walking = 1 << 2,
    BirdieInPlay = 1 << 3
}

public struct BattleSnapshot
{
    public int Sequence;
    public double RemoteTime;
    public Vector2 CharacterPosition;
    public Vector2 BirdiePosition;
    public int Facing;
    public BattleSnapshotFlags Flags;

    public bool IsSwinging => (Flags & BattleSnapshotFlags.Swinging) != 0;
    public bool IsServing => (Flags & BattleSnapshotFlags.Serving) != 0;
    public bool IsWalking => (Flags & BattleSnapshotFlags.Walking) != 0;
    public bool IsBirdieInPlay => (Flags & BattleSnapshotFlags.BirdieInPlay) != 0;
}

public class NetworkSnapshotBuffer
{
    const int MaxSnapshots = 32;

    readonly List<BattleSnapshot> snapshots = new List<BattleSnapshot>(MaxSnapshots);
    int newestSequence = -1;

    public int Count => snapshots.Count;

    public bool Add(BattleSnapshot snapshot)
    {
        if (newestSequence >= 0 && snapshot.Sequence <= newestSequence)
            return false;

        newestSequence = snapshot.Sequence;
        snapshots.Add(snapshot);

        if (snapshots.Count > MaxSnapshots)
            snapshots.RemoveAt(0);

        return true;
    }

    public void Clear()
    {
        snapshots.Clear();
        newestSequence = -1;
    }

    public bool TrySample(double renderTime, out BattleSnapshot sample)
    {
        sample = default;
        if (snapshots.Count == 0)
            return false;

        if (snapshots.Count == 1 || renderTime <= snapshots[0].RemoteTime)
        {
            sample = snapshots[0];
            return true;
        }

        int last = snapshots.Count - 1;
        if (renderTime >= snapshots[last].RemoteTime)
        {
            sample = snapshots[last];
            return true;
        }

        for (int i = 0; i < last; i++)
        {
            BattleSnapshot a = snapshots[i];
            BattleSnapshot b = snapshots[i + 1];
            if (renderTime < a.RemoteTime || renderTime > b.RemoteTime)
                continue;

            float t = Mathf.InverseLerp((float)a.RemoteTime, (float)b.RemoteTime, (float)renderTime);
            sample = b;
            sample.RemoteTime = renderTime;
            sample.CharacterPosition = Vector2.Lerp(a.CharacterPosition, b.CharacterPosition, t);
            sample.BirdiePosition = Vector2.Lerp(a.BirdiePosition, b.BirdiePosition, t);
            return true;
        }

        sample = snapshots[last];
        return true;
    }
}
