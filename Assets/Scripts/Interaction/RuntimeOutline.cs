// RuntimeOutline.cs - Feature: creates a runtime mesh outline for MeshRenderer objects; Usage: attach to a GameObject and call SetHighlighted(true/false).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class RuntimeOutline : MonoBehaviour
{
    const string OutlineObjectName = "__RuntimeOutline";

    [Header("Outline")]
    [SerializeField] Color outlineColor = Color.yellow;
    [SerializeField, Min(0f)] float thickness = 0.03f;
    [SerializeField] bool highlighted = true;
    [SerializeField] bool includeChildren = true;

    readonly List<GameObject> outlineObjects = new();
    Material outlineMaterial;

    void Awake()
    {
        BuildOutline();
        SetHighlighted(highlighted);
    }

    public void SetHighlighted(bool value)
    {
        highlighted = value;

        for (int i = 0; i < outlineObjects.Count; i++)
        {
            if (outlineObjects[i] != null)
                outlineObjects[i].SetActive(highlighted);
        }
    }

    public void Rebuild()
    {
        BuildOutline();
        SetHighlighted(highlighted);
    }

    void BuildOutline()
    {
        ClearOutline();

        outlineMaterial = CreateOutlineMaterial();

        var filters = includeChildren
            ? GetComponentsInChildren<MeshFilter>(true)
            : GetComponents<MeshFilter>();

        foreach (var sourceFilter in filters)
        {
            if (sourceFilter == null
                || sourceFilter.sharedMesh == null
                || sourceFilter.gameObject.name.StartsWith(OutlineObjectName))
            {
                continue;
            }

            var sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
            if (sourceRenderer == null)
                continue;

            var outlineMesh = CreateExpandedInvertedMesh(sourceFilter.sharedMesh, thickness);
            if (outlineMesh == null)
                continue;

            var outlineObject = new GameObject(OutlineObjectName);
            outlineObject.transform.SetParent(sourceFilter.transform, false);
            outlineObject.transform.localPosition = Vector3.zero;
            outlineObject.transform.localRotation = Quaternion.identity;
            outlineObject.transform.localScale = Vector3.one;

            var outlineFilter = outlineObject.AddComponent<MeshFilter>();
            outlineFilter.sharedMesh = outlineMesh;

            var outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
            outlineRenderer.sharedMaterials = CreateMaterialArray(outlineMaterial, outlineMesh.subMeshCount);
            outlineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            outlineRenderer.receiveShadows = false;
            outlineRenderer.enabled = sourceRenderer.enabled;

            outlineObjects.Add(outlineObject);
        }
    }

    Material CreateOutlineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var material = new Material(shader)
        {
            name = "__RuntimeOutlineMaterial",
            color = outlineColor
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", outlineColor);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", outlineColor);

        return material;
    }

    static Material[] CreateMaterialArray(Material material, int count)
    {
        var materials = new Material[Mathf.Max(1, count)];
        for (int i = 0; i < materials.Length; i++)
            materials[i] = material;

        return materials;
    }

    static Mesh CreateExpandedInvertedMesh(Mesh source, float amount)
    {
        try
        {
            var vertices = source.vertices;
            var normals = source.normals;

            if (normals == null || normals.Length != vertices.Length)
            {
                var normalSource = Instantiate(source);
                normalSource.RecalculateNormals();
                normals = normalSource.normals;
                Destroy(normalSource);
            }

            var expandedVertices = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                expandedVertices[i] = vertices[i] + normals[i].normalized * amount;

            var outlineMesh = new Mesh
            {
                name = source.name + "_RuntimeOutline",
                vertices = expandedVertices,
                subMeshCount = source.subMeshCount
            };

            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                var triangles = source.GetTriangles(subMesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    int first = triangles[i];
                    triangles[i] = triangles[i + 2];
                    triangles[i + 2] = first;
                }

                outlineMesh.SetTriangles(triangles, subMesh);
            }

            outlineMesh.RecalculateBounds();
            return outlineMesh;
        }
        catch (UnityException exception)
        {
            Debug.LogWarning($"[RuntimeOutline] Mesh '{source.name}' is not readable. Enable Read/Write on the mesh import settings. {exception.Message}");
            return null;
        }
    }

    void ClearOutline()
    {
        for (int i = 0; i < outlineObjects.Count; i++)
        {
            if (outlineObjects[i] != null)
                Destroy(outlineObjects[i]);
        }

        outlineObjects.Clear();

        if (outlineMaterial != null)
            Destroy(outlineMaterial);
    }

    void OnDestroy()
    {
        ClearOutline();
    }
}
