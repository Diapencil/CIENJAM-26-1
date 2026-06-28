using System.Collections.Generic;
using UnityEngine;

// Controls a registered group of Unity Lights by changing their intensity.
// Add this to a scene object, assign lights in the inspector, then call TurnOn/TurnOff/Toggle.
public class LightGroupController : MonoBehaviour
{
    [System.Serializable]
    public class LightNode
    {
        [SerializeField, Min(0f)] private float intensity = 1f;
        [SerializeField, Min(0f)] private float offIntensity;
        [SerializeField] private List<Light> targets = new();

        public float Intensity => intensity;
        public float OffIntensity => offIntensity;
        public IReadOnlyList<Light> Targets => targets;

        public LightNode()
        {
        }

        public LightNode(Light target)
        {
            AddLight(target);
        }

        public void AddLight(Light target)
        {
            if (target == null || targets.Contains(target)) return;

            targets.Add(target);

            if (targets.Count == 1)
                intensity = Mathf.Max(0f, target.intensity);
        }

        public void RemoveLight(Light target)
        {
            if (target == null) return;
            targets.Remove(target);
        }

        public bool Contains(Light target)
        {
            return target != null && targets.Contains(target);
        }

        public void Apply(float brightness)
        {
            float targetIntensity = Mathf.Lerp(offIntensity, intensity, Mathf.Clamp01(brightness));

            for (int i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null) continue;

                target.intensity = targetIntensity;
                target.enabled = targetIntensity > 0f;
            }
        }
    }

    [Header("Light Nodes")]
    [SerializeField] private List<LightNode> lightNodes = new();

    [Header("State")]
    [SerializeField] private bool startOn = true;
    [SerializeField, Range(0f, 1f)] private float brightness = 1f;
    [SerializeField] private bool applyOnAwake = true;

    public bool IsOn { get; private set; }
    public float Brightness => brightness;

    private void Awake()
    {
        IsOn = startOn;

        if (applyOnAwake)
            Apply();
    }

    public void TurnOn()
    {
        SetOn(true);
    }

    public void TurnOff()
    {
        SetOn(false);
    }

    public void Toggle()
    {
        SetOn(!IsOn);
    }

    public void SetOn(bool isOn)
    {
        IsOn = isOn;
        Apply();
    }

    public void SetBrightness(float value)
    {
        brightness = Mathf.Clamp01(value);
        Apply();
    }

    public void RegisterLight(Light target)
    {
        if (target == null || Contains(target)) return;

        var node = new LightNode(target);
        lightNodes.Add(node);
        node.Apply(IsOn ? brightness : 0f);
    }

    public void RegisterLight(int nodeIndex, Light target)
    {
        if (target == null || Contains(target)) return;

        if (nodeIndex < 0 || nodeIndex >= lightNodes.Count)
        {
            RegisterLight(target);
            return;
        }

        var node = lightNodes[nodeIndex];
        if (node == null)
        {
            node = new LightNode();
            lightNodes[nodeIndex] = node;
        }

        node.AddLight(target);
        node.Apply(IsOn ? brightness : 0f);
    }

    public void UnregisterLight(Light target)
    {
        if (target == null) return;

        for (int i = lightNodes.Count - 1; i >= 0; i--)
            lightNodes[i]?.RemoveLight(target);
    }

    public void Apply()
    {
        float targetBrightness = IsOn ? brightness : 0f;

        for (int i = 0; i < lightNodes.Count; i++)
            lightNodes[i]?.Apply(targetBrightness);
    }

    private bool Contains(Light target)
    {
        for (int i = 0; i < lightNodes.Count; i++)
        {
            if (lightNodes[i] != null && lightNodes[i].Contains(target))
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        brightness = Mathf.Clamp01(brightness);

        if (!Application.isPlaying)
            return;

        Apply();
    }
#endif
}
