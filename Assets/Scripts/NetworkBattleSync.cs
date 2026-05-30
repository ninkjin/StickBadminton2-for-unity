using UnityEngine;
using Unity.Netcode;

public class NetworkBattleSync : MonoBehaviour
{
    public static NetworkBattleSync Instance { get; private set; }

    public Vector2 RemotePos;
    public int RemoteFacing = 1;
    public bool RemoteSwing;
    public bool RemoteServe;
    public bool RemoteWalk;
    public bool Received;

    public Vector2 BirdiePos;
    public bool BirdieInPlay;

    bool ready;
    int sendCounter;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    float logTimer;

    void Update()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!ready)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("BS", OnMsg);
                ready = true;
                Debug.Log("[BS] handler 已注册");
            }
        }
        else
        {
            ready = false;
        }
        logTimer += Time.deltaTime;
        if (logTimer > 3f)
        {
            Debug.Log($"[BS] Alive, Received={Received} isServer={NetworkManager.Singleton.IsServer}");
            logTimer = 0f;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && ready)
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("BS");
    }

    void OnMsg(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string msg);
        var p = msg.Split('|');
        if (p.Length < 2) return;

        if (p[0] == "M")
        {
            bool isSvr = NetworkManager.Singleton.IsServer;
            if (isSvr) return;
            Debug.Log($"[BS] 收到暂停命令: {p[1]}");
            HandlePauseCommand(p[1]);
            return;
        }

        if (p.Length < 3) return;

        if (p[0] == "P")
        {
            bool isHostMsg = p[1] == "H";
            bool isSvr = NetworkManager.Singleton.IsServer;

            if ((isSvr && isHostMsg) || (!isSvr && !isHostMsg)) return;

            RemotePos = new Vector2(float.Parse(p[2]), float.Parse(p[3]));
            RemoteFacing = int.Parse(p[4]);
            RemoteSwing = p[5] == "1";
            RemoteServe = p[6] == "1";
            RemoteWalk = p[7] == "1";
            Received = true;

            // 球数据：H消息总是接受；C消息只在球未飞行时接受（发球阶段）
            if (p.Length > 10)
            {
                bool bInPlay = p[10] == "1";
                if (isHostMsg || !bInPlay)
                {
                    BirdiePos = new Vector2(float.Parse(p[8]), float.Parse(p[9]));
                    BirdieInPlay = bInPlay;
                }
            }
            // 比分数据：H消息附带权威比分（S消息的冗余备份，防UDP丢包）
            if (isHostMsg && p.Length > 13)
            {
                int rLeft = int.Parse(p[11]);
                int rRight = int.Parse(p[12]);
                string rServer = p[13];
                var gm = FindObjectOfType<GameManager>();
                int localTotal = gm != null ? gm.leftScore + gm.rightScore : 0;
                int remoteTotal = rLeft + rRight;
                // 总分数单调递增：防止旧P消息覆盖新比分
                if (gm != null && remoteTotal > localTotal)
                {
                    var sc = FindObjectOfType<Shuttlecock>();
                    bool ballStopped = sc == null || !sc.isInPlay;
                    Debug.Log($"[BS] P消息比分更新: 本地{gm.leftScore}-{gm.rightScore}(t={localTotal}) -> 远程{rLeft}-{rRight}(t={remoteTotal}), ballStopped={ballStopped}, state={gm.state}");
                    if (ballStopped)
                    {
                        gm.ApplyScoreSync(rLeft, rRight, rServer);
                    }
                    else
                    {
                        gm.leftScore = rLeft;
                        gm.rightScore = rRight;
                        gm.server = rServer;
                    }
                }
            }
            // 击球数据：H/C消息附带击球参数（H事件的冗余备份，防UDP丢包）
            if (p.Length > 18)
            {
                int hitId = int.Parse(p[14]);
                if (hitId != processedHitId && hitId > 0)
                {
                    processedHitId = hitId;
                    float hSpd = float.Parse(p[15]);
                    float hDir = float.Parse(p[16]);
                    float hPx = float.Parse(p[17]);
                    float hPy = float.Parse(p[18]);
                    var sc = FindObjectOfType<Shuttlecock>();
                    if (sc != null)
                    {
                        sc.transform.position = new Vector3(hPx, hPy, sc.transform.position.z);
                        sc.HitMe(hSpd, hDir);
                        var gm2 = FindObjectOfType<GameManager>();
                        if (gm2 != null && gm2.state != GameState.Playing && gm2.state != GameState.GameOver)
                            gm2.state = GameState.Playing;
                        Debug.Log($"[BS] P消息冗余击球: speed={hSpd} dir={hDir} pos=({hPx:F2},{hPy:F2})");
                    }
                }
            }
        }
        else if (p[0] == "H")
        {
            bool isSvr = NetworkManager.Singleton.IsServer;
            bool fromServer = senderId == NetworkManager.ServerClientId;
            Debug.Log($"[BS] H事件: isServer={isSvr} fromServer={fromServer} msg={msg}");
            if (isSvr == fromServer) return;
            // 去重：P消息已处理过的跳过
            if (p.Length >= 6 && int.Parse(p[5]) == processedHitId) return;
            var sc = FindObjectOfType<Shuttlecock>();
            if (sc != null)
            {
                float spd = float.Parse(p[1]);
                float dir = float.Parse(p[2]);
                if (p.Length >= 5)
                    sc.transform.position = new Vector3(float.Parse(p[3]), float.Parse(p[4]), sc.transform.position.z);
                sc.HitMe(spd, dir);
                if (p.Length >= 6)
                    processedHitId = int.Parse(p[5]);
                // 接收方切到Playing（不只是WaitingToServe：PointScored倒计时未结束时发球也需处理）
                var gm = FindObjectOfType<GameManager>();
                if (gm != null && gm.state != GameState.Playing && gm.state != GameState.GameOver)
                {
                    Debug.Log($"[BS] H事件触发状态切换: {gm.state} -> Playing");
                    gm.state = GameState.Playing;
                }
                Debug.Log($"[BS] 远程击球执行: speed={spd} dir={dir} pos={sc.transform.position}");
            }
        }
        else if (p[0] == "S")
        {
            BirdieInPlay = false;
            var gm = FindObjectOfType<GameManager>();
            if (gm != null)
            {
                gm.ApplyScoreSync(int.Parse(p[1]), int.Parse(p[2]), p[3]);
                Debug.Log($"[BS] 得分同步: {gm.leftScore}-{gm.rightScore} server={gm.server}");
            }
        }
        else if (p[0] == "W")
        {
            bool isSvr = NetworkManager.Singleton.IsServer;
            bool fromServer = senderId == NetworkManager.ServerClientId;
            if (isSvr == fromServer) return;

            int evtId = int.Parse(p[2]);
            if (evtId == lastSwingEventId) return;
            lastSwingEventId = evtId;

            bool isServe = p[1] == "1";
            Debug.Log($"[BS] 收到挥拍事件: isServe={isServe} id={evtId}");

            if (isSvr)
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
    }

    int lastSwingEventId;

    public void SendHostState(Vector2 pos, int facing, bool swinging, bool serving, bool walking,
        Vector2 birdiePos, bool birdieInPlay)
    {
        sendCounter++;
        if (sendCounter % 3 != 0 && !swinging && !serving) return;
        var gm = FindObjectOfType<GameManager>();
        int l = gm != null ? gm.leftScore : 0;
        int r = gm != null ? gm.rightScore : 0;
        string srv = gm != null ? gm.server : "Left";
        string msg = $"P|H|{pos.x:F2}|{pos.y:F2}|{facing}|{(swinging?1:0)}|{(serving?1:0)}|{(walking?1:0)}|{birdiePos.x:F2}|{birdiePos.y:F2}|{(birdieInPlay?1:0)}|{l}|{r}|{srv}|{lastHitId}|{lastHitSpeed:F2}|{lastHitDir:F2}|{lastHitPosX:F2}|{lastHitPosY:F2}";
        Bcast(msg);
    }

    public void SendClientState(Vector2 pos, int facing, bool swinging, bool serving, bool walking,
        Vector2 birdiePos, bool birdieInPlay)
    {
        sendCounter++;
        if (sendCounter % 3 != 0 && !swinging && !serving) return;
        string msg = $"P|C|{pos.x:F2}|{pos.y:F2}|{facing}|{(swinging?1:0)}|{(serving?1:0)}|{(walking?1:0)}|{birdiePos.x:F2}|{birdiePos.y:F2}|{(birdieInPlay?1:0)}|0|0|L|{lastHitId}|{lastHitSpeed:F2}|{lastHitDir:F2}|{lastHitPosX:F2}|{lastHitPosY:F2}";
        SendToServer(msg);
    }

    // 击球数据冗余：H事件丢失时P消息兜底
    int lastHitId;
    float lastHitSpeed, lastHitDir, lastHitPosX, lastHitPosY;
    int processedHitId = -1;

    public void SendHit(float speed, float dir, Vector2 ballPos)
    {
        lastHitId++;
        lastHitSpeed = speed;
        lastHitDir = dir;
        lastHitPosX = ballPos.x;
        lastHitPosY = ballPos.y;

        string msg = $"H|{speed}|{dir}|{ballPos.x:F2}|{ballPos.y:F2}|{lastHitId}";
        if (NetworkManager.Singleton.IsServer)
            Bcast(msg);
        else
            SendToServer(msg);
    }

    // 挥拍事件（立即发送，不节流）
    int swingEventId;

    public void SendSwingEvent(bool isServe)
    {
        swingEventId++;
        string msg = $"W|{(isServe ? 1 : 0)}|{swingEventId}";
        Debug.Log($"[BS] 发送挥拍事件: isServe={isServe} id={swingEventId}");
        if (NetworkManager.Singleton.IsServer)
            Bcast(msg);
        else
            SendToServer(msg);
    }

    // 暂停同步（主机→客户端）
    public void SendPauseCommand(string cmd)
    {
        if (!NetworkManager.Singleton.IsServer) return;
        string msg = $"M|{cmd}";
        Bcast(msg);
        Debug.Log($"[BS] 暂停命令广播: {cmd}");
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
        string msg = $"S|{left}|{right}|{server}";
        if (NetworkManager.Singleton.IsServer)
        {
            Debug.Log($"[BS] S消息广播: {left}-{right} server={server}");
            Bcast(msg);
        }
        else
        {
            Debug.Log($"[BS] S消息客户端发送(会被忽略): {left}-{right}");
        }
    }

    void SendToServer(string msg)
    {
        var w = new FastBufferWriter(256, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(msg);
        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("BS", NetworkManager.ServerClientId, w);
        w.Dispose();
    }

    void Bcast(string msg)
    {
        var w = new FastBufferWriter(256, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(msg);
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("BS", id, w);
        w.Dispose();
    }
}
