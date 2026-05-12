
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using EyeTracker;
using System.IO;
using System.Collections;
using UnityEngine.SceneManagement;


#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif



public class EyeTrackerController_prefab : MonoBehaviour
{
    //获取Unity窗口针对屏幕的偏移API
    #if UNITY_STANDALONE_WIN
        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    #endif


    public GameObject eyeTrackerPrefab;     // 拖入 EyeTracker 预制体
    //private Canvas canvas;                    // 拖入 Canvas

    [Header("绑定结果")]
    private Image UIBackground; // 注视点的 UI Image
    private List<Image> ballImages = new List<Image>(5); // 小球列表，按顺序从左上到右下排列
    private Image gazeDot; // 注视点的 UI Image
    private Image ballImage_move; // 当前正在移动的注视点小球（用于动画效果）

    public float moveDuration = 1f; // 移动时间（秒）
    //平滑移动协程
    private Coroutine moveCoroutine;

    //预制体实例
    public static EyeTrackerController_prefab Instance;

    private bool uiReady = false;         // UI 是否准备好
    private bool pendingStartTrace = false; // 如果切换场景时调用了 startTrace，需要延迟处理
    public _7i_coefficient_t pendingCoefficient = new _7i_coefficient_t();  //全局校准参数


    //保存的校准文件
    readonly string coe_file_name = @".\coefficient.dat";

    //校准点
    const int max_points = 5;

    // 当前正在校准的点索引（0~8）
    int cur_index = 4;

    //SDK状态参数
    private static bool is_init = false;
    private GCHandle handle;


    // 是否正在校准中
    bool is_calibrating = false;

    // 是否正在追踪中
    public bool is_tracing = false;

    // 是否全部点都校准完
    //bool is_all_points_finished = false;

    // 当前白色圆缩放系数（用于进度动画）
    private float scale = 1.0f;

    // SDK 校准进度回调函数
    public static ASeeTracker.processCallback processCB = new ASeeTracker.processCallback(process_callback);

    // SDK 校准完成回调函数（单个点）
    public static ASeeTracker.finishCallback finishCB = new ASeeTracker.finishCallback(finish_callback);
    private bool pointFinished = false;

    //UI注视球更新队列
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    //需要重新校准
    public static bool is_needrecalibrate  = false;

    //缩放大小
    private static float scaleFactor = 0.2f; // 用于缩放 UI 元素的系数

    //注视点
    public static float gaze_x = 0.0f;
    public static float gaze_y = 0.0f;

    // 注视点状态跟踪器
    private class GazeState
    {
        public Vector2 lastScreenPos;
        public Vector2 currentVelocity;
        public float lastUpdateTime;
        public bool isMoving;
    }

    private GazeState gazeState = new GazeState();


    // Start is called before the first frame update
    void Start()
    {

    }


    // Update is called once per frame
    void Update()
    {
        while (mainThreadActions.Count > 0)
        {
            var action = mainThreadActions.Dequeue();
            action?.Invoke();
        }
    }

    //private void Awake()
    //{
    //    DontDestroyOnLoad(this.gameObject);
    //    SceneManager.sceneLoaded += OnSceneLoaded;
    //}
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);  // 销毁多余的副本
            return;
        }

        Instance = this;  // 设置为全局唯一实例
        DontDestroyOnLoad(this.gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }



    void OnDestroy()
    {
        //is_init = false;
        //ASeeTracker._7i_stop();
        //if (handle.IsAllocated) handle.Free();

        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnApplicationQuit()
    {
        UnityEngine.Debug.Log("Play模式退出时触发（或构建时应用关闭）");
        ASeeTrackerStop();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 场景加载完后再尝试附着
        TryAttachToCanvasAndInitUI();
    }

    void TryAttachToCanvasAndInitUI()
    {
        GameObject canvasGO = GameObject.Find("Canvas");
        Canvas canvas = canvasGO?.GetComponent<Canvas>();
        if (canvas == null)
        {
            UnityEngine.Debug.Log("未找到 Canvas");
            return;
        }

        GameObject instance = Instantiate(eyeTrackerPrefab, canvas.transform);

        RectTransform parentRT = instance.GetComponent<RectTransform>();
        SetStretchFull(parentRT);

        // 绑定 UIBackground
        UIBackground = instance.transform.Find("background")?.GetComponent<Image>();
        if (UIBackground == null) UnityEngine.Debug.Log("未找到 UIBackground");

        SetStretchFull(UIBackground.rectTransform);

        // 绑定 ball_1 ~ ball_5
        ballImages.Clear();
        for (int i = 1; i <= 5; i++)
        {
            string name = $"ball_{i}";
            Transform ballTransform = instance.transform.Find(name);
            if (ballTransform != null)
            {
                Image ball_image = ballTransform.GetComponent<Image>();
                switch (i)
                {
                    case 1:
                        SetAnchorCenterScaled(ball_image.rectTransform, true);
                        break;
                    case 2:
                        SetAnchorTopLeftScaled(ball_image.rectTransform, new Vector2(50f, 50f));
                        break;
                    case 3:
                        SetAnchorTopRight(ball_image.rectTransform, new Vector2(50f, 50f));
                        break;
                    case 4:
                        SetAnchorBottomLeft(ball_image.rectTransform, new Vector2(50f, 50f));
                        break;
                    case 5:
                        SetAnchorBottomRight(ball_image.rectTransform, new Vector2(50f, 50f));
                        break;

                    default:
                        break;
                }


                ballImages.Add(ball_image);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"未找到 {name}");
            }
        }

        // 绑定 gazeDot
        gazeDot = instance.transform.Find("GazePoint")?.GetComponent<Image>();
        if (gazeDot == null) UnityEngine.Debug.Log("未找到 gazeDot");
        SetAnchorCenterScaled(gazeDot.rectTransform, false);

        //绑定移动的小球
        ballImage_move = instance.transform.Find("ball_move")?.GetComponent<Image>();
        if (gazeDot == null) UnityEngine.Debug.Log("未找到 ball_move");
        SetAnchorCenterScaled(ballImage_move.rectTransform, true);

        // UI 就绪
        uiReady = (UIBackground != null && ballImages.Count == 5 && gazeDot != null);

        if (is_tracing && gazeDot != null)
        {
            gazeDot.gameObject.SetActive(true);
        }

        UnityEngine.Debug.Log($"[UI Ready] = {uiReady}");

        // 若切场景前设置了 pendingStartTrace，则在 UI Ready 后重新开始
        if (pendingStartTrace)
        {
            pendingStartTrace = false;
            StartCoroutine(StartTraceWhenUIReady(pendingCoefficient));
        }
    }

    public int ASeeTrackerStart()
    {
        handle = GCHandle.Alloc(this);
        ASeeTracker._7i_set_image_callback(
            Marshal.GetFunctionPointerForDelegate(new ASeeTracker.imageCallback(ImageCallback)),
            (IntPtr)handle);

        ASeeTracker._7i_set_gaze_callback(
            Marshal.GetFunctionPointerForDelegate(new ASeeTracker.gazeCallback(GazeCallback)),
            (IntPtr)handle);

        //设置屏幕尺寸和平滑的
        // 假设是 1920x1080 像素屏幕
        double screenWidth = Screen.width;
        double screenHeight = Screen.height;

        int screenResult = ASeeTracker._7i_set_screen_size(screenWidth, screenHeight);
        Debug.Log($"设置屏幕尺寸返回值: {screenResult}");

        // 设置平滑等级（支持 1~10，越大越平滑但越慢）
        int smoothLevel = 4;
        int smoothResult = ASeeTracker._7i_set_smooth(smoothLevel);
        Debug.Log($"设置平滑返回值: {smoothResult}");

        int ret = ASeeTracker._7i_start("./config");
        UnityEngine.Debug.Log("SDK Start Result: " + ret);

        if (ret == 0)
            is_init = true;

        



        return ret;
    }

    public void ASeeTrackerStop()
    {
        if (is_tracing)
            stopTrace();

        is_init = false;
        ASeeTracker._7i_stop();
        if (handle.IsAllocated) handle.Free();
    }


    public static void ImageCallback(int eye, IntPtr image, int size, int width, int height, long timestamp, IntPtr context)
    {
        if (!is_init || context == IntPtr.Zero) return;

        // 实际处理图像：可使用 Texture2D.LoadRawTextureData() 绑定到 cameraDisplay
        UnityEngine.Debug.Log($"[Image] Eye:{eye}, Size:{size}, W:{width}, H:{height}");
    }


    //左眼注视点坐标是否有效，0为无效，1为有效
    public static int _is_valid_left_eye_gaze_point(ref _7i_eye_data_ex_t eyes)
    {
        return (int)_get_valid_value((byte)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT, eyes.left_gaze.ex_data_bit_mask);
    }

    //右眼注视点坐标是否有效，0为无效，1为有效
    public static int _is_valid_right_eye_gaze_point(ref _7i_eye_data_ex_t eyes)
    {
        return (int)_get_valid_value((byte)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT, eyes.right_gaze.ex_data_bit_mask);
    }

    //推荐眼注视点坐标是否有效
    public static int _is_valid_recom_eye_gaze_point(ref _7i_eye_data_ex_t eyes)
    {
        return (int)_get_valid_value((byte)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT, eyes.recom_gaze.gaze_bit_mask);
    }

    public static UInt32 _get_valid_value(byte position, UInt32 bits)
    {
        UInt32 the_mask = (((UInt32)1) << position);
        return (the_mask &= bits) >> position;
    }

    public static void GazeCallback(ref _7i_eye_data_ex_t eyes, IntPtr context)
    {
      

        var tracker = (EyeTrackerController_prefab)((GCHandle)context).Target;
        if (tracker == null || !is_init) return;

        var gaze = eyes.recom_gaze;

        // 检查 gaze 坐标是否有效
        //if ((gaze.gaze_bit_mask & (1 << (int)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT)) == 0)
        //{
        //    Debug.LogWarning("Gaze point is not valid, skipping update.");
        //    return;
        //} 

        if (1 == _is_valid_recom_eye_gaze_point(ref eyes))
        {

            gaze_x = eyes.recom_gaze.gaze_point.x;
            gaze_y = eyes.recom_gaze.gaze_point.y;
        }
        else
        {
            Debug.LogWarning("Use the last one");
            // Use the last one
        }

        if (float.IsNaN(gaze_x) || float.IsNaN(gaze_y))
        {
            Debug.LogWarning("Gaze point is not valid, skipping update.");
            return;
        }

        float x = gaze_x; // 归一化 [0,1]
        float y = gaze_y;

        // 检查是否为 NaN
        //if (float.IsNaN(x) || float.IsNaN(y))
        //{
        //    UnityEngine.Debug.Log($"Gaze is NaN! x:{x}, y:{y}");
        //    is_needrecalibrate = true;
        //    return;
        //}
        //is_needrecalibrate = false;
        //UnityEngine.Debug.Log($"[Gaze] x:{x}, y:{y}");

        Vector2 GazePoint_Get = new Vector2(x, y);
        //SetUIImageByNormalizedPos(tracker.gazeDot, GazePoint_Get);
        if (tracker.gazeDot == null)
        {
            UnityEngine.Debug.Log($"gazeDot 为空");
            //return;
        }

        if (tracker.is_tracing)
        {
            tracker.mainThreadActions.Enqueue(() =>
            {

                SetUIImageByNormalizedPos(tracker.gazeDot, GazePoint_Get);
            });
        }

       


    }

    // SDK 校准进度回调函数
    public static void process_callback(int index, int percent, IntPtr context)
    {
        var tracker = (EyeTrackerController_prefab)((GCHandle)context).Target;
        if (tracker == null || !is_init) return;
        UnityEngine.Debug.Log($"process: {index},{percent}");

        tracker.EnqueueScale(tracker.ballImages[index - 1], percent);
    }

    public static void finish_callback(int index, int error, IntPtr context)
    {
        UnityEngine.Debug.Log($"finish: {index}, {error}");

        var tracker = (EyeTrackerController_prefab)((GCHandle)context).Target;
        if (tracker != null)
        {
            tracker.pointFinished = true;
        }
    }

    public void startCalibration()
    {
        foreach (Image img in ballImages)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false); ; // 设置 alpha 为 0
                img.rectTransform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }

        }

        if (!is_init)
        {
            UnityEngine.Debug.Log($"startCalibration 未初始化");
            return;
        }

        if (is_calibrating)
        {
            UnityEngine.Debug.Log("校准已在进行中，忽略重复点击");
            return;
        }

        if (is_tracing)
        {
            UnityEngine.Debug.Log($"关闭追踪");
            stopTrace();
        }
       

        UIBackground.gameObject.SetActive(true); // 设置 alpha 为 0
        ballImages[0].gameObject.SetActive(true);

        StartCoroutine(CalibrationCoroutine());
    }


    //校准协程
    private IEnumerator CalibrationCoroutine()
    {
        yield return new WaitForSeconds(1.5f);

        _7i_coefficient_t coefficient = new _7i_coefficient_t();

        if (is_calibrating)
        {
            UnityEngine.Debug.Log($"Calibration already in progress or finished.is_calibrating:{is_calibrating}");
            yield break;
        }

        is_calibrating = true;
        //is_all_points_finished = false;

        int ret = ASeeTracker._7i_start_calibration(max_points);
        if (ret != 0)
        {
            UnityEngine.Debug.Log($"Failed to start calibration: {ret}");
            is_calibrating = false;
            yield break;
        }

        UnityEngine.Debug.Log("Calibration started.");

        int pointIndex = 1;

        for (int i = 0; i < max_points; i++)
        {

            if (i >= 1)
            {
                
                MoveBallBetween(ballImages[i - 1], ballImages[i], () =>
                {
                    Debug.Log("小球已移动完成！");
                    ballImages[i].gameObject.SetActive(true); ;
                });
            }
            else
            {
                ballImages[i].gameObject.SetActive(true); ;
            }
            

            //cur_index = i;
            // 等待 10 秒（用于用户注视）
            yield return new WaitForSeconds(2.5f);

            _7i_point2d_t pt = new _7i_point2d_t
            {
                x = GetScreenPosition(ballImages[i]).x,
                y = GetScreenPosition(ballImages[i]).y
                //x = GetSystemScreenPosition(ballImages[i]).x,
                //y = GetSystemScreenPosition(ballImages[i]).y
            };


            pointFinished = false;

            ret = ASeeTracker._7i_start_calibration_point(pointIndex, ref pt,
                Marshal.GetFunctionPointerForDelegate(processCB), (IntPtr)handle,
                Marshal.GetFunctionPointerForDelegate(finishCB), (IntPtr)handle);

            UnityEngine.Debug.Log($"_7i_start_calibration_point: {pointIndex}, {ret}");

            ++pointIndex;

            // 等待当前点校准完成
            yield return new WaitUntil(() => pointFinished);

            //// 等待 1 秒（用于用户注视）
            //yield return new WaitForSeconds(2f);
        }

        //// 如果不是最后点被终止
        //if (cur_index != 5)
        //{
        //    ret = ASeeTracker._7i_cancel_calibration();
        //    UnityEngine.Debug.Log($"_7i_cancel_calibration: {ret}");
        //}

        int calibration_success = ASeeTracker._7i_compute_calibration(ref coefficient);
        UnityEngine.Debug.Log($"_7i_compute_calibration: {calibration_success}");

        ret = ASeeTracker._7i_complete_calibration();
        UnityEngine.Debug.Log($"_7i_complete_calibration: {ret}");

        float left_score = 0, right_score = 0;
        ASeeTracker._7i_get_calibration_score(ref left_score, ref right_score);
        UnityEngine.Debug.Log($"Calibration Scores - Left: {left_score}, Right: {right_score}");

        if (left_score == 0 || right_score == 0)
        {
            UnityEngine.Debug.Log("Calibration failed, scores are zero.");
            //calibration_success = -1; // 标记校准失败
        }


        UnityEngine.Debug.Log("Calibration done.");

        if (calibration_success == 0)
        {
            //gazeDot.gameObject.SetActive(true); // 设置 alpha 为 0
            //ret = ASeeTracker._7i_start_tracking(ref coefficient);
            //UnityEngine.Debug.Log($"_7i_start_tracking: {ret}");
            //is_tracing = true;
            CompareCoefficients(pendingCoefficient, coefficient);
            pendingCoefficient = coefficient;
           
            startTrace(coefficient);

            File.WriteAllBytes(coe_file_name, coefficient.buf);
        }

        //is_all_points_finished = true;
        is_calibrating = false;


        //校准结束UI恢复到初始状态

        UIBackground.gameObject.SetActive(false); // 设置 alpha 为 0
        is_needrecalibrate = false;

        //协程结束
        UnityEngine.Debug.Log("协程结束");
    }

    /// <summary>
    /// 让 ballImage_move 从 startBall 平滑移动到 endBall
    /// </summary>
    public void MoveBallBetween(Image startBall, Image endBall, System.Action onComplete = null)
    {
        if (ballImage_move == null || startBall == null || endBall == null) return;

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);

        ballImage_move.rectTransform.position = startBall.rectTransform.position;

        moveCoroutine = StartCoroutine(SmoothMoveTo(endBall.rectTransform.position, moveDuration, onComplete));
    }


    /// <summary>
    /// 平滑移动协程
    /// </summary>
    private IEnumerator SmoothMoveTo(Vector3 targetPosition, float duration, System.Action onComplete)
    {
        ballImage_move.gameObject.SetActive(true);

        RectTransform rt = ballImage_move.rectTransform;
        Vector3 startPos = rt.position;
        float time = 0f;
        
        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            rt.position = Vector3.Lerp(startPos, targetPosition, t);
            yield return null;
        }

        rt.position = targetPosition;

        ballImage_move.gameObject.SetActive(false);

        onComplete?.Invoke(); // ✅ 移动完成时调用回调
    }


    public void EnqueueScale(Image image, float targetProgress)
    {
        if (image == null) return;

        UnityEngine.Debug.Log($"EnqueueScale progress:{targetProgress}");

        float targetScale = Mathf.Lerp(scaleFactor, 0f, Mathf.Clamp01(targetProgress / 100f));

        // 入队一个 Coroutine 启动器
        mainThreadActions.Enqueue(() =>
        {
            StartCoroutine(AnimateScale(image.rectTransform, targetScale, 0.3f)); // 0.3 秒过渡
        });
    }

    private IEnumerator AnimateScale(RectTransform rt, float targetScale, float duration)
    {
        float time = 0f;
        Vector3 startScale = rt.localScale;
        Vector3 endScale = new Vector3(targetScale, targetScale, 1f);

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / duration);
            rt.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        rt.localScale = endScale;
    }


    /// <summary>
    /// 获取 UI Image 在窗口上的位置（单位：像素坐标）
    /// </summary>
    /// <param name="image">目标 UI Image 组件</param>
    /// <returns>屏幕坐标（Vector2），如果为空则返回 Vector2.zero</returns>
    public static Vector2 GetScreenPosition(Image image)
    {
        if (image == null) return Vector2.zero;

        RectTransform rt = image.GetComponent<RectTransform>();
        Canvas canvas = image.canvas;
        if (rt == null || canvas == null) return Vector2.zero;

        Vector2 screenPos;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // ScreenSpace-Overlay 模式：RectTransform.position 就是屏幕坐标
            screenPos = rt.position;
        }
        else
        {
            // ScreenSpace-Camera 或 WorldSpace 模式
            screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rt.position);
        }

        UnityEngine.Debug.Log($"GetScreenPosition get pos: x:{screenPos.x} y:{screenPos.y}");

        //unity的左下角原点要到左上角去
        screenPos = BottomLeftToTopLeft(screenPos);
        UnityEngine.Debug.Log($"GetScreenPosition after get BottomLeftToTopLeft pos: x:{screenPos.x} y:{screenPos.y}");

        return screenPos;
    }

    /// <summary>
    /// 获取 UI Image 在屏幕上()的位置（单位：像素坐标）
    /// </summary>
    /// <param name="image">目标 UI Image 组件</param>
    /// <returns>屏幕坐标（Vector2），如果为空则返回 Vector2.zero</returns>
    public static Vector2 GetSystemScreenPosition(Image image)
    {
        if (image == null) return Vector2.zero;

        RectTransform rt = image.GetComponent<RectTransform>();
        Canvas canvas = image.canvas;
        if (rt == null || canvas == null) return Vector2.zero;

        Vector2 localScreenPos;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            localScreenPos = rt.position;
        }
        else
        {
            localScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rt.position);
        }

#if UNITY_STANDALONE_WIN
        // 获取 Unity 窗口在操作系统中的左上角坐标
        IntPtr hwnd = GetActiveWindow();
        if (GetWindowRect(hwnd, out RECT rect))
        {
            int windowX = rect.Left;
            int windowY = rect.Top;

            // Unity 的屏幕坐标 (0,0) 是左下角，Win32 的 (0,0) 是左上角
            // 所以 y 方向要反转
            float systemX = windowX + localScreenPos.x;
            float systemY = windowY + (Screen.currentResolution.height - localScreenPos.y);

            return new Vector2(systemX, systemY);
        }
#endif

        return localScreenPos;
    }

    private static float nanStartTime = -1f;
    private static bool hasWarned = false;
    private const float nanTimeout = 3f;

    //public static void SetUIImageByNormalizedPos(Image img, Vector2 normalizedScreenPos)
    //{
    //    if (img == null) return;

    //    UnityEngine.Debug.Log($"SetUIImageByNormalizedPos input: {normalizedScreenPos}");


    //    if (float.IsNaN(normalizedScreenPos.x) || float.IsNaN(normalizedScreenPos.y))
    //    {
    //        if (nanStartTime < 0)
    //            nanStartTime = Time.unscaledTime;

    //        float duration = Time.unscaledTime - nanStartTime;

    //        if (duration >= nanTimeout && !hasWarned)
    //        {
    //            UnityEngine.Debug.LogWarning($"⚠️ [SetUIImageByNormalizedPos] 已持续 {nanTimeout} 秒为 NaN，可能需要重新校准眼动仪！");
    //            hasWarned = true;
    //        }
    //        UnityEngine.Debug.LogWarning($"[SetUIImageByNormalizedPos] Invalid NaN input: {normalizedScreenPos}");
    //        return;
    //    }

    //    RectTransform rt = img.GetComponent<RectTransform>();
    //    Canvas canvas = img.canvas;

    //    if (rt == null || canvas == null) return;

    //    //映射到像素坐标（眼动仪为左上角原点，Unity为左下角）
    //    Vector2 screenPos = new Vector2(
    //        normalizedScreenPos.x * Screen.width,
    //        normalizedScreenPos.y * Screen.height
    //    );

    //    //int systemWidth = Display.main.systemWidth;
    //    //int systemHeight = Display.main.systemHeight;

    //    //Vector2 screenPos = new Vector2(
    //    //normalizedScreenPos.x * systemWidth,
    //    //normalizedScreenPos.y * systemHeight
    //    //);


    //    screenPos = TopLeftToBottomLeft(screenPos);

    //    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
    //    {
    //        Vector2 targetAnchored = screenPos - new Vector2(Screen.width, Screen.height) / 2f;
    //        img.StartCoroutine(SmoothMoveAnchored(rt, targetAnchored, 0.1f));  // 平滑到目标点
    //    }
    //    else
    //    {
    //        Vector3 worldPos;
    //        RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, screenPos, canvas.worldCamera, out worldPos);
    //        img.StartCoroutine(SmoothMoveWorld(rt, worldPos, 0.1f));  // 平滑到目标点
    //    }
    //}


    //public static void SetUIImageByNormalizedPos(Image img, Vector2 normalizedScreenPos)
    //{
    //    if (img == null) return;

    //    //检查是否为 NaN
    //    if (float.IsNaN(normalizedScreenPos.x) || float.IsNaN(normalizedScreenPos.y))
    //    {
    //        UnityEngine.Debug.LogWarning($"[SetUIImageByNormalizedPos] Invalid NaN input: {normalizedScreenPos}");
    //        return;
    //    }


    //    RectTransform rt = img.GetComponent<RectTransform>();
    //    Canvas canvas = img.canvas;

    //    if (rt == null || canvas == null) return;

    //    // 将归一化坐标转换为屏幕像素坐标（眼动仪左上角原点 → Unity 左下角原点）
    //    Vector2 screenPos = new Vector2(
    //        normalizedScreenPos.x * Screen.width,
    //        normalizedScreenPos.y * Screen.height
    //    );
    //    screenPos = TopLeftToBottomLeft(screenPos);

    //    UnityEngine.Debug.Log($"注视点坐标{screenPos}");

    //    // 获取当前 UI 的屏幕坐标
    //    Vector2 currentScreenPos;
    //    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
    //    {
    //        currentScreenPos = rt.position;
    //    }
    //    else
    //    {
    //        currentScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rt.position);
    //    }

    //    // 如果移动距离小于 20 像素则不移动
    //    float distance = Vector2.Distance(currentScreenPos, screenPos);
    //    if (distance < 50)
    //    {
    //        UnityEngine.Debug.Log("注视点移动距离小于50个像素");
    //        return;
    //    }

    //    // 执行平滑移动
    //    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
    //    {
    //        Vector2 targetAnchored = screenPos - new Vector2(Screen.width, Screen.height) / 2f;
    //        img.StartCoroutine(SmoothMoveAnchored(rt, targetAnchored, 0.1f));
    //    }
    //    else
    //    {
    //        Vector3 worldPos;
    //        RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, screenPos, canvas.worldCamera, out worldPos);
    //        img.StartCoroutine(SmoothMoveWorld(rt, worldPos, 0.1f));
    //    }
    //}


    public static void SetUIImageByNormalizedPos(Image img, Vector2 normalizedScreenPos)
    {
        if (img == null) return;

        // 检查是否为 NaN
        if (float.IsNaN(normalizedScreenPos.x) || float.IsNaN(normalizedScreenPos.y))
        {
            UnityEngine.Debug.LogWarning($"[SetUIImageByNormalizedPos] Invalid NaN input: {normalizedScreenPos}");
            return;
        }

        RectTransform rt = img.GetComponent<RectTransform>();
        Canvas canvas = img.canvas;

        if (rt == null || canvas == null) return;

        // 将归一化坐标转换为屏幕像素坐标（眼动仪左上角原点 → Unity 左下角原点）
        Vector2 screenPos = new Vector2(
            normalizedScreenPos.x * Screen.width,
            (1 - normalizedScreenPos.y) * Screen.height  // 更精确的坐标系转换
        );

        // 调试日志（生产环境可关闭）
        // Debug.Log($"注视点坐标{screenPos}");

        // 获取UI元素的当前屏幕位置
        Vector2 currentScreenPos = RectTransformUtility.WorldToScreenPoint(
            canvas.worldCamera,
            rt.position
        );

        // 计算UI空间的实际距离（考虑Canvas缩放）
        RectTransform parentRT = rt.parent as RectTransform;
        if (parentRT != null)
        {
            Vector2 localCurrent, localTarget;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, currentScreenPos, canvas.worldCamera, out localCurrent);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPos, canvas.worldCamera, out localTarget);

            float uiDistance = Vector2.Distance(localCurrent, localTarget);

            // 动态调整阈值（基于Canvas缩放比例）
            float dynamicThreshold = 50f / canvas.scaleFactor;

            if (uiDistance < dynamicThreshold)
            {
                // Debug.Log($"注视点移动距离小于{dynamicThreshold}个UI像素");
                return;
            }
        }

        // 使用平滑阻尼算法更新位置
        Instance.mainThreadActions.Enqueue(() =>
        {
            Instance.UpdateGazePosition(rt, canvas, screenPos);
        });
    }

    // 平滑更新注视点位置
    private void UpdateGazePosition(RectTransform rt, Canvas canvas, Vector2 targetScreenPos)
    {
        // 计算平滑移动的目标位置
        Vector2 targetAnchored = CalculateTargetPosition(rt, canvas, targetScreenPos);

        // 使用平滑阻尼算法（SmoothDamp）替代Lerp
        rt.anchoredPosition = Vector2.SmoothDamp(
            rt.anchoredPosition,
            targetAnchored,
            ref gazeState.currentVelocity,
            0.02f, // 平滑时间（秒）
            Mathf.Infinity, // 最大速度（无限制）
            Time.deltaTime // 使用未缩放时间
        );

        // 更新状态跟踪
        gazeState.lastScreenPos = targetScreenPos;
        gazeState.lastUpdateTime = Time.unscaledTime;
    }


    private Vector2 CalculateTargetPosition(RectTransform rt, Canvas canvas, Vector2 screenPos)
    {
        RectTransform parentRT = rt.parent as RectTransform;
        if (parentRT == null) return rt.anchoredPosition;

        Vector2 localPoint;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // 对于 Overlay 模式，也需要 ScreenPoint → LocalPoint 的转换
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPos, null, out localPoint))
            {
                return localPoint;
            }
        }
        else
        {
            // WorldCamera 模式仍需传入 camera
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRT, screenPos, canvas.worldCamera, out localPoint))
            {
                return localPoint;
            }
        }

        return rt.anchoredPosition;
    }

    private static IEnumerator SmoothMoveAnchored(RectTransform rt, Vector2 target, float duration)
    {
        Vector2 start = rt.anchoredPosition;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            rt.anchoredPosition = Vector2.Lerp(start, target, t);
            yield return null;
        }

        rt.anchoredPosition = target;
    }

    private static IEnumerator SmoothMoveWorld(RectTransform rt, Vector3 target, float duration)
    {
        Vector3 start = rt.position;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, time / duration);
            rt.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        rt.position = target;
    }


    //public void startTrace()
    //{
    //    if (!is_init)
    //    {
    //        UnityEngine.Debug.Log("startTrace 未初始化");
    //        return;
    //    }

    //    if (is_tracing)
    //    {
    //        UnityEngine.Debug.Log("已经在追踪中了");
    //        return;
    //    }

    //    if (uiReady)
    //    {
    //        StartCoroutine(StartTraceWhenUIReady());
    //    }
    //    else
    //    {
    //        UnityEngine.Debug.Log("UI 未准备好，延迟 startTrace");
    //        pendingStartTrace = true;
    //    }
    //}

    //private IEnumerator StartTraceWhenUIReady()
    //{
    //    // 等待 UI 全部挂载
    //    yield return new WaitUntil(() => uiReady);

    //    _7i_coefficient_t coefficient;

    //    if (LoadCalibration(out coefficient))
    //    {
    //        UnityEngine.Debug.Log("成功加载校准数据");
    //        if (gazeDot != null)
    //            gazeDot.gameObject.SetActive(true);

    //        int ret = ASeeTracker._7i_start_tracking(ref coefficient);
    //        is_tracing = true;
    //        UnityEngine.Debug.Log($"_7i_start_tracking: {ret}");
    //    }
    //    else
    //    {
    //        UnityEngine.Debug.Log("未找到校准数据，需要重新校准");
    //    }
    //}

    public void startTrace(_7i_coefficient_t coefficient)
    {
        if (!is_init)
        {
            UnityEngine.Debug.Log("startTrace 未初始化");
            return;
        }

        if (is_tracing)
        {
            UnityEngine.Debug.Log("已经在追踪中了");
            return;
        }

        if (uiReady)
        {
            StartCoroutine(StartTraceWhenUIReady(coefficient));
        }
        else
        {
            UnityEngine.Debug.Log("UI 未准备好，延迟 startTrace");
            pendingStartTrace = true;
            pendingCoefficient = coefficient; // 存储待用参数
        }
    }


    private IEnumerator StartTraceWhenUIReady(_7i_coefficient_t coefficient)
    {
        yield return new WaitUntil(() => uiReady);

        UnityEngine.Debug.Log("UI 准备完成，开始追踪");

        if (gazeDot != null)
            gazeDot.gameObject.SetActive(true);

        if (coefficient.buf == null )
        {
            Debug.LogWarning("StartTraceWhenUIReady的coefficient 未初始化（buf 为 null）");
        }

        int ret = ASeeTracker._7i_start_tracking(ref coefficient);
        is_tracing = true;
        UnityEngine.Debug.Log($"_7i_start_tracking: {ret}");
    }





    //停止追踪
    public void stopTrace()
    {
        if (!is_init && !is_tracing)
        {
            UnityEngine.Debug.Log($"stopTrace 未初始化或未追踪中");
            return;
        }


        ASeeTracker._7i_stop_tracking();
        gazeDot.gameObject.SetActive(false); // 设置 alpha 为 0
        is_tracing = false;
    }

    //读取校准系数
    public bool LoadCalibration()
    { 
        if (File.Exists(coe_file_name))
        {
            pendingCoefficient.buf = File.ReadAllBytes(coe_file_name);
            return true;
        }
        else
        {
            Debug.Log("校准文件不存在");
            return false;
        }
    }

    //检查配置文件
    public bool CheckCoeFile()
    {

        if (File.Exists(coe_file_name))
        {
             pendingCoefficient.buf = File.ReadAllBytes(coe_file_name);
            return true;
        }
        else
        {
            return false;
        }
    }

    //左下角为原点 的坐标系转换为 左上角为原点
    public static Vector2 BottomLeftToTopLeft(Vector2 bottomLeftPos)
    {
        return new Vector2(bottomLeftPos.x, Screen.height - bottomLeftPos.y);
    }

    //左上角为原点 的坐标系转换为 左下角为原点
    public static Vector2 TopLeftToBottomLeft(Vector2 topLeftPos)
    {
        //int systemHeight = Display.main.systemHeight;
        //return new Vector2(topLeftPos.x, systemHeight - topLeftPos.y);
        return new Vector2(topLeftPos.x, Screen.height - topLeftPos.y);
    }

    //对16:10 UI适配
    //中心(0.5, 0.5)  (0.5, 0.5)	(0, 0)
    //左上角(0, 1)  (0, 1)	(x, -y)
    //右上角(1, 1)  (1, 1)	(-x, -y)
    //左下角(0, 0)  (0, 0)	(x, y)
    //右下角(1, 0)  (1, 0)	(-x, y)
    public static float GetScreenScale()
    {
        Vector2 baseResolution = new Vector2(1920f, 1080f);
        Vector2 currentResolution = new Vector2(Display.main.systemWidth, Display.main.systemHeight);
        return Mathf.Min(currentResolution.x / baseResolution.x, currentResolution.y / baseResolution.y);
    }

    public static void SetStretchFull(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rt.anchorMin = Vector2.zero;      // (0,0) = 父左下
        rt.anchorMax = Vector2.one;       // (1,1) = 父右上
        rt.pivot = new Vector2(0.5f, 0.5f);  // 习惯性居中（可改）

        // offsetMin：左/下边距   offsetMax：右/上“负边距”
        rt.offsetMin = new Vector2(left, bottom);      // 距父左、下
        rt.offsetMax = new Vector2(-right, -top);       // 距父右、上（注意是负值）
    }

    //居中
    public static void SetAnchorCenterScaled(RectTransform rt, bool needscale)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        if (needscale)
        {
            float scaleFactor_screen = GetScreenScale();
            if (scaleFactor_screen > 1.0f)
            {
                scaleFactor_screen *= scaleFactor;
            }



            rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
        }
      
    }

    //左上角
    public static void SetAnchorTopLeftScaled(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);  // 左上角锚点
        rt.anchoredPosition = new Vector2(baseOffset.x, -baseOffset.y) * GetScreenScale();  // Y向下负值

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f)
        {
            scaleFactor_screen *= scaleFactor;
        }

        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }

    //右上角
    public static void SetAnchorTopRight(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);  // 右上角锚点
        rt.anchoredPosition = new Vector2(-baseOffset.x, -baseOffset.y) * GetScreenScale();  // X反向，Y负值

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f)
        {
            scaleFactor_screen *= scaleFactor;
        }

        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }

    //左下角
    public static void SetAnchorBottomLeft(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0f);  // 左下角锚点
        rt.anchoredPosition = baseOffset * GetScreenScale();  // 正向

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f)
        {
            scaleFactor_screen *= scaleFactor;
        }

        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }


    //右下角
    public static void SetAnchorBottomRight(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);  // 右下角锚点
        rt.anchoredPosition = new Vector2(-baseOffset.x, baseOffset.y) * GetScreenScale();  // X反向，Y正向

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f)
        {
            scaleFactor_screen *= scaleFactor;
        }

        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }


    public static void CompareCoefficients(_7i_coefficient_t a, _7i_coefficient_t b)
    {
        if (a.buf == null)
        {
            Debug.LogWarning("a coefficient 未初始化（buf 为 null）");
            return;
        }

        if (b.buf == null)
        {
            Debug.LogWarning("b coefficient 未初始化（buf 为 null）");
            return;
        }


        if (a.buf.Length != b.buf.Length)
        {
            Debug.LogWarning($"coefficient 长度不同: {a.buf.Length} vs {b.buf.Length}");
            return;
        }

        int diffCount = 0;
        for (int i = 0; i < a.buf.Length; i++)
        {
            if (a.buf[i] != b.buf[i])
            {
                Debug.Log($"字节差异：索引 {i}, a = {a.buf[i]}, b = {b.buf[i]}");
                diffCount++;
                if (diffCount >= 10) // 最多显示前10个差异
                {
                    Debug.Log("... 差异过多，已省略后续");
                    break;
                }
            }
        }

        if (diffCount == 0)
        {
            Debug.Log("✅ 两个 coefficient 完全一致");
        }
        else
        {
            Debug.Log($"⚠️ coefficient 有 {diffCount} 处不同");
        }
    }

}

