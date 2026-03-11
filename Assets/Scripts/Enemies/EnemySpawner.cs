using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private struct ScheduledWave
    {
        public float TriggerZ;
        public int Count;
        public EnemyType EnemyType;
    }

    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private int pooledEnemyCount = 520;
    [SerializeField] private float spawnWidth = 5.5f;
    [SerializeField] private float spawnAheadDistance = 42f;
    [SerializeField] private float constantSpawnIntervalStart = 2.8f;
    [SerializeField] private float constantSpawnIntervalEnd = 0.16f;
    [SerializeField] private float constantSpawnRampDistance = 280f;
    [SerializeField] private int constantSpawnCountStart = 1;
    [SerializeField] private int constantSpawnCountEnd = 10;

    private readonly List<ScheduledWave> _scheduledWaves = new List<ScheduledWave>();
    private Pool<Enemy> _enemyPool;
    private SquadController _squadController;
    private EnemyType _fallbackType;
    private Transform _poolRoot;
    private float _nextConstantSpawnAt;

    public void Configure(Enemy prefab, SquadController squad, EnemyType fallbackType)
    {
        if (prefab != null)
        {
            enemyPrefab = prefab;
        }

        _squadController = squad;
        _fallbackType = fallbackType;
        _nextConstantSpawnAt = Time.time + 1.25f;

        EnsurePool();
    }

    private void Update()
    {
        if (!GameManager.Instance.IsRunning || _squadController == null)
        {
            return;
        }

        float squadZ = _squadController.CurrentRunZ;

        for (int i = _scheduledWaves.Count - 1; i >= 0; i--)
        {
            if (squadZ >= _scheduledWaves[i].TriggerZ)
            {
                var wave = _scheduledWaves[i];
                _scheduledWaves.RemoveAt(i);
                SpawnWaveAtTopOfTrack(wave.Count, wave.EnemyType);
            }
        }

        if (Time.time >= _nextConstantSpawnAt)
        {
            float t = Mathf.Clamp01(squadZ / Mathf.Max(1f, constantSpawnRampDistance));
            // Keep early/mid fair while still ramping into late game.
            float easedT = Mathf.Pow(t, 1.55f);
            int spawnCount = Mathf.RoundToInt(Mathf.Lerp(constantSpawnCountStart, constantSpawnCountEnd, easedT));
            spawnCount += Mathf.FloorToInt(easedT * 3f);
            spawnCount = Mathf.Max(1, spawnCount + UnityEngine.Random.Range(0, 3));
            SpawnWaveAtTopOfTrack(spawnCount, _fallbackType);

            float interval = Mathf.Lerp(constantSpawnIntervalStart, constantSpawnIntervalEnd, easedT);
            _nextConstantSpawnAt = Time.time + Mathf.Max(0.12f, interval);
        }
    }

    public void ScheduleWave(float triggerZ, int count, EnemyType type)
    {
        _scheduledWaves.Add(new ScheduledWave
        {
            TriggerZ = triggerZ,
            Count = Mathf.Max(1, count),
            EnemyType = type != null ? type : _fallbackType
        });
    }

    public void SpawnAddsNearBoss(float centerZ, int count, EnemyType type)
    {
        SpawnWaveNow(count, type, centerZ + 10f, 4f);
    }

    public void ClearScheduledWaves()
    {
        _scheduledWaves.Clear();
        _nextConstantSpawnAt = Time.time + 1.25f;
    }

    public void ResetForNewRun()
    {
        ClearScheduledWaves();
        EnsurePool();
        _enemyPool.ReleaseAllActive();
    }

    private void SpawnWaveAtTopOfTrack(int count, EnemyType type)
    {
        float topSpawnZ = _squadController.CurrentRunZ + spawnAheadDistance;
        SpawnWaveNow(count, type, topSpawnZ, 8f);
    }

    private void SpawnWaveNow(int count, EnemyType type, float baseZ, float zSpread)
    {
        EnsurePool();

        for (int i = 0; i < count; i++)
        {
            var enemy = _enemyPool.Get();
            float x = UnityEngine.Random.Range(-spawnWidth, spawnWidth);
            float z = baseZ + UnityEngine.Random.Range(0f, Mathf.Max(0.5f, zSpread));
            enemy.transform.position = new Vector3(x, 0.55f, z);
            enemy.transform.rotation = Quaternion.identity;
            enemy.Initialize(type != null ? type : _fallbackType, _squadController, HandleEnemyRelease);
        }
    }

    private void HandleEnemyRelease(Enemy enemy)
    {
        _enemyPool.Release(enemy);
    }

    private void EnsurePool()
    {
        if (_enemyPool != null)
        {
            return;
        }

        if (enemyPrefab == null)
        {
            enemyPrefab = BuildRuntimeEnemyPrefab();
        }

        _poolRoot = new GameObject("EnemyPool").transform;
        _poolRoot.SetParent(transform, false);
        _enemyPool = new Pool<Enemy>(enemyPrefab, pooledEnemyCount, _poolRoot);
    }

    private Enemy BuildRuntimeEnemyPrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "EnemyRuntimePrefab";
        go.transform.localScale = new Vector3(1f, 1f, 1f);

        var enemy = go.GetComponent<Enemy>();
        if (enemy == null)
        {
            enemy = go.AddComponent<Enemy>();
        }

        go.SetActive(false);
        return enemy;
    }
}
