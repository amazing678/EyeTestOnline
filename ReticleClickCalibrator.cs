using UnityEngine;
using UnityEngine.UI;

public class ReticleClickCalibrator : MonoBehaviour
{
    [Header("UI References")]
    public Canvas canvas;                // 你的Canvas（建议拖上，方便取UI Camera）
    public RectTransform reticleRoot;    // 居中坐标根：local(0,0)=屏幕中心
    public RectTransform outerRing;      // 用它的“矩形范围”判定鼠标进入/移出
    public RectTransform targetCross;    // 点击后固定
    public RectTransform cursorCross;    // 仅在outerRing范围内显示

    //private
    // 你当前脚本所在的 Canvas（若是Overlay可不填，但建议填上）
    //public Canvas canvas;

    // ASeeTracker UI 根物体名（可能带 Clone）
    public string aseeUiName = "ASeeTracker_UI_Scipr(Clone)";
    // 如果场景里也可能是非Clone
    public string aseeUiNameAlt = "ASeeTracker_UI_Scipr";

    // ASeeTracker UI 内 targetCross 的路径（按你的层级改）
    // 例：ASeeTracker_UI_Scipr(Clone)/ReticleRoot/targetCross
    public string aseeTargetCrossPath = "targetCross"; // 最简单：直接子物体叫 targetCross
    public string aseeReticleRootPath = "GazePoint";     

    [Header("Options")]
    public bool hideSystemCursorWhenInside = true;

    // 点击偏移（相对中心0,0）
    public Vector2 savedOffsetLocal;
    public bool hasSavedPoint = false;

    public bool HasPoint => hasSavedPoint;
    public Vector2 LastOffset => savedOffsetLocal;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (cursorCross) cursorCross.gameObject.SetActive(false);
        if (targetCross) targetCross.gameObject.SetActive(false);
    }

    void Update()
    {
        Camera uiCam = GetUICamera();

        // 1) 是否在 outerRing 的矩形范围内（不按圆形算）
        bool insideOuterRect = RectTransformUtility.RectangleContainsScreenPoint(
            outerRing, Input.mousePosition, uiCam);

        // 2) 进入/移出时切换鼠标形态
        SetCursorMode(insideOuterRect);

        // 3) 更新 cursorCross 位置（只有在范围内才显示）
        if (insideOuterRect && cursorCross)
        {
            Vector2 localInRoot;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    reticleRoot, Input.mousePosition, uiCam, out localInRoot))
            {
                cursorCross.anchoredPosition = localInRoot;
            }
        }

        // 4) 在范围内点击：记录相对中心偏移 + 移动targetCross（覆盖上一次）
        if (insideOuterRect && Input.GetMouseButtonDown(0))
        {
            Vector2 localInRoot;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    reticleRoot, Input.mousePosition, uiCam, out localInRoot))
            {
                SavePoint(localInRoot);
            }
        }

        // 5) targetCross 始终显示在上次点击处
        if (hasSavedPoint && targetCross)
        {
            if (!targetCross.gameObject.activeSelf)
                targetCross.gameObject.SetActive(true);

            targetCross.anchoredPosition = savedOffsetLocal;
            
        }
    }

    // ★★★ 新增：重置状态函数 ★★★
    public void ResetState()
    {
        // 1. 清除标志位
        hasSavedPoint = false;
        savedOffsetLocal = Vector2.zero;

        // 2. 隐藏已经锁定的十字瞄准线（让用户知道需要重新点）
        if (targetCross)
            targetCross.gameObject.SetActive(false);

        // 3. 隐藏远程（ASeeTracker）十字，防止残留
        ResetRemoteTargetCross();

        Debug.Log("ReticleClickCalibrator 已重置，请重新设定偏移点");
    }

    private void SavePoint(Vector2 localInRoot)
    {
        savedOffsetLocal = localInRoot;  // 覆盖上一次
        hasSavedPoint = true;

        Debug.Log($"偏移量:{savedOffsetLocal}");
        if (targetCross)
        {
            targetCross.gameObject.SetActive(true);
            targetCross.anchoredPosition = savedOffsetLocal;
        }

        // 关键：同步到 ASeeTracker_UI_Scipr 下的 targetCross
        SyncToASeeTrackerUI(savedOffsetLocal);
    }

    private void SetCursorMode(bool insideOuter)
    {
        if (cursorCross)
            cursorCross.gameObject.SetActive(insideOuter);

        if (hideSystemCursorWhenInside)
            Cursor.visible = !insideOuter;
        else
            Cursor.visible = true;
    }

    // ★ 新增：重置远程十字 ★
    private void ResetRemoteTargetCross()
    {
        // 尝试找到远程 UI 并隐藏它的 targetCross
        RectTransform remoteTarget = FindRemoteTargetCross();
        if (remoteTarget != null)
        {
            remoteTarget.gameObject.SetActive(false);
        }
    }

    private RectTransform FindRemoteTargetCross()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        if (!canvasGO) return null;

        Transform aseeUI = canvasGO.transform.Find(aseeUiName);
        if (!aseeUI) aseeUI = canvasGO.transform.Find(aseeUiNameAlt);
        if (!aseeUI) return null;

        Transform targetT = string.IsNullOrEmpty(aseeTargetCrossPath) ? null : aseeUI.Find(aseeTargetCrossPath);
        if (!targetT) targetT = FindDeepChild(aseeUI, "targetCross");

        return targetT ? targetT.GetComponent<RectTransform>() : null;
    }

    private void SyncToASeeTrackerUI(Vector2 savedOffsetLocalInMyRoot)
    {
        // 1) 找 Canvas
        GameObject canvasGO = GameObject.Find("Canvas");
        if (!canvasGO)
        {
            Debug.LogWarning("未找到 Canvas");
            return;
        }

        // 2) 找 ASeeTracker_UI_Scipr(Clone) 或 ASeeTracker_UI_Scipr
        Transform aseeUI = canvasGO.transform.Find(aseeUiName);
        if (!aseeUI) aseeUI = canvasGO.transform.Find(aseeUiNameAlt);
        if (!aseeUI)
        {
            Debug.LogWarning("未找到 ASeeTracker_UI_Scipr(Clone) 或 ASeeTracker_UI_Scipr");
            return;
        }

        // 3) 找它下面的 targetCross
        Transform targetT = string.IsNullOrEmpty(aseeTargetCrossPath) ? null : aseeUI.Find(aseeTargetCrossPath);
        if (!targetT)
        {
            // 兜底：在子层级里全局搜
            targetT = FindDeepChild(aseeUI, "targetCross");
        }
        if (!targetT)
        {
            Debug.LogWarning("未找到 ASeeTracker_UI_Scipr 下的 targetCross（请检查路径/命名）");
            return;
        }

        RectTransform targetRT = targetT.GetComponent<RectTransform>();
        if (!targetRT)
        {
            Debug.LogWarning("targetCross 没有 RectTransform");
            return;
        }

        // 4) 选择对方的“坐标根”：通常应该是对方自己的ReticleRoot（中心为0,0）
        RectTransform otherRoot = null;
        if (!string.IsNullOrEmpty(aseeReticleRootPath))
        {
            Transform otherRootT = aseeUI.Find(aseeReticleRootPath);
            if (otherRootT) otherRoot = otherRootT.GetComponent<RectTransform>();
        }
        if (!otherRoot)
        {
            // 没提供就默认用 ASeeTracker_UI_Scipr 的RectTransform作为根
            otherRoot = aseeUI.GetComponent<RectTransform>();
        }

        if (!otherRoot)
        {
            Debug.LogWarning("ASeeTracker_UI_Scipr 没有 RectTransform 作为坐标根");
            return;
        }

        // 5) 把“我这边的 local offset”转成屏幕坐标
        //    注意：savedOffsetLocalInMyRoot 是在 reticleRoot 坐标系下的 anchoredPosition
        Camera uiCam = GetUICamera();
        Vector3 myWorld = reticleRoot.TransformPoint(savedOffsetLocalInMyRoot);
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCam, myWorld);

        // 6) 再把屏幕坐标转成对方 root 的 local，并移动对方 targetCross
        Vector2 otherLocal;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(otherRoot, screenPos, uiCam, out otherLocal))
        {
            Debug.LogWarning("无法将屏幕坐标转换到 ASeeTracker 的坐标根");
            return;
        }

        targetRT.gameObject.SetActive(true);
        targetRT.anchoredPosition = otherLocal;
    }



    private Camera GetUICamera()
    {
        // Screen Space - Overlay：传null
        // Screen Space - Camera / World Space：用canvas.worldCamera
        if (canvas == null) return null;
        return (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
    }

    // ====== 递归找子物体（兜底用）======
    private Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found) return found;
        }
        return null;
    }
}
