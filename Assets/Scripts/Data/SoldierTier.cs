using UnityEngine;

[CreateAssetMenu(menuName = "Hypercasual/Soldier Tier", fileName = "SoldierTier")]
public class SoldierTier : ScriptableObject
{
    public int tierIndex = 0;
    public float damageMultiplier = 1f;
    public float fireRateMultiplier = 1f;
    public float projectileScale = 1f;
    public Color projectileColor = new Color(1f, 0.9f, 0.2f);
}
