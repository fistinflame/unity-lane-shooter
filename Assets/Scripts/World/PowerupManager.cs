using System.Collections.Generic;
using UnityEngine;

public class PowerupManager : MonoBehaviour
{
    private struct ActiveEffect
    {
        public PowerupKind Kind;
        public float Multiplier;
        public int Additive;
        public float ExpiresAt;
    }

    private readonly List<ActiveEffect> _activeEffects = new List<ActiveEffect>();

    public float FireRateMultiplier { get; private set; } = 1f;
    public float DamageMultiplier { get; private set; } = 1f;
    public int TemporaryTierBonus { get; private set; }
    public int PiercingBonus { get; private set; }

    private void Update()
    {
        if (_activeEffects.Count == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = _activeEffects.Count - 1; i >= 0; i--)
        {
            if (Time.time >= _activeEffects[i].ExpiresAt)
            {
                _activeEffects.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            RecalculateValues();
        }
    }

    public void ApplyPowerup(PowerupType type)
    {
        if (type == null)
        {
            return;
        }

        if (type.kind == PowerupKind.Piercing)
        {
            for (int i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i].Kind == PowerupKind.Piercing)
                {
                    _activeEffects.RemoveAt(i);
                }
            }

            _activeEffects.Add(new ActiveEffect
            {
                Kind = PowerupKind.Piercing,
                Multiplier = 1f,
                Additive = 1,
                ExpiresAt = Time.time + 5f // Piercing is always exactly 5 seconds.
            });

            RecalculateValues();
            return;
        }

        var effect = new ActiveEffect
        {
            Kind = type.kind,
            Multiplier = Mathf.Max(1f, type.magnitude),
            Additive = Mathf.Max(1, Mathf.RoundToInt(type.magnitude)),
            ExpiresAt = Time.time + Mathf.Max(0.25f, type.duration)
        };

        _activeEffects.Add(effect);
        RecalculateValues();
    }

    public void ResetRunEffects()
    {
        _activeEffects.Clear();
        RecalculateValues();
    }

    public float GetRemainingDuration(PowerupKind kind)
    {
        float remaining = 0f;
        for (int i = 0; i < _activeEffects.Count; i++)
        {
            if (_activeEffects[i].Kind != kind)
            {
                continue;
            }

            remaining = Mathf.Max(remaining, _activeEffects[i].ExpiresAt - Time.time);
        }

        return Mathf.Max(0f, remaining);
    }

    private void RecalculateValues()
    {
        FireRateMultiplier = 1f;
        DamageMultiplier = 1f;
        TemporaryTierBonus = 0;
        PiercingBonus = 0;

        for (int i = 0; i < _activeEffects.Count; i++)
        {
            var effect = _activeEffects[i];
            switch (effect.Kind)
            {
                case PowerupKind.FireRateBoost:
                    FireRateMultiplier *= effect.Multiplier;
                    break;
                case PowerupKind.DamageBoost:
                    DamageMultiplier *= effect.Multiplier;
                    break;
                case PowerupKind.TierUpgrade:
                    TemporaryTierBonus += effect.Additive;
                    break;
                case PowerupKind.Piercing:
                    PiercingBonus += effect.Additive;
                    break;
            }
        }
    }
}
