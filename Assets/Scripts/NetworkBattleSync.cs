using Unity.Netcode;
using UnityEngine;

public class NetworkBattleSync : MonoBehaviour
{
    const byte MsgState = 1;
    const byte MsgHit = 2;
    const byte MsgScore = 3;
    const byte MsgSwing = 4;
    const byte MsgPause = 5;

    const byte SideHost = 0;
    const byte SideClient = 1;
    const byte ServerLeft = 0;
    const byte ServerRight = 1;

    public static NetworkBattleSync Instance { get; private set; }

    [Header("Snapshot Playback")]
    public float interpolationDelay = 0.04f;
    public float stateSendRate = 90f;

    public Vector2 RemotePos;
    public int RemoteFacing = 1;
    public bool RemoteSwing;
    public bool RemoteServe;
    public bool RemoteWalk;
    public bool Received;

    public Vector2 BirdiePos;
    public bool BirdieInPlay;
    public float BirdieUpdateTime;
    public bool IsScorePending => pendingScore.Active;

    readonly NetworkSnapshotBuffer remoteSnapshots = new NetworkSnapshotBuffer();

    bool ready;
    float logTimer;
    float lastHostStateSent;
    float lastClientStateSent;
    int hostStateSequence;
    int clientStateSequence;

    int lastSwingEventId;
    int swingEventId;

    int lastHitId;
    float lastHitSpeed;
    float lastHitDir;
    float lastHitPosX;
    float lastHitPosY;
    int processedHitId = -1;

    PendingScore pendingScore;

    struct PendingScore
    {
        public bool Active;
        public int Left;
        public int Right;
        public string Server;
        public Vector2 LandingPos;
        public double EventTime;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            if (!ready)
            {
                nm.CustomMessagingManager.RegisterNamedMessageHandler("BS", OnMsg);
                ready = true;
                Debug.Log("[BS] handler registered");
            }
        }
        else
        {
            ready = false;
            remoteSnapshots.Clear();
            Received = false;
        }

        TryApplyPendingScore();

        logTimer += Time.deltaTime;
        if (logTimer > 3f && nm != null)
        {
            Debug.Log($"[BS] Alive, Received={Received} isServer={nm.IsServer}");
            logTimer = 0f;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && ready)
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("BS");
    }

    public bool TryGetRemoteSnapshot(out BattleSnapshot snapshot)
    {
        double renderTime = NetworkTime - interpolationDelay;
        return remoteSnapshots.TrySample(renderTime, out snapshot);
    }

    double NetworkTime
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
                return nm.ServerTime.Time;
            return Time.unscaledTimeAsDouble;
        }
    }

    void OnMsg(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte type);
        switch (type)
        {
            case MsgState:
                HandleState(reader);
                break;
            case MsgHit:
                HandleHit(senderId, reader);
                break;
            case MsgScore:
                HandleScore(reader);
                break;
            case MsgSwing:
                HandleSwing(senderId, reader);
                break;
            case MsgPause:
                HandlePause(reader);
                break;
        }
    }

    void HandleState(FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte side);
        reader.ReadValueSafe(out int seq);
        reader.ReadValueSafe(out double remoteTime);
        reader.ReadValueSafe(out Vector2 pos);
        reader.ReadValueSafe(out int facing);
        reader.ReadValueSafe(out byte flagsByte);
        reader.ReadValueSafe(out Vector2 birdiePos);
        reader.ReadValueSafe(out int leftScore);
        reader.ReadValueSafe(out int rightScore);
        reader.ReadValueSafe(out byte serverByte);
        reader.ReadValueSafe(out int hitId);
        reader.ReadValueSafe(out float hitSpeed);
        reader.ReadValueSafe(out float hitDir);
        reader.ReadValueSafe(out Vector2 hitPos);

        bool isHostMsg = side == SideHost;
        bool isServer = NetworkManager.Singleton.IsServer;
        if ((isServer && isHostMsg) || (!isServer && !isHostMsg))
            return;

        var flags = (BattleSnapshotFlags)flagsByte;
        var snapshot = new BattleSnapshot
        {
            Sequence = seq,
            RemoteTime = remoteTime,
            CharacterPosition = pos,
            BirdiePosition = birdiePos,
            Facing = facing,
            Flags = flags
        };

        if (!remoteSnapshots.Add(snapshot))
            return;

        RemotePos = pos;
        RemoteFacing = facing;
        RemoteSwing = snapshot.IsSwinging;
        RemoteServe = snapshot.IsServing;
        RemoteWalk = snapshot.IsWalking;
        BirdiePos = birdiePos;
        BirdieUpdateTime = Time.time;
        BirdieInPlay = snapshot.IsBirdieInPlay;
        Received = true;

        if (isHostMsg)
            HandleHostScoreBackup(leftScore, rightScore, serverByte, birdiePos, remoteTime, snapshot.IsBirdieInPlay);

        if (hitId > 0 && hitId != processedHitId)
        {
            processedHitId = hitId;
            ApplyRemoteHit(hitSpeed, hitDir, hitPos);
            Debug.Log($"[BS] backup hit applied: speed={hitSpeed:F2} dir={hitDir:F2} pos={hitPos}");
        }
    }

    void HandleHostScoreBackup(int left, int right, byte serverByte, Vector2 landingPos, double eventTime, bool birdieInPlay)
    {
        if (birdieInPlay || pendingScore.Active)
            return;

        var gm = FindObjectOfType<GameManager>();
        int localTotal = gm != null ? gm.leftScore + gm.rightScore : 0;
        int remoteTotal = left + right;
        if (gm != null && remoteTotal > localTotal)
            QueueScoreSync(left, right, DecodeServer(serverByte), landingPos, eventTime);
    }

    void HandleHit(ulong senderId, FastBufferReader reader)
    {
        bool isServer = NetworkManager.Singleton.IsServer;
        bool fromServer = senderId == NetworkManager.ServerClientId;
        if (isServer == fromServer)
            return;

        reader.ReadValueSafe(out float speed);
        reader.ReadValueSafe(out float dir);
        reader.ReadValueSafe(out Vector2 ballPos);
        reader.ReadValueSafe(out int hitId);

        if (hitId == processedHitId)
            return;

        processedHitId = hitId;
        ApplyRemoteHit(speed, dir, ballPos);
        Debug.Log($"[BS] hit event applied: speed={speed:F2} dir={dir:F2} pos={ballPos}");
    }

    void ApplyRemoteHit(float speed, float dir, Vector2 ballPos)
    {
        var sc = FindObjectOfType<Shuttlecock>();
        if (sc == null)
            return;

        sc.transform.position = new Vector3(ballPos.x, ballPos.y, sc.transform.position.z);
        sc.HitMe(speed, dir);

        var gm = FindObjectOfType<GameManager>();
        if (gm != null && gm.state != GameState.Playing && gm.state != GameState.GameOver)
            gm.state = GameState.Playing;
    }

    void HandleScore(FastBufferReader reader)
    {
        if (NetworkManager.Singleton.IsServer)
            return;

        reader.ReadValueSafe(out int left);
        reader.ReadValueSafe(out int right);
        reader.ReadValueSafe(out byte serverByte);
        reader.ReadValueSafe(out Vector2 landingPos);
        reader.ReadValueSafe(out double eventTime);

        QueueScoreSync(left, right, DecodeServer(serverByte), landingPos, eventTime);
    }

    void QueueScoreSync(int left, int right, string server, Vector2 landingPos, double eventTime)
    {
        pendingScore = new PendingScore
        {
            Active = true,
            Left = left,
            Right = right,
            Server = server,
            LandingPos = landingPos,
            EventTime = eventTime > 0 ? eventTime : NetworkTime
        };
        BirdieInPlay = true;
        Debug.Log($"[BS] score queued: {left}-{right} server={server} eventTime={pendingScore.EventTime:F3}");
    }

    void TryApplyPendingScore()
    {
        if (!pendingScore.Active)
            return;

        if (NetworkTime < pendingScore.EventTime + interpolationDelay)
            return;

        var sc = FindObjectOfType<Shuttlecock>();
        if (sc != null)
        {
            sc.transform.position = new Vector3(pendingScore.LandingPos.x, pendingScore.LandingPos.y, sc.transform.position.z);
            sc.isInPlay = false;
            sc.hasScored = false;
            sc.dx = 0f;
            sc.dy = 0f;
        }

        BirdieInPlay = false;

        var gm = FindObjectOfType<GameManager>();
        if (gm != null)
        {
            gm.ApplyScoreSync(pendingScore.Left, pendingScore.Right, pendingScore.Server);
            Debug.Log($"[BS] score applied: {gm.leftScore}-{gm.rightScore} server={gm.server}");
        }

        pendingScore.Active = false;
    }

    void HandleSwing(ulong senderId, FastBufferReader reader)
    {
        bool isServer = NetworkManager.Singleton.IsServer;
        bool fromServer = senderId == NetworkManager.ServerClientId;
        if (isServer == fromServer)
            return;

        reader.ReadValueSafe(out byte isServeByte);
        reader.ReadValueSafe(out int evtId);
        if (evtId == lastSwingEventId)
            return;

        lastSwingEventId = evtId;
        bool isServe = isServeByte != 0;
        Debug.Log($"[BS] swing event received: isServe={isServe} id={evtId}");

        if (isServer)
        {
            var ai = FindObjectOfType<AIController>();
            if (ai != null && ai.isNetworkRemote)
            {
                if (isServe) { ai.serving = true; ai.SetupServe(); }
                ai.SwingMe();
            }
        }
        else
        {
            var player = FindObjectOfType<BattleCharacter>();
            if (player != null && player.isNetworkRemote)
            {
                if (isServe) { player.serving = true; player.SetupServe(); }
                player.SwingMe();
            }
        }
    }

    void HandlePause(FastBufferReader reader)
    {
        if (NetworkManager.Singleton.IsServer)
            return;

        reader.ReadValueSafe(out string cmd);
        Debug.Log($"[BS] pause command received: {cmd}");
        HandlePauseCommand(cmd);
    }

    public void SendHostState(Vector2 pos, int facing, bool swinging, bool serving, bool walking,
        Vector2 birdiePos, bool birdieInPlay)
    {
        if (!ShouldSendState(ref lastHostStateSent))
            return;

        SendState(SideHost, ++hostStateSequence, pos, facing, swinging, serving, walking, birdiePos, birdieInPlay,
            NetworkDelivery.UnreliableSequenced);
    }

    public void SendClientState(Vector2 pos, int facing, bool swinging, bool serving, bool walking,
        Vector2 birdiePos, bool birdieInPlay)
    {
        if (!ShouldSendState(ref lastClientStateSent))
            return;

        SendState(SideClient, ++clientStateSequence, pos, facing, swinging, serving, walking, birdiePos, birdieInPlay,
            NetworkDelivery.UnreliableSequenced);
    }

    bool ShouldSendState(ref float lastSent)
    {
        float minInterval = 1f / Mathf.Max(1f, stateSendRate);
        if (Time.unscaledTime - lastSent < minInterval)
            return false;

        lastSent = Time.unscaledTime;
        return true;
    }

    void SendState(byte side, int sequence, Vector2 pos, int facing, bool swinging, bool serving, bool walking,
        Vector2 birdiePos, bool birdieInPlay, NetworkDelivery delivery)
    {
        var gm = FindObjectOfType<GameManager>();
        int l = gm != null ? gm.leftScore : 0;
        int r = gm != null ? gm.rightScore : 0;
        byte server = gm != null ? EncodeServer(gm.server) : ServerLeft;

        byte flags = 0;
        if (swinging) flags |= (byte)BattleSnapshotFlags.Swinging;
        if (serving) flags |= (byte)BattleSnapshotFlags.Serving;
        if (walking) flags |= (byte)BattleSnapshotFlags.Walking;
        if (birdieInPlay) flags |= (byte)BattleSnapshotFlags.BirdieInPlay;

        var w = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(MsgState);
        w.WriteValueSafe(side);
        w.WriteValueSafe(sequence);
        w.WriteValueSafe(NetworkTime);
        w.WriteValueSafe(pos);
        w.WriteValueSafe(facing);
        w.WriteValueSafe(flags);
        w.WriteValueSafe(birdiePos);
        w.WriteValueSafe(l);
        w.WriteValueSafe(r);
        w.WriteValueSafe(server);
        w.WriteValueSafe(lastHitId);
        w.WriteValueSafe(lastHitSpeed);
        w.WriteValueSafe(lastHitDir);
        w.WriteValueSafe(new Vector2(lastHitPosX, lastHitPosY));

        if (NetworkManager.Singleton.IsServer)
            Bcast(w, delivery);
        else
            SendToServer(w, delivery);

        w.Dispose();
    }

    public void SendHit(float speed, float dir, Vector2 ballPos)
    {
        lastHitId++;
        lastHitSpeed = speed;
        lastHitDir = dir;
        lastHitPosX = ballPos.x;
        lastHitPosY = ballPos.y;

        var w = new FastBufferWriter(64, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(MsgHit);
        w.WriteValueSafe(speed);
        w.WriteValueSafe(dir);
        w.WriteValueSafe(ballPos);
        w.WriteValueSafe(lastHitId);

        if (NetworkManager.Singleton.IsServer)
            Bcast(w, NetworkDelivery.ReliableSequenced);
        else
            SendToServer(w, NetworkDelivery.ReliableSequenced);

        w.Dispose();
    }

    public void SendSwingEvent(bool isServe)
    {
        swingEventId++;

        var w = new FastBufferWriter(32, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(MsgSwing);
        w.WriteValueSafe((byte)(isServe ? 1 : 0));
        w.WriteValueSafe(swingEventId);

        Debug.Log($"[BS] swing event sent: isServe={isServe} id={swingEventId}");
        if (NetworkManager.Singleton.IsServer)
            Bcast(w, NetworkDelivery.ReliableSequenced);
        else
            SendToServer(w, NetworkDelivery.ReliableSequenced);

        w.Dispose();
    }

    public void SendPauseCommand(string cmd)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        var w = new FastBufferWriter(64, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(MsgPause);
        w.WriteValueSafe(cmd);
        Bcast(w, NetworkDelivery.ReliableSequenced);
        w.Dispose();

        Debug.Log($"[BS] pause command broadcast: {cmd}");
    }

    void HandlePauseCommand(string cmd)
    {
        var pm = FindObjectOfType<PauseMenu>();
        if (pm == null) return;
        switch (cmd)
        {
            case "P": pm.DoPause(); break;
            case "R": pm.DoResume(); break;
            case "M": pm.DoReturn(); break;
        }
    }

    public void SyncScore(int left, int right, string server)
    {
        if (!NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"[BS] client score send ignored: {left}-{right}");
            return;
        }

        var sc = FindObjectOfType<Shuttlecock>();
        Vector2 landingPos = sc != null ? sc.transform.position : Vector2.zero;

        var w = new FastBufferWriter(64, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(MsgScore);
        w.WriteValueSafe(left);
        w.WriteValueSafe(right);
        w.WriteValueSafe(EncodeServer(server));
        w.WriteValueSafe(landingPos);
        w.WriteValueSafe(NetworkTime);
        Bcast(w, NetworkDelivery.ReliableSequenced);
        w.Dispose();

        Debug.Log($"[BS] score broadcast: {left}-{right} server={server} landing={landingPos}");
    }

    void SendToServer(FastBufferWriter writer, NetworkDelivery delivery)
    {
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("BS", NetworkManager.ServerClientId, writer, delivery);
    }

    void Bcast(FastBufferWriter writer, NetworkDelivery delivery)
    {
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("BS", id, writer, delivery);
    }

    static byte EncodeServer(string server)
    {
        return server == "Right" ? ServerRight : ServerLeft;
    }

    static string DecodeServer(byte server)
    {
        return server == ServerRight ? "Right" : "Left";
    }
}
