using System;
using UnityEngine;

[Serializable]
public class ThreadData
{
    [SerializeField] private GeneralColor m_Color;
    public GeneralColor Color => m_Color;
    private bool m_IsQuestionThread;
    public bool IsQuestionThread => m_IsQuestionThread;
    private Vector2Int m_Coordinate;
    public Vector2Int Coordinate => m_Coordinate;
    private Vector3 m_Position;
    public Vector3 Position => m_Position;

    public ThreadData(GeneralColor color, bool isQuestionThread, Vector2Int cordinate, Vector3 position)
    {
        m_Color = color;
        m_IsQuestionThread = isQuestionThread;
        m_Coordinate = cordinate;
        m_Position = position;
    }
}

public enum GeneralColor
{
    None,
    Blue,
    Green,
    Orange,
    Pink,
    Purple,
    Red,
    Yellow
}