using Grids;
using TMPro;
using UnityEngine;

public class TunnelVisual : MonoBehaviour
{
    [SerializeField] private Transform tunnelModel;
    [SerializeField] private TextMeshPro countText;

    public void Construct(Direction direction, int count)
    {
        Face(direction);
        SetCount(count);
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
        Vector3 facing = FacingOnPlane(direction);
        tunnelModel.localRotation = Quaternion.LookRotation(facing, Vector3.up);
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
