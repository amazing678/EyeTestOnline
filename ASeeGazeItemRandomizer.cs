using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking; // ★ 新增：用于发送 HTTP 请求
using System.Text;            // ★ 新增

public class ASeeGazeItemRandomizer : MonoBehaviour
{
    [Header("Web API Settings")]
    public string baseApiUrl = "http://127.0.0.1:9620/amd-eeg-eyes-tracking";

    [Header("Find Targets")]
    public string canvasName = "Canvas";
    public string aseeUiName = "ASeeTracker_UI_Scipr(Clone)";
    public string aseeUiNameAlt = "ASeeTracker_UI_Scipr";

    public string gazePointPath = "";
    public string targetCrossPath = "targetCross";

    [Header("Follow Switch")]
    public bool followMouse = true;

    [Header("Items (UI Images)")]
    public List<RectTransform> itemRects = new List<RectTransform>();
    public string itemsContainerName = "Items";

    [Header("Placement Area")]
    public RectTransform placementArea;
    public float padding = 20f;

    [Header("Dwell Trigger")]
    public float dwellTime = 2.0f;
    public float respawnDelay = 0.0f;

    [Header("Proximity Audio")]
    public AudioClip beepClip;
    public float maxBeepInterval = 1.0f;
    public float minBeepInterval = 0.02f;
    public float maxDistanceForSound = 800f;
    [Range(0, 1)] public float beepVolume = 1.0f;
    [Range(0, 1)] public float minBeepVolume = 0.1f;

    [Header("Debug Info (Read Only)")]
    [SerializeField] private float debugCurrentDistance;
    [SerializeField] private float debugCalculatedVolume;

    // ===== runtime refs =====
    private Canvas canvas;
    private Camera uiCam;
    private RectTransform canvasRect;

    private RectTransform aseeUI;
    private RectTransform gazePoint;
    private RectTransform targetCross;

    private RectTransform currentHoveredItem = null;
    private float hoverStartTime = 0f;
    private bool respawning = false;

    private AudioSource audioSource;
    private float lastBeepTime = 0f;

    void Start()
    {
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
        if (aseeUI == null || gazePoint == null || targetCross == null || canvasRect == null)
        {
            TryBindAll();
            if (gazePoint == null || targetCross == null || canvasRect == null) return;
        }

        if (followMouse)
        {
            Vector2 localInGazeParent;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    (RectTransform)gazePoint.parent, Input.mousePosition, uiCam, out localInGazeParent))
            {
                gazePoint.anchoredPosition = localInGazeParent;
            }
        }

        Vector2 targetCenterScreen = GetRectTransformCenterScreen(targetCross);

        HandleProximitySound(targetCenterScreen);

        RectTransform hovered = GetHoveredItem(targetCenterScreen);

        if (respawning)
        {
            currentHoveredItem = null;
            return;
        }

        if (hovered == null)
        {
            currentHoveredItem = null;
            hoverStartTime = 0f;
            return;
        }

        if (hovered != currentHoveredItem)
        {
            currentHoveredItem = hovered;
            hoverStartTime = Time.unscaledTime;
            return;
        }

        float t = Time.unscaledTime - hoverStartTime;
        if (t >= dwellTime)
        {
            StartCoroutine(RespawnAndRelocate(currentHoveredItem));
            currentHoveredItem = null;
            hoverStartTime = 0f;
        }
    }

    private void HandleProximitySound(Vector2 gazeScreenPos)
    {
        if (beepClip == null || respawning) return;

        RectTransform activeTarget = GetActiveTarget();
        if (activeTarget == null) return;

        Vector2 itemScreenPos = GetRectTransformCenterScreen(activeTarget);
        float distance = Vector2.Distance(gazeScreenPos, itemScreenPos);
        debugCurrentDistance = distance;

        float t = Mathf.Clamp01(distance / maxDistanceForSound);
        float currentInterval = Mathf.Lerp(minBeepInterval, maxBeepInterval, t);
        float volumeFactor = (1.0f - t) * (1.0f - t);
        float currentVolume = Mathf.Lerp(minBeepVolume, beepVolume, volumeFactor);

        debugCalculatedVolume = currentVolume;
        audioSource.volume = currentVolume;

        if (Time.time - lastBeepTime >= currentInterval)
        {
            audioSource.PlayOneShot(beepClip, 1f);
            lastBeepTime = Time.time;
        }
    }

    private RectTransform GetActiveTarget()
    {
        if (itemRects == null) return null;
        foreach (var item in itemRects)
        {
            if (item != null && item.gameObject.activeInHierarchy)
                return item;
        }
        return null;
    }

    private void TryBindAll()
    {
        GameObject canvasGO = GameObject.Find(canvasName);
        if (!canvasGO) return;

        canvas = canvasGO.GetComponent<Canvas>();
        canvasRect = canvasGO.GetComponent<RectTransform>();
        uiCam = GetUICamera(canvas);

        Transform aseeT = canvasGO.transform.Find(aseeUiName);
        if (!aseeT) aseeT = canvasGO.transform.Find(aseeUiNameAlt);
        if (!aseeT) return;
        aseeUI = aseeT.GetComponent<RectTransform>();

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

        if (!placementArea) placementArea = canvasRect;

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
        Vector3 worldCenter = rt.TransformPoint(rt.rect.center);
        return RectTransformUtility.WorldToScreenPoint(uiCam, worldCenter);
    }

    // =========================
    // ★ 核心修改：向 API 推送物品显隐事件
    // =========================
    private IEnumerator RespawnAndRelocate(RectTransform item)
    {
        if (!item || !placementArea) yield break;
        respawning = true;

        // 1) 通知服务器：物体消失
        StartCoroutine(PostObjectEvent("/eventApi/objDisappear", item.gameObject.name));

        item.gameObject.SetActive(false); // 消失

        if (respawnDelay > 0f)
            yield return new WaitForSecondsRealtime(respawnDelay);

        // 2) 随机换位置
        PlaceItemRandomly(item, placementArea);

        // 3) 出现
        item.gameObject.SetActive(true);

        // 通知服务器：物体出现
        StartCoroutine(PostObjectEvent("/eventApi/objAppear", item.gameObject.name));

        respawning = false;
    }

    // ★ 发送 HTTP POST 的协程
    private IEnumerator PostObjectEvent(string endpoint, string objName)
    {
        string url = baseApiUrl + endpoint;
        string jsonPayload = $"{{\"objName\": \"{objName}\"}}"; // 构造JSON {"objName": "物体名字"}

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[API 推送失败] URL: {url} \n错误: {request.error}");
            }
            else
            {
                Debug.Log($"[API 推送成功] URL: {url} \n参数: {jsonPayload} \n返回: {request.downloadHandler.text}");
            }
        }
    }

    private void PlaceItemRandomly(RectTransform item, RectTransform area)
    {
        RectTransform parent = item.parent as RectTransform;
        if (!parent) return;

        Rect ar = area.rect;

        float halfW = item.rect.width * 0.5f;
        float halfH = item.rect.height * 0.5f;

        float marginX = ar.width / 6.0f;
        float marginY = ar.height / 6.0f;

        float minX = ar.xMin + marginX + padding + halfW;
        float maxX = ar.xMax - marginX - padding - halfW;
        float minY = ar.yMin + marginY + padding + halfH;
        float maxY = ar.yMax - marginY - padding - halfH;

        if (maxX < minX) { float mid = (minX + maxX) * 0.5f; minX = maxX = mid; }
        if (maxY < minY) { float mid = (minY + maxY) * 0.5f; minY = maxY = mid; }

        float x = Random.Range(minX, maxX);
        float y = Random.Range(minY, maxY);

        Vector3 worldPos = area.TransformPoint(new Vector3(x, y, 0f));
        Vector3 localInParent = parent.InverseTransformPoint(worldPos);

        item.anchoredPosition = new Vector2(localInParent.x, localInParent.y);
    }
}
