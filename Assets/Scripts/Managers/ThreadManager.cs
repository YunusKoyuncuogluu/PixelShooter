using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Zenject;

public class ThreadManager : MonoBehaviour
{
    [Inject] private Thread.Factory m_ThreadFactory;

    [Button]
    public void SpawnTest()
    {
        m_ThreadFactory.Create();
    }
}