using UnityEngine;
using UnityEngine.UI;

public class AttentionBlurDebugPreview : MonoBehaviour
{
    [Header("Debug Control")]
    [Range(0, 100)]
    public int debugAttention = 50;

    public bool useSlider = true;
    public Slider attentionSlider;   // 可选：拖一个UI Slider进来

    [Header("Apply To")]
    public AttentionBlurUI target;   // 如果为空，会自动用 AttentionBlurUI.Instance
    public bool applyEveryFrame = true;

    private void Start()
    {
        if (attentionSlider != null)
        {
            attentionSlider.minValue = 0;
            attentionSlider.maxValue = 100;
            attentionSlider.wholeNumbers = true;
            attentionSlider.value = debugAttention;
            attentionSlider.onValueChanged.AddListener(v => debugAttention = Mathf.RoundToInt(v));
        }

        ApplyOnce();
    }

    private void Update()
    {
        // 键盘快速调试：↑/↓ 每次+/-1，PageUp/PageDown 每次+/-10
        if (Input.GetKeyDown(KeyCode.UpArrow)) debugAttention = Mathf.Clamp(debugAttention + 1, 0, 100);
        if (Input.GetKeyDown(KeyCode.DownArrow)) debugAttention = Mathf.Clamp(debugAttention - 1, 0, 100);
        if (Input.GetKeyDown(KeyCode.PageUp)) debugAttention = Mathf.Clamp(debugAttention + 10, 0, 100);
        if (Input.GetKeyDown(KeyCode.PageDown)) debugAttention = Mathf.Clamp(debugAttention - 10, 0, 100);

        if (attentionSlider != null && useSlider)
            attentionSlider.value = debugAttention;

        if (applyEveryFrame) ApplyOnce();
    }

    [ContextMenu("Apply Once")]
    public void ApplyOnce()
    {
        var t = target != null ? target : AttentionBlurUI.Instance;
        if (t != null) t.SetAttention(debugAttention);
    }
}
