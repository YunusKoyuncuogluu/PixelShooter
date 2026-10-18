using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
        private Dictionary<PoolTags, List<GameObject>> poolInstances;

        [Inject]
        private void Construct()
        {
            PreparePools();
        }

        private void PreparePools()
        {
            if (poolDictionary != null)
                return;

            GameObject rootObject = new GameObject("POOL_PARENT");
            rootObject.transform.SetParent(transform, false);
            poolRoot = rootObject.transform;
            poolDictionary = new Dictionary<PoolTags, Queue<GameObject>>(pools.Count);
            poolInstances = new Dictionary<PoolTags, List<GameObject>>(pools.Count);

            for (int i = 0; i < pools.Count; i++)
            {
                Pool pool = pools[i];
                if (pool == null || pool.tag == PoolTags.None || pool.prefab == null || pool.size <= 0)
                    continue;

                if (poolDictionary.ContainsKey(pool.tag))
                    continue;

                GameObject parentObject = new GameObject(pool.tag.ToString());
                pool.poolParent = parentObject.transform;
                pool.poolParent.SetParent(poolRoot, false);

                Queue<GameObject> objectPool = new Queue<GameObject>(pool.size);
                poolInstances.Add(pool.tag, new List<GameObject>(pool.size));

                for (int j = 0; j < pool.size; j++)
                {
                    GameObject instance = CreateInstance(pool);
                    instance.name = pool.prefab.name + " clone " + j;
                    objectPool.Enqueue(instance);
                }

                poolDictionary.Add(pool.tag, objectPool);
            }
        }

        public GameObject SpawnFromPool(PoolTags poolTag, Vector3 position, Quaternion rotation, bool activate = true)
        {
            if (poolDictionary == null || !poolDictionary.ContainsKey(poolTag))
                return null;

            Queue<GameObject> queue = poolDictionary[poolTag];

            while (queue.Count > 0)
            {
                GameObject objectToSpawn = queue.Dequeue();

                if (objectToSpawn == null)
                    continue;

                objectToSpawn.transform.SetPositionAndRotation(position, rotation);

                if (activate && !objectToSpawn.activeSelf)
                    objectToSpawn.SetActive(true);

                return objectToSpawn;
            }

            SpawnNewObject(poolTag, 1);

            if (queue.Count == 0)
                return null;

            GameObject created = queue.Dequeue();

            if (created == null)
                return null;

            created.transform.SetPositionAndRotation(position, rotation);

            if (activate && !created.activeSelf)
                created.SetActive(true);

            return created;
        }

        public void DestroyToPool(PoolTags poolTag, GameObject poolObject, bool deactivate = true)
        {
            if (poolObject == null || poolDictionary == null || !poolDictionary.ContainsKey(poolTag))
                return;

            Transform parent = FindPoolParent(poolTag);

            if (parent != null)
                poolObject.transform.SetParent(parent, false);

            if (deactivate && poolObject.activeSelf)
                poolObject.SetActive(false);
            IPoolObject poolBehaviour = poolObject.GetComponent<IPoolObject>();

            if (poolBehaviour != null)
                poolBehaviour.Reset();

            poolDictionary[poolTag].Enqueue(poolObject);
        }

        private void SpawnNewObject(PoolTags poolTag, int count)
        {
            for (int i = 0; i < pools.Count; i++)
            {
                if (pools[i] == null || pools[i].tag != poolTag)
                    continue;

                for (int j = 0; j < count; j++)
                {
                    GameObject instance = CreateInstance(pools[i]);
                    poolDictionary[poolTag].Enqueue(instance);
                }

                break;
            }
        }

        private GameObject CreateInstance(Pool pool)
        {
            GameObject instance = Instantiate(pool.prefab, pool.poolParent, false);
            instance.SetActive(false);

            if (poolInstances != null && poolInstances.TryGetValue(pool.tag, out List<GameObject> instances))
                instances.Add(instance);

            return instance;
        }

        private Transform FindPoolParent(PoolTags poolTag)
        {
            for (int i = 0; i < pools.Count; i++)
            {
                Pool pool = pools[i];

                if (pool == null || pool.tag != poolTag)
                    continue;

                return pool.poolParent;
            }

            return null;
        }

        private void OnEnable()
        {
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene unusedScene)
        {
            RecallActive();
        }

        private void RecallActive()
        {
            if (poolInstances == null)
                return;

            for (int i = 0; i < pools.Count; i++)
            {
                Pool pool = pools[i];

                if (pool == null || !poolInstances.TryGetValue(pool.tag, out List<GameObject> instances))
                    continue;

                for (int j = instances.Count - 1; j >= 0; j--)
                {
                    GameObject instance = instances[j];

                    if (instance == null)
                    {
                        instances.RemoveAt(j);
                        continue;
                    }

                    if (!instance.activeSelf)
                        continue;

                    DestroyToPool(pool.tag, instance);
                }
            }
        }
    }

    public enum PoolTags
    {
        None = 0,
        Bullet = 1,
        AudioVoice = 2,
        TravelSpline = 3,
        PixelCube = 4
    }
}
