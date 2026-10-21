using Grids;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

public class TunnelVisual : MonoBehaviour
{
    [SerializeField] private Transform tunnelModel;
    [SerializeField] private TextMeshPro countText;
    [FormerlySerializedAs("mouthFlex")]
    [SerializeField] private float mouthPulse = 0.2f;
    [FormerlySerializedAs("lengthFlex")]
    [SerializeField] private float lengthPulse = 0.08f;

    private Vector3 modelScale;
    private Vector3 modelPosition;
    private bool modelPoseReady;

    public void Construct(Direction direction, int count)
    {
        Face(direction);
        SetCount(count);
    }

    public void ApplyExit(float time)
    {
        if (tunnelModel == null || !modelPoseReady)
            return;

        float clamped = time;
        if (clamped < 0f)
            clamped = 0f;
        if (clamped > 1f)
            clamped = 1f;

        float squeeze = mouthPulse > 0f ? mouthPulse : 0f;
        float stretch = lengthPulse > 0f ? lengthPulse : 0f;
        float mouth;
        float length;

        if (clamped < 0.16f)
        {
            float blend = SmoothStep(clamped / 0.16f);
            mouth = Mathf.LerpUnclamped(1f, 1f - squeeze, blend);
            length = Mathf.LerpUnclamped(1f, 1f - stretch, blend);
        }
        else if (clamped < 0.42f)
        {
            float blend = SmoothStep((clamped - 0.16f) / 0.26f);
            mouth = Mathf.LerpUnclamped(1f - squeeze, 1f + squeeze * 1.35f, blend);
            length = Mathf.LerpUnclamped(1f - stretch, 1f + stretch * 1.6f, blend);
        }
        else if (clamped < 0.7f)
        {
            float blend = SmoothStep((clamped - 0.42f) / 0.28f);
            mouth = Mathf.LerpUnclamped(1f + squeeze * 1.35f, 1f - squeeze * 0.55f, blend);
            length = Mathf.LerpUnclamped(1f + stretch * 1.6f, 1f - stretch * 0.25f, blend);
        }
        else
        {
            float blend = SmoothStep((clamped - 0.7f) / 0.3f);
            mouth = Mathf.LerpUnclamped(1f - squeeze * 0.55f, 1f, blend);
            length = Mathf.LerpUnclamped(1f - stretch * 0.25f, 1f, blend);
        }

        tunnelModel.localScale = new Vector3(modelScale.x * mouth, modelScale.y * mouth, modelScale.z * length);
        tunnelModel.localPosition = modelPosition + new Vector3(0f, 0f, (length - 1f) * 0.18f);
    }

    public void RestoreModel()
    {
        if (tunnelModel == null || !modelPoseReady)
            return;

        tunnelModel.localScale = modelScale;
        tunnelModel.localPosition = modelPosition;
    }

    public void SetCount(int count)
    {
        if (countText == null)
            return;

        countText.text = count.ToString();
    }

    private void Face(Direction direction)
    {
        if (tunnelModel == null)
            return;

        tunnelModel.gameObject.SetActive(true);
        CacheModelPose();
        Vector3 facing = FacingOnPlane(direction);
        tunnelModel.localRotation = Quaternion.LookRotation(facing, Vector3.up);
    }

    private void CacheModelPose()
    {
        if (modelPoseReady)
            return;

        modelScale = tunnelModel.localScale;
        modelPosition = tunnelModel.localPosition;
        modelPoseReady = true;
    }

    private float SmoothStep(float time)
    {
        if (time < 0f)
            time = 0f;
        if (time > 1f)
            time = 1f;

        return time * time * (3f - 2f * time);
    }

    private void OnDisable()
    {
        RestoreModel();
    }

    private Vector3 FacingOnPlane(Direction direction)
    {
        if (direction == Direction.Left)
            return Vector3.left;

        if (direction == Direction.Right)
            return Vector3.right;

        if (direction == Direction.Down)
            return Vector3.back;

        return Vector3.forward;
    }
}
