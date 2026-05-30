using UnityEngine;
using Unity.Netcode;
using System;

public class NetworkLobbySync : MonoBehaviour
{
    public static NetworkLobbySync Instance { get; private set; }

    public int P1Selection = -1;
    public int P2Selection = -1;
    public bool P1Ready;
    public bool P2Ready;
    public bool Scene1Ready;
    public bool Scene2Ready;

    public event Action OnSelectionChanged;
    public event Action OnBothReady;
    public event Action OnBothSceneReady;

    bool handlerRegistered;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Update()
    {
        // 注册 handler，注意退房重连时 NetworkManager 会更换
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!handlerRegistered)
            {
                NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler("LS", OnMessage);
                handlerRegistered = true;
                Debug.Log("[LS] handler 已注册");
            }
        }
        else
        {
            // NetworkManager 不在监听 → 重置，下次连接重新注册
            handlerRegistered = false;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null && handlerRegistered)
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler("LS");
    }

    void OnMessage(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out string msg);
        var p = msg.Split('|');

        if (p[0] == "S") // Select
        {
            int slot = int.Parse(p[1]);
            int val = int.Parse(p[2]);
            if (slot == 1) P1Selection = val;
            else P2Selection = val;

            // 服务器转发给其他客户端
            if (NetworkManager.Singleton.IsServer && senderId != NetworkManager.ServerClientId)
                Forward(msg);

            OnSelectionChanged?.Invoke();
        }
        else if (p[0] == "R") // Ready
        {
            if (p[1] == "1") P1Ready = true;
            else P2Ready = true;

            if (NetworkManager.Singleton.IsServer && senderId != NetworkManager.ServerClientId)
                Forward(msg);

            if (P1Ready && P2Ready)
                OnBothReady?.Invoke();
        }
        else if (p[0] == "G") // SceneReady
        {
            if (p[1] == "1") Scene1Ready = true;
            else Scene2Ready = true;

            // 主机收到远程G消息时，重发双方状态（防UDP丢包导致客户端收不到对方的G）
            if (NetworkManager.Singleton.IsServer && senderId != NetworkManager.ServerClientId)
            {
                if (Scene1Ready) Forward("G|1");
                if (Scene2Ready) Forward("G|2");
            }

            if (Scene1Ready && Scene2Ready)
                OnBothSceneReady?.Invoke();
        }
    }

    void Forward(string msg)
    {
        var w = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(msg);
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LS", id, w);
        w.Dispose();
    }

    void Send(string msg)
    {
        var w = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(msg);

        if (NetworkManager.Singleton.IsServer)
        {
            // 发给所有客户端（包括远程的）
            foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
                NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LS", id, w);
        }
        else
        {
            NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage("LS",
                NetworkManager.ServerClientId, w);
        }

        w.Dispose();
        OnMessage(0, MakeReader(msg));
    }

    FastBufferReader MakeReader(string msg)
    {
        var w = new FastBufferWriter(128, Unity.Collections.Allocator.Temp);
        w.WriteValueSafe(msg);
        return new FastBufferReader(w, Unity.Collections.Allocator.Temp);
    }

    public bool IsMyTurn()
    {
        if (NetworkManager.Singleton.IsServer)
            return P1Selection == -1;
        return P1Selection != -1 && P2Selection == -1;
    }

    public void SelectCharacter(int idx)
    {
        int slot = NetworkManager.Singleton.IsServer ? 1 : 2;
        Send($"S|{slot}|{idx}");
    }

    public void SetReady()
    {
        int slot = NetworkManager.Singleton.IsServer ? 1 : 2;
        Send($"R|{slot}");
    }

    public void SetSceneReady()
    {
        int slot = NetworkManager.Singleton.IsServer ? 1 : 2;
        Send($"G|{slot}");
    }

    public void ResetState()
    {
        P1Selection = -1;
        P2Selection = -1;
        P1Ready = false;
        P2Ready = false;
        Scene1Ready = false;
        Scene2Ready = false;
        Debug.Log("[LS] 大厅状态已重置");
    }

    public int GetMySelection()
    {
        return NetworkManager.Singleton.IsServer ? P1Selection : P2Selection;
    }

    public int GetOpponentSelection()
    {
        return NetworkManager.Singleton.IsServer ? P2Selection : P1Selection;
    }
}
