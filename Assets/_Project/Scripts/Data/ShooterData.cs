using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public class ShooterData
{
    [FormerlySerializedAs("m_Color")]
    [SerializeField] private GeneralColor color;
    public GeneralColor Color => color;

    [SerializeField] private int shotCount;
    public int ShotCount => shotCount;

    private bool isQuestionShooter;
    public bool IsQuestionShooter => isQuestionShooter;

    private Vector2Int coordinate;
    public Vector2Int Coordinate => coordinate;

    private Vector3 position;
    public Vector3 Position => position;

    public ShooterData(GeneralColor color, bool isQuestionShooter, Vector2Int coordinate, Vector3 position, int shotCount = 0)
    {
        this.color = color;
        this.isQuestionShooter = isQuestionShooter;
        this.coordinate = coordinate;
        this.position = position;
        this.shotCount = shotCount > 0 ? shotCount : 0;
    }

    public void SetShotCount(int value) => shotCount = value > 0 ? value : 0;

    public void SetColor(GeneralColor value) => color = value;
}

[EnumToggleButtons]
public enum GeneralColor
{
    None,
    Red,
    Yellow,
    Blue,
    Orange,
    Aqua,
    Brown,
    Gray,
    Green,
    LightGreen,
    Null,
    Lilac,
    Pink,
    Purple,
    White,
    WoodenColor,
}
