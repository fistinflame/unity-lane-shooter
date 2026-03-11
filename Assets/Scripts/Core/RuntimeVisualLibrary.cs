using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public static class RuntimeVisualLibrary
{
    private static bool _loaded;
    private static Texture2D _backgroundTexture;
    private static Texture2D _playerTexture;
    private static Texture2D _zombieTexture;
    private static Texture2D _bossTexture;
    private static readonly Dictionary<int, Material> MaterialCache = new Dictionary<int, Material>();

    public static Texture2D BackgroundTexture
    {
        get
        {
            EnsureLoaded();
            return _backgroundTexture;
        }
    }

    public static Texture2D PlayerTexture
    {
        get
        {
            EnsureLoaded();
            return _playerTexture;
        }
    }

    public static Texture2D ZombieTexture
    {
        get
        {
            EnsureLoaded();
            return _zombieTexture;
        }
    }

    public static Texture2D BossTexture
    {
        get
        {
            EnsureLoaded();
            return _bossTexture;
        }
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

#if UNITY_EDITOR
        string[] spriteRoots = { "Assets/Sprites" };
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", spriteRoots);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
            {
                continue;
            }

            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (_backgroundTexture == null && name.Contains("background"))
            {
                _backgroundTexture = texture;
                continue;
            }

            if (_playerTexture == null && name.Contains("player"))
            {
                _playerTexture = texture;
                continue;
            }

            if (_zombieTexture == null && name.Contains("zombie"))
            {
                _zombieTexture = texture;
                continue;
            }

            if (_bossTexture == null && (name.Contains("boss") || name.Contains("final")))
            {
                _bossTexture = texture;
            }
        }
#endif
    }

    public static Material GetTexturedMaterial(Texture2D texture)
    {
        if (texture == null)
        {
            return null;
        }

        int key = texture.GetInstanceID();
        if (MaterialCache.TryGetValue(key, out var material) && material != null)
        {
            return material;
        }

        material = new Material(ResolveSpriteShader());
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", texture);
        }

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", texture);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        MaterialCache[key] = material;
        return material;
    }

    private static Shader ResolveSpriteShader()
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Transparent");
        }

        if (shader == null)
        {
            shader = Shader.Find("Unlit/Texture");
        }

        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        return shader;
    }
}
