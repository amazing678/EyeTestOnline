using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class AttentionUIText : MonoBehaviour
{
    [SerializeField] private Text attentionText;
    // 单例实例（方便其他脚本调用）
    public static AttentionUIText Instance { get; private set; }
    private void Awake()
    {
        // 确保全局唯一实例
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    void Start()
    {
        // 初始化显示
        UpdateAttentionText(0);
    }


    /// <summary>
    /// 更新注意力值显示（供外部调用）
    /// </summary>
    /// <param name="value">注意力数值</param>
    public void UpdateAttentionText(int value)
    {
        if (attentionText != null)
        {
            attentionText.text = $"当前注意力值：{value}";
        }
        else
        {
            Debug.LogWarning("请在Inspector中为AttentionUIManager赋值Text组件");
        }
    }
}
