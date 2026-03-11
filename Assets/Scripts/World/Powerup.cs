using System.Collections.Generic;
using UnityEngine;

public class Powerup : MonoBehaviour
{
    [SerializeField] private PowerupType powerupType;

    private PowerupManager _powerupManager;
    private Transform _pyramidVisual;
    private Transform _hourglassVisual;

    public void Configure(PowerupType type, PowerupManager manager)
    {
        powerupType = type;
        _powerupManager = manager;

        ApplyVisual();
    }

    private void Awake()
    {
        EnsureCollider();
    }

    private void Update()
    {
        transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        var squad = other.GetComponent<SquadController>();
        if (squad == null)
        {
            return;
        }

        if (_powerupManager == null)
        {
            _powerupManager = Object.FindFirstObjectByType<PowerupManager>();
        }

        _powerupManager?.ApplyPowerup(powerupType);
        gameObject.SetActive(false);
    }

    private void ApplyVisual()
    {
        var kind = powerupType != null ? powerupType.kind : PowerupKind.FireRateBoost;
        var color = GetColorFromType(kind);
        bool usePiercingPyramid = kind == PowerupKind.Piercing;
        bool useFireRateHourglass = kind == PowerupKind.FireRateBoost;

        var rootRenderer = GetComponent<Renderer>();
        if (rootRenderer != null)
        {
            rootRenderer.material.color = color;
            rootRenderer.enabled = !usePiercingPyramid && !useFireRateHourglass;
        }

        if (usePiercingPyramid)
        {
            EnsurePyramidVisual(color);
            SetHourglassVisible(false);
            transform.localScale = Vector3.one;
        }
        else if (useFireRateHourglass)
        {
            SetPyramidVisible(false);
            EnsureHourglassVisual(color);
            transform.localScale = Vector3.one;
        }
        else
        {
            SetPyramidVisible(false);
            SetHourglassVisible(false);

            transform.localScale = Vector3.one * 0.8f;
        }
    }

    private void EnsurePyramidVisual(Color color)
    {
        if (_pyramidVisual == null)
        {
            var go = new GameObject("PiercingPyramid");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreatePyramidMesh();

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(ResolveDefaultShader());
            _pyramidVisual = go.transform;
        }

        var renderer = _pyramidVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        _pyramidVisual.gameObject.SetActive(true);
    }

    private void EnsureHourglassVisual(Color color)
    {
        if (_hourglassVisual == null)
        {
            var go = new GameObject("FireRateHourglass");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var meshFilter = go.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = CreateHourglassMesh();

            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(ResolveDefaultShader());
            _hourglassVisual = go.transform;
        }

        var renderer = _hourglassVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }

        _hourglassVisual.gameObject.SetActive(true);
    }

    private void SetPyramidVisible(bool visible)
    {
        if (_pyramidVisual != null)
        {
            _pyramidVisual.gameObject.SetActive(visible);
        }
    }

    private void SetHourglassVisible(bool visible)
    {
        if (_hourglassVisual != null)
        {
            _hourglassVisual.gameObject.SetActive(visible);
        }
    }

    private void EnsureCollider()
    {
        var collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<SphereCollider>();
        }

        collider.isTrigger = true;

        var body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    private static Mesh CreatePyramidMesh()
    {
        var mesh = new Mesh();

        var vertices = new List<Vector3>
        {
            new Vector3(0f, 0.75f, 0f),
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f)
        };

        var triangles = new List<int>
        {
            0, 1, 2,
            0, 2, 3,
            0, 3, 4,
            0, 4, 1,
            1, 4, 3,
            1, 3, 2
        };

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh CreateHourglassMesh()
    {
        const int radialSegments = 12;
        float[] heights = { 0.72f, 0.38f, 0f, -0.38f, -0.72f };
        float[] radii = { 0.42f, 0.2f, 0.08f, 0.2f, 0.42f };

        var vertices = new List<Vector3>(heights.Length * radialSegments);
        var triangles = new List<int>((heights.Length - 1) * radialSegments * 6);

        for (int ring = 0; ring < heights.Length; ring++)
        {
            for (int seg = 0; seg < radialSegments; seg++)
            {
                float angle = (seg / (float)radialSegments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radii[ring];
                float z = Mathf.Sin(angle) * radii[ring];
                vertices.Add(new Vector3(x, heights[ring], z));
            }
        }

        for (int ring = 0; ring < heights.Length - 1; ring++)
        {
            int currentStart = ring * radialSegments;
            int nextStart = (ring + 1) * radialSegments;

            for (int seg = 0; seg < radialSegments; seg++)
            {
                int nextSeg = (seg + 1) % radialSegments;

                int a = currentStart + seg;
                int b = nextStart + seg;
                int c = nextStart + nextSeg;
                int d = currentStart + nextSeg;

                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(c);

                triangles.Add(a);
                triangles.Add(c);
                triangles.Add(d);
            }
        }

        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Shader ResolveDefaultShader()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        return shader;
    }

    private static Color GetColorFromType(PowerupKind kind)
    {
        switch (kind)
        {
            case PowerupKind.FireRateBoost:
                return new Color(1f, 0.8f, 0.1f);
            case PowerupKind.DamageBoost:
                return new Color(1f, 0.35f, 0.15f);
            case PowerupKind.TierUpgrade:
                return new Color(0.15f, 0.8f, 1f);
            case PowerupKind.Piercing:
                return new Color(0.45f, 1f, 0.45f);
            default:
                return Color.white;
        }
    }
}
