using System;
using UnityEngine;

public class Projectile : MonoBehaviour, IPoolable
{
    [SerializeField] private float speed = 22f;
    [SerializeField] private int hitVfxPoolSize = 96;

    private Vector3 _direction = Vector3.forward;
    private float _damage = 1f;
    private float _lifetime = 3f;
    private int _remainingHits = 1;
    private bool _pierceAll;
    private Color _tint = Color.yellow;
    private Action<Projectile> _releaseAction;

    private static Pool<HitVfx> _hitVfxPool;
    private static Transform _hitVfxRoot;
    private static HitVfx _hitVfxPrefab;

    public float Damage => _damage;

    private void Awake()
    {
        EnsureColliderAndBody();
        EnsureHitVfxPool();
    }

    private void Update()
    {
        transform.position += _direction * (speed * Time.deltaTime);
        _lifetime -= Time.deltaTime;

        if (_lifetime <= 0f)
        {
            Release();
        }
    }

    public void Initialize(
        Vector3 direction,
        float damage,
        int hitsBeforeDespawn,
        float lifeTimeSeconds,
        Action<Projectile> releaseAction,
        bool pierceAll)
    {
        _direction = direction.normalized;
        _damage = Mathf.Max(0.1f, damage);
        _remainingHits = Mathf.Max(1, hitsBeforeDespawn);
        _lifetime = Mathf.Max(0.1f, lifeTimeSeconds);
        _pierceAll = pierceAll;
        _releaseAction = releaseAction;
    }

    public void SetVisual(Color tint, float scale)
    {
        float clamped = Mathf.Clamp(scale, 0.55f, 1f);
        // 3x visual size so bullets read clearly in motion.
        transform.localScale = new Vector3(0.18f, 0.18f, 0.6f) * clamped;
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Capsule points forward like a bullet.
        _tint = tint;
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = tint;
        }
    }

    public void OnSpawned()
    {
        _lifetime = 3f;
        _pierceAll = false;
    }

    public void OnDespawned()
    {
        _pierceAll = false;
        _releaseAction = null;
    }

    public void ConsumeHit()
    {
        if (_pierceAll)
        {
            return;
        }

        _remainingHits--;
        if (_remainingHits <= 0)
        {
            Release();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        bool hitApplied = false;

        var enemy = other.GetComponentInParent<Enemy>();
        if (enemy != null)
        {
            enemy.TakeDamage(_damage);
            hitApplied = true;
        }

        var gate = other.GetComponentInParent<Gate>();
        if (gate != null)
        {
            gate.ReceiveBulletDamage(_damage);
            hitApplied = true;
        }

        var upgradeBox = other.GetComponentInParent<UpgradeBox>();
        if (upgradeBox != null)
        {
            if (upgradeBox.ReceiveBulletDamage(_damage))
            {
                hitApplied = true;
            }
        }

        var boss = other.GetComponentInParent<BossController>();
        if (boss != null)
        {
            boss.TakeDamage(_damage);
            hitApplied = true;
        }

        if (!hitApplied)
        {
            return;
        }

        SpawnHitVfx(other);
        ConsumeHit();
    }

    private void Release()
    {
        _releaseAction?.Invoke(this);
    }

    private void EnsureColliderAndBody()
    {
        var collider = GetComponent<SphereCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<SphereCollider>();
        }

        collider.radius = 0.1f;
        collider.isTrigger = true;

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private void SpawnHitVfx(Collider other)
    {
        EnsureHitVfxPool();
        if (_hitVfxPool == null)
        {
            return;
        }

        Vector3 hitPos = transform.position;
        if (other != null)
        {
            Vector3 closest = other.ClosestPoint(transform.position);
            if ((closest - transform.position).sqrMagnitude > 0.000001f)
            {
                hitPos = closest;
            }
        }

        var vfx = _hitVfxPool.Get();
        vfx.transform.SetParent(null, true);
        vfx.Initialize(HandleHitVfxRelease);
        vfx.Play(hitPos + Vector3.up * 0.04f, _tint);
    }

    private void EnsureHitVfxPool()
    {
        if (_hitVfxPool != null && _hitVfxRoot != null)
        {
            return;
        }

        _hitVfxPool = null;
        _hitVfxRoot = new GameObject("HitVfxPool").transform;
        _hitVfxRoot.position = Vector3.zero;

        if (_hitVfxPrefab == null)
        {
            _hitVfxPrefab = BuildRuntimeHitVfxPrefab();
        }

        _hitVfxPool = new Pool<HitVfx>(_hitVfxPrefab, Mathf.Max(16, hitVfxPoolSize), _hitVfxRoot);
    }

    private static HitVfx BuildRuntimeHitVfxPrefab()
    {
        var go = new GameObject("HitVfxRuntimePrefab");
        go.SetActive(false);
        if (_hitVfxRoot != null)
        {
            go.transform.SetParent(_hitVfxRoot, false);
        }

        var vfx = go.GetComponent<HitVfx>();
        if (vfx == null)
        {
            vfx = go.AddComponent<HitVfx>();
        }

        return vfx;
    }

    private static void HandleHitVfxRelease(HitVfx vfx)
    {
        if (_hitVfxPool == null || vfx == null)
        {
            return;
        }

        _hitVfxPool.Release(vfx);
    }
}
