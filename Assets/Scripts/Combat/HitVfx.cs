using System;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class HitVfx : MonoBehaviour, IPoolable
{
    [SerializeField] private float lifeTime = 0.32f;
    [SerializeField] private int particleCount = 28;

    private float _remainingLife;
    private Action<HitVfx> _releaseAction;
    private ParticleSystem _particleSystem;

    private void Awake()
    {
        EnsureParticleSystem();
    }

    private void Update()
    {
        if (!gameObject.activeSelf)
        {
            return;
        }

        _remainingLife -= Time.deltaTime;
        if (_remainingLife <= 0f)
        {
            _releaseAction?.Invoke(this);
        }
    }

    public void Initialize(Action<HitVfx> releaseAction)
    {
        _releaseAction = releaseAction;
        EnsureParticleSystem();
    }

    public void Play(Vector3 worldPosition, Color color)
    {
        EnsureParticleSystem();

        transform.position = worldPosition;
        transform.rotation = Quaternion.identity;

        var main = _particleSystem.main;
        Color brightColor = Color.Lerp(color, Color.white, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(color, brightColor);

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _particleSystem.Clear(true);
        _particleSystem.Emit(Mathf.Max(1, particleCount));
        _remainingLife = Mathf.Max(0.05f, lifeTime);
    }

    public void OnSpawned()
    {
        _remainingLife = Mathf.Max(0.05f, lifeTime);
    }

    public void OnDespawned()
    {
        _remainingLife = 0f;
        if (_particleSystem != null)
        {
            _particleSystem.Clear(true);
        }
    }

    private void EnsureParticleSystem()
    {
        if (_particleSystem != null)
        {
            return;
        }

        _particleSystem = GetComponent<ParticleSystem>();
        if (_particleSystem == null)
        {
            _particleSystem = gameObject.AddComponent<ParticleSystem>();
        }

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _particleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(5.8f, 9.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.maxParticles = 96;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.05f;

        var emission = _particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = _particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;

        var velocityOverLifetime = _particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = false;

        var sizeOverLifetime = _particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1.15f);
        sizeCurve.AddKey(1f, 0.05f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var colorOverLifetime = _particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var fadeGradient = new Gradient();
        fadeGradient.SetKeys(
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
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(fadeGradient);

        var renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            Shader shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            }

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
}
