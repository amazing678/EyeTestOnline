using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ButtonEvent : MonoBehaviour
{
    public static ButtonEvent Instance;

    [Header("界面引用 (UI References)")]
    public GameObject StartUI;
    public GameObject SettingUI;
    public GameObject TrainUI;

    [Header("警告弹窗 (Warnings)")]
    public GameObject WraningUI_3;
    public GameObject WraningUI_4;

    [Header("功能开关")]
    public Toggle fastModeToggle;
    public Toggle debugModeToggle;

    [Header("脑电状态指示灯")]
    public Image eegStatusImage;
    public Color connectedColor = Color.green;
    public Color disconnectedColor = Color.red;

    public float eegTimeoutSeconds = 3.0f;
    private float lastEEGDataTime = -999f;

    [HideInInspector]
    public bool isEEGConnected = false;

    void Awake() { if (Instance == null) Instance = this; }

    void Start()
    {
        if (fastModeToggle != null)
        {
            fastModeToggle.onValueChanged.AddListener(OnFastModeChanged);
            OnFastModeChanged(fastModeToggle.isOn);
        }

        if (debugModeToggle != null)
        {
            debugModeToggle.onValueChanged.AddListener(OnDebugModeChanged);
            OnDebugModeChanged(debugModeToggle.isOn);
        }

        UpdateEEGStatusUI(false);
        ResetAllWarnings();
    }

    void OnFastModeChanged(bool isOn)
    {
        if (EyeTrackerController_prefab.Instance != null)
            EyeTrackerController_prefab.Instance.SetGazePointSprite(isOn);
    }

    void OnDebugModeChanged(bool isOn)
    {
        if (EyeTrackerController_prefab.Instance != null)
            EyeTrackerController_prefab.Instance.isDebugMode = isOn;

        if (isOn && WraningUI_4 != null) WraningUI_4.SetActive(false);
    }

    private void UpdateEEGStatusUI(bool isConnected)
    {
        if (eegStatusImage != null) eegStatusImage.color = isConnected ? connectedColor : disconnectedColor;
    }

    public void OnReceiveEEGData()
    {
        lastEEGDataTime = Time.time;
        if (!isEEGConnected)
        {
            isEEGConnected = true;
            UpdateEEGStatusUI(true);
            if (WraningUI_4) WraningUI_4.SetActive(false);
        }
    }

    void Update()
    {
        if (isEEGConnected && Time.time - lastEEGDataTime > eegTimeoutSeconds)
        {
            isEEGConnected = false;
            UpdateEEGStatusUI(false);
        }
    }

    public void CloseWarningUI_3() { if (WraningUI_3) WraningUI_3.SetActive(false); }
    public void CloseWarningUI_4() { if (WraningUI_4) WraningUI_4.SetActive(false); }

    // ★ 补回缺失的辅助函数
    private void ResetAllWarnings()
    {
        if (WraningUI_3) WraningUI_3.SetActive(false);
        if (WraningUI_4) WraningUI_4.SetActive(false);
    }

    public void EnableSetting()
    {
        bool isDebug = debugModeToggle != null && debugModeToggle.isOn;

        // 强制向下同步一次模式，防止进入时错位
        if (EyeTrackerController_prefab.Instance != null)
            EyeTrackerController_prefab.Instance.isDebugMode = isDebug;

        if (!isEEGConnected && !isDebug)
        {
            if (WraningUI_4) WraningUI_4.SetActive(true);
            return;
        }

        if (StartUI != null) StartUI.SetActive(false);

        if (fastModeToggle != null && fastModeToggle.isOn)
        {
            ForceCenterCross();
            StartTrain();
        }
        else
        {
            if (SettingUI != null) SettingUI.SetActive(true);

            // 通知控制器显示并追踪圆环
            if (EyeTrackerController_prefab.Instance != null)
                EyeTrackerController_prefab.Instance.startTrace();
        }
    }

    private void ForceCenterCross()
    {
        if (EyeTrackerController_prefab.Instance != null && EyeTrackerController_prefab.Instance.gazeDot != null)
        {
            Transform tc = EyeTrackerController_prefab.Instance.gazeDot.transform.Find("targetCross");
            if (tc) tc.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
        }
    }

    public void StartTrain()
    {
        if (StartUI != null) StartUI.SetActive(false);
        if (SettingUI != null) SettingUI.SetActive(false);
        if (TrainUI != null) TrainUI.SetActive(true);

        // 通知控制器显示并追踪圆环
        if (EyeTrackerController_prefab.Instance != null)
        {
            EyeTrackerController_prefab.Instance.startTrace();
        }
    }

    public void BackToStart()
    {
        if (EyeTrackerController_prefab.Instance != null)
        {
            EyeTrackerController_prefab.Instance.stopTrace();
        }

        if (TrainUI != null) TrainUI.SetActive(false);
        if (SettingUI != null) SettingUI.SetActive(false);
        if (StartUI != null) StartUI.SetActive(true);

        ResetAllWarnings();
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.ExitPlaymode();
#else
        Application.Quit();
#endif
    }
}
