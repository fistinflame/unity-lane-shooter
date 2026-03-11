using UnityEngine;

public class Gate : MonoBehaviour
{
    [SerializeField] private int initialValue = -8;
    [SerializeField] private float improveFactor = 0.35f;
    [SerializeField] private float maxHp = 80f;
    [SerializeField] private int minGateValue = -999;
    [SerializeField] private int maxGateValue = 20;

    private float _appliedImproveFactor;
    private float _remainingHp;
    private float _currentValueFloat;
    private int _currentValue;
    private bool _consumed;
    private TextMesh _valueText;
    private Material _runtimeMaterial;

    public int CurrentValue => _currentValue;

    private void Awake()
    {
        EnsureCollider();
        EnsureLabel();
        EnsureRuntimeMaterial();
    }

    private void OnEnable()
    {
        _consumed = false;
        _remainingHp = maxHp;
        _currentValueFloat = initialValue;
        _currentValue = Mathf.RoundToInt(_currentValueFloat);

        if (_appliedImproveFactor <= 0f)
        {
            _appliedImproveFactor = Mathf.Max(0f, improveFactor);
        }

        RefreshVisual();
    }

    public void Configure(int startValue, float improveFactorMultiplier)
    {
        initialValue = Mathf.Clamp(startValue, minGateValue, maxGateValue);
        _appliedImproveFactor = Mathf.Max(0f, improveFactor) * Mathf.Max(0f, improveFactorMultiplier);

        _consumed = false;
        _remainingHp = maxHp;
        _currentValueFloat = initialValue;
        _currentValue = Mathf.RoundToInt(_currentValueFloat);

        RefreshVisual();
    }

    public void ReceiveBulletDamage(float damage)
    {
        if (_consumed || _remainingHp <= 0f)
        {
            return;
        }

        _currentValueFloat += damage * _appliedImproveFactor;
        _currentValue = Mathf.Clamp(Mathf.RoundToInt(_currentValueFloat), minGateValue, maxGateValue);

        _remainingHp -= damage;
        RefreshVisual();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_consumed)
        {
            return;
        }

        var squad = other.GetComponent<SquadController>();
        if (squad == null)
        {
            return;
        }

        if (_currentValue >= 0)
        {
            squad.AddSoldiers(_currentValue);
        }
        else
        {
            squad.RemoveSoldiers(Mathf.Abs(_currentValue));
        }

        _consumed = true;
        gameObject.SetActive(false);
    }

    private void RefreshVisual()
    {
        if (_valueText != null)
        {
            _valueText.text = _currentValue >= 0 ? "+" + _currentValue : _currentValue.ToString();
            _valueText.color = _currentValue >= 0 ? Color.green : Color.red;
        }

        var renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            if (_runtimeMaterial == null)
            {
                EnsureRuntimeMaterial();
            }

            Color gateColor = _currentValue >= 0
                ? new Color(0.2f, 0.85f, 0.25f, 0.5f)
                : new Color(0.85f, 0.2f, 0.2f, 0.5f);

            if (_runtimeMaterial != null)
            {
                if (_runtimeMaterial.HasProperty("_Color"))
                {
                    _runtimeMaterial.SetColor("_Color", gateColor);
                }

                if (_runtimeMaterial.HasProperty("_BaseColor"))
                {
                    _runtimeMaterial.SetColor("_BaseColor", gateColor);
                }
            }
            else
            {
                renderer.material.color = gateColor;
            }
        }
    }

    private void EnsureCollider()
    {
        var collider = GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider>();
        }

        // Keep transform scale and collider in sync so visual size == hitbox size.
        transform.localScale = new Vector3(2.4f, 1.6f, 0.45f);
        collider.isTrigger = true;
        collider.size = Vector3.one;
        collider.center = Vector3.zero;
    }

    private void EnsureLabel()
    {
        if (_valueText != null)
        {
            return;
        }

        var textGo = new GameObject("GateValue");
        textGo.transform.SetParent(transform, false);
        textGo.transform.localPosition = new Vector3(0f, 2.1f, 0f);

        _valueText = textGo.AddComponent<TextMesh>();
        _valueText.fontSize = 80;
        _valueText.characterSize = 0.06f;
        _valueText.anchor = TextAnchor.MiddleCenter;
        _valueText.alignment = TextAlignment.Center;
    }

    private void EnsureRuntimeMaterial()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer == null || _runtimeMaterial != null)
        {
            return;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
        }

        if (shader == null)
        {
            return;
        }

        _runtimeMaterial = new Material(shader);
        renderer.material = _runtimeMaterial;
    }
}
