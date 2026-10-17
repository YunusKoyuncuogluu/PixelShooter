using System;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

namespace PixelShooter.Pooling
{
    public class ObjectPool : MonoBehaviour
    {
        [Serializable]
        public class Pool
        {
            public PoolTags tag;
            public GameObject prefab;
            public int size;
            [HideInInspector] public Transform poolParent;
        }

        [SerializeField] private List<Pool> pools = new List<Pool>();

        private Transform poolRoot;
        private Dictionary<PoolTags, Queue<GameObject>> poolDictionary;

        [Inject]
        private void Construct()
        {
            PreparePools();
        }

        private void PreparePools()
        {
            if (poolDictionary != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("POOL_PARENT");
            rootObject.transform.SetParent(transform, false);
            poolRoot = rootObject.transform;
            poolDictionary = new Dictionary<PoolTags, Queue<GameObject>>(pools.Count);

            for (int i = 0; i < pools.Count; i++)
            {
                Pool pool = pools[i];
                if (pool == null || pool.tag == PoolTags.None || pool.prefab == null || pool.size <= 0)
                {
                    continue;
                }

                if (poolDictionary.ContainsKey(pool.tag))
                {
                    continue;
                }

                GameObject parentObject = new GameObject(pool.tag.ToString());
                pool.poolParent = parentObject.transform;
                pool.poolParent.SetParent(poolRoot, false);

                Queue<GameObject> objectPool = new Queue<GameObject>(pool.size);
                for (int j = 0; j < pool.size; j++)
                {
                    GameObject instance = CreateInstance(pool);
                    instance.name = pool.prefab.name + " clone " + j;
                    objectPool.Enqueue(instance);
                }

                poolDictionary.Add(pool.tag, objectPool);
            }
        }

        public GameObject SpawnFromPool(PoolTags poolTag, Vector3 position, Quaternion rotation)
        {
            if (poolDictionary == null || !poolDictionary.ContainsKey(poolTag))
            {
                return null;
            }

            Queue<GameObject> queue = poolDictionary[poolTag];
            if (queue.Count == 0)
            {
                SpawnNewObject(poolTag, 1);
            }

            if (queue.Count == 0)
            {
                return null;
            }

            GameObject objectToSpawn = queue.Dequeue();
            objectToSpawn.transform.SetPositionAndRotation(position, rotation);
            objectToSpawn.SetActive(true);
            return objectToSpawn;
        }

        public void DestroyToPool(PoolTags poolTag, GameObject poolObject)
        {
            if (poolObject == null || poolDictionary == null || !poolDictionary.ContainsKey(poolTag))
            {
                return;
            }

            poolObject.SetActive(false);
            IPoolObject poolBehaviour = poolObject.GetComponent<IPoolObject>();
            if (poolBehaviour != null)
            {
                poolBehaviour.Reset();
            }

            poolDictionary[poolTag].Enqueue(poolObject);
        }

        private void SpawnNewObject(PoolTags poolTag, int count)
        {
            for (int i = 0; i < pools.Count; i++)
            {
                if (pools[i] == null || pools[i].tag != poolTag)
                {
                    continue;
                }

                for (int j = 0; j < count; j++)
                {
                    GameObject instance = CreateInstance(pools[i]);
                    poolDictionary[poolTag].Enqueue(instance);
                }

                break;
            }
        }

        private static GameObject CreateInstance(Pool pool)
        {
            GameObject instance = Instantiate(pool.prefab, pool.poolParent, false);
            instance.SetActive(false);
            return instance;
        }
    }

    public enum PoolTags
    {
        None = 0,
        Bullet = 1,
        AudioVoice = 2,
        TravelSpline = 3
    }

    public interface IPoolObject
    {
        void Reset();
    }
}
