using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

public class TcpJsonServer : MonoBehaviour
{
    [Header("模式开关")]
    [SerializeField] private bool useLocalhostOnly = false; // true: 127.0.0.1  false: 0.0.0.0

    [Header("服务器配置")]
    [SerializeField] private int port = 9090;

    [Tooltip("跨设备模式下的绑定地址，默认 0.0.0.0")]
    [SerializeField] private string remoteBindAddress = "0.0.0.0";

    [Tooltip("本地模式绑定地址，固定 127.0.0.1")]
    [SerializeField] private string localhostBindAddress = "127.0.0.1";

    private TcpListener server;
    private TcpClient currentClient;
    private NetworkStream stream;
    private Thread listenThread;
    private volatile bool isRunning;

    private readonly StringBuilder _recvBuf = new StringBuilder();

    [Serializable]
    public class MessageData
    {
        public int attention;
        public int meditation;
        public long timestamp;
    }

    private string CurrentBindAddress => useLocalhostOnly ? localhostBindAddress : remoteBindAddress;

    void Start()
    {
        if (MainThreadDispatcher.Instance == null)
        {
            Debug.LogError("场景中缺少 MainThreadDispatcher，请添加到任意GameObject上！");
            enabled = false;
            return;
        }
        StartServer();
    }

    void OnDestroy()
    {
        StopServer();
    }

    /// <summary>
    /// 给 UI Toggle 调用：true=本地(127.0.0.1)，false=跨设备(0.0.0.0)
    /// </summary>
    public void SetUseLocalhostOnly(bool enabled)
    {
        if (useLocalhostOnly == enabled) return;
        useLocalhostOnly = enabled;
        RestartServer();
    }

    public void RestartServer()
    {
        StopServer();
        StartServer();
    }

    public void StartServer()
    {
        if (isRunning) return;

        isRunning = true;
        listenThread = new Thread(ListenAndReceive);
        listenThread.IsBackground = true;
        listenThread.Start();
    }

    public void StopServer()
    {
        if (!isRunning) return;

        isRunning = false;

        // 关键：先 Stop() 打断 AcceptTcpClient 的阻塞
        try { server?.Stop(); } catch { }
        server = null;

        CloseClient();

        if (listenThread != null && listenThread.IsAlive)
        {
            // 给一个超时，避免编辑器退出卡住
            if (!listenThread.Join(500))
            {
                try { listenThread.Interrupt(); } catch { }
            }
        }
        listenThread = null;
    }

    private void ListenAndReceive()
    {
        try
        {
            string bind = CurrentBindAddress;
            IPAddress ip = IPAddress.Parse(bind);

            server = new TcpListener(ip, port);
            server.Start();
            Debug.Log($"服务器已启动，监听 {bind}:{port} （useLocalhostOnly={useLocalhostOnly}）");

            while (isRunning)
            {
                try
                {
                    currentClient = server.AcceptTcpClient(); // StopServer 会打断这里
                }
                catch (SocketException se)
                {
                    // 正常停止（Windows 常见 10004）
                    if (!isRunning || se.ErrorCode == 10004) return;
                    throw;
                }

                Debug.Log("客户端已连接");
                stream = currentClient.GetStream();
                byte[] buffer = new byte[1024];
                _recvBuf.Clear();

                while (isRunning && currentClient.Connected)
                {
                    int bytesRead = 0;
                    try
                    {
                        bytesRead = stream.Read(buffer, 0, buffer.Length);
                    }
                    catch (SocketException se)
                    {
                        if (!isRunning || se.ErrorCode == 10004) break;
                        throw;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    if (bytesRead <= 0) break;

                    string chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    _recvBuf.Append(chunk);

                    // 按 '\n' 切分（兼容 \r\n）
                    while (true)
                    {
                        string all = _recvBuf.ToString();
                        int nl = all.IndexOf('\n');
                        if (nl < 0) break;

                        string line = all.Substring(0, nl).Trim();
                        string rest = all.Substring(nl + 1);

                        _recvBuf.Clear();
                        _recvBuf.Append(rest);

                        if (!string.IsNullOrEmpty(line))
                            ParseJsonData(line);
                    }
                }

                // 只关闭客户端，不停止 server（允许下一台设备继续连）
                CloseClient();
            }
        }
        catch (Exception ex)
        {
            if (!isRunning) return;
            Debug.LogError("TCP服务器错误: " + ex);
        }
        finally
        {
            try { server?.Stop(); } catch { }
            server = null;
            CloseClient();
        }
    }

    private void CloseClient()
    {
        try { stream?.Close(); } catch { }
        stream = null;

        try { currentClient?.Close(); } catch { }
        currentClient = null;
    }

    private void ParseJsonData(string json)
    {
        try
        {
            MessageData data = JsonConvert.DeserializeObject<MessageData>(json);
            if (data != null)
            {
                MainThreadDispatcher.Enqueue(() => ProcessReceivedData(data));
            }
        }
        catch (JsonException ex)
        {
            Debug.LogError("JSON解析错误: " + ex.Message);
            Debug.LogError("错误的JSON内容: " + json);
        }
    }

    private void ProcessReceivedData(MessageData data)
    {
        int attentionValue = data.attention;
        // Debug.Log($"处理数据 - 注意力: {data.attention}, 冥想值: {data.meditation}, 时间戳: {data.timestamp}");

        if (AttentionUIText.Instance != null)
            AttentionUIText.Instance.UpdateAttentionText(attentionValue);

        if (AttentionBlurUI.Instance != null)
            AttentionBlurUI.Instance.SetAttention(attentionValue);
    }
}
