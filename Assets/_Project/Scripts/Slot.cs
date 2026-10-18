using UnityEngine;
using Zenject;
using UnityEngine.Serialization;

public class Slot : MonoBehaviour
{
    private const float WrongFadeSpeed = 2.85f;

    [FormerlySerializedAs("m_LockedTransform")]
    [SerializeField] private Transform lockedTransform;
    [SerializeField] private SpriteRenderer wrongRenderer;
    [SerializeField, Range(0f, 1f)] private float wrongMaxAlpha = 1f;
    [SerializeField, Min(0f)] private float wrongPulseSpeed = 1.8f;

    private bool isLocked;
    private bool isFull;
    private int wrongPhase;
    private float wrongAlpha;
    private Color wrongColor;

    public bool IsLocked => this.isLocked;
    public bool IsFull => this.isFull;
    public bool WrongWarningBusy => wrongPhase != 0;

    private Transform cachedTransform;

    public void Construct(Vector3 localPosition)
    {
        cachedTransform = transform;
        cachedTransform.localPosition = localPosition;
        lockedTransform.gameObject.SetActive(this.isLocked);

        if (wrongRenderer == null)
            return;

        wrongColor = wrongRenderer.color;
        wrongPhase = 0;
        wrongRenderer.gameObject.SetActive(false);
    }

    public void SetLocked(bool isLocked) => this.isLocked = isLocked;
    public void SetFull(bool isFull) => this.isFull = isFull;

    public void SetWrongWarning(bool visible)
    {
        if (wrongRenderer == null || isLocked)
            visible = false;

        if (visible)
        {
            if (wrongPhase == 1)
                return;

            wrongRenderer.gameObject.SetActive(true);
            wrongPhase = 1;
            return;
        }

        if (wrongPhase != 1)
            return;

        wrongPhase = 2;
    }

    public void TickWrongWarning(float deltaTime)
    {
        if (wrongPhase == 0 || wrongRenderer == null)
            return;

        if (wrongPhase == 1)
        {
            wrongAlpha = Mathf.PingPong(Time.time * wrongPulseSpeed, 1f) * wrongMaxAlpha;
        }
        else
        {
            wrongAlpha -= deltaTime * wrongMaxAlpha * WrongFadeSpeed;
            if (wrongAlpha <= 0f)
            {
                wrongAlpha = 0f;
                wrongPhase = 0;
                wrongRenderer.gameObject.SetActive(false);
            }
        }

        wrongColor.a = wrongAlpha;
        wrongRenderer.color = wrongColor;
    }

    public class Factory : PlaceholderFactory<Slot>
    {
    }
}
