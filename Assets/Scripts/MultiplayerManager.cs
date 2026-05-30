using UnityEngine;
using System.Collections;
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
            return networkManager.ConnectedClientsIds.Count >= 1; // 至少一个远程客户端
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

    public void JoinRoom()
    {
        if (networkManager.IsListening) return;

        var transport = GetComponent<UnityTransport>();
        transport.ConnectionData.Address = "127.0.0.1";
        transport.ConnectionData.Port = 7777;

        statusText = "正在加入房间...";
        Debug.Log("[MP] 调用 StartClient...");
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

    public void StopNetwork()
    {
        if (connectTimeoutRoutine != null)
        {
            StopCoroutine(connectTimeoutRoutine);
            connectTimeoutRoutine = null;
        }
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
