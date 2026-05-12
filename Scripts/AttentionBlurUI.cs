using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttentionBlurUI : MonoBehaviour
{
    public static AttentionBlurUI Instance { get; private set; }

    public enum FeedbackMode
    {
        AlphaTransparency, // 透明度模式
        GaussianBlur       // 高斯模糊模式
    }

    public FeedbackMode currentMode = FeedbackMode.AlphaTransparency;

    [Header("Targets")]
    [Tooltip("需要改变透明度的目标图片（如草莓）。背景图不要拖进去。")]
    [SerializeField] private Image targetImage;

    // 为了兼容你之前可能拖拽了imageA，保留这个变量名，但在Awake里做处理
    [HideInInspector] public Image imageA;
    [HideInInspector] public Image imageB; // 废弃，不再处理背景

    [Header("Settings")]
    [Range(0, 100)]
    [SerializeField] private int clearThreshold = 50; // >= 这个值 → 完全不透明(Alpha=1)

    [Tooltip("最低透明度(0-1)，防止变成0完全看不见，建议设为0.1或0")]
    [SerializeField] private float minAlpha = 0.3f;

    [Tooltip("注意力为0时的最大模糊强度 (Shader中的 _BlurSize)")]
    [SerializeField] private float maxBlurSize = 6f;

    private int _lastAttention = 0;
    private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 兼容旧设置：如果你之前把草莓拖在 imageA 上，自动赋值给 targetImage
        if (targetImage == null && imageA != null)
        {
            targetImage = imageA;
        }
        ResetEffects();
        // 初始化
        ApplyFeedback(_lastAttention);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>外部调用：更新注意力值(0-100)</summary>
    public void SetAttention(int attention)
    {
        _lastAttention = Mathf.Clamp(attention, 0, 100);
        ApplyFeedback(_lastAttention);
    }

    public void ToggleBlurMode(bool useBlur)
    {
        FeedbackMode newMode = useBlur ? FeedbackMode.GaussianBlur : FeedbackMode.AlphaTransparency;

        if (currentMode == newMode) return;

        currentMode = newMode;
        ResetEffects(); // 切换模式时，必须先把之前的残留效果清空
        ApplyFeedback(_lastAttention); // 重新计算并应用新效果

        Debug.Log($"反馈模式已切换至: {currentMode}");
    }

    private void ResetEffects()
    {
        if (targetImage == null) return;

        // 1. 恢复完全不透明
        Color c = targetImage.color;
        c.a = 1f;
        targetImage.color = c;

        // 2. 恢复完全不模糊
        if (targetImage.material != null)
        {
            targetImage.material.SetFloat(BlurSizeId, 0f);
        }
    }

    private void ApplyFeedback(int attention)
    {
        if (targetImage == null) return;

        // 1. 计算分心比例 t (0 到 1)
        // attention >= clearThreshold 时，t = 0 (完全专注)
        // attention = 0 时，t = 1 (完全分心)
        float t = 0f;
        if (attention < clearThreshold)
        {
            t = Mathf.InverseLerp(clearThreshold, 0f, attention);
        }

        // 2. 根据当前模式，应用对应的视觉效果
        if (currentMode == FeedbackMode.AlphaTransparency)
        {
            // 透明度映射：t 从 0->1，Alpha 从 1f->minAlpha
            float alpha = Mathf.Lerp(1f, minAlpha, t);
            Color c = targetImage.color;
            c.a = alpha;
            targetImage.color = c;
        }
        else if (currentMode == FeedbackMode.GaussianBlur)
        {
            // 模糊映射：t 从 0->1，BlurSize 从 0f->maxBlurSize
            if (targetImage.material != null)
            {
                float blur = Mathf.Lerp(0f, maxBlurSize, t);
                targetImage.material.SetFloat(BlurSizeId, blur);
            }
        }
    }
}