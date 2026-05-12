using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class ButtonEvent : MonoBehaviour
{
    [Header("UI References")]
    public GameObject WraningUI_1, WraningUI_2, WraningUI_3, WraningUI_4, WraningUI_5;

    EyeTrackerController eyeTrackerController;

    public GameObject StartUI, SettingUI, TrainUI;

    //切换健康小朋友的开关
    public Toggle fastModeToggle;

    // 记录脑电连接状态
    private bool isEEGConnected = false;
    // 防止用户在连接过程中狂点按钮 (新增)
    private bool isConnecting = false;

    // 服务器地址和设备名
    //private const string EEG_API_URL = "http://127.0.0.1:8080/api/connect";
    private const string EEG_DEVICE_NAME = "HR-S0A9265";
    [Header("EEG Network Settings")]
    [Tooltip("拖入那个用来切换本地/远端的 Toggle")]
    public Toggle eegLocalToggle;

    // 动态获取当前的 API 地址
    private string CurrentEegApiUrl
    {
        get
        {
            // 如果取消勾选了本地连接，则去连接 192.168.10.22
            if (eegLocalToggle != null && !eegLocalToggle.isOn)
            {
                return "http://192.168.10.22:8080/api/connect";
            }
            // 否则默认连接本地
            return "http://127.0.0.1:8080/api/connect";
        }
    }

    public ReticleClickCalibrator reticleCalibrator;

    [Header("EEG Status Indicator")]
    [Tooltip("用来显示脑电连接状态的图片（Image组件）")]
    public Image eegStatusImage;
    public Color connectedColor = Color.green; // 连接成功显示的颜色
    public Color disconnectedColor = Color.red; // 断开或未连接显示的颜色

    [System.Serializable]
    public class EEGResponse
    {
        public string status;
        public string message;
        public string error;
    }

    void Start()
    {
        GameObject eyeTrackerPrefab = GameObject.Find("ASeeTracker");
        if (eyeTrackerPrefab != null)
        {
            eyeTrackerController = eyeTrackerPrefab.GetComponent<EyeTrackerController>();
            Debug.Log($"eyeTrackerController is_tracing:{eyeTrackerController.is_tracing}");
        }
        else
        {
            Debug.LogError("未找到 ASeeTracker 物体，请检查场景！");
        }

        // ★★★ 监听 Toggle 变化 ★★★
        if (fastModeToggle != null)
        {
            fastModeToggle.onValueChanged.AddListener(OnFastModeChanged);
            // 初始化：根据当前勾选状态设置图片
            OnFastModeChanged(fastModeToggle.isOn);
        }

        // ★ 初始化状态颜色 ★
        UpdateEEGStatusUI(false);

        ResetAllWarnings();
    }

    // ★★★ Toggle 回调：切换图片 ★★★
    void OnFastModeChanged(bool isOn)
    {
        if (eyeTrackerController != null)
        {
            eyeTrackerController.SetGazePointSprite(isOn);
        }
    }

    //更新UI颜色
    private void UpdateEEGStatusUI(bool isConnected)
    {
        if (eegStatusImage != null)
        {
            eegStatusImage.color = isConnected ? connectedColor : disconnectedColor;
        }
    }

    void Update()
    {

    }

    // ==========================================
    // EEG 连接逻辑
    // ==========================================
    public void OnClickConnectEEG()
    {
        // ★★★ 新增：如果已经连接成功（绿灯状态），直接拦截，不再发送任何请求 ★★★
        if (isEEGConnected)
        {
            Debug.Log("脑电已成功连接，为防止Bug，拦截重复请求。");
            // 如果你想的话，也可以在这里弹出一个提示UI，比如：WraningUI_3.SetActive(true);
            return;
        }

        // 如果正在连接中，直接忽略点击，防止重复请求
        if (isConnecting)
        {
            Debug.Log("正在连接中，请稍候...");
            return;
        }

        //点击按钮时显示“正在连接”(Warning_5)
        if (WraningUI_5) WraningUI_5.SetActive(true);

        StartCoroutine(PostConnectEEG());
    }

    IEnumerator PostConnectEEG()
    {
        // 标记为正在连接
        isConnecting = true;

        string jsonData = $"{{\"device_name\": \"{EEG_DEVICE_NAME}\"}}";
        // 获取当前应该使用的 URL
        string targetUrl = CurrentEegApiUrl;

        using (UnityWebRequest request = new UnityWebRequest(targetUrl, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            Debug.Log($"正在连接EEG服务器: {targetUrl} ...");
            yield return request.SendWebRequest();

            if (WraningUI_5) WraningUI_5.SetActive(false);
            // 请求结束（无论成功失败），解除锁定，允许再次点击重试
            isConnecting = false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("EEG服务器响应: " + request.downloadHandler.text);
                EEGResponse response = JsonUtility.FromJson<EEGResponse>(request.downloadHandler.text);

                if (response != null && response.status == "connected")
                {
                    isEEGConnected = true;
                    UpdateEEGStatusUI(true); //变绿
                    if (WraningUI_3) WraningUI_3.SetActive(true);
                }
                else
                {
                    // 业务逻辑失败（连上了服务器但返回failed）
                    isEEGConnected = false;
                    UpdateEEGStatusUI(false); //变红
                    if (WraningUI_4) WraningUI_4.SetActive(true);
                    Debug.LogWarning("EEG连接逻辑失败: " + (response != null ? response.error : "Unknown Error"));
                }
            }
            else
            {
                // 网络通信本身失败
                isEEGConnected = false;
                UpdateEEGStatusUI(false); //变红
                if (WraningUI_4) WraningUI_4.SetActive(true);
                Debug.Log("EEG网络请求错误: " + request.error);
            }
        }
    }

    public void CloseWarningUI_3()
    {
        if (WraningUI_3) WraningUI_3.SetActive(false);
    }

    public void CloseWarningUI_4()
    {
        if (WraningUI_4) WraningUI_4.SetActive(false);
        // 关闭失败窗口后，用户可以再次点击 EEG 按钮，上面的逻辑会重新执行
    }

    // ==========================================
    // 流程控制逻辑
    // ==========================================

    public void StartCalibration()
    {
        // 如果用户点了标定按钮，强制取消快速模式
        if (fastModeToggle != null && fastModeToggle.isOn)
        {
            fastModeToggle.isOn = false;
            // 这会自动触发 OnFastModeChanged(false)，把注视点图片换回来
            Debug.Log("用户点击手动标定，自动关闭快速模式");
        }

        if (eyeTrackerController.ASeeTrackerStart() != 0)
        {
            StartCoroutine(ShowWarningForSeconds(WraningUI_1, 1.5f));
            return;
        }

        Debug.Log("进入校准流程");
        StartUI.SetActive(false);
        //SettingUI.SetActive(true);
        // 3. 开始校准，并传入“完成后回到 Start 界面”的逻辑
        eyeTrackerController.startCalibration(() =>
        {
            Debug.Log("收到校准完成回调，重置 UI");

            // ★★★ 修复关键点：校准完后，必须关闭眼动仪服务 ★★★
            // 否则回到Start界面后，下次点击Train再次调用Start()会报错导致 Warning_1
            if (eyeTrackerController != null)
            {
                eyeTrackerController.ASeeTrackerStop();
            }

            // 校准结束，重新显示 Start 界面
            if (StartUI != null) StartUI.SetActive(true);

            // 确保 SettingUI 是关闭的
            if (SettingUI != null) SettingUI.SetActive(false);
        });
    }

    private IEnumerator ShowWarningForSeconds(GameObject WarningUI, float seconds)
    {
        WarningUI.SetActive(true);
        yield return new WaitForSeconds(seconds);
        WarningUI.SetActive(false);
    }

    public void Set_WarningUI_1_Show()
    {
        WraningUI_1.SetActive(!WraningUI_1.activeSelf);
    }

    public void Set_WarningUI_2_Show()
    {
        WraningUI_2.SetActive(!WraningUI_2.activeSelf);
        eyeTrackerController.startCalibration();
    }

    public void Set_WarningUI_2_ShowCancle()
    {
        WraningUI_2.SetActive(!WraningUI_2.activeSelf);
    }

    // Train 按钮逻辑
    public void EnableSetting()
    {
        // 1. 检查EEG是否连接
        if (!isEEGConnected)
        {
            if (WraningUI_4) WraningUI_4.SetActive(true);
            return;
        }

        // 2. 检查眼动仪连接
        if (eyeTrackerController.ASeeTrackerStart() != 0)
        {
            StartCoroutine(ShowWarningForSeconds(WraningUI_1, 1.5f));
            return;
        }

        // ★★★ 判断 Toggle：如果勾选，直接跳过 Setting 界面，进入 Train ★★★
        if (fastModeToggle != null && fastModeToggle.isOn)
        {
            // 1. 清理一下旧状态（防止ReticleCalibrator残留）
            if (reticleCalibrator != null) reticleCalibrator.ResetState();

            // 2. ★ 核心修复：强制找到并显示十字，且位置归零 ★
            ForceCenterCross();

            // 3. 开始训练
            StartTrain();
            return;
        }

        // 3. 检查配置文件
        if (!eyeTrackerController.CheckCoeFile())
        {
            WraningUI_2.SetActive(true);
            return;
        }

        StartUI.SetActive(false);
        SettingUI.SetActive(true);
    }

    // ★★★ 新增辅助函数：强制归零并显示十字 ★★★
    private void ForceCenterCross()
    {
        GameObject canvas = GameObject.Find("Canvas");
        if (!canvas) return;

        // 1. 找到 UI 根节点
        Transform aseeUI = canvas.transform.Find("ASeeTracker_UI_Scipr(Clone)");
        if (!aseeUI) aseeUI = canvas.transform.Find("ASeeTracker_UI_Scipr");

        if (aseeUI)
        {
            // 2. 找到 GazePoint (圆球)
            Transform gazePoint = aseeUI.Find("GazePoint");
            if (gazePoint)
            {
                // 确保圆球本身是显示的（通常是显示的，但保险起见）
                gazePoint.gameObject.SetActive(true);

                // 3. 找到 targetCross (十字)
                // 注意：根据之前的代码，targetCross 是 GazePoint 的子物体
                Transform targetCross = gazePoint.Find("targetCross");

                // 如果找不到，尝试深度搜索（防止层级变动）
                if (!targetCross)
                {
                    foreach (Transform t in gazePoint) if (t.name == "targetCross") targetCross = t;
                }

                if (targetCross)
                {
                    // ★ 强制显示 ★
                    targetCross.gameObject.SetActive(true);

                    // ★ 强制位置归零 (居中) ★
                    RectTransform rt = targetCross.GetComponent<RectTransform>();
                    if (rt) rt.anchoredPosition = Vector2.zero;

                    Debug.Log("快速模式：十字已强制归位并显示");
                }
                else
                {
                    Debug.LogWarning("快速模式：未找到 targetCross，请检查 Prefab 层级！");
                }
            }
        }
    }

    public void StartTrain()
    {
        StartUI.SetActive(false);
        SettingUI.SetActive(false);
        TrainUI.SetActive(true);
        eyeTrackerController.startTrace();
    }

    // 返回 Start 界面
    public void BackToStart()
    {
        Debug.Log("返回主界面，正在彻底关闭眼动仪服务并重置状态...");
        // 1. 停止眼动仪 (这是关键)
        // 调用你在 EyeTrackerController.cs 里写好的 ASeeTrackerStop()
        // 这个方法会：if (is_tracing) stopTrace(); -> running=false -> _7i_stop() -> handle.Free()
        if (eyeTrackerController != null)
        {
            eyeTrackerController.ASeeTrackerStop();
        }

        ResetGazePointPosition();

        // 2. 重置 EEG 连接状态 (强制用户必须重新点连接)
        //isEEGConnected = false;
        isConnecting = false;

        // ★★★ 3. 重置点击偏移校验 (新增) ★★★
        if (reticleCalibrator != null)
        {
            reticleCalibrator.ResetState();
        }

        // 3. UI 切换：关闭游戏和设置，回到开始
        if (TrainUI != null) TrainUI.SetActive(false);
        if (SettingUI != null) SettingUI.SetActive(false);
        if (StartUI != null) StartUI.SetActive(true);

        // 4. 关闭所有残留的弹窗
        ResetAllWarnings();

        // 返回界面时，根据当前保存的状态刷新一下UI颜色（以防万一）
        UpdateEEGStatusUI(isEEGConnected);
    }

    // 返回时寻找并归位注视点 GazePoint 
    private void ResetGazePointPosition()
    {
        // 1. 找到 Canvas
        GameObject canvas = GameObject.Find("Canvas");
        if (!canvas) return;

        // 2. 找到 ASeeTracker UI (Clone 或 原名)
        Transform aseeUI = canvas.transform.Find("ASeeTracker_UI_Scipr(Clone)");
        if (!aseeUI) aseeUI = canvas.transform.Find("ASeeTracker_UI_Scipr");

        if (aseeUI)
        {
            // 3. 找到 GazePoint (小球)
            Transform gazePoint = aseeUI.Find("GazePoint");
            if (gazePoint)
            {
                RectTransform rt = gazePoint.GetComponent<RectTransform>();
                if (rt)
                {
                    // 强制归零 (屏幕中心)
                    rt.anchoredPosition = Vector2.zero;
                    Debug.Log("GazePoint 已强制归位到中心");
                }
            }
        }
    }

    private void ResetAllWarnings()
    {
        if (WraningUI_1) WraningUI_1.SetActive(false);
        if (WraningUI_2) WraningUI_2.SetActive(false);
        if (WraningUI_3) WraningUI_3.SetActive(false);
        if (WraningUI_4) WraningUI_4.SetActive(false);
        if (WraningUI_5) WraningUI_5.SetActive(false);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}