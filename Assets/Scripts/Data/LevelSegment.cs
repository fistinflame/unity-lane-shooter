using UnityEngine;

[CreateAssetMenu(menuName = "Hypercasual/Level Segment", fileName = "LevelSegment")]
public class LevelSegment : ScriptableObject
{
    public float length = 35f;
    public int waveCount = 2;
    public int minEnemiesPerWave = 6;
    public int maxEnemiesPerWave = 12;
    public int gateCount = 2;
    [Range(0f, 1f)] public float powerupChance = 0.5f;
}
