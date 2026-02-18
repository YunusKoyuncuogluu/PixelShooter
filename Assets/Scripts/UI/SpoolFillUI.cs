using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makaranın altında doluluk gösterir: Slider + % metni. Hedef yüzde lerp ile yumuşak güncellenir.
/// Her kare kameraya döner (billboard). Başlangıçta kapalı olmalı; slota gelme animasyonu bitince açılır.
/// </summary>
public class SpoolFillUI : MonoBehaviour
{
    [SerializeField] private Slider m_Slider;
    [SerializeField] private Image m_FillImage;
    [SerializeField] private TextMeshProUGUI m_PercentText;
    [SerializeField] private float m_LerpDuration = 0.4f;
    [SerializeField] private Camera m_Camera;

    private float m_DisplayPercent;
    private Tween m_LerpTween;

    private void Awake()
    {
        if (m_Camera == null)
            m_Camera = Camera.main;
        if (m_FillImage != null)
        {
            m_FillImage.type = Image.Type.Filled;
            m_FillImage.fillMethod = Image.FillMethod.Horizontal;
            m_FillImage.fillAmount = 0f;
        }
        if (m_Slider != null)
            m_Slider.value = 0f;
        SetPercentText("%0");
        m_DisplayPercent = 0f;
    }

    private void OnEnable()
    {
        ApplyDisplay();
    }

    private void LateUpdate()
    {
        if (!gameObject.activeInHierarchy)
            return;
        if (m_Camera == null)
            m_Camera = Camera.main;
        if (m_Camera != null && transform != null)
            transform.rotation = Quaternion.LookRotation(transform.position - m_Camera.transform.position);
    }

    /// <summary>
    /// Hedef doluluk yüzdesini ayarlar (0–100). durationOverride > 0 ise lerp süresi onu kullanır (sarma ile eşzamanlı UI için).
    /// </summary>
    public void SetTargetPercent(float targetPercent, float durationOverride = -1f)
    {
        float target = Mathf.Clamp(targetPercent, 0f, 100f);
        float duration = durationOverride > 0f ? durationOverride : m_LerpDuration;
        m_LerpTween?.Kill();
        ApplyDisplay();
        m_LerpTween = DOTween.To(
                () => m_DisplayPercent,
                x =>
                {
                    m_DisplayPercent = x;
                    ApplyDisplay();
                },
                target,
                duration)
            .SetEase(Ease.Linear);
    }

    private void SetPercentText(string s)
    {
        if (m_PercentText != null)
            m_PercentText.text = s;
    }

    private void ApplyDisplay()
    {
        float normalized = Mathf.Clamp01(m_DisplayPercent / 100f);
        if (m_Slider != null)
            m_Slider.value = normalized;
        if (m_FillImage != null)
            m_FillImage.fillAmount = normalized;
        int percentInt = Mathf.RoundToInt(m_DisplayPercent);
        SetPercentText("%" + percentInt);
    }

    private void OnDestroy()
    {
        m_LerpTween?.Kill();
    }
}
