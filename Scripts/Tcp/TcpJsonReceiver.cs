using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json.Linq; // 使用 JObject 动态解析

public class TcpJsonServer : MonoBehaviour
{
    [Header("Web API 设置")]
    [SerializeField] private string baseApiUrl = "http://127.0.0.1:9620/amd-eeg-eyes-tracking";
    [SerializeField] private string sseEndpoint = "/sseApi/sseGameConnect";

    private Thread listenThread;
    private volatile bool isRunning;
    private HttpWebRequest currentRequest;

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

    public void RestartServer()
    {
        StopServer();
        StartServer();
    }

    public void StartServer()
    {
        if (isRunning) return;

        isRunning = true;
        listenThread = new Thread(ListenSSE);
        listenThread.IsBackground = true;
        listenThread.Start();
    }

    public void StopServer()
    {
        if (!isRunning) return;
        isRunning = false;

        try { currentRequest?.Abort(); } catch { }

        if (listenThread != null && listenThread.IsAlive)
        {
            if (!listenThread.Join(500))
            {
                try { listenThread.Interrupt(); } catch { }
            }
        }
        listenThread = null;
    }

    private void ListenSSE()
    {
        try
        {
            string url = baseApiUrl + sseEndpoint;
            currentRequest = (HttpWebRequest)WebRequest.Create(url);
            currentRequest.Method = "GET";
            // currentRequest.Accept = "text/event-stream"; // 标准 SSE
            currentRequest.Timeout = Timeout.Infinite; // 保持长连接

            Debug.Log("正在连接 SSE 服务器: " + url);

            using (WebResponse response = currentRequest.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
            {
                Debug.Log("SSE 流已连接成功！");

                while (isRunning && !reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // 解析标准 SSE 格式 (通常以 data: 开头，有些后台直接推纯 json)
                    string jsonStr = line.StartsWith("data:") ? line.Substring(5).Trim() : line.Trim();

                    if (jsonStr.StartsWith("{"))
                    {
                        ParseJsonData(jsonStr);
                    }
                }
            }
        }
        catch (ThreadInterruptedException) { }
        catch (WebException wex)
        {
            if (isRunning) Debug.LogWarning("SSE 网络异常或断开: " + wex.Message);
        }
        catch (Exception ex)
        {
            if (isRunning) Debug.LogError("SSE 接收错误: " + ex);
        }
        finally
        {
            currentRequest = null;
            if (isRunning)
            {
                Debug.Log("SSE 连接已断开，尝试重连...");
                Thread.Sleep(2000); // 断线后等待2秒重连
                if (isRunning) ListenSSE();
            }
        }
    }

    private void ParseJsonData(string json)
    {
        try
        {
            JObject data = JObject.Parse(json);
            int type = data["type"]?.Value<int>() ?? 0;
            int subType = data["subType"]?.Value<int>() ?? 0;

            if (type == 3)
            {
                JToken content = data["content"];
                if (content == null) return;

                if (subType == 3206) //眼动数据
                {
                    float gazeX = content["gazeX"]?.Value<float>() ?? 0f;
                    float gazeY = content["gazeY"]?.Value<float>() ?? 0f;

                    MainThreadDispatcher.Enqueue(() => {
                        if (EyeTrackerController_prefab.Instance != null)
                        {
                            EyeTrackerController_prefab.Instance.UpdateNetworkGaze(gazeX, gazeY);
                        }
                    });
                }
                else if (subType == 3207) //脑电数据
                {
                    int attention = content["attention"]?.Value<int>() ?? 0;

                    MainThreadDispatcher.Enqueue(() => {
                        // 1. 实时更新 Text 文本
                        if (AttentionUIText.Instance != null)
                            AttentionUIText.Instance.UpdateAttentionText(attention);

                        // 2. 更新模糊 UI 效果
                        if (AttentionBlurUI.Instance != null)
                            AttentionBlurUI.Instance.SetAttention(attention);

                        // ★ 3. 通知 ButtonEvent 数据已到达，自动点亮绿灯 ★
                        if (ButtonEvent.Instance != null)
                            ButtonEvent.Instance.OnReceiveEEGData();
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("JSON解析错误: " + ex.Message + "\n错误内容: " + json);
        }
    }
}
