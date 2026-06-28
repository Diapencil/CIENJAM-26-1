// InteractionPromptUI.cs
// Feature: UI Toolkit key prompt that follows the active interactable's screen position.
//          Shows "[Key : Label]" (e.g. [F : 줍기]) and scales font size by distance (closer = larger).
// Usage: InteractionManager calls Show/Hide automatically. No prefab/scene setup is required;
//        it auto-creates its own UIDocument and reuses the shared PanelSettings found in the scene.

using UnityEngine;
using UnityEngine.UIElements;

public class InteractionPromptUI : MonoBehaviour
{
    static InteractionPromptUI instance;

    [Header("Distance Font Scaling")]
    [SerializeField, Min(1f)] float minFontSize = 16f; // 사거리 끝(멀 때)
    [SerializeField, Min(1f)] float maxFontSize = 40f; // 대상에 붙었을 때(가까울 때)

    UIDocument uiDocument;
    VisualElement root;
    VisualElement box;
    Label label;
    bool built;

    public static InteractionPromptUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<InteractionPromptUI>();
                if (instance == null)
                {
                    var go = new GameObject(nameof(InteractionPromptUI));
                    instance = go.AddComponent<InteractionPromptUI>();
                }
            }

            return instance;
        }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    // keyLabel: 입력 키 표기(예: "F"), actionLabel: 동작 표기(예: "줍기")
    // distanceNormalized: 0(대상에 붙음) ~ 1(사거리 끝)
    public void Show(string keyLabel, string actionLabel, Vector3 screenPosition, float distanceNormalized)
    {
        if (!EnsureBuilt()) return;

        // 카메라 뒤쪽(z<=0)이면 숨김
        if (screenPosition.z <= 0f)
        {
            Hide();
            return;
        }

        string key = string.IsNullOrWhiteSpace(keyLabel) ? "F" : keyLabel;
        string action = string.IsNullOrWhiteSpace(actionLabel) ? "상호작용" : actionLabel;
        label.text = $"[{key} : {action}]";

        float t = Mathf.Clamp01(distanceNormalized);
        label.style.fontSize = Mathf.Lerp(maxFontSize, minFontSize, t);

        // WorldToScreenPoint 는 좌하단 원점(y up), ScreenToPanel 은 좌상단 원점이므로 y 를 뒤집는다.
        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(box.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        box.style.left = panelPos.x;
        box.style.top = panelPos.y;

        if (box.style.display != DisplayStyle.Flex)
            box.style.display = DisplayStyle.Flex;
    }

    public void Hide()
    {
        if (box != null)
            box.style.display = DisplayStyle.None;
    }

    bool EnsureBuilt()
    {
        if (built) return true;

        if (uiDocument == null)
            uiDocument = GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();

        // 공용 PanelSettings 재사용: 씬의 다른 UIDocument 에서 가져와 동일 패널을 공유한다.
        if (uiDocument.panelSettings == null)
        {
            var settings = FindSharedPanelSettings();
            if (settings == null)
                return false; // 아직 다른 UIDocument 가 준비되지 않음 → 다음 프레임 재시도

            uiDocument.panelSettings = settings;
        }

        root = uiDocument.rootVisualElement;
        if (root == null)
            return false;

        root.pickingMode = PickingMode.Ignore;

        box = new VisualElement { name = "interaction-prompt-box" };
        box.pickingMode = PickingMode.Ignore;
        box.style.position = Position.Absolute;
        // (left, top) 을 대상 중심에 두고 자기 크기의 절반만큼 보정해 중앙 정렬
        box.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
        box.style.paddingLeft = 10f;
        box.style.paddingRight = 10f;
        box.style.paddingTop = 4f;
        box.style.paddingBottom = 4f;
        box.style.backgroundColor = new Color(0.05f, 0.05f, 0.05f, 0.82f);
        box.style.borderTopLeftRadius = 6f;
        box.style.borderTopRightRadius = 6f;
        box.style.borderBottomLeftRadius = 6f;
        box.style.borderBottomRightRadius = 6f;
        box.style.display = DisplayStyle.None;

        label = new Label("[F : 상호작용]") { name = "interaction-prompt-label" };
        label.pickingMode = PickingMode.Ignore;
        label.style.color = Color.white;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleCenter;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        box.Add(label);

        root.Add(box);
        built = true;
        return true;
    }

    static PanelSettings FindSharedPanelSettings()
    {
        var documents = FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        foreach (var doc in documents)
        {
            if (doc.panelSettings != null)
                return doc.panelSettings;
        }

        return null;
    }
}
