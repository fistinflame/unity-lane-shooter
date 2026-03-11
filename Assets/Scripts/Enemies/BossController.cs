using UnityEngine;

public class BossController : MonoBehaviour
{
    [Header("Boss Tuning")]
    [SerializeField] private float maxHp = 420f;
    [SerializeField] private float moveSpeed = 0.78f;
    [SerializeField] private float loseDistance = 2.1f;
    [SerializeField] private int contactDamagePerTick = 20;
    [SerializeField] private float contactDamageInterval = 1f;
    [SerializeField] private float contactAttackJumpHeight = 0.38f;
    [SerializeField] private float contactAttackJumpDuration = 0.22f;
    [SerializeField] private float hitJitterDuration = 0.1f;
    [SerializeField] private float hitJitterAmplitude = 0.08f;
    [SerializeField] private float hitSlowMultiplier = 0.5f;
    [SerializeField] private float hitSlowDuration = 0.3f;
    [SerializeField] private int addsPerPhase = 2;
    [SerializeField] private float periodicAddsInterval = 11f;
    [SerializeField] private float[] phaseThresholds = { 0.6f, 0.3f };

    private float _currentHp;
    private float _nextPeriodicAddTick;
    private float _nextContactDamageTick;
    private float _attackJumpStartTime;
    private float _attackJumpEndTime;
    private float _hitJitterUntil;
    private float _slowedUntil;
    private int _phaseIndex;
    private bool _isAlive;
    private bool _isInContactPhase;
    private Transform _spriteQuad;
    private Vector3 _spriteBaseLocalPosition;
    private bool _spriteBaseLocalPositionSet;

    private EnemySpawner _enemySpawner;
    private SquadController _targetSquad;
    private GameManager _gameManager;
    private EnemyType _addType;

    public float HealthNormalized => Mathf.Clamp01(_currentHp / Mathf.Max(1f, maxHp));

    private void Awake()
    {
        EnsurePhysics();
    }

    public void Initialize(EnemySpawner spawner, SquadController squad, GameManager gameManager, EnemyType addType)
    {
        _enemySpawner = spawner;
        _targetSquad = squad;
        _gameManager = gameManager;
        _addType = addType;

        _currentHp = maxHp;
        _phaseIndex = 0;
        _nextPeriodicAddTick = Time.time + periodicAddsInterval;
        _nextContactDamageTick = Time.time;
        _attackJumpStartTime = -1f;
        _attackJumpEndTime = -1f;
        _hitJitterUntil = 0f;
        _slowedUntil = 0f;
        _isAlive = true;
        _isInContactPhase = false;

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = new Color(0.9f, 0.25f, 0.2f);
        }

        ApplyBossSprite();
        _gameManager.RegisterBoss(this, transform.position.z);
    }

    private void Update()
    {
        if (!_isAlive || _targetSquad == null || _gameManager == null || !_gameManager.IsRunning)
        {
            return;
        }

        float forwardGap = transform.position.z - _targetSquad.transform.position.z;
        if (_isInContactPhase || forwardGap <= loseDistance)
        {
            HandleContactPhase();
            UpdateAttackJumpVisual();
            return;
        }

        Vector3 toSquad = _targetSquad.transform.position - transform.position;
        toSquad.y = 0f;

        float speedMultiplier = Time.time < _slowedUntil ? hitSlowMultiplier : 1f;
        transform.position += toSquad.normalized * (moveSpeed * speedMultiplier * Time.deltaTime);

        if (_phaseIndex < phaseThresholds.Length && HealthNormalized <= phaseThresholds[_phaseIndex])
        {
            SpawnAdds(addsPerPhase + _phaseIndex);
            _phaseIndex++;
        }

        if (Time.time >= _nextPeriodicAddTick)
        {
            SpawnAdds(Mathf.Max(1, addsPerPhase / 2));
            _nextPeriodicAddTick = Time.time + periodicAddsInterval;
        }

        UpdateAttackJumpVisual();
    }

    public void TakeDamage(float amount)
    {
        if (!_isAlive)
        {
            return;
        }

        _currentHp -= Mathf.Max(0f, amount);
        _slowedUntil = Time.time + hitSlowDuration;
        _hitJitterUntil = Time.time + Mathf.Max(0.01f, hitJitterDuration);

        if (_currentHp <= 0f)
        {
            _isAlive = false;
            _currentHp = 0f;
            _gameManager.OnBossDefeated();
            gameObject.SetActive(false);
        }
    }

    private void SpawnAdds(int count)
    {
        _enemySpawner?.SpawnAddsNearBoss(transform.position.z, count, _addType);
    }

    private void HandleContactPhase()
    {
        if (!_isInContactPhase)
        {
            _isInContactPhase = true;
            _nextContactDamageTick = Time.time;
        }

        // Keep boss centered in lane while staying in front of the squad.
        Vector3 targetPosition = transform.position;
        targetPosition.x = Mathf.MoveTowards(transform.position.x, 0f, moveSpeed * 2.5f * Time.deltaTime);
        targetPosition.z = _targetSquad.transform.position.z + loseDistance;
        transform.position = targetPosition;

        if (Time.time < _nextContactDamageTick)
        {
            return;
        }

        _nextContactDamageTick = Time.time + Mathf.Max(0.15f, contactDamageInterval);

        _gameManager.ApplyPlayerDamage(Mathf.Max(1, contactDamagePerTick));
        _targetSquad.PlayBossDamageVfx();
        TriggerAttackJump();
    }

    private void TriggerAttackJump()
    {
        _attackJumpStartTime = Time.time;
        _attackJumpEndTime = Time.time + Mathf.Max(0.06f, contactAttackJumpDuration);
    }

    private void UpdateAttackJumpVisual()
    {
        if (_spriteQuad == null || !_spriteBaseLocalPositionSet)
        {
            return;
        }

        bool jumpActive = Time.time <= _attackJumpEndTime && _attackJumpEndTime > _attackJumpStartTime;
        bool jitterActive = Time.time <= _hitJitterUntil;

        if (!jumpActive && !jitterActive)
        {
            if (_spriteQuad.localPosition != _spriteBaseLocalPosition)
            {
                _spriteQuad.localPosition = _spriteBaseLocalPosition;
            }

            return;
        }

        float jumpOffset = 0f;
        if (jumpActive)
        {
            float duration = _attackJumpEndTime - _attackJumpStartTime;
            float normalized = Mathf.Clamp01((Time.time - _attackJumpStartTime) / Mathf.Max(0.0001f, duration));
            jumpOffset = Mathf.Sin(normalized * Mathf.PI) * Mathf.Max(0.01f, contactAttackJumpHeight);
        }

        Vector3 localPos = _spriteBaseLocalPosition + new Vector3(0f, jumpOffset, 0f);
        if (jitterActive)
        {
            float amp = Mathf.Max(0.01f, hitJitterAmplitude);
            localPos.x += Random.Range(-amp, amp);
            localPos.y += Random.Range(-amp * 0.6f, amp * 0.6f);
        }

        _spriteQuad.localPosition = localPos;
    }

    private void EnsurePhysics()
    {
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<CapsuleCollider>();
        }

        collider.isTrigger = true;

        var body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void ApplyBossSprite()
    {
        var texture = RuntimeVisualLibrary.BossTexture;
        var bodyRenderer = GetComponent<Renderer>();

        if (texture == null)
        {
            if (bodyRenderer != null)
            {
                bodyRenderer.enabled = true;
            }

            if (_spriteQuad != null)
            {
                _spriteQuad.gameObject.SetActive(false);
            }

            return;
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = false;
        }

        if (_spriteQuad == null)
        {
            var spriteGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spriteGo.name = "BossSprite";
            spriteGo.transform.SetParent(transform, false);

            var quadCollider = spriteGo.GetComponent<Collider>();
            if (quadCollider != null)
            {
                Destroy(quadCollider);
            }

            _spriteQuad = spriteGo.transform;
        }

        _spriteQuad.gameObject.SetActive(true);
        _spriteQuad.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var wobble = _spriteQuad.GetComponent<SpriteWobble>();
        if (wobble == null)
        {
            wobble = _spriteQuad.gameObject.AddComponent<SpriteWobble>();
        }

        wobble.ResetBaseRotation(_spriteQuad.localRotation);
        wobble.Configure(3.8f, 4.8f, Vector3.forward);
        const float spriteLift = 0.35f; // Lift sprite so feet don't look sunk into the lane.

        var collider = GetComponent<Collider>();
        if (collider != null)
        {
            Vector3 hitboxSizeWorld = collider.bounds.size;
            Vector3 hitboxCenterWorld = collider.bounds.center;
            Vector3 lossy = transform.lossyScale;

            float localWidth = hitboxSizeWorld.x / Mathf.Max(0.0001f, lossy.x);
            float localHeight = hitboxSizeWorld.y / Mathf.Max(0.0001f, lossy.y);

            // Width increased by 1.5x from previous size.
            _spriteQuad.localScale = new Vector3(localWidth * 3f, localHeight * 2f, 1f);
            _spriteQuad.localPosition = transform.InverseTransformPoint(hitboxCenterWorld) + new Vector3(0f, spriteLift, 0f);
        }
        else
        {
            _spriteQuad.localScale = new Vector3(2f, 2f, 1f);
            _spriteQuad.localPosition = new Vector3(0f, 1f + spriteLift, 0f);
        }

        _spriteBaseLocalPosition = _spriteQuad.localPosition;
        _spriteBaseLocalPositionSet = true;

        var quadRenderer = _spriteQuad.GetComponent<Renderer>();
        if (quadRenderer != null)
        {
            var material = RuntimeVisualLibrary.GetTexturedMaterial(texture);
            if (material != null)
            {
                quadRenderer.material = material;
            }
        }
    }
}
