using System.Collections.Generic;
using UnityEngine;

public class LevelGenerator : MonoBehaviour
{
    private readonly List<GameObject> _spawned = new List<GameObject>();
    private readonly List<Vector3> _collectiblePositions = new List<Vector3>();
    private readonly List<Vector3> _gatePositions = new List<Vector3>();
    private static readonly float[] Lanes = { -3.2f, 0f, 3.2f };

    private GameManager _gameManager;
    private EnemySpawner _enemySpawner;
    private SquadController _squadController;
    private Gate _gatePrefab;
    private Powerup _powerupPrefab;
    private BossController _bossPrefab;
    private UpgradeBox _upgradeBoxPrefab;
    private Transform _root;
    private bool _piercingSpawnedThisRun;
    private int _forcedPiercingSegmentIndex;
    private int _lastSegmentIndex;

    public void Configure(
        GameManager gameManager,
        EnemySpawner enemySpawner,
        SquadController squadController,
        Gate gatePrefab,
        Powerup powerupPrefab,
        BossController bossPrefab,
        UpgradeBox upgradeBoxPrefab,
        Transform root)
    {
        _gameManager = gameManager;
        _enemySpawner = enemySpawner;
        _squadController = squadController;
        _gatePrefab = gatePrefab;
        _powerupPrefab = powerupPrefab;
        _bossPrefab = bossPrefab;
        _upgradeBoxPrefab = upgradeBoxPrefab;
        _root = root;
    }

    public float GenerateLevel(IReadOnlyList<LevelSegment> segmentData)
    {
        ClearLevel();

        _collectiblePositions.Clear();
        _gatePositions.Clear();
        _piercingSpawnedThisRun = false;

        float zCursor = 20f;
        int segmentCount = segmentData != null && segmentData.Count > 0 ? segmentData.Count : 6;
        _lastSegmentIndex = Mathf.Max(0, segmentCount - 1);

        // Keep one guaranteed piercing spawn, but randomize its segment so runs feel less scripted.
        int minPiercingSegment = Mathf.Min(2, _lastSegmentIndex);
        int maxPiercingSegment = Mathf.Max(minPiercingSegment, _lastSegmentIndex - 1);
        _forcedPiercingSegmentIndex = Random.Range(minPiercingSegment, maxPiercingSegment + 1);

        for (int i = 0; i < segmentCount; i++)
        {
            LevelSegment segment = segmentData != null && i < segmentData.Count ? segmentData[i] : CreateFallbackSegment(i);
            zCursor = GenerateSegment(segment, i, zCursor);
        }

        float bossZ = zCursor + 46f;
        SpawnBoss(bossZ);

        return bossZ;
    }

    public void ClearLevel()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i] != null)
            {
                Destroy(_spawned[i]);
            }
        }

        _spawned.Clear();
    }

    private float GenerateSegment(LevelSegment segment, int depthIndex, float startZ)
    {
        float length = Mathf.Max(30f, segment.length);
        int waves = Mathf.Max(1, segment.waveCount);

        for (int w = 0; w < waves; w++)
        {
            float triggerZ = startZ + ((w + 1) * (length / (waves + 1f)));
            int baseEnemies = Random.Range(segment.minEnemiesPerWave, segment.maxEnemiesPerWave + 1);
            float progressionScale;
            if (depthIndex <= 1)
            {
                // Easier early game ramp.
                progressionScale = 1f + (depthIndex * 0.3f) + (w * 0.1f);
            }
            else if (depthIndex <= 3)
            {
                // Slightly harder mid-game.
                progressionScale = 1f + (depthIndex * 0.68f) + (w * 0.27f);
            }
            else
            {
                // Keep late game intense.
                progressionScale = 1f + (depthIndex * 0.8f) + (w * 0.32f);
                progressionScale *= 1.15f;
            }

            int enemies = Mathf.Clamp(Mathf.RoundToInt(baseEnemies * progressionScale), 1, 60);
            _enemySpawner.ScheduleWave(triggerZ, enemies, _gameManager.GetEnemyTypeForDepth(depthIndex));
        }

        int gates = Mathf.Max(0, segment.gateCount);
        int[] gateLaneOrder = GetShuffledLaneOrder();
        for (int g = 0; g < gates; g++)
        {
            float gateZ = startZ + ((g + 1) * (length / (gates + 1f))) + Random.Range(-1.2f, 1.2f);
            int laneIndex = gateLaneOrder[g % gateLaneOrder.Length];
            float gateX = Lanes[laneIndex] + Random.Range(-0.35f, 0.35f);
            int startValue = GenerateGateValue(depthIndex, g);
            SpawnGate(new Vector3(gateX, 0f, gateZ), startValue);
        }

        if (ShouldSpawnUpgradeBox(depthIndex))
        {
            float z = startZ + length * 0.68f;
            Vector3 boxPos = GetFreeCollectiblePosition(z);
            int unlockHits = depthIndex < 2 ? 2 : 3;
            int fireRateLevels = depthIndex < 2 ? 2 : depthIndex < 4 ? 3 : 4;
            SpawnUpgradeBox(boxPos, unlockHits, fireRateLevels);
        }

        bool shouldForcePiercing = !_piercingSpawnedThisRun && depthIndex >= 2 &&
            (depthIndex == _forcedPiercingSegmentIndex || depthIndex == _lastSegmentIndex);
        bool shouldSpawnRegularPowerup = depthIndex >= 2 && Random.value < segment.powerupChance * 0.45f;

        if (shouldForcePiercing || shouldSpawnRegularPowerup)
        {
            float z = startZ + length * 0.5f;
            Vector3 powerupPos = GetFreeCollectiblePosition(z);
            PowerupType forcedType = shouldForcePiercing ? _gameManager.GetPowerupTypeByKind(PowerupKind.Piercing) : null;
            SpawnPowerup(powerupPos, forcedType);

            if (forcedType != null)
            {
                _piercingSpawnedThisRun = true;
            }
        }

        return startZ + length;
    }

    private static bool ShouldSpawnUpgradeBox(int depthIndex)
    {
        if (depthIndex == 0)
        {
            return true;
        }

        return Random.value < 0.8f;
    }

    private Vector3 GetFreeCollectiblePosition(float z)
    {
        int[] laneOrder = GetShuffledLaneOrder();
        for (int i = 0; i < laneOrder.Length; i++)
        {
            int lane = laneOrder[i];
            float xJitter = Random.Range(-0.28f, 0.28f);
            float zJitter = Random.Range(-0.65f, 0.65f);
            Vector3 candidate = new Vector3(Lanes[lane] + xJitter, 0.8f, z + zJitter + (i * 0.08f));
            if (IsCollectiblePositionFree(candidate, 2.2f))
            {
                _collectiblePositions.Add(candidate);
                return candidate;
            }
        }

        int fallbackLane = laneOrder[Random.Range(0, laneOrder.Length)];
        Vector3 fallback = new Vector3(Lanes[fallbackLane] + Random.Range(-0.2f, 0.2f), 0.8f, z + Random.Range(0.5f, 1.2f));
        _collectiblePositions.Add(fallback);
        return fallback;
    }

    private static int[] GetShuffledLaneOrder()
    {
        int[] order = { 0, 1, 2 };
        for (int i = 0; i < order.Length - 1; i++)
        {
            int swapIndex = Random.Range(i, order.Length);
            int temp = order[i];
            order[i] = order[swapIndex];
            order[swapIndex] = temp;
        }

        return order;
    }

    private bool IsCollectiblePositionFree(Vector3 candidate, float minDistance)
    {
        for (int i = 0; i < _collectiblePositions.Count; i++)
        {
            Vector3 existing = _collectiblePositions[i];
            float distance = Vector2.Distance(new Vector2(existing.x, existing.z), new Vector2(candidate.x, candidate.z));
            if (distance < minDistance)
            {
                return false;
            }
        }

        for (int i = 0; i < _gatePositions.Count; i++)
        {
            Vector3 gatePos = _gatePositions[i];
            float distance = Vector2.Distance(new Vector2(gatePos.x, gatePos.z), new Vector2(candidate.x, candidate.z));
            if (distance < 2.3f)
            {
                return false;
            }
        }

        return true;
    }

    private static int GenerateGateValue(int depthIndex, int gateIndex)
    {
        if (depthIndex <= 1)
        {
            if (gateIndex > 0 && Random.value < 0.15f)
            {
                return -Random.Range(1, 4);
            }

            return Random.Range(4, 11);
        }

        if (depthIndex <= 3)
        {
            bool shouldBeNegative = Random.value < 0.4f;
            if (shouldBeNegative)
            {
                return -Random.Range(3, 10);
            }

            return Random.Range(5, 14);
        }

        if (depthIndex <= 5)
        {
            if (Random.value < 0.62f)
            {
                return -Random.Range(6, 15);
            }

            return Random.Range(5, 13);
        }

        if (Random.value < 0.78f)
        {
            return -Random.Range(8, 19);
        }

        return Random.Range(4, 12);
    }

    private void SpawnGate(Vector3 position, int startValue)
    {
        Gate gate;

        if (_gatePrefab != null)
        {
            gate = Instantiate(_gatePrefab, position, Quaternion.identity, _root);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_root, false);
            go.name = "Gate";
            go.transform.position = position;
            go.transform.localScale = new Vector3(2.2f, 1.6f, 0.45f);

            gate = go.GetComponent<Gate>();
            if (gate == null)
            {
                gate = go.AddComponent<Gate>();
            }
        }

        gate.Configure(startValue, _gameManager.GateImproveMultiplier);
        _gatePositions.Add(position);
        _spawned.Add(gate.gameObject);
    }

    private void SpawnPowerup(Vector3 position, PowerupType forcedType = null)
    {
        Powerup powerup;

        if (_powerupPrefab != null)
        {
            powerup = Instantiate(_powerupPrefab, position, Quaternion.identity, _root);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(_root, false);
            go.name = "Powerup";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.8f;

            powerup = go.GetComponent<Powerup>();
            if (powerup == null)
            {
                powerup = go.AddComponent<Powerup>();
            }
        }

        var type = forcedType != null ? forcedType : _gameManager.GetRandomPowerupType();
        powerup.Configure(type, _gameManager.PowerupManager);
        _spawned.Add(powerup.gameObject);
    }

    private void SpawnUpgradeBox(Vector3 position, int hitsToUnlock, int fireRateLevels)
    {
        UpgradeBox box;

        if (_upgradeBoxPrefab != null)
        {
            box = Instantiate(_upgradeBoxPrefab, position, Quaternion.identity, _root);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(_root, false);
            go.name = "UpgradeBox";
            go.transform.position = position;
            go.transform.localScale = new Vector3(1.1f, 1.1f, 1.1f);

            box = go.GetComponent<UpgradeBox>();
            if (box == null)
            {
                box = go.AddComponent<UpgradeBox>();
            }
        }

        box.Configure(hitsToUnlock, fireRateLevels);
        _spawned.Add(box.gameObject);
    }

    private void SpawnBoss(float bossZ)
    {
        BossController boss;

        if (_bossPrefab != null)
        {
            boss = Instantiate(_bossPrefab, new Vector3(0f, 1f, bossZ), Quaternion.identity, _root);
        }
        else
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(_root, false);
            go.name = "Boss";
            go.transform.position = new Vector3(0f, 1f, bossZ);
            go.transform.localScale = new Vector3(3.0f, 3.0f, 3.0f);

            boss = go.GetComponent<BossController>();
            if (boss == null)
            {
                boss = go.AddComponent<BossController>();
            }
        }

        boss.Initialize(_enemySpawner, _squadController, _gameManager, _gameManager.GetEnemyTypeForDepth(999));
        _spawned.Add(boss.gameObject);
    }

    private static LevelSegment CreateFallbackSegment(int index)
    {
        var segment = ScriptableObject.CreateInstance<LevelSegment>();
        segment.length = 34f + (index * 5f);
        segment.waveCount = Mathf.Clamp(1 + index / 2, 1, 3);
        segment.minEnemiesPerWave = 1 + index;
        segment.maxEnemiesPerWave = 2 + index * 2;
        segment.gateCount = index < 2 ? 1 : 2;
        segment.powerupChance = 0.35f + (index * 0.06f);
        return segment;
    }
}
