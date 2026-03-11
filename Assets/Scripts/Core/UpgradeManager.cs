using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    private const string BaseDamageKey = "meta_base_damage_level";
    private const string FireRateKey = "meta_fire_rate_level";
    private const string StartingSoldiersKey = "meta_starting_soldiers_level";
    private const string GateImproveKey = "meta_gate_improve_level";
    private const string TierUnlockKey = "meta_tier_unlock_level";

    public int BaseDamageLevel { get; private set; }
    public int FireRateLevel { get; private set; }
    public int StartingSoldiersLevel { get; private set; }
    public int GateImproveLevel { get; private set; }
    public int TierUnlockLevel { get; private set; }

    public float BaseDamageMultiplier => 1f + BaseDamageLevel * 0.12f;
    public float FireRateMultiplier => 1f + FireRateLevel * 0.1f;
    public int StartingSoldiers => 1 + StartingSoldiersLevel; // Start easy and scale slowly.
    public float GateImproveMultiplier => 1f + GateImproveLevel * 0.18f;
    public int MaxUnlockedTierIndex => Mathf.Max(0, TierUnlockLevel);

    private void Awake()
    {
        Load();
    }

    public void Load()
    {
        BaseDamageLevel = PlayerPrefs.GetInt(BaseDamageKey, 0);
        FireRateLevel = PlayerPrefs.GetInt(FireRateKey, 0);
        StartingSoldiersLevel = PlayerPrefs.GetInt(StartingSoldiersKey, 0);
        GateImproveLevel = PlayerPrefs.GetInt(GateImproveKey, 0);
        TierUnlockLevel = PlayerPrefs.GetInt(TierUnlockKey, 0);
    }

    public void Save()
    {
        PlayerPrefs.SetInt(BaseDamageKey, BaseDamageLevel);
        PlayerPrefs.SetInt(FireRateKey, FireRateLevel);
        PlayerPrefs.SetInt(StartingSoldiersKey, StartingSoldiersLevel);
        PlayerPrefs.SetInt(GateImproveKey, GateImproveLevel);
        PlayerPrefs.SetInt(TierUnlockKey, TierUnlockLevel);
        PlayerPrefs.Save();
    }

    public void SetLevels(int baseDamage, int fireRate, int startingSoldiers, int gateImprove, int tierUnlock)
    {
        BaseDamageLevel = Mathf.Max(0, baseDamage);
        FireRateLevel = Mathf.Max(0, fireRate);
        StartingSoldiersLevel = Mathf.Max(0, startingSoldiers);
        GateImproveLevel = Mathf.Max(0, gateImprove);
        TierUnlockLevel = Mathf.Max(0, tierUnlock);
        Save();
    }

    public void ResetAll()
    {
        SetLevels(0, 0, 0, 0, 0);
    }
}
