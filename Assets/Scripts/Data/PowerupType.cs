using UnityEngine;

public enum PowerupKind
{
    FireRateBoost,
    DamageBoost,
    TierUpgrade,
    Piercing
}

[CreateAssetMenu(menuName = "Hypercasual/Powerup Type", fileName = "PowerupType")]
public class PowerupType : ScriptableObject
{
    public string displayName = "Fire Rate";
    public PowerupKind kind = PowerupKind.FireRateBoost;
    public float magnitude = 1.5f;
    public float duration = 6f;
}
