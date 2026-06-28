using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class StaminaHudController : MonoBehaviour
{
    private const string RootName = "stamina-hud-root";
    private const string FillName = "stamina-fill";

    [SerializeField] private float findPlayerInterval = 0.5f;
    [SerializeField] private float displayLerpSpeed = 10f;

    private UIDocument _document;
    private VisualElement _root;
    private VisualElement _fill;
    private Player_Ctrl _player;
    private float _displayRatio = 1f;
    private float _findTimer;

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        yield return null;

        VisualElement documentRoot = _document.rootVisualElement;
        if (documentRoot == null)
        {
            Debug.LogError("[StaminaHudController] UIDocument rootVisualElement is missing.", this);
            yield break;
        }

        _root = documentRoot.Q<VisualElement>(RootName);
        if (_root == null)
        {
            _root = new VisualElement { name = RootName };
            documentRoot.Add(_root);
        }

        ConfigureHud(_root);
        _fill = _root.Q<VisualElement>(FillName);
        ResolvePlayer();
        Refresh(instant: true);
    }

    private void Update()
    {
        if (_root == null || _fill == null)
        {
            return;
        }

        if (_player == null)
        {
            _findTimer -= Time.unscaledDeltaTime;
            if (_findTimer <= 0f)
            {
                ResolvePlayer();
            }
        }

        Refresh(instant: false);
    }

    private void ConfigureHud(VisualElement root)
    {
        root.Clear();
        root.pickingMode = PickingMode.Ignore;
        root.style.position = Position.Absolute;
        root.style.left = 40;
        root.style.bottom = 36;
        root.style.width = 338.4f;
        root.style.height = 33;
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;
        root.style.opacity = 0.72f;

        var accent = new VisualElement { pickingMode = PickingMode.Ignore };
        accent.style.width = 6;
        accent.style.height = 24;
        accent.style.marginRight = 11;
        accent.style.borderTopLeftRadius = 3;
        accent.style.borderTopRightRadius = 3;
        accent.style.borderBottomLeftRadius = 3;
        accent.style.borderBottomRightRadius = 3;
        accent.style.backgroundColor = new Color(0.43f, 0.98f, 0.76f, 0.9f);
        root.Add(accent);

        var frame = new VisualElement { pickingMode = PickingMode.Ignore };
        frame.style.position = Position.Relative;
        frame.style.flexGrow = 1;
        frame.style.height = 21;
        frame.style.paddingLeft = 3;
        frame.style.paddingRight = 3;
        frame.style.paddingTop = 3;
        frame.style.paddingBottom = 3;
        frame.style.borderTopLeftRadius = 7.5f;
        frame.style.borderTopRightRadius = 7.5f;
        frame.style.borderBottomLeftRadius = 7.5f;
        frame.style.borderBottomRightRadius = 7.5f;
        frame.style.borderLeftWidth = 1;
        frame.style.borderRightWidth = 1;
        frame.style.borderTopWidth = 1;
        frame.style.borderBottomWidth = 1;
        frame.style.borderLeftColor = new Color(1f, 1f, 1f, 0.32f);
        frame.style.borderRightColor = new Color(1f, 1f, 1f, 0.18f);
        frame.style.borderTopColor = new Color(1f, 1f, 1f, 0.24f);
        frame.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        frame.style.backgroundColor = new Color(0f, 0f, 0f, 0.48f);
        root.Add(frame);

        var fill = new VisualElement
        {
            name = FillName,
            pickingMode = PickingMode.Ignore
        };
        fill.style.height = Length.Percent(100f);
        fill.style.width = Length.Percent(100f);
        fill.style.borderTopLeftRadius = 4.5f;
        fill.style.borderTopRightRadius = 4.5f;
        fill.style.borderBottomLeftRadius = 4.5f;
        fill.style.borderBottomRightRadius = 4.5f;
        fill.style.backgroundColor = new Color(0.34f, 0.95f, 0.69f, 0.92f);
        frame.Add(fill);

        for (int i = 1; i < 4; i++)
        {
            var tick = new VisualElement { pickingMode = PickingMode.Ignore };
            tick.style.position = Position.Absolute;
            tick.style.left = Length.Percent(i * 25f);
            tick.style.top = 4.5f;
            tick.style.bottom = 4.5f;
            tick.style.width = 1;
            tick.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
            frame.Add(tick);
        }
    }

    private void ResolvePlayer()
    {
        _player = FindAnyObjectByType<Player_Ctrl>();
        _findTimer = Mathf.Max(findPlayerInterval, 0.1f);
    }

    private void Refresh(bool instant)
    {
        if (_player == null)
        {
            _root.style.display = DisplayStyle.None;
            return;
        }

        _root.style.display = DisplayStyle.Flex;

        float max = Mathf.Max(_player.maxStamina, 0.01f);
        float targetRatio = Mathf.Clamp01(_player.stamina / max);
        _displayRatio = instant
            ? targetRatio
            : Mathf.Lerp(_displayRatio, targetRatio, 1f - Mathf.Exp(-displayLerpSpeed * Time.unscaledDeltaTime));

        _fill.style.width = Length.Percent(_displayRatio * 100f);
        _fill.style.backgroundColor = ResolveFillColor(_displayRatio);
        _root.style.opacity = ResolveOpacity(targetRatio);
    }

    private Color ResolveFillColor(float ratio)
    {
        if (ratio <= 0.25f)
        {
            return Color.Lerp(
                new Color(1f, 0.18f, 0.16f, 0.96f),
                new Color(1f, 0.56f, 0.2f, 0.94f),
                Mathf.InverseLerp(0f, 0.25f, ratio));
        }

        return Color.Lerp(
            new Color(1f, 0.56f, 0.2f, 0.94f),
            new Color(0.34f, 0.95f, 0.69f, 0.92f),
            Mathf.InverseLerp(0.25f, 1f, ratio));
    }

    private float ResolveOpacity(float ratio)
    {
        if (_player.IsRunning || ratio < 0.98f)
        {
            return 0.92f;
        }

        return 0.58f;
    }
}
