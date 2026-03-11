using System;
using System.Collections.Generic;
using UnityEngine;

public class SquadController : MonoBehaviour
{
    private static readonly Vector3 PlayerSpriteBaseLocalPosition = new Vector3(0f, 4.34f, 0f);

    [Header("Movement")]
    [SerializeField] private float forwardSpeed = 5.5f; // Tuned for fast hypercasual pace.
    [SerializeField] private float lateralMoveSpeed = 20f;
    [SerializeField] private float lateralLimit = 5f;

    [Header("Soldiers")]
    [SerializeField] private int maxVisualSoldiers = 150; // Hard cap for rendering cost.
    [SerializeField] private float formationSpacing = 0.42f;
    [SerializeField] private float damageJitterAmplitude = 0.22f;

    private readonly List<Transform> _visualSoldiers = new List<Transform>();
    private readonly List<Transform> _playerSpriteVisuals = new List<Transform>();
    private float _targetNormalizedX;
    private int _logicalSoldierCount;
    private ParticleSystem _bossDamageVfx;
    private float _damageJitterUntil;

    public event Action<int> SoldierCountChanged;

    public int LogicalSoldierCount => _logicalSoldierCount;
    public float CurrentRunZ => transform.position.z;
    public float FormationHalfWidth { get; private set; }
    public float TargetNormalizedX => _targetNormalizedX;

    private void Awake()
    {
        EnsureBodyCollider();
    }

    private void Update()
    {
        if (!GameManager.Instance.IsRunning)
        {
            if (_damageJitterUntil > 0f)
            {
                _damageJitterUntil = 0f;
                UpdateDamageJitterVisual();
            }

            return;
        }

        var position = transform.position;
        float targetX = _targetNormalizedX * lateralLimit;

        position.x = Mathf.MoveTowards(position.x, targetX, lateralMoveSpeed * Time.deltaTime);
        position.z += forwardSpeed * Time.deltaTime;

        transform.position = position;
        UpdateDamageJitterVisual();
    }

    public void ResetRunPosition()
    {
        transform.position = new Vector3(0f, 0f, 0f);
        _targetNormalizedX = 0f;
    }

    public void SetLateralInput(float normalizedX)
    {
        _targetNormalizedX = Mathf.Clamp(normalizedX, -1f, 1f);
    }

    public void SetSoldierCount(int value)
    {
        _logicalSoldierCount = Mathf.Max(0, value);
        RefreshVisualSoldiers();
        _damageJitterUntil = 0f;
        UpdateDamageJitterVisual();
        SoldierCountChanged?.Invoke(_logicalSoldierCount);
    }

    public void AddSoldiers(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetSoldierCount(_logicalSoldierCount + amount);
    }

    public void RemoveSoldiers(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetSoldierCount(_logicalSoldierCount - amount);
    }

    public void PlayBossDamageVfx()
    {
        PlayDamageFeedback(34, 0f);
    }

    public void PlayZombieDamageFeedback()
    {
        PlayDamageFeedback(34, 0.1f);
    }

    private void PlayDamageFeedback(int burstCount, float jitterDuration)
    {
        EnsureBossDamageVfx();
        if (_bossDamageVfx == null)
        {
            return;
        }

        float halfWidth = Mathf.Max(0.3f, FormationHalfWidth * 0.55f);
        Vector3 worldPos = transform.position +
            new Vector3(UnityEngine.Random.Range(-halfWidth, halfWidth), 0.9f, UnityEngine.Random.Range(-0.45f, 0.45f));

        _bossDamageVfx.transform.position = worldPos;
        _bossDamageVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _bossDamageVfx.Emit(Mathf.Max(1, burstCount));
        _damageJitterUntil = Mathf.Max(_damageJitterUntil, Time.time + Mathf.Max(0f, jitterDuration));
    }

    private void RefreshVisualSoldiers()
    {
        int visibleCount = Mathf.Min(_logicalSoldierCount, maxVisualSoldiers);

        while (_visualSoldiers.Count < visibleCount)
        {
            var unit = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            unit.name = "SoldierVisual";
            unit.transform.SetParent(transform, false);
            unit.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);

            var collider = unit.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            var renderer = unit.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.2f, 0.75f, 0.95f);
            }

            var spriteVisual = ApplyPlayerSprite(unit.transform);
            _visualSoldiers.Add(unit.transform);
            _playerSpriteVisuals.Add(spriteVisual);
        }

        for (int i = 0; i < _visualSoldiers.Count; i++)
        {
            _visualSoldiers[i].gameObject.SetActive(i < visibleCount);
        }

        if (visibleCount == 0)
        {
            FormationHalfWidth = 0.5f;
            return;
        }

        int columns = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(visibleCount)), 1, 24);
        int rows = Mathf.CeilToInt(visibleCount / (float)columns);

        FormationHalfWidth = (columns - 1) * formationSpacing * 0.5f + 0.25f;

        for (int i = 0; i < visibleCount; i++)
        {
            int row = i / columns;
            int column = i % columns;

            float x = (column - (columns - 1) * 0.5f) * formationSpacing;
            float z = -(row * formationSpacing);

            _visualSoldiers[i].localPosition = new Vector3(x, 0.5f, z);
        }

        float centerOffset = (rows - 1) * formationSpacing * 0.5f;
        for (int i = 0; i < visibleCount; i++)
        {
            var local = _visualSoldiers[i].localPosition;
            local.z += centerOffset;
            _visualSoldiers[i].localPosition = local;
        }
    }

    private void EnsureBodyCollider()
    {
        var capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = gameObject.AddComponent<CapsuleCollider>();
        }

        capsule.radius = 1.2f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.isTrigger = false;

        var rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void EnsureBossDamageVfx()
    {
        if (_bossDamageVfx != null)
        {
            return;
        }

        var vfxGo = new GameObject("BossDamageVfx");
        vfxGo.transform.SetParent(transform, false);
        vfxGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);

        _bossDamageVfx = vfxGo.AddComponent<ParticleSystem>();
        var main = _bossDamageVfx.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(4.5f, 7.4f);
        // Doubled hit-burst particle size for stronger impact readability.
        main.startSize = new ParticleSystem.MinMaxCurve(0.24f, 0.44f);
        main.maxParticles = 80;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.95f, 0.12f, 0.12f, 1f),
            new Color(1f, 0.55f, 0.15f, 1f));

        var emission = _bossDamageVfx.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = _bossDamageVfx.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var sizeOverLifetime = _bossDamageVfx.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1.15f);
        sizeCurve.AddKey(1f, 0.08f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = _bossDamageVfx.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var renderer = _bossDamageVfx.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Legacy Shaders/Particles/Additive");
            }

            if (shader != null)
            {
                renderer.material = new Material(shader);
            }
        }
    }

    private void UpdateDamageJitterVisual()
    {
        bool jitterActive = Time.time < _damageJitterUntil;
        float amp = Mathf.Max(0.001f, damageJitterAmplitude);

        int count = Mathf.Min(_playerSpriteVisuals.Count, _visualSoldiers.Count);
        for (int i = 0; i < count; i++)
        {
            if (_visualSoldiers[i] == null || !_visualSoldiers[i].gameObject.activeInHierarchy)
            {
                continue;
            }

            var sprite = _playerSpriteVisuals[i];
            if (sprite == null)
            {
                continue;
            }

            if (!jitterActive)
            {
                if (sprite.localPosition != PlayerSpriteBaseLocalPosition)
                {
                    sprite.localPosition = PlayerSpriteBaseLocalPosition;
                }

                continue;
            }

            sprite.localPosition = PlayerSpriteBaseLocalPosition + new Vector3(
                UnityEngine.Random.Range(-amp, amp),
                UnityEngine.Random.Range(-amp * 0.45f, amp * 0.45f),
                0f);
        }
    }

    private static Transform ApplyPlayerSprite(Transform unitRoot)
    {
        var texture = RuntimeVisualLibrary.PlayerTexture;
        if (texture == null)
        {
            return null;
        }

        var meshRenderer = unitRoot.GetComponent<Renderer>();
        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        var spriteQuad = unitRoot.Find("PlayerSprite");
        if (spriteQuad == null)
        {
            var spriteGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            spriteGo.name = "PlayerSprite";
            spriteGo.transform.SetParent(unitRoot, false);

            var quadCollider = spriteGo.GetComponent<Collider>();
            if (quadCollider != null)
            {
                Destroy(quadCollider);
            }

            spriteQuad = spriteGo.transform;
        }

        // Move down by 30% of current offset so sprite aligns better with hitbox and road.
        spriteQuad.localPosition = PlayerSpriteBaseLocalPosition;
        spriteQuad.localRotation = Quaternion.Euler(0f, 180f, 0f);
        // Reduce vertical stretch by 30% while keeping width unchanged.
        spriteQuad.localScale = new Vector3(6.75f, 8.505f, 1f);

        var wobble = spriteQuad.GetComponent<SpriteWobble>();
        if (wobble == null)
        {
            wobble = spriteQuad.gameObject.AddComponent<SpriteWobble>();
        }

        wobble.ResetBaseRotation(spriteQuad.localRotation);
        wobble.Configure(4.5f, 6.2f, Vector3.forward);

        var quadRenderer = spriteQuad.GetComponent<Renderer>();
        if (quadRenderer != null)
        {
            var material = RuntimeVisualLibrary.GetTexturedMaterial(texture);
            if (material != null)
            {
                quadRenderer.material = material;
            }
        }

        return spriteQuad;
    }
}
