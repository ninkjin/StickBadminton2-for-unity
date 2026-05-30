using UnityEngine;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class MultiplayerManager : MonoBehaviour
{
    public static MultiplayerManager Instance { get; private set; }
    public string statusText = "未连接";

    private NetworkManager networkManager;
    private Coroutine connectTimeoutRoutine;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SetupNetworkManager();
    }

    void SetupNetworkManager()
    {
        networkManager = gameObject.AddComponent<NetworkManager>();
        var transport = gameObject.AddComponent<UnityTransport>();

        networkManager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            ProtocolVersion = 1
        };

        networkManager.OnServerStarted += () =>
        {
            statusText = "房间已创建，等待对手加入...";
            Debug.Log("[MP] Host 启动成功！");
            SpawnSyncObject();
            StartHostBroadcast();
        };

        networkManager.OnClientConnectedCallback += (id) =>
        {
            if (connectTimeoutRoutine != null)
            {
                StopCoroutine(connectTimeoutRoutine);
                connectTimeoutRoutine = null;
            }

            if (networkManager.IsServer && id == NetworkManager.ServerClientId)
            {
                SpawnSyncObject();
                return;
            }

            if (networkManager.IsServer)
            {
                statusText = "对手已加入！";
                Debug.Log("[MP] 对手 " + id + " 加入了房间！");
            }
            else
            {
                statusText = "已加入房间！";
                Debug.Log("[MP] Client 已连接！");
                SpawnSyncObjectClient();
            }
        };

        networkManager.OnClientDisconnectCallback += (id) =>
        {
            if (networkManager.IsServer && id != NetworkManager.ServerClientId)
            {
                statusText = "对手已离开，等待重新加入...";
                Debug.Log("[MP] 对手 " + id + " 离开了房间");
            }
            else
            {
                statusText = "连接断开";
                Debug.Log("[MP] 连接断开");
            }
        };

        networkManager.OnTransportFailure += () =>
        {
            if (connectTimeoutRoutine != null)
            {
                StopCoroutine(connectTimeoutRoutine);
                connectTimeoutRoutine = null;
            }
            statusText = "连接失败，请重试";
            Debug.LogError("[MP] Transport 失败！");
        };
    }

    public bool IsHost
    {
        get { return networkManager != null && networkManager.IsServer; }
    }

    public bool HasRemoteClient()
    {
        if (networkManager == null) return false;
        if (networkManager.IsServer)
            return networkManager.ConnectedClientsIds.Count >= 1;
        return networkManager.IsConnectedClient;
    }

    public bool IsConnected()
    {
        return networkManager != null && (networkManager.IsServer || networkManager.IsClient);
    }

    public void CreateRoom()
    {
        if (networkManager.IsListening) return;

        var transport = GetComponent<UnityTransport>();
        transport.ConnectionData.Address = "0.0.0.0";
        transport.ConnectionData.Port = 7777;

        statusText = "正在创建房间...";
        Debug.Log("[MP] 调用 StartHost...");
        networkManager.StartHost();
    }

    public void JoinRoom(string ip = null)
    {
        if (networkManager.IsListening) return;

        if (string.IsNullOrEmpty(ip))
        {
            statusText = "请输入主机IP地址";
            return;
        }

        var transport = GetComponent<UnityTransport>();
        transport.ConnectionData.Address = ip;
        transport.ConnectionData.Port = 7777;

        statusText = "正在加入 " + ip + "...";
        Debug.Log("[MP] 尝试连接: " + ip);
        networkManager.StartClient();
        connectTimeoutRoutine = StartCoroutine(ConnectTimeout());
    }

    IEnumerator ConnectTimeout()
    {
        yield return new WaitForSeconds(5f);
        if (networkManager.IsListening && !networkManager.IsConnectedClient)
        {
            statusText = "连接超时，未找到房间";
            Debug.Log("[MP] 连接超时");
            networkManager.Shutdown();
        }
        connectTimeoutRoutine = null;
    }

    // ===== UDP 自动发现 =====

    private Coroutine broadcastRoutine;

    public void StartHostBroadcast()
    {
        if (broadcastRoutine != null) StopCoroutine(broadcastRoutine);
        broadcastRoutine = StartCoroutine(BroadcastLoop());
    }

    IEnumerator BroadcastLoop()
    {
        var udp = new UdpClient();
        udp.EnableBroadcast = true;
        var endpoint = new IPEndPoint(IPAddress.Broadcast, 17778);
        var data = Encoding.UTF8.GetBytes("BADMINTON_HOST");

        while (networkManager != null && networkManager.IsServer)
        {
            try { udp.Send(data, data.Length, endpoint); }
            catch (System.Exception) { }
            yield return new WaitForSeconds(0.5f);
        }
        udp.Close();
        Debug.Log("[MP] 广播已停止");
    }

    public void StartDiscoveryAndJoin()
    {
        StartCoroutine(DiscoveryRoutine());
    }

    IEnumerator DiscoveryRoutine()
    {
        statusText = "正在搜索房间...";
        Debug.Log("[MP] 开始UDP搜索...");

        Socket sock = null;
        try
        {
            sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sock.Bind(new IPEndPoint(IPAddress.Any, 17778));
            sock.ReceiveTimeout = 500;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[MP] UDP绑定失败: " + e.Message);
            statusText = "搜索失败，请手动输入IP";
            if (sock != null) sock.Close();
            yield break;
        }

        var buffer = new byte[256];
        float timeout = 3f;
        float elapsed = 0f;
        string foundIP = "";

        while (elapsed < timeout && string.IsNullOrEmpty(foundIP))
        {
            try
            {
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                int len = sock.ReceiveFrom(buffer, ref remoteEP);
                string msg = Encoding.UTF8.GetString(buffer, 0, len);
                if (msg == "BADMINTON_HOST")
                {
                    foundIP = ((IPEndPoint)remoteEP).Address.ToString();
                    Debug.Log("[MP] 发现主机: " + foundIP);
                }
            }
            catch (SocketException) { }
            catch (System.Exception e)
            {
                Debug.LogWarning("[MP] UDP异常: " + e.Message);
            }
            yield return null;
            elapsed += Time.deltaTime;
        }

        sock.Close();

        if (!string.IsNullOrEmpty(foundIP))
        {
            statusText = "发现主机: " + foundIP + "，正在加入...";
            JoinRoom(foundIP);
        }
        else
        {
            statusText = "未发现主机，请手动输入IP";
            Debug.Log("[MP] UDP搜索超时");
        }
    }

    public void StopNetwork()
    {
        if (broadcastRoutine != null) { StopCoroutine(broadcastRoutine); broadcastRoutine = null; }
        if (connectTimeoutRoutine != null) { StopCoroutine(connectTimeoutRoutine); connectTimeoutRoutine = null; }
        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
        }
        statusText = "未连接";
        Debug.Log("[MP] 网络已关闭");
    }

    void SpawnSyncObject()
    {
        if (!networkManager.IsServer) return;
        if (NetworkLobbySync.Instance != null) return;

        GameObject go = new GameObject("NetworkSync");
        go.AddComponent<NetworkLobbySync>();
        go.AddComponent<NetworkBattleSync>();
        DontDestroyOnLoad(go);
        Debug.Log("[MP] 同步对象已生成");
    }

    void SpawnSyncObjectClient()
    {
        if (NetworkLobbySync.Instance != null) return;
        GameObject go = new GameObject("NetworkSync");
        go.AddComponent<NetworkLobbySync>();
        go.AddComponent<NetworkBattleSync>();
        DontDestroyOnLoad(go);
    }
}
