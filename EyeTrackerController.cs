
//using System;
//using System.Collections.Generic;
//using System.Runtime.InteropServices;
//using UnityEngine;
//using UnityEngine.UI;
//using EyeTracker;
//using System.IO;
//using System.Collections;
//using UnityEditor;

//public class EyeTrackerController : MonoBehaviour
//{
//    [Header("背景")]
//    public Image UIBackground; // 注视点的 UI Image

//    [Header("小球")]
//    public List<Image> ballImages; // 小球列表，按顺序从左上到右下排列

//    [Header("注视点")]
//    public Image gazeDot; // 注视点的 UI Image

//    //预制体实例
//    public static EyeTrackerController Instance;

//    readonly string coe_file_name = @".\coefficient.dat";

//    //校准点
//    const int max_points = 5;

//    // 当前正在校准的点索引（0~8）
//    int cur_index = 4;

//    // 是否正在校准中
//    bool is_calibrating = false;

//    bool is_tracing = false;

//    // 是否全部点都校准完
//    bool is_all_points_finished = false;

//    // 当前白色圆缩放系数（用于进度动画）
//    private float scale = 1.0f;

//    // SDK 校准进度回调函数
//    public static ASeeTracker.processCallback processCB = new ASeeTracker.processCallback(process_callback);

//    // SDK 校准完成回调函数（单个点）
//    public static ASeeTracker.finishCallback finishCB = new ASeeTracker.finishCallback(finish_callback);
//    private bool pointFinished = false;

//    //UI注视球更新队列
//    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

//    //SDK状态参数
//    private static bool running = false;
//    private GCHandle handle;
//    // Start is called before the first frame update
//    void Start()
//    {



//        //先设置所有小球不可见
//        //foreach (Image img in ballImages)
//        //{
//        //    if (img != null)
//        //        img.color = new Color(img.color.r, img.color.g, img.color.b, 0f); // 设置 alpha 为 0
//        //}

//        //Background.color = new Color(Background.color.r, Background.color.g, Background.color.b, 0f); // 设置 alpha 为 0

//        //gazeDot.color = new Color(gazeDot.color.r, gazeDot.color.g, gazeDot.color.b, 0f); // 设置 alpha 为 0

//        //startCalibration();
//        //startTrace();
//    }

//    public int ASeeTrackerStart()
//    {
//        handle = GCHandle.Alloc(this);
//        ASeeTracker._7i_set_image_callback(
//            Marshal.GetFunctionPointerForDelegate(new ASeeTracker.imageCallback(ImageCallback)),
//            (IntPtr)handle);

//        ASeeTracker._7i_set_gaze_callback(
//            Marshal.GetFunctionPointerForDelegate(new ASeeTracker.gazeCallback(GazeCallback)),
//            (IntPtr)handle);

//        int ret = ASeeTracker._7i_start("./config");
//        Debug.Log("SDK Start Result: " + ret);

//        if (ret == 0)
//            running = true;

//        return ret;
//    }


//    // Update is called once per frame
//    void Update()
//    {
//        while (mainThreadActions.Count > 0)
//        {
//            var action = mainThreadActions.Dequeue();
//            action?.Invoke();
//        }
//    }

//    void Awake()
//    {
//        //if (Instance == null)
//        //{
//        //    Instance = this;
//        //    DontDestroyOnLoad(gameObject);  // 保证切换场景不销毁
//        //}
//        //else
//        //{
//        //    Destroy(gameObject); // 如果已有实例，销毁重复的
//        //}
//    }


//    void OnDestroy()
//    {
//        running = false;
//        ASeeTracker._7i_stop();
//        if (handle.IsAllocated) handle.Free();
//    }

//    public static void ImageCallback(int eye, IntPtr image, int size, int width, int height, long timestamp, IntPtr context)
//    {
//        if (!running || context == IntPtr.Zero) return;

//        // 实际处理图像：可使用 Texture2D.LoadRawTextureData() 绑定到 cameraDisplay
//        Debug.Log($"[Image] Eye:{eye}, Size:{size}, W:{width}, H:{height}");
//    }

//    public static void GazeCallback(ref _7i_eye_data_ex_t eyes, IntPtr context)
//    {
//        var tracker = (EyeTrackerController)((GCHandle)context).Target;
//        if (tracker == null || !running) return;

//        var gaze = eyes.recom_gaze;

//        // 检查 gaze 坐标是否有效
//        if ((gaze.gaze_bit_mask & (1 << (int)_7I_EYE_GAZE_VALIDITY.ID_EYE_GAZE_POINT)) == 0) return;

//        float x = gaze.gaze_point.x; // 归一化 [0,1]
//        float y = gaze.gaze_point.y;

//        // 检查是否为 NaN
//        if (float.IsNaN(x) || float.IsNaN(y))
//        {
//            Debug.Log($"Gaze is NaN! x:{x}, y:{y}");
//            return;
//        }

//        Debug.Log($"[Gaze] x:{x}, y:{y}");

//        Vector2 GazePoint_Get = new Vector2(x, y);
//        //SetUIImageByNormalizedPos(tracker.gazeDot, GazePoint_Get);

//        tracker.mainThreadActions.Enqueue(() =>
//        {
//            SetUIImageByNormalizedPos(tracker.gazeDot, GazePoint_Get);
//        });


//    }

//    // SDK 校准进度回调函数
//    public static void process_callback(int index, int percent, IntPtr context)
//    {
//        var tracker = (EyeTrackerController)((GCHandle)context).Target;
//        if (tracker == null || !running) return;
//        Debug.Log($"process: {index},{percent}");

//        tracker.EnqueueScale(tracker.ballImages[index - 1], percent);
//    }

//    public static void finish_callback(int index, int error, IntPtr context)
//    {
//        Debug.Log($"finish: {index}, {error}");

//        var tracker = (EyeTrackerController)((GCHandle)context).Target;
//        if (tracker != null)
//        {
//            tracker.pointFinished = true;
//        }
//    }

//    public void startCalibration()
//    {
//        UIBackground.gameObject.SetActive(true) ; // 设置 alpha 为 0
//        ballImages[0].gameObject.SetActive(true);

//        StartCoroutine(CalibrationCoroutine());
//    }


//    //校准携程
//    private IEnumerator CalibrationCoroutine()
//    {
//        yield return new WaitForSeconds(1.5f);

//        _7i_coefficient_t coefficient = new _7i_coefficient_t();

//        if (is_calibrating || is_all_points_finished)
//        {
//            Debug.LogWarning("Calibration already in progress or finished.");
//            yield break;
//        }

//        is_calibrating = true;
//        is_all_points_finished = false;

//        int ret = ASeeTracker._7i_start_calibration(max_points);
//        if (ret != 0)
//        {
//            Debug.LogError($"Failed to start calibration: {ret}");
//            is_calibrating = false;
//            yield break;
//        }

//        Debug.Log("Calibration started.");

//        int pointIndex = 1;

//        for (int i = 0; i < max_points; i++)
//        {
//            ballImages[i].gameObject.SetActive(true); ;

//            //cur_index = i;
//            // 等待 10 秒（用于用户注视）
//            yield return new WaitForSeconds(1.5f);

//            _7i_point2d_t pt = new _7i_point2d_t
//            {
//                x = GetScreenPosition(ballImages[i]).x,
//                y = GetScreenPosition(ballImages[i]).y
//            };


//            pointFinished = false;

//            ret = ASeeTracker._7i_start_calibration_point(pointIndex, ref pt,
//                Marshal.GetFunctionPointerForDelegate(processCB), (IntPtr)handle,
//                Marshal.GetFunctionPointerForDelegate(finishCB), (IntPtr)handle);

//            Debug.Log($"_7i_start_calibration_point: {pointIndex}, {ret}");

//            ++pointIndex;

//            // 等待当前点校准完成
//            yield return new WaitUntil(() => pointFinished);

//            // 等待 10 秒（用于用户注视）
//            yield return new WaitForSeconds(2f);
//        }

//        //// 如果不是最后点被终止
//        //if (cur_index != 5)
//        //{
//        //    ret = ASeeTracker._7i_cancel_calibration();
//        //    Debug.Log($"_7i_cancel_calibration: {ret}");
//        //}

//        int calibration_success = ASeeTracker._7i_compute_calibration(ref coefficient);
//        Debug.Log($"_7i_compute_calibration: {calibration_success}");

//        ret = ASeeTracker._7i_complete_calibration();
//        Debug.Log($"_7i_complete_calibration: {ret}");

//        float left_score = 0, right_score = 0;
//        ASeeTracker._7i_get_calibration_score(ref left_score, ref right_score);
//        Debug.Log($"Calibration Scores - Left: {left_score}, Right: {right_score}");

//        if (left_score == 0 || right_score == 0)
//        {
//            Debug.LogError("Calibration failed, scores are zero.");
//            //calibration_success = -1; // 标记校准失败
//        }


//        Debug.Log("Calibration done.");

//        if (calibration_success == 0)
//        {
//            gazeDot.gameObject.SetActive(true); // 设置 alpha 为 0
//            ret = ASeeTracker._7i_start_tracking(ref coefficient);
//            Debug.Log($"_7i_start_tracking: {ret}");
//            is_tracing = true;

//            File.WriteAllBytes(coe_file_name, coefficient.buf);
//        }

//        is_all_points_finished = true;
//        is_calibrating = false;


//        //校准结束UI恢复到初始状态
//        foreach (Image img in ballImages)
//        {
//            if (img != null)
//            {
//                img.gameObject.SetActive(false); ; // 设置 alpha 为 0
//                img.rectTransform.localScale = new Vector3(1f, 1f, 1f);
//            }

//        }

//        UIBackground.gameObject.SetActive(false); // 设置 alpha 为 0
//    }


//    public void EnqueueScale(Image image, float targetProgress)
//    {
//        if (image == null) return;

//        Debug.Log($"EnqueueScale progress:{targetProgress}");

//        float targetScale = Mathf.Lerp(1f, 0f, Mathf.Clamp01(targetProgress / 100f));

//        // 入队一个 Coroutine 启动器
//        mainThreadActions.Enqueue(() =>
//        {
//            StartCoroutine(AnimateScale(image.rectTransform, targetScale, 0.3f)); // 0.3 秒过渡
//        });
//    }

//    private IEnumerator AnimateScale(RectTransform rt, float targetScale, float duration)
//    {
//        float time = 0f;
//        Vector3 startScale = rt.localScale;
//        Vector3 endScale = new Vector3(targetScale, targetScale, 1f);

//        while (time < duration)
//        {
//            time += Time.deltaTime;
//            float t = Mathf.Clamp01(time / duration);
//            rt.localScale = Vector3.Lerp(startScale, endScale, t);
//            yield return null;
//        }

//        rt.localScale = endScale;
//    }


//    /// <summary>
//    /// 获取 UI Image 在屏幕上的位置（单位：像素坐标）
//    /// </summary>
//    /// <param name="image">目标 UI Image 组件</param>
//    /// <returns>屏幕坐标（Vector2），如果为空则返回 Vector2.zero</returns>
//    public static Vector2 GetScreenPosition(Image image)
//    {
//        if (image == null) return Vector2.zero;

//        RectTransform rt = image.GetComponent<RectTransform>();
//        Canvas canvas = image.canvas;
//        if (rt == null || canvas == null) return Vector2.zero;

//        Vector2 screenPos;

//        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
//        {
//            // ScreenSpace-Overlay 模式：RectTransform.position 就是屏幕坐标
//            screenPos = rt.position;
//        }
//        else
//        {
//            // ScreenSpace-Camera 或 WorldSpace 模式
//            screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rt.position);
//        }

//        Debug.Log($"GetScreenPosition get pos: x:{screenPos.x} y:{screenPos.y}");

//        //unity的左下角原点要到左上角去
//        screenPos = BottomLeftToTopLeft(screenPos);
//        Debug.Log($"GetScreenPosition after get BottomLeftToTopLeft pos: x:{screenPos.x} y:{screenPos.y}");

//        return screenPos;
//    }


//    /// <summary>
//    /// 将归一化屏幕坐标 [0,1] 映射到 UI Image 上
//    /// </summary>
//    /// <param name="img">目标 Image</param>
//    /// <param name="normalizedScreenPos">归一化坐标（x,y 取值范围：0~1）</param>
//    //public static void SetUIImageByNormalizedPos(Image img, Vector2 normalizedScreenPos)
//    //{
//    //    if (img == null) return;

//    //    RectTransform rt = img.GetComponent<RectTransform>();
//    //    Canvas canvas = img.canvas;

//    //    if (rt == null || canvas == null) return;

//    //    // 映射到像素坐标
//    //    Vector2 screenPos = new Vector2(
//    //        normalizedScreenPos.x * Screen.width,
//    //        normalizedScreenPos.y * Screen.height
//    //    );

//    //    Debug.Log($"SetUIImageByNormalizedPos get pos: x:{screenPos.x} y:{screenPos.y}");

//    //    //眼动仪的左上角原点要到左下角去
//    //    screenPos = TopLeftToBottomLeft(screenPos);
//    //    Debug.Log($"SetUIImageByNormalizedPos after get TopLeftToBottomLeft pos: x:{screenPos.x} y:{screenPos.y}");


//    //    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
//    //    {
//    //        // ScreenSpace-Overlay 模式下，直接设置 anchoredPosition（相对中心）
//    //        rt.anchoredPosition = screenPos - new Vector2(Screen.width, Screen.height) / 2f;
//    //    }
//    //    else
//    //    {
//    //        // ScreenSpace-Camera 或 WorldSpace，需要转换坐标
//    //        Vector3 worldPos;
//    //        RectTransformUtility.ScreenPointToWorldPointInRectangle(
//    //            rt, screenPos, canvas.worldCamera, out worldPos);
//    //        rt.position = worldPos;
//    //    }
//    //}
//    public static void SetUIImageByNormalizedPos(Image img, Vector2 normalizedScreenPos)
//    {
//        if (img == null) return;

//        RectTransform rt = img.GetComponent<RectTransform>();
//        Canvas canvas = img.canvas;

//        if (rt == null || canvas == null) return;

//        // 映射到像素坐标（眼动仪为左上角原点，Unity为左下角）
//        Vector2 screenPos = new Vector2(
//            normalizedScreenPos.x * Screen.width,
//            normalizedScreenPos.y * Screen.height
//        );
//        screenPos = TopLeftToBottomLeft(screenPos);

//        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
//        {
//            Vector2 targetAnchored = screenPos - new Vector2(Screen.width, Screen.height) / 2f;
//            img.StartCoroutine(SmoothMoveAnchored(rt, targetAnchored, 0.1f));  // 平滑到目标点
//        }
//        else
//        {
//            Vector3 worldPos;
//            RectTransformUtility.ScreenPointToWorldPointInRectangle(rt, screenPos, canvas.worldCamera, out worldPos);
//            img.StartCoroutine(SmoothMoveWorld(rt, worldPos, 0.1f));  // 平滑到目标点
//        }
//    }

//    private static IEnumerator SmoothMoveAnchored(RectTransform rt, Vector2 target, float duration)
//    {
//        Vector2 start = rt.anchoredPosition;
//        float time = 0f;

//        while (time < duration)
//        {
//            time += Time.deltaTime;
//            float t = Mathf.SmoothStep(0f, 1f, time / duration);
//            rt.anchoredPosition = Vector2.Lerp(start, target, t);
//            yield return null;
//        }

//        rt.anchoredPosition = target;
//    }

//    private static IEnumerator SmoothMoveWorld(RectTransform rt, Vector3 target, float duration)
//    {
//        Vector3 start = rt.position;
//        float time = 0f;

//        while (time < duration)
//        {
//            time += Time.deltaTime;
//            float t = Mathf.SmoothStep(0f, 1f, time / duration);
//            rt.position = Vector3.Lerp(start, target, t);
//            yield return null;
//        }

//        rt.position = target;
//    }



//    //开始追踪
//    public void startTrace()
//    {
//        if (!running)
//            return;

//        _7i_coefficient_t coefficient;

//        if (LoadCalibration(out coefficient))
//        {
//            Debug.Log("成功加载校准数据");
//            gazeDot.gameObject.SetActive(true); // 设置 alpha 为 0
//            int ret = ASeeTracker._7i_start_tracking(ref coefficient);
//            is_tracing = true;
//            Console.WriteLine("_7i_start_tracking: {0:D}", ret);
//        }
//        else
//        {
//            Debug.LogError("未找到或读取失败，需要重新校准");
//            return;
//        }
//    }

//    //停止追踪
//    public void stopTrace()
//    {
//        if (!running && !is_tracing)
//            return;


//        ASeeTracker._7i_stop_tracking();
//        gazeDot.gameObject.SetActive(false); // 设置 alpha 为 0
//        is_tracing = false;
//    }

//    //读取校准系数
//    public bool LoadCalibration(out _7i_coefficient_t coefficient)
//    {
//        coefficient = new _7i_coefficient_t();
//        coefficient.buf = new byte[2048];

//        if (File.Exists(coe_file_name))
//        {
//            byte[] data = File.ReadAllBytes(coe_file_name);
//            int length = Mathf.Min(data.Length, 2048);
//            Array.Copy(data, coefficient.buf, length);
//            return true;
//        }
//        else
//        {
//            return false;
//        }
//    }

//    //检查配置文件
//    public bool CheckCoeFile()
//    {

//        if (File.Exists(coe_file_name))
//        {
//            return true;
//        }
//        else
//        {
//            return false;
//        }
//    }

//    //左下角为原点 的坐标系转换为 左上角为原点
//    public static Vector2 BottomLeftToTopLeft(Vector2 bottomLeftPos)
//    {
//        return new Vector2(bottomLeftPos.x, Screen.height - bottomLeftPos.y);
//    }

//    //左上角为原点 的坐标系转换为 左下角为原点
//    public static Vector2 TopLeftToBottomLeft(Vector2 topLeftPos)
//    {
//        return new Vector2(topLeftPos.x, Screen.height - topLeftPos.y);
//    }
//}



using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UI;
using EyeTracker;
using System.IO;
using System.Collections;
using UnityEditor;
using UnityEngine.SceneManagement;



public class EyeTrackerController : MonoBehaviour
{
    public GameObject eyeTrackerPrefab;     // 拖入 EyeTracker 预制体

    [Header("背景")]
    private Image UIBackground; // 注视点的 UI Image

    [Header("小球")]
    private List<Image> ballImages = new List<Image>(9); // 小球列表，按顺序从左上到右下排列

    [Header("注视点")]
    private Image gazeDot; // 注视点的 UI Image

    [Header("Gaze Point Style")]
    public Sprite customGazeSprite; // ★ 请在Inspector里把你要替换的那张图片拖进去
    private Sprite originalGazeSprite; // 用来存原本的图片，方便切回来

    //预制体实例
    public static EyeTrackerController Instance;

    private bool uiReady = false;         // UI 是否准备好

    readonly string coe_file_name = @".\coefficient.dat";

    //校准点
    const int max_points = 9;

    // 是否正在校准中
    bool is_calibrating = false;

    public bool is_tracing = false;

    // 是否全部点都校准完
    bool is_all_points_finished = false;

    // SDK 校准进度回调函数
    public static ASeeTracker.processCallback processCB = new ASeeTracker.processCallback(process_callback);

    // SDK 校准完成回调函数（单个点）
    public static ASeeTracker.finishCallback finishCB = new ASeeTracker.finishCallback(finish_callback);
    private bool pointFinished = false;


    //SDK状态参数
    public static bool running = false;
    private GCHandle handle;

    //注视点
    public static Vector2 gazeNormalized = Vector2.zero;
    private Vector2 velocity = Vector2.zero;



    // 注视点状态跟踪器
    private class GazeState
    {
        public Vector2 lastScreenPos;
        public Vector2 currentVelocity;
        public float lastUpdateTime;
        public bool isMoving;
    }
    private GazeState gazeState = new GazeState();

    //小球缩放状态
    private class ScaleState
    {
        public RectTransform rect;
        public float targetScale = scaleFactor;
        public float velocity = 0f;
    }
    private List<ScaleState> scaleStates = new List<ScaleState>();
    private Image ballImage_move; // 当前正在移动的注视点小球（用于动画效果）    
    private Coroutine moveCoroutine;  //平滑移动协程
    public float moveDuration = 1f; // 移动时间（秒）
    private static float scaleFactor = 0.5f; // 缩放系数，用于适配不同分辨率的屏幕


    [DllImport("gdi32.dll")]
    static extern int GetDeviceCaps(IntPtr hdc, int index);

    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hwnd);

    const int HORZSIZE = 4;
    const int VERTSIZE = 6;


    // Start is called before the first frame update
    void Start()
    {
        // 初始化小球缩放状态
        //foreach (var img in ballImages)
        //{
        //    scaleStates.Add(new ScaleState
        //    {
        //        rect = img.rectTransform,
        //        targetScale = 1f,
        //        velocity = 0f
        //    });
        //}

        //ASeeTrackerStart();
        //startCalibration();
    }

    //asee初始化
    public int ASeeTrackerStart()
    {
        handle = GCHandle.Alloc(this);
        ASeeTracker._7i_set_image_callback(
            Marshal.GetFunctionPointerForDelegate(new ASeeTracker.imageCallback(ImageCallback)),
            (IntPtr)handle);

        ASeeTracker._7i_set_gaze_callback(
            Marshal.GetFunctionPointerForDelegate(new ASeeTracker.gazeCallback(GazeCallback)),
            (IntPtr)handle);


        int ret = ASeeTracker._7i_start("./config");
        Debug.Log("SDK Start Result: " + ret);

        if (ret == 0)
            running = true;

        // 立即设置 flash data（非常重要！）
        SetupFlashData();

        ////设置屏幕尺寸和平滑
        //// 假设是 1920x1080 像素屏幕
        //double screenWidth = Screen.width;
        //double screenHeight = Screen.height;

        //int screenResult = ASeeTracker._7i_set_screen_size(screenWidth, screenHeight);
        //Debug.Log($"设置屏幕尺寸返回值: {screenResult}");

        // 设置平滑等级（支持 1~10，越大越平滑但越慢）
        int smoothLevel = 6;
        int smoothResult = ASeeTracker._7i_set_smooth(smoothLevel);
        Debug.Log($"设置平滑返回值: {smoothResult}");

        return ret;
    }

    //asee关闭
    public void ASeeTrackerStop()
    {
        if (is_tracing)
            stopTrace();

        running = false;
        ASeeTracker._7i_stop();
        if (handle.IsAllocated) handle.Free();
    }


    // Update is called once per frame
    //void Update()
    //{
    //    //注视点平滑移动
    //    //只有追踪时才移动
    //    if (is_tracing && uiReady)
    //    {

    //        if (gazeDot == null) return;

    //        RectTransform rt = gazeDot.rectTransform;
    //        Canvas canvas = gazeDot.canvas;

    //        if (canvas == null || rt == null) return;

    //        // 归一化坐标 → 屏幕坐标（左上角 → 左下角）
    //        Vector2 screenPos = new Vector2(
    //            gazeNormalized.x * Screen.width,
    //            (1f - gazeNormalized.y) * Screen.height
    //        );

    //        // 将屏幕坐标转换为目标 UI 的锚点坐标
    //        Vector2 targetAnchored = CalculateTargetPosition(rt, canvas, screenPos);

    //        // 使用 SmoothDamp 平滑移动
    //        rt.anchoredPosition = Vector2.SmoothDamp(
    //            rt.anchoredPosition,
    //            targetAnchored,
    //            ref gazeState.currentVelocity,   // 替代 velocity 的统一结构
    //            0.01f,         // 非常快，基本接近瞬移
    //            10000f,         // 显式最大速度，避免卡顿
    //            Time.deltaTime                  // 使用真实帧时间
    //        );

    //        // 可选：更新状态（如果需要记录时间或位置）
    //        gazeState.lastScreenPos = screenPos;
    //        gazeState.lastUpdateTime = Time.unscaledTime;

    //    }

    //    //校准时更新小球状态
    //    if (is_calibrating)
    //    {
    //        foreach (var state in scaleStates)
    //        {
    //            float current = state.rect.localScale.x; // x=y
    //            float newScale = Mathf.SmoothDamp(current, state.targetScale, ref state.velocity, 0.1f, 10f, Time.deltaTime);

    //            state.rect.localScale = new Vector3(newScale, newScale, 1f);
    //        }
    //    }

    //}

    void Update()
    {
        // 注视点移动：只有追踪时才移动（不再平滑）
        if (is_tracing && uiReady)
        {
            if (gazeDot == null) return;

            RectTransform rt = gazeDot.rectTransform;
            Canvas canvas = gazeDot.canvas;

            if (canvas == null || rt == null) return;

            // 归一化坐标 → 屏幕坐标（左上角 → 左下角）
            Vector2 screenPos = new Vector2(
                gazeNormalized.x * Screen.width,
                (1f - gazeNormalized.y) * Screen.height
            );

            // 将屏幕坐标转换为目标 UI 的锚点坐标
            Vector2 targetAnchored = CalculateTargetPosition(rt, canvas, screenPos);
            //Debug.Log($"Gaze Point:{targetAnchored}");

            // 直接更新位置（不使用 SmoothDamp）
            rt.anchoredPosition = targetAnchored;

            // 可选：更新状态（如果需要记录时间或位置）
            gazeState.currentVelocity = Vector2.zero; // 防止外部还在用这个值导致异常
            gazeState.lastScreenPos = screenPos;
            gazeState.lastUpdateTime = Time.unscaledTime;
        }

        // 校准时更新小球状态
        if (is_calibrating)
        {
            foreach (var state in scaleStates)
            {
                float current = state.rect.localScale.x; // x=y
                float newScale = Mathf.SmoothDamp(current, state.targetScale, ref state.velocity, 0.1f, 10f, Time.deltaTime);

                state.rect.localScale = new Vector3(newScale, newScale, 1f);
            }
        }
    }


    public int SetupFlashData()
    {
        try
        {
            // 1. 读取 Configs.dat
            string filePath = Path.Combine(Application.streamingAssetsPath, "config/Configs.dat");

            if (!File.Exists(filePath))
            {
                //Debug.LogError("Configs.dat not found: " + filePath);
                return -1;
            }

            byte[] flash_data = File.ReadAllBytes(filePath);
            int flash_size = flash_data.Length;

            // 2. 计算屏幕物理尺寸（毫米）
            // Unity 无法直接获取 mm，需要手动输入或平台 API
            // 如果你已经从 Windows API 获得 mm，则直接使用
            int screen_width_mm = GetScreenWidthMM();
            int screen_height_mm = GetScreenHeightMM();

            // 3. 固定 flash_data 在内存中，并获取指针
            GCHandle handle = GCHandle.Alloc(flash_data, GCHandleType.Pinned);
            IntPtr ptr = handle.AddrOfPinnedObject();

            // 4. 调用 SDK
            int ret = ASeeTracker._7i_set_gazepre_flash_data(
                ptr,
                flash_size,
                screen_width_mm,
                screen_height_mm
            );

            Debug.Log($"_7i_set_gazepre_flash_data ret = {ret}, size = {flash_size}, mm = {screen_width_mm} x {screen_height_mm}");

            // 5. 释放
            handle.Free();

            return ret;
        }
        catch (Exception ex)
        {
            Debug.LogError("SetupFlashData() exception: " + ex);
            return -1;
        }
    }

    public static int GetScreenWidthMM()
    {
        IntPtr screenDC = GetDC(IntPtr.Zero);
        return GetDeviceCaps(screenDC, HORZSIZE);
    }

    public static int GetScreenHeightMM()
    {
        IntPtr screenDC = GetDC(IntPtr.Zero);
        return GetDeviceCaps(screenDC, VERTSIZE);
    }



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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);  // 销毁多余的副本
            return;
        }

        Instance = this;  // 设置为全局唯一实例
        DontDestroyOnLoad(this.gameObject);

        //SceneManager.sceneLoaded -= OnSceneLoaded; // 防止重复注册
        SceneManager.sceneLoaded += OnSceneLoaded;
    }


    void OnDestroy()
    {
        //running = false;
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
        uiReady = false; //每次加载场景ui重新挂载
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

        // 在 Canvas 下查找 ASeeTracker_UI_Scipr(Clone),场景内已有先移除
        Transform childToRemove = canvasGO.transform.Find("ASeeTracker_UI_Scipr(Clone)");
        if (childToRemove != null)
        {
            GameObject.Destroy(childToRemove.gameObject);
            UnityEngine.Debug.Log("已移除 ASeeTracker_UI_Scipr(Clone)");
        }

        GameObject instance = Instantiate(eyeTrackerPrefab, canvas.transform);

        RectTransform parentRT = instance.GetComponent<RectTransform>();
        SetStretchFull(parentRT);

        // 绑定 UIBackground
        UIBackground = instance.transform.Find("background")?.GetComponent<Image>();
        if (UIBackground == null) UnityEngine.Debug.Log("未找到 UIBackground");

        SetStretchFull(UIBackground.rectTransform);

        // 绑定 ball_1 ~ ball_9
        ballImages.Clear();
        for (int i = 1; i <= 9; i++)
        {
            string name = $"ball_{i}";
            Transform ballTransform = instance.transform.Find(name);
            if (ballTransform != null)
            {
                Image ball_image = ballTransform.GetComponent<Image>();
                switch (i)
                {
                    case 1: SetAnchorCenterScaled(ball_image.rectTransform, true); break;
                    case 2: SetAnchorTopLeftScaled(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 3: SetAnchorTopCenter(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 4: SetAnchorTopRight(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 5: SetAnchorMiddleLeft(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 6: SetAnchorMiddleRight(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 7: SetAnchorBottomLeft(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 8: SetAnchorBottomCenter(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    case 9: SetAnchorBottomRight(ball_image.rectTransform, new Vector2(50f, 50f)); break;
                    default: break;
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

        scaleStates.Clear();
        foreach (var img in ballImages)
        {
            scaleStates.Add(new ScaleState
            {
                rect = img.rectTransform,
                targetScale = scaleFactor,
                velocity = 0f
            });
        }

        // UI 就绪
        uiReady = (UIBackground != null && ballImages.Count == 9 && gazeDot != null);

        if (is_tracing && gazeDot != null)
        {
            gazeDot.gameObject.SetActive(true);
        }

        //UnityEngine.Debug.Log($"[UI Ready] = {uiReady}");

        // 若切场景前设置了 pendingStartTrace，则在 UI Ready 后重新开始

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

    //是否有效值
    public static UInt32 _get_valid_value(byte position, UInt32 bits)
    {
        UInt32 the_mask = (((UInt32)1) << position);
        return (the_mask &= bits) >> position;
    }

    public static void ImageCallback(int eye, IntPtr image, int size, int width, int height, long timestamp, IntPtr context)
    {
        if (!running || context == IntPtr.Zero) return;

        // 实际处理图像：可使用 Texture2D.LoadRawTextureData() 绑定到 cameraDisplay
        //Debug.Log($"[Image] Eye:{eye}, Size:{size}, W:{width}, H:{height}");
    }

    // SDK 注视点回调函数
    public static void GazeCallback(ref _7i_eye_data_ex_t eyes, IntPtr context)
    {
        var tracker = (EyeTrackerController)((GCHandle)context).Target;
        if (tracker == null || !running) return;

        var gaze = eyes.recom_gaze;


        //float x = gaze.gaze_point.x;
        //float y = gaze.gaze_point.y;


        if (1 == _is_valid_recom_eye_gaze_point(ref eyes))
        {
            float x = gaze.gaze_point.x;
            float y = gaze.gaze_point.y;

            if (float.IsNaN(x) || float.IsNaN(y))
            {
                Debug.LogWarning("Gaze point is not valid, skipping update.");
                return;
            }

            gazeNormalized = new Vector2(x, y);

        }
        else
        {
            Debug.LogWarning("_is_valid_recom_eye_gaze_point fail");
            // Use the last one
        }



    }

    // SDK 校准进度回调函数
    public static void process_callback(int index, int percent, IntPtr context)
    {
        var tracker = (EyeTrackerController)((GCHandle)context).Target;
        if (tracker == null || !running) return;
        Debug.Log($"process: {index},{percent}");


        tracker.EnqueueScale(tracker.ballImages[index - 1], percent);
    }

    //SDK 校准完成回调函数（单个点）
    public static void finish_callback(int index, int error, IntPtr context)
    {
        Debug.Log($"finish: {index}, {error}");

        var tracker = (EyeTrackerController)((GCHandle)context).Target;
        if (tracker != null)
        {
            tracker.pointFinished = true;
        }
    }

    //小球缩放函数
    public void EnqueueScale(Image image, float targetProgress)
    {
        if (image == null) return;

        float targetScale = Mathf.Lerp(scaleFactor, 0f, Mathf.Clamp01(targetProgress / 100f));

        var state = scaleStates.Find(s => s.rect == image.rectTransform);
        if (state != null)
        {
            state.targetScale = targetScale;
        }
    }


    //开始校准
    public void startCalibration(System.Action onFinish = null)
    {
        if (!running)
        {
            UnityEngine.Debug.Log($"startCalibration 未初始化");
            onFinish?.Invoke(); // 失败也要回调，否则界面会卡住消失
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

        StartCoroutine(CalibrationCoroutine(onFinish));
    }


    //校准协程
    private IEnumerator CalibrationCoroutine(System.Action onFinish)
    {
        yield return new WaitForSeconds(1.5f);

        _7i_coefficient_t coefficient = new _7i_coefficient_t();

        if (is_calibrating)
        {
            Debug.LogWarning("Calibration already in progress or finished.");
            onFinish?.Invoke(); // 异常退出回调
            yield break;
        }

        is_calibrating = true;
        is_all_points_finished = false;

        int ret = ASeeTracker._7i_start_calibration(max_points);
        if (ret != 0)
        {
            Debug.LogError($"Failed to start calibration: {ret}");
            is_calibrating = false;
            onFinish?.Invoke(); // 失败回调
            yield break;
        }

        Debug.Log("Calibration started.");

        int pointIndex = 1;

        for (int i = 0; i < max_points; i++)
        {
            //ballImages[i].gameObject.SetActive(true);
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
            yield return new WaitForSeconds(2f);

            _7i_point2d_t pt = new _7i_point2d_t
            {
                x = GetNormalizedScreenPos(ballImages[i]).x,
                y = GetNormalizedScreenPos(ballImages[i]).y
            };


            pointFinished = false;

            ret = ASeeTracker._7i_start_calibration_point(pointIndex, ref pt,
                Marshal.GetFunctionPointerForDelegate(processCB), (IntPtr)handle,
                Marshal.GetFunctionPointerForDelegate(finishCB), (IntPtr)handle);

            Debug.Log($"_7i_start_calibration_point: {pointIndex}, {ret}");

            ++pointIndex;

            // 等待当前点校准完成
            yield return new WaitUntil(() => pointFinished);

            // 等待 10 秒（用于用户注视）
            //yield return new WaitForSeconds(2f);
        }

        //// 如果不是最后点被终止
        //if (cur_index != 5)
        //{
        //    ret = ASeeTracker._7i_cancel_calibration();
        //    Debug.Log($"_7i_cancel_calibration: {ret}");
        //}

        int calibration_success = ASeeTracker._7i_compute_calibration(ref coefficient);
        Debug.Log($"_7i_compute_calibration: {calibration_success}");

        ret = ASeeTracker._7i_complete_calibration();
        Debug.Log($"_7i_complete_calibration: {ret}");

        float left_score = 0, right_score = 0;
        ASeeTracker._7i_get_calibration_score(ref left_score, ref right_score);
        Debug.Log($"Calibration Scores - Left: {left_score}, Right: {right_score}");

        //if (left_score == 0 || right_score == 0)
        //{
        //    Debug.LogError("Calibration failed, scores are zero.");
        //    //calibration_success = -1; // 标记校准失败
        //}


        Debug.Log("Calibration done.");

        if (calibration_success == 0)
        {
            //CompareCoefficients(coefficient);

            //gazeDot.gameObject.SetActive(true); // 设置 alpha 为 0
            //ret = ASeeTracker._7i_start_tracking(ref coefficient);
            ////startTrace();
            //Debug.Log($"_7i_start_tracking: {ret}");
            //is_tracing = true;

            File.WriteAllBytes(coe_file_name, coefficient.buf);
        }

        is_all_points_finished = true;
        is_calibrating = false;


        //校准结束UI恢复到初始状态
        foreach (Image img in ballImages)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false); ; // 设置 alpha 为 0
                img.rectTransform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
            }

        }

        foreach (ScaleState states in scaleStates)
        {
            if (states != null)
            {
                states.targetScale = scaleFactor; // 重置缩放目标
            }

        }

        

        UIBackground.gameObject.SetActive(false); // 设置 alpha 为 0

        // ★★★ 关键：在这里调用回调 ★★★
        Debug.Log("校准协程结束，调用回调");
        onFinish?.Invoke();
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

        onComplete?.Invoke(); //移动完成时调用回调
    }


    /// <summary>
    /// 获取 UI Image 在屏幕上的位置（单位：像素坐标）
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

        Debug.Log($"GetScreenPosition get pos: x:{screenPos.x} y:{screenPos.y}");

        //unity的左下角原点要到左上角去
        screenPos = BottomLeftToTopLeft(screenPos);
        Debug.Log($"GetScreenPosition after get BottomLeftToTopLeft pos: x:{screenPos.x} y:{screenPos.y}");

        return screenPos;
    }

    public static Vector2 GetNormalizedScreenPos(Image image)
    {
        if (image == null) return Vector2.zero;

        RectTransform rt = image.rectTransform;
        Canvas canvas = image.canvas;
        if (rt == null || canvas == null) return Vector2.zero;

        Vector2 screenPos;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Overlay 直接就是屏幕坐标
            screenPos = rt.position;
        }
        else
        {
            // ScreenSpace-Camera / WorldSpace
            screenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rt.position);
        }

        // Unity screen origin = bottom-left → SDK 需要 top-left
        screenPos.y = Screen.height - screenPos.y;

        // ---- 关键：转换成 SDK 要的 0~1 归一化坐标 ----
        float normX = Mathf.Clamp01(screenPos.x / Screen.width);
        float normY = Mathf.Clamp01(screenPos.y / Screen.height);

        return new Vector2(normX, normY);
    }



    //开始追踪
    public void startTrace()
    {
        if (!running)
            return;


        if (is_tracing)
        {
            UnityEngine.Debug.Log("已经在追踪中了");
            return;
        }

       

        _7i_coefficient_t coefficient;

        if (LoadCalibration(out coefficient))
        {
            Debug.Log("成功加载校准数据");
            if (coefficient.buf == null)
            {
                Debug.LogWarning("a coefficient 未初始化（buf 为 null）");
                return;
            }
            gazeDot.gameObject.SetActive(true); // 设置 alpha 为 0
            int ret = ASeeTracker._7i_start_tracking(ref coefficient);
            is_tracing = true;
            Console.WriteLine("_7i_start_tracking: {0:D}", ret);
        }
        else
        {
            Debug.LogError("未找到或读取失败，需要重新校准");
            return;
        }
    }

    //停止追踪
    public void stopTrace()
    {
        if (!running && !is_tracing)
            return;


        ASeeTracker._7i_stop_tracking();
        gazeDot.gameObject.SetActive(false); // 设置 alpha 为 0
        is_tracing = false;
    }

    //读取校准系数
    public bool LoadCalibration(out _7i_coefficient_t coefficient)
    {
        coefficient = new _7i_coefficient_t();
        coefficient.buf = new byte[2048];

        if (File.Exists(coe_file_name))
        {
            byte[] data = File.ReadAllBytes(coe_file_name);
            int length = Mathf.Min(data.Length, 2048);
            Array.Copy(data, coefficient.buf, length);
            return true;
        }
        else
        {
            return false;
        }
    }

    //检查配置文件
    public bool CheckCoeFile()
    {

        if (File.Exists(coe_file_name))
        {
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

            Debug.Log($"scaleFactor_screen scaleFactor: {scaleFactor_screen}, rt.sizeDelta:{rt.sizeDelta}");
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

    //上部中间
    public static void SetAnchorTopCenter(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(baseOffset.x, -baseOffset.y) * GetScreenScale();

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f) scaleFactor_screen *= scaleFactor;
        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }

    //左边中间
    public static void SetAnchorMiddleLeft(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(baseOffset.x, baseOffset.y) * GetScreenScale();

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f) scaleFactor_screen *= scaleFactor;
        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }

    //右边中间
    public static void SetAnchorMiddleRight(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-baseOffset.x, baseOffset.y) * GetScreenScale();

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f) scaleFactor_screen *= scaleFactor;
        rt.sizeDelta = rt.sizeDelta * scaleFactor_screen;
    }

    //下部中间
    public static void SetAnchorBottomCenter(RectTransform rt, Vector2 baseOffset)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(baseOffset.x, baseOffset.y) * GetScreenScale();

        float scaleFactor_screen = GetScreenScale();
        if (scaleFactor_screen > 1.0f) scaleFactor_screen *= scaleFactor;
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
            Debug.Log("两个 coefficient 完全一致");
        }
        else
        {
            Debug.Log($"coefficient 有 {diffCount} 处不同");
        }
    }

    //正常小朋友切换注视点图片
    public void SetGazePointSprite(bool useNew)
    {
        if (gazeDot == null) return;

        // 如果还没存过原图，就先存下来（懒加载）
        if (originalGazeSprite == null)
            originalGazeSprite = gazeDot.sprite;

        // 如果没有指定新图，就用原图
        Sprite targetSprite = (useNew && customGazeSprite != null) ? customGazeSprite : originalGazeSprite;

        gazeDot.sprite = targetSprite;

        // 可选：如果是自定义图片，可能需要调整一下Native Size
        // if (useNew) gazeDot.SetNativeSize(); 
    }
}



