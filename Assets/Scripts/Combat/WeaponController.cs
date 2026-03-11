using System.Collections.Generic;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    private struct PendingShot
    {
        public float fireTime;
        public Vector3 spawnPos;
        public Vector3 direction;
        public Color color;
        public float visualScale;
        public float damage;
        public int hitsBeforeDespawn;
        public bool pierceAll;
    }

    [Header("Tuning")]
    [SerializeField] private float baseFireRate = 1.15f; // Slow start; upgraded via orange boxes.
    [SerializeField] private float baseDamage = 1f;
    [SerializeField] private float projectileLifetime = 3f;
    [SerializeField] private int pooledProjectileCount = 128;
    [SerializeField] private float runFireRateLevelMultiplier = 0.7f; // Stronger per-upgrade fire-rate gain.
    [SerializeField] private float volleyStaggerWindow = 0.09f; // Visual timing variation only; volley rate unchanged.
    [SerializeField] private float lateralShotJitter = 0.18f;
    [SerializeField] private float aimJitter = 0.035f;

    [Header("References")]
    [SerializeField] private SquadController squadController;
    [SerializeField] private PowerupManager powerupManager;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private Projectile projectilePrefab;

    private Pool<Projectile> _projectilePool;
    private Transform _poolRoot;
    private float _nextShotTime;
    private bool _firingEnabled;
    private IReadOnlyList<SoldierTier> _tiers;
    private int _runFireRateUpgradeLevel;
    private readonly List<PendingShot> _pendingShots = new List<PendingShot>(64);

    public int RunFireRateUpgradeLevel => _runFireRateUpgradeLevel;
    public float CurrentFireRate => GetFinalFireRate();

    public void Configure(
        SquadController squad,
        PowerupManager powerups,
        UpgradeManager upgrades,
        IReadOnlyList<SoldierTier> tiers,
        Projectile prefabOverride = null)
    {
        squadController = squad;
        powerupManager = powerups;
        upgradeManager = upgrades;
        _tiers = tiers;

        if (prefabOverride != null)
        {
            projectilePrefab = prefabOverride;
        }

        EnsurePool();
    }

    private void Update()
    {
        if (squadController == null || !GameManager.Instance.IsRunning)
        {
            return;
        }

        ProcessPendingShots();

        if (!_firingEnabled)
        {
            return;
        }

        if (squadController.LogicalSoldierCount <= 0)
        {
            return;
        }

        if (Time.time < _nextShotTime)
        {
            return;
        }

        float fireRate = GetFinalFireRate();
        _nextShotTime = Time.time + (1f / Mathf.Max(0.01f, fireRate));

        FireVolley();
    }

    public void SetFiringEnabled(bool value)
    {
        _firingEnabled = value;
        if (!value)
        {
            _pendingShots.Clear();
        }
    }

    public void ResetRunUpgrades()
    {
        _runFireRateUpgradeLevel = 0;
    }

    public void AddRunFireRateUpgradeLevel(int levels)
    {
        _runFireRateUpgradeLevel = Mathf.Clamp(_runFireRateUpgradeLevel + Mathf.Max(1, levels), 0, 12);
    }

    public void ResetForNewRun()
    {
        EnsurePool();
        _projectilePool.ReleaseAllActive();
        _nextShotTime = Time.time;
        _pendingShots.Clear();
        ResetRunUpgrades();
    }

    private void FireVolley()
    {
        int logicalSoldiers = squadController.LogicalSoldierCount;
        int shots = Mathf.Clamp(Mathf.CeilToInt(logicalSoldiers / 10f), 1, 22);

        float width = Mathf.Clamp(squadController.FormationHalfWidth * 0.55f, 0.25f, 2.1f);
        float damage = GetFinalDamage();
        bool pierceAll = powerupManager != null && powerupManager.PiercingBonus > 0;
        int hitsPerProjectile = pierceAll ? 999 : 1 + Mathf.Max(0, powerupManager.PiercingBonus);

        var tier = GetActiveTier();

        for (int i = 0; i < shots; i++)
        {
            float t = shots == 1 ? 0.5f : i / (float)(shots - 1);
            float xOffset = Mathf.Lerp(-width, width, t) + Random.Range(-lateralShotJitter, lateralShotJitter);
            float yOffset = 0.8f + Random.Range(-0.03f, 0.04f);
            float zOffset = 1.2f + Random.Range(-0.08f, 0.12f);

            Vector3 direction = (Vector3.forward + new Vector3(Random.Range(-aimJitter, aimJitter), 0f, 0f)).normalized;
            float delay = shots <= 1 ? 0f : Random.Range(0f, volleyStaggerWindow);

            _pendingShots.Add(new PendingShot
            {
                fireTime = Time.time + delay,
                spawnPos = transform.position + new Vector3(xOffset, yOffset, zOffset),
                direction = direction,
                color = tier.projectileColor,
                visualScale = tier.projectileScale,
                damage = damage,
                hitsBeforeDespawn = hitsPerProjectile,
                pierceAll = pierceAll
            });
        }
    }

    private void ProcessPendingShots()
    {
        if (_pendingShots.Count == 0)
        {
            return;
        }

        float now = Time.time;
        for (int i = _pendingShots.Count - 1; i >= 0; i--)
        {
            if (_pendingShots[i].fireTime > now)
            {
                continue;
            }

            EmitShot(_pendingShots[i]);
            _pendingShots.RemoveAt(i);
        }
    }

    private void EmitShot(PendingShot shot)
    {
        var projectile = _projectilePool.Get();
        projectile.transform.SetParent(null, true); // Keep projectiles in world space after firing.
        projectile.transform.position = shot.spawnPos;
        projectile.transform.rotation = Quaternion.identity;
        projectile.SetVisual(shot.color, shot.visualScale);
        projectile.Initialize(
            shot.direction,
            shot.damage,
            shot.hitsBeforeDespawn,
            projectileLifetime,
            HandleProjectileRelease,
            shot.pierceAll);
    }

    private float GetFinalDamage()
    {
        float powerupMul = powerupManager != null ? powerupManager.DamageMultiplier : 1f;
        float metaMul = upgradeManager != null ? upgradeManager.BaseDamageMultiplier : 1f;
        float tierMul = GetActiveTier().damageMultiplier;

        return baseDamage * powerupMul * metaMul * tierMul;
    }

    private float GetFinalFireRate()
    {
        float powerupMul = powerupManager != null ? powerupManager.FireRateMultiplier : 1f;
        float metaMul = upgradeManager != null ? upgradeManager.FireRateMultiplier : 1f;
        float tierMul = GetActiveTier().fireRateMultiplier;
        float runUpgradeMul = 1f + (_runFireRateUpgradeLevel * runFireRateLevelMultiplier);

        return baseFireRate * powerupMul * metaMul * tierMul * runUpgradeMul;
    }

    private SoldierTier GetActiveTier()
    {
        if (_tiers == null || _tiers.Count == 0)
        {
            return CreateFallbackTier();
        }

        int maxUnlocked = upgradeManager != null ? upgradeManager.MaxUnlockedTierIndex : 0;
        int temporaryBonus = powerupManager != null ? powerupManager.TemporaryTierBonus : 0;

        int targetTier = Mathf.Clamp(maxUnlocked + temporaryBonus, 0, _tiers.Count - 1);
        return _tiers[targetTier] != null ? _tiers[targetTier] : CreateFallbackTier();
    }

    private void EnsurePool()
    {
        if (_projectilePool != null)
        {
            return;
        }

        if (projectilePrefab == null)
        {
            projectilePrefab = BuildRuntimeProjectilePrefab();
        }

        _poolRoot = new GameObject("ProjectilePool").transform;
        _poolRoot.position = Vector3.zero;
        _projectilePool = new Pool<Projectile>(projectilePrefab, pooledProjectileCount, _poolRoot);
    }

    private Projectile BuildRuntimeProjectilePrefab()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "ProjectileRuntimePrefab";
        go.transform.localScale = new Vector3(0.06f, 0.1f, 0.06f);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var colliders = go.GetComponents<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Destroy(colliders[i]);
        }

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = Color.yellow;
        }

        var projectile = go.GetComponent<Projectile>();
        if (projectile == null)
        {
            projectile = go.AddComponent<Projectile>();
        }

        go.SetActive(false);
        return projectile;
    }

    private void HandleProjectileRelease(Projectile projectile)
    {
        _projectilePool.Release(projectile);
    }

    private static SoldierTier CreateFallbackTier()
    {
        var tier = ScriptableObject.CreateInstance<SoldierTier>();
        tier.tierIndex = 0;
        tier.damageMultiplier = 1f;
        tier.fireRateMultiplier = 1f;
        tier.projectileColor = Color.yellow;
        tier.projectileScale = 1f;
        return tier;
    }
}


