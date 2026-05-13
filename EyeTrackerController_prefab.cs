using UnityEngine;
using UnityEngine.UI;

public class EyeTrackerController_prefab : MonoBehaviour
{
    // ★ 核心修复1：强制全局单例查找（即使该物体默认是被隐藏的，也能找到）
    private static EyeTrackerController_prefab _instance;
    public static EyeTrackerController_prefab Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<EyeTrackerController_prefab>(true);
            }
            return _instance;
        }
    }

    [Header("UI 绑定 (若为空会自动寻找)")]
    public Image gazeDot;
    public Sprite normalSprite;
    public Sprite adhdSprite;

    [Header("网络状态 (只读)")]
    public bool is_tracing = false;
    public Vector2 networkGazeScreen = Vector2.zero;

    [Header("调试模式")]
    public bool isDebugMode = false;

    void Awake()
    {
        _instance = this;

        // ★ 核心修复2：防呆设计。如果面板里忘了拖拽 gazeDot，代码自动去找
        if (gazeDot == null)
        {
            Transform t = transform.Find("gazePoint");
            if (t == null) t = transform.Find("GazePoint");
            if (t != null) gazeDot = t.GetComponent<Image>();
        }
    }

    public void UpdateNetworkGaze(float x, float y)
    {
        networkGazeScreen = new Vector2(x, y);
    }

    public void startTrace()
    {
        is_tracing = true;
        if (gazeDot != null)
        {
            gazeDot.gameObject.SetActive(true);
            Debug.Log("✅ [EyeTracker] 追踪已开启，圆球已显示！");
        }
        else
        {
            Debug.LogError("❌ [EyeTracker] 严重错误：场景中找不到 gazeDot (圆球)！请检查层级名字。");
        }
    }

    public void stopTrace()
    {
        is_tracing = false;
        if (gazeDot != null)
        {
            gazeDot.gameObject.SetActive(false); // 1. 隐藏圆环
            gazeDot.rectTransform.anchoredPosition = Vector2.zero; // 2. 圆球位置归零

            // 3. 寻找里面的十字，也强制归零
            Transform tc = gazeDot.transform.Find("targetCross");
            if (tc) tc.GetComponent<RectTransform>().anchoredPosition = Vector2.zero;

            Debug.Log("✅ [EyeTracker] 追踪已停止，圆球已归零并隐藏！");
        }
    }

    public void SetGazePointSprite(bool useNew)
    {
        if (gazeDot == null) return;
        if (normalSprite && adhdSprite) gazeDot.sprite = useNew ? adhdSprite : normalSprite;
    }

    void LateUpdate()
    {
        // 只有在 is_tracing 为 true 且 gazeDot 存在时，才进行坐标更新
        if (is_tracing && gazeDot != null)
        {
            RectTransform rt = gazeDot.rectTransform;
            Canvas canvas = gazeDot.canvas;
            if (canvas == null) canvas = gazeDot.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

            Vector2 screenPos;

            // 根据调试模式决定坐标来源
            if (isDebugMode)
            {
                screenPos = Input.mousePosition;
            }
            else
            {
                screenPos = new Vector2(networkGazeScreen.x, Screen.height - networkGazeScreen.y);
            }

            Vector2 localPos;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)rt.parent, screenPos, uiCam, out localPos))
            {
                rt.anchoredPosition = localPos;
            }
        }
    }
}
