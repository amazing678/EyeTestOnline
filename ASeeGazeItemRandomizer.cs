using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ASeeGazeItemRandomizer : MonoBehaviour
{
    [Header("Find Targets")]
    public string canvasName = "Canvas";
    public string aseeUiName = "ASeeTracker_UI_Scipr(Clone)";
    public string aseeUiNameAlt = "ASeeTracker_UI_Scipr"; // 兜底

    [Tooltip("ASeeTracker_UI_Scipr(Clone) 下 gazePoint 的路径，例如：ReticleRoot/gazePoint；留空则深度搜索名字 gazePoint")]
    public string gazePointPath = "";

    [Tooltip("gazePoint 下 targetCross 的路径，例如：targetCross；留空则深度搜索名字 targetCross")]
    public string targetCrossPath = "targetCross";

    [Header("Follow Switch")]
    [Tooltip("开启：gazePoint 跟随鼠标；关闭：由外部(眼动)驱动 gazePoint，本脚本只做检测")]
    public bool followMouse = true;

    [Header("Items (UI Images)")]
    [Tooltip("要随机移动的物品RectTransform列表。若为空，可用 itemsContainerName 自动收集。")]
    public List<RectTransform> itemRects = new List<RectTransform>();

    [Tooltip("如果 itemRects 为空，则在 Canvas 下找这个容器并收集其子物体 RectTransform 作为物品")]
    public string itemsContainerName = "Items";

    [Header("Placement Area")]
    [Tooltip("物品随机摆放的参考区域（通常就是物品父容器）。不填则默认用Canvas RectTransform。")]
    public RectTransform placementArea;

    [Tooltip("边缘留白（像素），避免物品出界")]
    public float padding = 20f;

    [Header("Dwell Trigger")]
    [Tooltip("停留超过该时间（秒）触发：物品消失并随机换位再出现")]
    public float dwellTime = 2.0f;

    [Tooltip("触发后物品消失多久再出现（秒），0表示立即换位出现")]
    public float respawnDelay = 0.0f;

    [Header("Proximity Audio (新功能)")]
    [Tooltip("提示音效（滴）")]
    public AudioClip beepClip;

    [Tooltip("最慢频率（秒）：距离很远时，多久响一次")]
    public float maxBeepInterval = 1.0f; // 慢

    [Tooltip("最快频率（秒）：距离非常近时，多久响一次")]
    public float minBeepInterval = 0.02f; // 快

    [Tooltip("最大感应距离（像素）：超过这个距离保持最慢频率，小于这个距离开始变快")]
    public float maxDistanceForSound = 800f;

    [Tooltip("最大音量")]
    [Range(0, 1)] public float beepVolume = 1.0f;

    [Tooltip("最小音量")]
    [Range(0, 1)] public float minBeepVolume = 0.1f;

    [Header("Debug Info (Read Only)")]
    [SerializeField] private float debugCurrentDistance; // 当前距离
    [SerializeField] private float debugCalculatedVolume; // 当前计算出的音量
    [SerializeField] private float debugCalculatedInterval;//显示声音频率


    // ===== runtime refs =====
    private Canvas canvas;
    private Camera uiCam;
    private RectTransform canvasRect;

    private RectTransform aseeUI;
    private RectTransform gazePoint;
    private RectTransform targetCross;

    // dwell tracking
    private RectTransform currentHoveredItem = null;
    private float hoverStartTime = 0f;
    private bool respawning = false;

    // Audio
    private AudioSource audioSource;
    private float lastBeepTime = 0f;

    void Start()
    {
        //防止没有AUDIO组件导致报错
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        TryBindAll();
    }

    void Update()
    {
        // 容错：ASee UI 可能晚生成
        if (aseeUI == null || gazePoint == null || targetCross == null || canvasRect == null)
        {
            TryBindAll();
            if (gazePoint == null || targetCross == null || canvasRect == null) return;
        }

        // 1) 可选：gazePoint 跟随鼠标（关闭则不改动 gazePoint，外部可用眼动驱动它）
        if (followMouse)
        {
            Vector2 localInGazeParent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)gazePoint.parent, Input.mousePosition, uiCam, out localInGazeParent))
            {
                gazePoint.anchoredPosition = localInGazeParent;
            }
        }

        // 2) 计算 targetCross “中心点”的屏幕坐标（不依赖pivot是否中心）
        Vector2 targetCenterScreen = GetRectTransformCenterScreen(targetCross);

        // 3) 处理声音逻辑 (Proximity Beep)
        HandleProximitySound(targetCenterScreen);

        // 4) 找当前命中的物品（矩形命中）
        RectTransform hovered = GetHoveredItem(targetCenterScreen);

        // 5) 停留计时逻辑
        if (respawning)
        {
            // 正在重生时，不计时不触发
            currentHoveredItem = null;
            return;
        }

        if (hovered == null)
        {
            // 离开所有物品：清空计时
            currentHoveredItem = null;
            hoverStartTime = 0f;
            return;
        }

        if (hovered != currentHoveredItem)
        {
            // 切换到新物品：重新计时
            currentHoveredItem = hovered;
            hoverStartTime = Time.unscaledTime;
            return;
        }

        // 同一个物品上持续停留
        float t = Time.unscaledTime - hoverStartTime;
        if (t >= dwellTime)
        {
            // 触发：该物品消失 -> 随机换位 -> 再出现
            StartCoroutine(RespawnAndRelocate(currentHoveredItem));
            currentHoveredItem = null;
            hoverStartTime = 0f;
        }
    }

    // =========================
    // Audio Logic
    // =========================
    private void HandleProximitySound(Vector2 gazeScreenPos)
    {
        if (beepClip == null || respawning) return;

        // 找到当前激活的目标（草莓）
        RectTransform activeTarget = GetActiveTarget();

        if (activeTarget == null) return;

        // 获取目标的屏幕坐标
        Vector2 itemScreenPos = GetRectTransformCenterScreen(activeTarget);

        // 计算距离
        float distance = Vector2.Distance(gazeScreenPos, itemScreenPos);
        debugCurrentDistance = distance;

        // 根据距离计算播放间隔 (Lerp)
        // 距离越大 t 越接近 1，间隔越大(慢)
        // 距离越小 t 越接近 0，间隔越小(快)
        float t = Mathf.Clamp01(distance / maxDistanceForSound);
        //频率
        float currentInterval = Mathf.Lerp(minBeepInterval, maxBeepInterval, t);
        //音量
        float volumeFactor = (1.0f - t) * (1.0f - t);
        float currentVolume = Mathf.Lerp(minBeepVolume, beepVolume, volumeFactor);

        // 将计算结果显示在面板上
        debugCalculatedVolume = currentVolume;

        // ★关键修改★：直接设置 AudioSource 的音量属性，这样你在 Inspector 面板能看到滑条在动
        audioSource.volume = currentVolume;

        // 播放逻辑
        if (Time.time - lastBeepTime >= currentInterval)
        {
            audioSource.PlayOneShot(beepClip, 1f);
            lastBeepTime = Time.time;
        }
    }

    private RectTransform GetActiveTarget()
    {
        if (itemRects == null) return null;
        // 返回第一个处于激活状态的 Item
        foreach (var item in itemRects)
        {
            if (item != null && item.gameObject.activeInHierarchy)
                return item;
        }
        return null;
    }


    // =========================
    // Bind / Find
    // =========================
    private void TryBindAll()
    {
        GameObject canvasGO = GameObject.Find(canvasName);
        if (!canvasGO) return;

        canvas = canvasGO.GetComponent<Canvas>();
        canvasRect = canvasGO.GetComponent<RectTransform>();
        uiCam = GetUICamera(canvas);

        // 找 ASee UI
        Transform aseeT = canvasGO.transform.Find(aseeUiName);
        if (!aseeT) aseeT = canvasGO.transform.Find(aseeUiNameAlt);
        if (!aseeT) return;
        aseeUI = aseeT.GetComponent<RectTransform>();

        // 找 gazePoint
        gazePoint = null;
        if (!string.IsNullOrEmpty(gazePointPath))
        {
            Transform gp = aseeT.Find(gazePointPath);
            if (gp) gazePoint = gp.GetComponent<RectTransform>();
        }
        if (!gazePoint)
        {
            Transform gp = FindDeepChild(aseeT, "gazePoint");
            if (gp) gazePoint = gp.GetComponent<RectTransform>();
        }

        // 找 targetCross（在 gazePoint 下）
        targetCross = null;
        if (gazePoint)
        {
            gazePoint.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(targetCrossPath))
            {
                Transform tc = gazePoint.Find(targetCrossPath);
                if (tc) targetCross = tc.GetComponent<RectTransform>();
            }
            if (!targetCross)
            {
                Transform tc = FindDeepChild(gazePoint, "targetCross");
                if (tc) targetCross = tc.GetComponent<RectTransform>();
            }
        }

        // placementArea
        if (!placementArea) placementArea = canvasRect;

        // items auto collect
        if (itemRects == null) itemRects = new List<RectTransform>();
        if (itemRects.Count == 0)
        {
            Transform itemsContainer = canvasGO.transform.Find(itemsContainerName);
            if (itemsContainer)
            {
                CollectChildRects(itemsContainer, itemRects);
            }
        }
    }

    private static Camera GetUICamera(Canvas c)
    {
        if (c == null) return null;
        return (c.renderMode == RenderMode.ScreenSpaceOverlay) ? null : c.worldCamera;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found) return found;
        }
        return null;
    }

    private static void CollectChildRects(Transform parent, List<RectTransform> outList)
    {
        outList.Clear();
        foreach (Transform child in parent)
        {
            RectTransform rt = child.GetComponent<RectTransform>();
            if (rt) outList.Add(rt);
        }
    }

    // =========================
    // Hover / Hit
    // =========================
    private RectTransform GetHoveredItem(Vector2 screenPoint)
    {
        if (itemRects == null || itemRects.Count == 0) return null;

        for (int i = 0; i < itemRects.Count; i++)
        {
            RectTransform item = itemRects[i];
            if (!item || !item.gameObject.activeInHierarchy) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(item, screenPoint, uiCam))
                return item;
        }
        return null;
    }

    private Vector2 GetRectTransformCenterScreen(RectTransform rt)
    {
        // 用 rect.center 获取“矩形中心点”，再 TransformPoint 到世界坐标
        Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
        return RectTransformUtility.WorldToScreenPoint(uiCam, worldCenter);
    }

    // =========================
    // Respawn & Relocate
    // =========================
    private IEnumerator RespawnAndRelocate(RectTransform item)
    {
        if (!item || !placementArea) yield break;
        respawning = true;

        // 1) 消失
        item.gameObject.SetActive(false);

        if (respawnDelay > 0f)
            yield return new WaitForSecondsRealtime(respawnDelay);

        // 2) 随机换位置（把随机点定义在 placementArea 内）
        PlaceItemRandomly(item, placementArea);

        // 3) 出现
        item.gameObject.SetActive(true);

        respawning = false;
    }

    private void PlaceItemRandomly(RectTransform item, RectTransform area)
    {
        RectTransform parent = item.parent as RectTransform;
        if (!parent) return;

        // area 局部空间下的可用范围
        Rect ar = area.rect;

        float halfW = item.rect.width * 0.5f;
        float halfH = item.rect.height * 0.5f;

        // 计算屏幕/区域宽高的 1/6 作为强制留白
        float marginX = ar.width / 6.0f;
        float marginY = ar.height / 6.0f;

        // minX = 左边界 + 1/6宽 + padding + 物品半宽
        float minX = ar.xMin + marginX + padding + halfW;
        float maxX = ar.xMax - marginX - padding - halfW;
        float minY = ar.yMin + marginY + padding + halfH;
        float maxY = ar.yMax - marginY - padding - halfH;

        if (maxX < minX) { float mid = (minX + maxX) * 0.5f; minX = maxX = mid; }
        if (maxY < minY) { float mid = (minY + maxY) * 0.5f; minY = maxY = mid; }

        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);

        // 随机点先在 area 的局部坐标
        Vector3 worldPos = area.TransformPoint(new Vector3(x, y, 0f));
        Vector3 localInParent = parent.InverseTransformPoint(worldPos);

        item.anchoredPosition = new Vector2(localInParent.x, localInParent.y);
    }

    // =========================
    // 外部接口：当 followMouse=false 时，可由眼动驱动 gazePoint
    // =========================
    public void SetGazePointFromScreen(Vector2 screenPx)
    {
        if (!gazePoint) return;
        Vector2 localInParent;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)gazePoint.parent, screenPx, uiCam, out localInParent))
        {
            gazePoint.anchoredPosition = localInParent;
        }
    }

    public void SetGazePointFromNormalized(Vector2 gaze01)
    {
        Vector2 px = new Vector2(gaze01.x * Screen.width, gaze01.y * Screen.height);
        SetGazePointFromScreen(px);
    }
}
