    public class ObjectPooler : MonoBehaviour
    {

        private void Awake()
        {
            InitializeIfNeeded();
        }

        [SerializeField] private bool _addToDontDestroyOnLoad = false;

        // Parent object for all pools
        private static GameObject _emptyHolder;

        // Dictionary to store the pool for each prefab
        private static Dictionary<GameObject, ObjectPool<GameObject>> _objectPools;
        // Dictionary to map a spawned clone back to its original prefab
        private static Dictionary<GameObject, GameObject> _cloneToPrefabMap;
        // Dictionary to store parent objects for each pool type
        private static Dictionary<PoolType, GameObject> _poolParents;

        /// <summary>
        /// Enum defining the different types of pools available.
        /// Used to organize pooled objects under specific parent objects.
        /// </summary>
        public enum PoolType
        {
            Nodes,
            NodesSpawner,
            GameObjects,
            Billboards,
        }

        /// <summary>
        /// Initializes the pools and dictionaries if they haven't been initialized yet.
        /// Safe to call multiple times.
        /// </summary>
        private static void InitializeIfNeeded()
        {
            if (_objectPools != null) return;

            _objectPools = new Dictionary<GameObject, ObjectPool<GameObject>>();
            _cloneToPrefabMap = new Dictionary<GameObject, GameObject>();
            _poolParents = new Dictionary<PoolType, GameObject>();

            // Create the main holder for all pools
            if (_emptyHolder == null)
            {
                _emptyHolder = new GameObject("Object Pools");
                
                // Try to find the instance to check for DontDestroyOnLoad
                var instance = FindFirstObjectByType<ObjectPooler>();
                if (instance != null && instance._addToDontDestroyOnLoad)
                {
                    DontDestroyOnLoad(_emptyHolder);
                }
            }
        }

        /// <summary>
        /// Creates a new object pool for the specified prefab.
        /// </summary>
        /// <param name="prefab">The prefab to pool.</param>
        /// <param name="pos">Initial position for new objects.</param>
        /// <param name="rot">Initial rotation for new objects.</param>
        /// <param name="poolType">The type of pool, determining the parent container.</param>
        private static void CreatePool(GameObject prefab, Vector3 pos, Quaternion rot,
            PoolType poolType = PoolType.GameObjects)
        {
            InitializeIfNeeded();
            ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
                createFunc: () => CreateObject(prefab, pos, rot, poolType),
                actionOnGet: OnGetObject,
                actionOnRelease: OnReleaseObject,
                actionOnDestroy: OnDestroyObject
            );

            _objectPools.Add(prefab, pool);
        }

        /// <summary>
        /// Creates a new object pool for the specified prefab with a specific parent.
        /// </summary>
        /// <param name="prefab">The prefab to pool.</param>
        /// <param name="parent">The parent transform for new objects.</param>
        /// <param name="rot">Initial rotation for new objects.</param>
        /// <param name="poolType">The type of pool.</param>
        private static void CreatePool(GameObject prefab, Transform parent, Quaternion rot,
            PoolType poolType = PoolType.GameObjects)
        {
            InitializeIfNeeded();
            ObjectPool<GameObject> pool = new ObjectPool<GameObject>(
                createFunc: () => CreateObject(prefab, parent, rot, poolType),
                actionOnGet: OnGetObject,
                actionOnRelease: OnReleaseObject,
                actionOnDestroy: OnDestroyObject
            );

            _objectPools.Add(prefab, pool);
        }

        /// <summary>
        /// Instantiates a new object for the pool.
        /// </summary>
        private static GameObject CreateObject(GameObject prefab, Vector3 pos, Quaternion rot,
            PoolType poolType = PoolType.GameObjects)
        {
            prefab.SetActive(false); // Ensure prefab is inactive before instantiation

            GameObject obj = Instantiate(prefab, pos, rot);

            prefab.SetActive(true);

            GameObject parentObject = SetParentObject(poolType);
            obj.transform.SetParent(parentObject.transform);

            return obj;
        }

        /// <summary>
        /// Instantiates a new object for the pool with a specific parent.
        /// </summary>
        private static GameObject CreateObject(GameObject prefab, Transform parent, Quaternion rot,
            PoolType poolType = PoolType.GameObjects)
        {
            prefab.SetActive(false);

            GameObject obj = Instantiate(prefab, parent);

            // Handle RectTransform for UI elements, otherwise standard Transform
            if (obj.TryGetComponent<RectTransform>(out var rectTransform))
            {
                rectTransform.anchoredPosition = Vector2.zero;
                rectTransform.localRotation = rot;
                rectTransform.localScale = Vector3.one;
            }
            else
            {
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = rot;
                obj.transform.localScale = Vector3.one;
            }

            prefab.SetActive(true);

            return obj;
        }

        /// <summary>
        /// Called when an object is retrieved from the pool.
        /// </summary>
        private static void OnGetObject(GameObject obj)
        {
            // Optional logic when object is retrieved from pool (e.g., enabling it is handled in SpawnObject)
        }

        /// <summary>
        /// Called when an object is returned to the pool.
        /// </summary>
        private static void OnReleaseObject(GameObject obj)
        {
            // Logic when object is returned from pool
            obj.SetActive(false);
        }

        /// <summary>
        /// Called when the pool destroys an object (e.g., if the pool is full or cleared).
        /// </summary>
        private static void OnDestroyObject(GameObject obj)
        {
            if (_cloneToPrefabMap.ContainsKey(obj))
            {
                _cloneToPrefabMap.Remove(obj);
            }
        }

        /// <summary>
        /// Determines the parent GameObject based on the PoolType.
        /// Dynamically creates the parent if it doesn't exist.
        /// </summary>
        private static GameObject SetParentObject(PoolType poolType)
        {
            InitializeIfNeeded();

            if (_poolParents.TryGetValue(poolType, out GameObject parent))
            {
                if (parent != null) return parent;
                _poolParents.Remove(poolType); // Remove null reference if object was destroyed
            }

            // Create new parent for this pool type
            string parentName = poolType.ToString();
            GameObject newParent = new GameObject(parentName);
            newParent.transform.SetParent(_emptyHolder.transform);
            
            _poolParents[poolType] = newParent;
            
            return newParent;
        }

        /// <summary>
        /// Spawns an object from the pool. Creates a new pool if one doesn't exist for the prefab.
        /// </summary>
        /// <typeparam name="T">The component type to return.</typeparam>
        /// <param name="objectToSpawn">The prefab to spawn.</param>
        /// <param name="spawnPos">The position to spawn at.</param>
        /// <param name="spawnRotation">The rotation to spawn with.</param>
        /// <param name="poolType">The type of pool.</param>
        /// <returns>The component of type T on the spawned object, or null if not found.</returns>
        private static T SpawnObject<T>(GameObject objectToSpawn, Vector3 spawnPos, Quaternion spawnRotation,
            PoolType poolType = PoolType.GameObjects) where T : Component
        {
            InitializeIfNeeded();

            if (!_objectPools.ContainsKey(objectToSpawn))
            {
                CreatePool(objectToSpawn, spawnPos, spawnRotation, poolType);
            }

            GameObject obj = _objectPools[objectToSpawn].Get();

            if (obj != null)
            {
                if (!_cloneToPrefabMap.ContainsKey(obj))
                {
                    _cloneToPrefabMap.Add(obj, objectToSpawn);
                }

                obj.transform.position = spawnPos;
                obj.transform.rotation = spawnRotation;
                obj.SetActive(true);

                if (typeof(T) == typeof(GameObject))
                {
                    return obj as T;
                }

                T component = obj.GetComponent<T>();
                if (component == null)
                {
                    Debug.LogError($"Object {objectToSpawn.name} doesn't have a component of type {typeof(T)}");
                    return null;
                }

                return component;
            }

            return null;
        }

        /// <summary>
        /// Spawns an object from the pool using a component reference as the prefab.
        /// </summary>
        public static T SpawnObject<T>(T typePrefab, Vector3 spawnPos, Quaternion spawnRotation,
            PoolType poolType = PoolType.GameObjects) where T : Component
        {
            return SpawnObject<T>(typePrefab.gameObject, spawnPos, spawnRotation, poolType);
        }

        /// <summary>
        /// Spawns a GameObject from the pool.
        /// </summary>
        public static GameObject SpawnObject(GameObject objectToSpawn, Vector3 spawnPos, Quaternion spawnRotation,
            PoolType poolType = PoolType.GameObjects)
        {
            var result = SpawnObject<Transform>(objectToSpawn, spawnPos, spawnRotation, poolType);
            return result != null ? result.gameObject : null;
        }

        /// <summary>
        /// Spawns an object from the pool and sets it as a child of the specified parent.
        /// </summary>
        private static T SpawnObject<T>(GameObject objectToSpawn, Transform parent, Quaternion spawnRotation,
            PoolType poolType = PoolType.GameObjects) where T : Component
        {
            InitializeIfNeeded();

            if (!_objectPools.ContainsKey(objectToSpawn))
            {
                CreatePool(objectToSpawn, parent, spawnRotation, poolType);
            }

            GameObject obj = _objectPools[objectToSpawn].Get();

            if (obj != null)
            {
                if (!_cloneToPrefabMap.ContainsKey(obj))
                {
                    _cloneToPrefabMap.Add(obj, objectToSpawn);
                }

                obj.transform.SetParent(parent);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = spawnRotation;
                obj.SetActive(true);

                if (typeof(T) == typeof(GameObject))
                {
                    return obj as T;
                }

                T component = obj.GetComponent<T>();
                if (component == null)
                {
                    Debug.LogError($"Object {objectToSpawn.name} doesn't have a component of type {typeof(T)}");
                    return null;
                }

                return component;
            }

            return null;
        }

                /// <summary>
        /// Pre-warms the pool with a set number of objects.
        /// </summary>
        /// <param name="prefab">The prefab to pool.</param>
        /// <param name="count">The number of objects to create.</param>
        /// <param name="poolType">The type of pool.</param>
        public static void InitializePool(GameObject prefab, int count, PoolType poolType = PoolType.GameObjects)
        {
            InitializeIfNeeded();

            if (!_objectPools.ContainsKey(prefab))
            {
                CreatePool(prefab, Vector3.zero, Quaternion.identity, poolType);
            }

            List<GameObject> tempObjects = new List<GameObject>(count);

            // Spawn objects to fill the pool
            for (int i = 0; i < count; i++)
            {
                GameObject obj = _objectPools[prefab].Get();
                tempObjects.Add(obj);
            }

            // Return them immediately so they are ready for use
            foreach (GameObject obj in tempObjects)
            {
                _objectPools[prefab].Release(obj);
            }
        }

        /// <summary>
        /// Spawns an object from the pool using a component reference and sets it as a child of the specified parent.
        /// </summary>
        public static T SpawnObject<T>(T typePrefab, Transform parent, Quaternion spawnRotation,
            PoolType poolType = PoolType.GameObjects) where T : Component
        {
            return SpawnObject<T>(typePrefab.gameObject, parent, spawnRotation, poolType);
        }

        /// <summary>
        /// Spawns a GameObject from the pool and sets it as a child of the specified parent.
        /// </summary>
        public static GameObject SpawnObject(GameObject objectToSpawn, Transform parent, Quaternion spawnRotation,
            PoolType poolType = PoolType.GameObjects)
        {
            // Call the private implementation directly with Transform component
            // and then return the gameObject
            var result = SpawnObject<Transform>(objectToSpawn, parent, spawnRotation, poolType);
            return result != null ? result.gameObject : null;
        }


        /// <summary>
        /// Returns an active object to its pool.
        /// </summary>
        /// <param name="obj">The object to return.</param>
        /// <param name="poolType">The type of pool it belongs to (optional).</param>
        public static void ReturnObjectToPool(GameObject obj, PoolType poolType = PoolType.GameObjects)
        {
            InitializeIfNeeded();

            if (_cloneToPrefabMap.TryGetValue(obj, out GameObject prefab))
            {
                GameObject parentObject = SetParentObject(poolType);

                if (obj.transform.parent != parentObject.transform)
                {
                    obj.transform.SetParent(parentObject.transform);
                }

                if (_objectPools.TryGetValue(prefab, out ObjectPool<GameObject> pool))
                {
                    pool.Release(obj);
                }
            }
            else
            {
                Debug.LogWarning("Trying to return an object that is not pooled: " + obj.name);
            }
        }
    }
}