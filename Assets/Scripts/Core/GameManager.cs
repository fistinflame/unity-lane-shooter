using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    public enum RunState
    {
        Idle,
        Running,
        Win,
        Fail
    }

    public static GameManager Instance { get; private set; }

    [Header("Core References")]
    [SerializeField] private SquadController squadController;
    [SerializeField] private PlayerInputController playerInputController;
    [SerializeField] private WeaponController weaponController;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private PowerupManager powerupManager;
    [SerializeField] private BossController bossController;
    [SerializeField] private UIController uiController;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private LevelGenerator levelGenerator;

    [Header("Optional Prefabs")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private Gate gatePrefab;
    [SerializeField] private Powerup powerupPrefab;
    [SerializeField] private UpgradeBox upgradeBoxPrefab;
    [SerializeField] private BossController bossPrefab;

    [Header("Data (ScriptableObjects)")]
    [SerializeField] private List<SoldierTier> soldierTiers = new List<SoldierTier>();
    [SerializeField] private List<EnemyType> enemyTypes = new List<EnemyType>();
    [SerializeField] private List<PowerupType> powerupTypes = new List<PowerupType>();
    [SerializeField] private List<LevelSegment> levelSegments = new List<LevelSegment>();

    [Header("Camera")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 11f, -12f);

    [Header("Environment")]
    [SerializeField] private GameObject bakerHousePrefab;
    [SerializeField] private List<GameObject> lowPolyTreePrefabs = new List<GameObject>();
    [SerializeField] private float environmentLength = 2600f;

    private readonly Dictionary<Material, Material> _decorMaterialOverrides = new Dictionary<Material, Material>();
    private Transform _runtimeRoot;
    private Camera _mainCamera;
    private float _levelEndZ = 100f;
    private bool _bossEncounterStarted;

    public RunState State { get; private set; } = RunState.Idle;
    public bool IsRunning => State == RunState.Running;
    public PowerupManager PowerupManager => powerupManager;
    public float GateImproveMultiplier => upgradeManager != null ? upgradeManager.GateImproveMultiplier : 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureDataDefaults();
        EnsureSceneSetup();
    }

    private void Start()
    {
        StartRun();
    }

    private void Update()
    {
        if (squadController == null)
        {
            return;
        }

        if (bossController != null && bossController.gameObject.activeInHierarchy)
        {
            if (!_bossEncounterStarted &&
                (squadController.CurrentRunZ >= bossController.transform.position.z - 95f || bossController.HealthNormalized < 0.999f))
            {
                _bossEncounterStarted = true;
            }

            uiController.SetBossVisible(_bossEncounterStarted);
            uiController.SetBossHp(bossController.HealthNormalized);
        }
        else
        {
            uiController.SetBossVisible(false);
        }

        if (weaponController != null)
        {
            uiController.SetFireRate(weaponController.CurrentFireRate);
        }

        if (powerupManager != null)
        {
            uiController.SetPiercingDuration(powerupManager.GetRemainingDuration(PowerupKind.Piercing));
            uiController.SetFireRateBoostDuration(powerupManager.GetRemainingDuration(PowerupKind.FireRateBoost));
        }
    }

    private void LateUpdate()
    {
        if (_mainCamera == null || squadController == null)
        {
            return;
        }

        Vector3 target = squadController.transform.position + cameraOffset;
        _mainCamera.transform.position = Vector3.Lerp(_mainCamera.transform.position, target, Time.deltaTime * 8f);
    }

    private void OnDestroy()
    {
        if (squadController != null)
        {
            squadController.SoldierCountChanged -= OnSoldierCountChanged;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public EnemyType GetEnemyTypeForDepth(int depth)
    {
        if (enemyTypes == null || enemyTypes.Count == 0)
        {
            return null;
        }

        return enemyTypes[0];
    }

    public PowerupType GetRandomPowerupType()
    {
        if (powerupTypes == null || powerupTypes.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;
        for (int i = 0; i < powerupTypes.Count; i++)
        {
            totalWeight += GetPowerupWeight(powerupTypes[i]);
        }

        float roll = Random.Range(0f, Mathf.Max(0.001f, totalWeight));
        for (int i = 0; i < powerupTypes.Count; i++)
        {
            float weight = GetPowerupWeight(powerupTypes[i]);
            roll -= weight;
            if (roll <= 0f)
            {
                return powerupTypes[i];
            }
        }

        return powerupTypes[powerupTypes.Count - 1];
    }

    public PowerupType GetPowerupTypeByKind(PowerupKind kind)
    {
        if (powerupTypes == null)
        {
            return null;
        }

        for (int i = 0; i < powerupTypes.Count; i++)
        {
            if (powerupTypes[i] != null && powerupTypes[i].kind == kind)
            {
                return powerupTypes[i];
            }
        }

        return null;
    }

    public void RegisterBoss(BossController boss, float bossZ)
    {
        bossController = boss;
        _levelEndZ = Mathf.Max(_levelEndZ, bossZ);
    }

    public void ApplyPlayerDamage(int amount)
    {
        if (!IsRunning || amount <= 0 || squadController == null)
        {
            return;
        }

        squadController.RemoveSoldiers(amount);
    }

    public void ApplyRunFireRateUpgrade(int levels)
    {
        if (!IsRunning || weaponController == null)
        {
            return;
        }

        weaponController.AddRunFireRateUpgradeLevel(levels);
    }

    public void OnBossDefeated()
    {
        if (!IsRunning)
        {
            return;
        }

        State = RunState.Win;
        weaponController.SetFiringEnabled(false);
        uiController.SetState(string.Empty);
        uiController.SetBossVisible(false);
        uiController.ShowEndMenu(true, StartRun, QuitGame);
    }

    public void FailRun()
    {
        FailRun(false);
    }

    public void FailRun(bool showEndMenu)
    {
        if (!IsRunning)
        {
            return;
        }

        State = RunState.Fail;
        weaponController.SetFiringEnabled(false);
        if (showEndMenu)
        {
            uiController.SetState(string.Empty);
            uiController.ShowEndMenu(false, StartRun, QuitGame);
        }
        else
        {
            uiController.HideVictoryMenu();
            uiController.SetState("FAILED");
        }

        uiController.SetBossVisible(false);
    }

    private void StartRun()
    {
        State = RunState.Idle;
        _bossEncounterStarted = false;

        weaponController.SetFiringEnabled(false);
        powerupManager.ResetRunEffects();
        weaponController.ResetForNewRun();
        enemySpawner.ResetForNewRun();
        levelGenerator.ClearLevel();
        bossController = null;

        squadController.ResetRunPosition();
        squadController.SetSoldierCount(upgradeManager.StartingSoldiers);

        _levelEndZ = levelGenerator.GenerateLevel(levelSegments);

        State = RunState.Running;
        weaponController.SetFiringEnabled(true);

        uiController.HideVictoryMenu();
        uiController.SetState(string.Empty);
        uiController.SetSoldierCount(squadController.LogicalSoldierCount);
        uiController.SetPlayerHealth(squadController.LogicalSoldierCount, squadController.LogicalSoldierCount);
        uiController.SetFireRate(weaponController.CurrentFireRate);
        uiController.SetPiercingDuration(0f);
        uiController.SetFireRateBoostDuration(0f);
        uiController.SetBossHp(1f);
        uiController.SetBossVisible(false);
    }

    private void OnSoldierCountChanged(int count)
    {
        uiController.SetSoldierCount(count);
        uiController.SetPlayerHealth(count, count);

        if (count <= 0)
        {
            FailRun(true);
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureSceneSetup()
    {
        if (_runtimeRoot == null)
        {
            var root = new GameObject("GeneratedWorld");
            _runtimeRoot = root.transform;
        }

        RuntimeVisualLibrary.EnsureLoaded();
        EnsureCameraAndLighting();
        EnsureGround();

        if (upgradeManager == null)
        {
            upgradeManager = GetComponent<UpgradeManager>();
            if (upgradeManager == null)
            {
                upgradeManager = gameObject.AddComponent<UpgradeManager>();
            }
        }

        if (powerupManager == null)
        {
            powerupManager = GetComponent<PowerupManager>();
            if (powerupManager == null)
            {
                powerupManager = gameObject.AddComponent<PowerupManager>();
            }
        }

        if (uiController == null)
        {
            var uiGo = new GameObject("UIController");
            uiGo.transform.SetParent(transform, false);
            uiController = uiGo.AddComponent<UIController>();
        }

        uiController.BuildIfNeeded();

        if (squadController == null)
        {
            var squadGo = new GameObject("Squad");
            squadGo.transform.SetParent(_runtimeRoot, false);
            squadController = squadGo.AddComponent<SquadController>();
        }

        if (playerInputController == null)
        {
            playerInputController = GetComponent<PlayerInputController>();
            if (playerInputController == null)
            {
                playerInputController = gameObject.AddComponent<PlayerInputController>();
            }
        }

        playerInputController.SetSquadController(squadController);

        if (weaponController == null)
        {
            weaponController = squadController.GetComponent<WeaponController>();
            if (weaponController == null)
            {
                weaponController = squadController.gameObject.AddComponent<WeaponController>();
            }
        }

        weaponController.Configure(squadController, powerupManager, upgradeManager, soldierTiers, projectilePrefab);

        if (enemySpawner == null)
        {
            var spawnerGo = new GameObject("EnemySpawner");
            spawnerGo.transform.SetParent(_runtimeRoot, false);
            enemySpawner = spawnerGo.AddComponent<EnemySpawner>();
        }

        enemySpawner.Configure(enemyPrefab, squadController, GetEnemyTypeForDepth(0));

        if (levelGenerator == null)
        {
            var levelGenGo = new GameObject("LevelGenerator");
            levelGenGo.transform.SetParent(_runtimeRoot, false);
            levelGenerator = levelGenGo.AddComponent<LevelGenerator>();
        }

        levelGenerator.Configure(this, enemySpawner, squadController, gatePrefab, powerupPrefab, bossPrefab, upgradeBoxPrefab, _runtimeRoot);

        squadController.SoldierCountChanged -= OnSoldierCountChanged;
        squadController.SoldierCountChanged += OnSoldierCountChanged;
    }

    private void EnsureCameraAndLighting()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            _mainCamera = cameraGo.AddComponent<Camera>();
            _mainCamera.clearFlags = CameraClearFlags.SolidColor;
            _mainCamera.backgroundColor = new Color(0.09f, 0.12f, 0.16f);
            cameraGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
            cameraGo.AddComponent<AudioListener>();
        }

        if (Object.FindFirstObjectByType<Light>() == null)
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
        }
    }

    private void EnsureGround()
    {
        // Remove previous image-based backgrounds.
        var legacyLaneBackground = GameObject.Find("StreetBackground");
        if (legacyLaneBackground != null)
        {
            Destroy(legacyLaneBackground);
        }

        var legacyWorldBackdrop = GameObject.Find("StreetBackdrop");
        if (legacyWorldBackdrop != null)
        {
            Destroy(legacyWorldBackdrop);
        }

        if (_mainCamera != null)
        {
            var cameraBackdrop = _mainCamera.transform.Find("CameraBackdrop");
            if (cameraBackdrop != null)
            {
                Destroy(cameraBackdrop.gameObject);
            }
        }

        // Make lane visible again.
        var ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(_runtimeRoot, false);
        }

        float worldLength = Mathf.Max(1200f, environmentLength);
        ground.transform.position = new Vector3(0f, -0.01f, worldLength * 0.5f);
        ground.transform.localScale = new Vector3(1.5f, 1f, worldLength / 10f);

        var groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer != null)
        {
            groundRenderer.enabled = true;
            groundRenderer.material.color = new Color(0.16f, 0.16f, 0.18f);
        }

        ResolveEnvironmentPrefabs();
        EnsureStreetScenery(worldLength);
    }

    private void EnsureStreetScenery(float worldLength)
    {
        var existing = GameObject.Find("StreetScenery");
        if (existing != null)
        {
            Destroy(existing);
        }

        var root = new GameObject("StreetScenery");
        root.transform.SetParent(_runtimeRoot, false);

        CreateSidewalk(root.transform, -7.5f, worldLength);
        CreateSidewalk(root.transform, 7.5f, worldLength);

        // Lane markers to make the run path visually clear.
        int markerCount = Mathf.CeilToInt(worldLength / 10f);
        for (int i = 0; i < markerCount; i++)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "LaneMarker_" + i;
            marker.transform.SetParent(root.transform, false);
            marker.transform.position = new Vector3(0f, 0.02f, 10f + i * 10f);
            marker.transform.localScale = new Vector3(0.2f, 0.02f, 2.2f);
            var markerRenderer = marker.GetComponent<Renderer>();
            if (markerRenderer != null)
            {
                markerRenderer.material.color = new Color(0.93f, 0.86f, 0.3f);
            }
        }

        float sceneryStartZ = 14f;
        float sceneryEndZ = worldLength - 18f;
        CreateHouseRow(root.transform, -21.5f, sceneryStartZ, sceneryEndZ, true);
        CreateHouseRow(root.transform, 21.5f, sceneryStartZ, sceneryEndZ, false);

        CreateTreesForSide(root.transform, -54f, -28f, sceneryStartZ - 8f, sceneryEndZ + 14f);
        CreateTreesForSide(root.transform, 28f, 54f, sceneryStartZ - 8f, sceneryEndZ + 14f);
    }

    private static void CreateSidewalk(Transform parent, float x, float worldLength)
    {
        var sidewalk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sidewalk.name = x < 0f ? "SidewalkLeft" : "SidewalkRight";
        sidewalk.transform.SetParent(parent, false);
        sidewalk.transform.position = new Vector3(x, 0.07f, worldLength * 0.5f);
        sidewalk.transform.localScale = new Vector3(3.4f, 0.14f, worldLength);

        var renderer = sidewalk.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.38f, 0.38f, 0.4f);
        }
    }

    private void CreateHouseRow(Transform parent, float baseX, float startZ, float endZ, bool leftSide)
    {
        if (bakerHousePrefab == null)
        {
            return;
        }

        // About 30% fewer houses than before.
        float spacing = 34f;
        int houseCount = Mathf.Max(1, Mathf.FloorToInt((endZ - startZ) / spacing));
        for (int i = 0; i <= houseCount; i++)
        {
            float z = startZ + i * spacing + Random.Range(-7f, 7f);
            if (z < startZ || z > endZ)
            {
                continue;
            }

            float x = baseX + Random.Range(-3.4f, 3.4f);
            float yRot = leftSide ? 90f : -90f;
            // Random house orientation in 90-degree steps (clockwise/counterclockwise).
            float stepTurn = Random.value < 0.72f ? (Random.value < 0.5f ? -90f : 90f) : 0f;
            Quaternion rotation = Quaternion.Euler(0f, yRot + stepTurn, 0f);
            var house = Instantiate(bakerHousePrefab, new Vector3(x, 0f, z), rotation, parent);
            house.name = leftSide ? "BakerHouse_Left" : "BakerHouse_Right";

            float scaleMul = Random.Range(0.88f, 1.18f);
            house.transform.localScale = house.transform.localScale * scaleMul;
            PrepareDecorInstance(house);
        }
    }

    private void CreateTreesForSide(Transform parent, float minX, float maxX, float startZ, float endZ)
    {
        if (lowPolyTreePrefabs == null || lowPolyTreePrefabs.Count == 0)
        {
            return;
        }

        // Denser tree fill to replace reduced house count.
        float spacing = 10f;
        int treeCount = Mathf.Max(8, Mathf.FloorToInt((endZ - startZ) / spacing));
        for (int i = 0; i <= treeCount; i++)
        {
            GameObject treePrefab = lowPolyTreePrefabs[Random.Range(0, lowPolyTreePrefabs.Count)];
            if (treePrefab == null)
            {
                continue;
            }

            float x = Random.Range(minX, maxX);
            float z = startZ + i * spacing + Random.Range(-5f, 5f);
            // Tree rotation snapped to 15-degree increments.
            float yRot = Random.Range(0, 24) * 15f;

            var tree = Instantiate(treePrefab, new Vector3(x, 0f, z), Quaternion.Euler(0f, yRot, 0f), parent);
            tree.name = "Tree_" + treePrefab.name;
            float scaleMul = Random.Range(0.82f, 1.45f);
            tree.transform.localScale = tree.transform.localScale * scaleMul;
            PrepareDecorInstance(tree);
        }
    }

    private void ResolveEnvironmentPrefabs()
    {
#if UNITY_EDITOR
        if (bakerHousePrefab == null)
        {
            bakerHousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LowpolyBakersHouse/Prefabs/Baker_house.prefab");
        }

        if (lowPolyTreePrefabs == null)
        {
            lowPolyTreePrefabs = new List<GameObject>();
        }

        if (lowPolyTreePrefabs.Count == 0)
        {
            string[] treeGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BrokenVector/LowPolyTreePack/Prefabs" });
            for (int i = 0; i < treeGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(treeGuids[i]);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    lowPolyTreePrefabs.Add(prefab);
                }
            }

            lowPolyTreePrefabs.Sort((a, b) =>
            {
                string an = a != null ? a.name : string.Empty;
                string bn = b != null ? b.name : string.Empty;
                return string.CompareOrdinal(an, bn);
            });
        }
#endif
    }

    private static void DisableDecorPhysics(GameObject instanceRoot)
    {
        if (instanceRoot == null)
        {
            return;
        }

        var colliders = instanceRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }

        var rigidbodies = instanceRoot.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].useGravity = false;
            rigidbodies[i].detectCollisions = false;
        }
    }

    private void PrepareDecorInstance(GameObject instanceRoot)
    {
        DisableDecorPhysics(instanceRoot);
        ApplyDecorMaterials(instanceRoot);
    }

    private void ApplyDecorMaterials(GameObject instanceRoot)
    {
        if (instanceRoot == null)
        {
            return;
        }

        var renderers = instanceRoot.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            var renderer = renderers[r];
            if (renderer == null)
            {
                continue;
            }

            var sourceMats = renderer.sharedMaterials;
            if (sourceMats == null || sourceMats.Length == 0)
            {
                continue;
            }

            bool changed = false;
            for (int i = 0; i < sourceMats.Length; i++)
            {
                var source = sourceMats[i];
                if (source == null)
                {
                    continue;
                }

                if (!_decorMaterialOverrides.TryGetValue(source, out var resolved))
                {
                    resolved = ResolveDecorMaterial(source);
                    _decorMaterialOverrides[source] = resolved != null ? resolved : source;
                }

                if (resolved != null && resolved != source)
                {
                    sourceMats[i] = resolved;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.sharedMaterials = sourceMats;
            }
        }
    }

    private static Material ResolveDecorMaterial(Material source)
    {
        if (source == null)
        {
            return null;
        }

        if (IsCompatibleDecorShader(source.shader))
        {
            return source;
        }

        Shader targetShader = Shader.Find("Universal Render Pipeline/Lit");
        if (targetShader == null)
        {
            targetShader = Shader.Find("Standard");
        }

        if (targetShader == null)
        {
            return source;
        }

        var material = new Material(targetShader);
        material.name = source.name + "_RuntimeFixed";

        Color tint = Color.white;
        if (source.HasProperty("_BaseColor"))
        {
            tint = source.GetColor("_BaseColor");
        }
        else if (source.HasProperty("_Color"))
        {
            tint = source.GetColor("_Color");
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", tint);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", tint);
        }

        Texture baseTex = null;
        if (source.HasProperty("_BaseMap"))
        {
            baseTex = source.GetTexture("_BaseMap");
        }

        if (baseTex == null && source.HasProperty("_MainTex"))
        {
            baseTex = source.GetTexture("_MainTex");
        }

        if (baseTex != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", baseTex);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", baseTex);
            }
        }

        if (source.HasProperty("_MainTex"))
        {
            Vector2 scale = source.GetTextureScale("_MainTex");
            Vector2 offset = source.GetTextureOffset("_MainTex");
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTextureScale("_BaseMap", scale);
                material.SetTextureOffset("_BaseMap", offset);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTextureScale("_MainTex", scale);
                material.SetTextureOffset("_MainTex", offset);
            }
        }

        bool alphaClip = source.HasProperty("_Cutoff");
        float cutoff = alphaClip ? source.GetFloat("_Cutoff") : 0.33f;
        if (alphaClip && material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 1f);
        }

        if (alphaClip && material.HasProperty("_Cutoff"))
        {
            material.SetFloat("_Cutoff", cutoff);
        }

        return material;
    }

    private static bool IsCompatibleDecorShader(Shader shader)
    {
        if (shader == null)
        {
            return false;
        }

        string name = shader.name ?? string.Empty;
        if (name == "Hidden/InternalErrorShader")
        {
            return false;
        }

        // Force-convert built-in and legacy shaders to avoid pink materials under URP.
        if (name.StartsWith("Legacy Shaders/"))
        {
            return false;
        }

        if (name == "Standard")
        {
            return false;
        }

        if (name.StartsWith("Universal Render Pipeline/"))
        {
            return shader.isSupported;
        }

        return false;
    }

    private void EnsureDataDefaults()
    {
        if (soldierTiers == null)
        {
            soldierTiers = new List<SoldierTier>();
        }

        if (enemyTypes == null)
        {
            enemyTypes = new List<EnemyType>();
        }

        if (powerupTypes == null)
        {
            powerupTypes = new List<PowerupType>();
        }

        if (levelSegments == null)
        {
            levelSegments = new List<LevelSegment>();
        }

        if (soldierTiers.Count == 0)
        {
            soldierTiers.Add(CreateTier(0, 1f, 1f, 0.9f, new Color(1f, 0.9f, 0.2f)));
            soldierTiers.Add(CreateTier(1, 1.25f, 1.08f, 1f, new Color(1f, 0.45f, 0.2f)));
            soldierTiers.Add(CreateTier(2, 1.55f, 1.15f, 1.1f, new Color(0.35f, 0.95f, 1f)));
        }

        if (enemyTypes.Count == 0)
        {
            enemyTypes.Add(CreateEnemyType("Grunt", 1f, 3.15f, 1, 0.85f, new Color(0.95f, 0.32f, 0.25f)));
        }

        var smallEnemy = enemyTypes[0];
        smallEnemy.typeId = "Grunt";
        smallEnemy.maxHealth = 1f;
        smallEnemy.moveSpeed = 3.15f;
        smallEnemy.contactDamage = 1;
        smallEnemy.scale = 0.85f;
        smallEnemy.tint = new Color(0.95f, 0.32f, 0.25f);

        if (enemyTypes.Count > 1)
        {
            enemyTypes.RemoveRange(1, enemyTypes.Count - 1);
        }

        if (powerupTypes.Count == 0)
        {
            powerupTypes.Add(CreatePowerup("Fire Rate", PowerupKind.FireRateBoost, 3.4f, 5f));
            powerupTypes.Add(CreatePowerup("Damage", PowerupKind.DamageBoost, 1.45f, 5f));
            powerupTypes.Add(CreatePowerup("Tier", PowerupKind.TierUpgrade, 1f, 7f));
            powerupTypes.Add(CreatePowerup("Piercing", PowerupKind.Piercing, 1f, 5f));
        }

        bool hasFireRate = false;
        bool hasPiercing = false;
        for (int i = 0; i < powerupTypes.Count; i++)
        {
            if (powerupTypes[i] != null && powerupTypes[i].kind == PowerupKind.FireRateBoost)
            {
                powerupTypes[i].magnitude = Mathf.Max(powerupTypes[i].magnitude, 3.4f); // Much stronger fire-rate burst.
                powerupTypes[i].duration = 5f;
                hasFireRate = true;
            }

            if (powerupTypes[i] != null && powerupTypes[i].kind == PowerupKind.Piercing)
            {
                powerupTypes[i].magnitude = 1f;
                powerupTypes[i].duration = 5f;
                hasPiercing = true;
            }
        }

        if (!hasFireRate)
        {
            powerupTypes.Insert(0, CreatePowerup("Fire Rate", PowerupKind.FireRateBoost, 3.4f, 5f));
        }

        if (!hasPiercing)
        {
            powerupTypes.Add(CreatePowerup("Piercing", PowerupKind.Piercing, 1f, 5f));
        }

        if (levelSegments.Count == 0)
        {
            levelSegments.Add(CreateSegment(36f, 1, 1, 2, 1, 0.25f));
            levelSegments.Add(CreateSegment(40f, 1, 1, 3, 1, 0.30f));
            levelSegments.Add(CreateSegment(46f, 2, 2, 4, 1, 0.35f));
            levelSegments.Add(CreateSegment(50f, 2, 2, 5, 1, 0.40f));
            levelSegments.Add(CreateSegment(56f, 2, 3, 6, 2, 0.45f));
            levelSegments.Add(CreateSegment(60f, 3, 3, 7, 2, 0.50f));
        }
    }

    private static float GetPowerupWeight(PowerupType type)
    {
        if (type == null)
        {
            return 0f;
        }

        if (type.kind == PowerupKind.Piercing)
        {
            return 0.28f;
        }

        return 1f;
    }

    private static SoldierTier CreateTier(int index, float damage, float fireRate, float scale, Color tint)
    {
        var tier = ScriptableObject.CreateInstance<SoldierTier>();
        tier.tierIndex = index;
        tier.damageMultiplier = damage;
        tier.fireRateMultiplier = fireRate;
        tier.projectileScale = scale;
        tier.projectileColor = tint;
        return tier;
    }

    private static EnemyType CreateEnemyType(string id, float hp, float speed, int contactDamage, float scale, Color tint)
    {
        var type = ScriptableObject.CreateInstance<EnemyType>();
        type.typeId = id;
        type.maxHealth = hp;
        type.moveSpeed = speed;
        type.contactDamage = contactDamage;
        type.scale = scale;
        type.tint = tint;
        return type;
    }

    private static PowerupType CreatePowerup(string label, PowerupKind kind, float magnitude, float duration)
    {
        var type = ScriptableObject.CreateInstance<PowerupType>();
        type.displayName = label;
        type.kind = kind;
        type.magnitude = magnitude;
        type.duration = duration;
        return type;
    }

    private static LevelSegment CreateSegment(float length, int waveCount, int minEnemies, int maxEnemies, int gates, float powerupChance)
    {
        var segment = ScriptableObject.CreateInstance<LevelSegment>();
        segment.length = length;
        segment.waveCount = waveCount;
        segment.minEnemiesPerWave = minEnemies;
        segment.maxEnemiesPerWave = maxEnemies;
        segment.gateCount = gates;
        segment.powerupChance = powerupChance;
        return segment;
    }
}
