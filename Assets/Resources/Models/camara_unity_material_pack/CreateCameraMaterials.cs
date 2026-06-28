#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public class CreateCameraMaterials
{
    struct MatInfo
    {
        public string name;
        public Color color;
        public float metallic;
        public float smoothness;
        public bool transparent;
        public MatInfo(string n, string hex, float met, float sm, bool tr)
        {
            name = n;
            ColorUtility.TryParseHtmlString(hex, out color);
            metallic = met;
            smoothness = sm;
            transparent = tr;
        }
    }

    [MenuItem("Tools/Create Camera Materials")]
    public static void CreateMaterials()
    {
        string folder = "Assets/CameraMaterials";
        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets", "CameraMaterials");
        }

        MatInfo[] mats = new MatInfo[] {
            new MatInfo("fujiXT3metalBright", "#73776F", 1.0f, 0.55f, false),
            new MatInfo("fujiXT3metalDark", "#171A18", 1.0f, 0.42f, false),
            new MatInfo("fujiXT3metalGlossy", "#222522", 1.0f, 0.75f, false),
            new MatInfo("fujiXT3metalGlossy.001", "#2F312D", 1.0f, 0.68f, false),
            new MatInfo("fujiXT3metalRubber", "#101211", 0.35f, 0.35f, false),
            new MatInfo("fujiXT3plasticBlack", "#070807", 0.0f, 0.28f, false),
            new MatInfo("fujiXT3glass", "#0A1519", 0.0f, 0.95f, true),
            new MatInfo("screen", "#07100F", 0.0f, 0.80f, false),
            new MatInfo("fujiXT3leather", "#12100E", 0.0f, 0.18f, false),
            new MatInfo("fujiXT3selectors", "#A39D8F", 1.0f, 0.50f, false),
            new MatInfo("leather", "#3B2719", 0.0f, 0.22f, false),
            new MatInfo("leather2", "#21160F", 0.0f, 0.18f, false),
            new MatInfo("rope", "#25231F", 0.0f, 0.12f, false),
            new MatInfo("rubber", "#0D0D0C", 0.0f, 0.20f, false),
        };

        foreach (MatInfo info in mats)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.name = info.name;
            mat.color = info.transparent ? new Color(info.color.r, info.color.g, info.color.b, 0.45f) : info.color;
            mat.SetFloat("_Metallic", info.metallic);
            mat.SetFloat("_Glossiness", info.smoothness);

            if (info.transparent)
            {
                mat.SetFloat("_Mode", 3);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.DisableKeyword("_ALPHATEST_ON");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                mat.renderQueue = 3000;
            }

            string path = folder + "/" + info.name + ".mat";
            AssetDatabase.CreateAsset(mat, path);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Camera materials created in Assets/CameraMaterials");
    }
}
#endif
