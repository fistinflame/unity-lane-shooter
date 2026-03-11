using System;
using UnityEngine;

public class Enemy : MonoBehaviour, IPoolable
{
    [SerializeField] private EnemyType defaultType;
    [SerializeField] private float squadReachZ = 0.65f;
    [SerializeField] private float contactAttackDuration = 0.3f;
    [SerializeField] private float contactAttackJumpHeight = 0.52f;
    [SerializeField] private float deathFadeDuration = 0.5f;
    [SerializeField] private float deathTiltDegrees = 90f;

    private EnemyType _activeType;
    private SquadController _targetSquad;
    private Action<Enemy> _releaseAction;
    private float _currentHp;
    private bool _isAlive;
    private bool _isDying;
    private bool _isAttacking;
    private float _attackStartTime;
    private float _attackEndTime;
    private Vector3 _attackStartPosition;
    private float _deathStartTime;
    private float _deathEndTime;
    private int _deathTiltSign;
    private Collider _enemyCollider;
    private Rigidbody _enemyBody;
    private Renderer _bodyRenderer;
    private Transform _spriteQuad;
    private Renderer _spriteRenderer;
    private MaterialPropertyBlock _materialPropertyBlock;

    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        EnsurePhysics();
        EnsurePropertyBlock();
    }

    private void Update()
    {
        if (_isAttacking)
        {
            UpdateAttackSequence();
            return;
        }

        if (_isDying)
        {
            UpdateDeathSequence();
            return;
        }

        if (!_isAlive || _targetSquad == null || !GameManager.Instance.IsRunning)
        {
            return;
        }

        // Enemy damages player once it reaches the squad depth, even if lateral dodge occurs.
        if (transform.position.z <= _targetSquad.transform.position.z + squadReachZ)
        {
            BeginContactAttack();
            return;
        }

        Vector3 toTarget = _targetSquad.transform.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.001f)
        {
            transform.position += toTarget.normalized * (_activeType.moveSpeed * Time.deltaTime);
        }
    }

    public void Initialize(EnemyType type, SquadController targetSquad, Action<Enemy> releaseAction)
    {
        _activeType = type != null ? type : defaultType;
        if (_activeType == null)
        {
            _activeType = CreateFallbackType();
        }

        _targetSquad = targetSquad;
        _releaseAction = releaseAction;
        _currentHp = _activeType.maxHealth;
        _isAlive = true;
        _isDying = false;
        _isAttacking = false;
        _attackStartTime = 0f;
        _attackEndTime = 0f;
        _attackStartPosition = Vector3.zero;
        _deathStartTime = 0f;
        _deathEndTime = 0f;

        transform.localScale = Vector3.one * Mathf.Max(0.2f, _activeType.scale);
        transform.rotation = Quaternion.identity;
        SetCollisionEnabled(true);

        _bodyRenderer = GetComponent<Renderer>();
        if (_bodyRenderer != null)
        {
            _bodyRenderer.enabled = true;
            ApplyRendererColor(_bodyRenderer, _activeType.tint);
        }

        ApplyZombieSprite();
    }

    public void TakeDamage(float amount)
    {
        if (!_isAlive || _isDying || _isAttacking)
        {
            return;
        }

        _currentHp -= amount;
        if (_currentHp <= 0f)
        {
            BeginDeathSequence();
        }
    }

    public void OnSpawned()
    {
        _isAlive = true;
        _isDying = false;
        _isAttacking = false;
        _attackStartTime = 0f;
        _attackEndTime = 0f;
        _attackStartPosition = Vector3.zero;
        _deathStartTime = 0f;
        _deathEndTime = 0f;
    }

    public void OnDespawned()
    {
        _targetSquad = null;
        _releaseAction = null;
        _currentHp = 0f;
        _isAlive = false;
        _isDying = false;
        _isAttacking = false;
        _attackStartTime = 0f;
        _attackEndTime = 0f;
        _attackStartPosition = Vector3.zero;
        _deathStartTime = 0f;
        _deathEndTime = 0f;
        transform.rotation = Quaternion.identity;
        transform.position = new Vector3(transform.position.x, 0.55f, transform.position.z);
        SetCollisionEnabled(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_isAlive || _isDying || _isAttacking)
        {
            return;
        }

        var squad = other.GetComponent<SquadController>();
        if (squad == null)
        {
            return;
        }

        BeginContactAttack();
    }

    private void DespawnImmediate()
    {
        if (!_isAlive && !_isDying && !_isAttacking)
        {
            return;
        }

        _isAlive = false;
        _isDying = false;
        _isAttacking = false;
        _releaseAction?.Invoke(this);
    }

    private void EnsurePhysics()
    {
        _enemyCollider = GetComponent<Collider>();
        if (_enemyCollider == null)
        {
            _enemyCollider = gameObject.AddComponent<BoxCollider>();
        }

        _enemyCollider.isTrigger = true;

        _enemyBody = GetComponent<Rigidbody>();
        if (_enemyBody == null)
        {
            _enemyBody = gameObject.AddComponent<Rigidbody>();
        }

        _enemyBody.useGravity = false;
        _enemyBody.isKinematic = true;
        _enemyBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private static EnemyType CreateFallbackType()
    {
        var type = ScriptableObject.CreateInstance<EnemyType>();
        type.typeId = "Fallback";
        type.maxHealth = 1f;
        type.moveSpeed = 3f;
        type.contactDamage = 1;
        type.scale = 0.85f;
        type.tint = new Color(0.9f, 0.25f, 0.25f);
        return type;
    }

    private void ApplyZombieSprite()
    {
        var texture = RuntimeVisualLibrary.ZombieTexture;
        _bodyRenderer = GetComponent<Renderer>();
        if (texture == null)
        {
            if (_bodyRenderer != null)
            {
                _bodyRenderer.enabled = true;
                ApplyRendererColor(_bodyRenderer, _activeType != null ? _activeType.tint : new Color(0.95f, 0.32f, 0.25f));
            }

            var existing = transform.Find("ZombieSprite");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                _spriteQuad = existing;
                _spriteRenderer = existing.GetComponent<Renderer>();
            }

            return;
        }

        if (_bodyRenderer != null)
        {
            _bodyRenderer.enabled = false;
        }

        var spriteQuad = transform.Find("ZombieSprite");
        if (spriteQuad == null)
        {
            var spriteGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spriteGo.name = "ZombieSprite";
            spriteGo.transform.SetParent(transform, false);

            var quadCollider = spriteGo.GetComponent<Collider>();
            if (quadCollider != null)
            {
                Destroy(quadCollider);
            }

            spriteQuad = spriteGo.transform;
        }

        spriteQuad.gameObject.SetActive(true);
        // Move down by 30% of current offset so sprite aligns better with hitbox and road.
        spriteQuad.localPosition = new Vector3(0f, 2.73f, 0f);
        spriteQuad.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // Reduce vertical stretch by 30% while keeping width unchanged.
        spriteQuad.localScale = new Vector3(5.7f, 5.25f, 1f);

        var wobble = spriteQuad.GetComponent<SpriteWobble>();
        if (wobble == null)
        {
            wobble = spriteQuad.gameObject.AddComponent<SpriteWobble>();
        }

        wobble.ResetBaseRotation(spriteQuad.localRotation);
        wobble.Configure(6f, 7.8f, Vector3.forward);

        _spriteQuad = spriteQuad;
        _spriteRenderer = spriteQuad.GetComponent<Renderer>();
        if (_spriteRenderer != null)
        {
            var material = RuntimeVisualLibrary.GetTexturedMaterial(texture);
            if (material != null)
            {
                _spriteRenderer.material = material;
            }

            ApplyRendererColor(_spriteRenderer, Color.white);
        }
    }

    private void BeginDeathSequence()
    {
        if (_isDying || _isAttacking || !_isAlive)
        {
            return;
        }

        _isAlive = false;
        _isDying = true;
        _deathStartTime = Time.time;
        _deathEndTime = _deathStartTime + Mathf.Max(0.05f, deathFadeDuration);
        _deathTiltSign = UnityEngine.Random.value < 0.5f ? -1 : 1;

        // Remove hitbox immediately so destroyed enemies never block anything.
        SetCollisionEnabled(false);

        var deathColor = new Color(1f, 0.12f, 0.12f, 1f);
        ApplyRendererColor(_bodyRenderer, deathColor);
        ApplyRendererColor(_spriteRenderer, deathColor);
    }

    private void BeginContactAttack()
    {
        if (_isAttacking || _isDying || !_isAlive)
        {
            return;
        }

        _isAlive = false;
        _isAttacking = true;
        _attackStartTime = Time.time;
        _attackEndTime = _attackStartTime + Mathf.Max(0.05f, contactAttackDuration);
        _attackStartPosition = transform.position;

        SetCollisionEnabled(false);

        if (_activeType != null)
        {
            GameManager.Instance.ApplyPlayerDamage(_activeType.contactDamage);
        }

        _targetSquad?.PlayZombieDamageFeedback();
    }

    private void UpdateAttackSequence()
    {
        float duration = Mathf.Max(0.05f, contactAttackDuration);
        float t = Mathf.Clamp01((Time.time - _attackStartTime) / duration);
        float jump = Mathf.Sin(t * Mathf.PI) * Mathf.Max(0.01f, contactAttackJumpHeight);

        transform.position = new Vector3(_attackStartPosition.x, _attackStartPosition.y + jump, _attackStartPosition.z);

        if (Time.time >= _attackEndTime)
        {
            _isAttacking = false;
            _releaseAction?.Invoke(this);
        }
    }

    private void UpdateDeathSequence()
    {
        float duration = Mathf.Max(0.05f, deathFadeDuration);
        float t = Mathf.Clamp01((Time.time - _deathStartTime) / duration);

        float tilt = Mathf.Lerp(0f, deathTiltDegrees * _deathTiltSign, t);
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);

        float alpha = Mathf.Lerp(1f, 0f, t);
        var fadeColor = new Color(1f, 0.12f, 0.12f, alpha);
        ApplyRendererColor(_bodyRenderer, fadeColor);
        ApplyRendererColor(_spriteRenderer, fadeColor);

        if (Time.time >= _deathEndTime)
        {
            _isDying = false;
            _releaseAction?.Invoke(this);
        }
    }

    private void SetCollisionEnabled(bool enabled)
    {
        if (_enemyCollider != null)
        {
            _enemyCollider.enabled = enabled;
        }

        if (_enemyBody != null)
        {
            _enemyBody.detectCollisions = enabled;
        }
    }

    private void ApplyRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        EnsurePropertyBlock();

        var sharedMaterial = renderer.sharedMaterial;
        bool hasColor = sharedMaterial != null && sharedMaterial.HasProperty(ColorId);
        bool hasBaseColor = sharedMaterial != null && sharedMaterial.HasProperty(BaseColorId);

        if (!hasColor && !hasBaseColor)
        {
            renderer.material.color = color;
            return;
        }

        renderer.GetPropertyBlock(_materialPropertyBlock);
        if (hasColor)
        {
            _materialPropertyBlock.SetColor(ColorId, color);
        }

        if (hasBaseColor)
        {
            _materialPropertyBlock.SetColor(BaseColorId, color);
        }

        renderer.SetPropertyBlock(_materialPropertyBlock);
    }

    private void EnsurePropertyBlock()
    {
        if (_materialPropertyBlock == null)
        {
            _materialPropertyBlock = new MaterialPropertyBlock();
        }
    }
}
