using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class StaminaHudController : MonoBehaviour
{
    private const string RootName = "stamina-hud-root";
    private const string FillName = "stamina-fill";
    private const string KeyIconResourcePath = "UI/Icons/hud_key";
    private const string KeycardIconResourcePath = "UI/Icons/hud_keycard";

    [SerializeField] private float findPlayerInterval = 0.5f;
    [SerializeField] private float displayLerpSpeed = 10f;

    private UIDocument _document;
    private VisualElement _root;
    private VisualElement _barRow;
    private VisualElement _fill;
    private VisualElement _itemRow;
    private VisualElement _keySlot;
    private VisualElement _keycardSlot;
    private Player_Ctrl _player;
    private GameManager _gameManager;
    private float _displayRatio = 1f;
    private float _findTimer;

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        StartCoroutine(InitializeWhenReady());
    }

    private void OnDisable()
    {
        UnbindGameManager();
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
        BindGameManager();
        RefreshItemIcons();
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

        if (_gameManager == null)
        {
            BindGameManager();
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
        root.style.height = 68;

        _itemRow = new VisualElement
        {
            name = "stamina-item-row",
            pickingMode = PickingMode.Ignore
        };
        _itemRow.style.position = Position.Absolute;
        _itemRow.style.left = 17;
        _itemRow.style.bottom = 39;
        _itemRow.style.height = 28;
        _itemRow.style.flexDirection = FlexDirection.Row;
        _itemRow.style.alignItems = Align.Center;
        _itemRow.style.display = DisplayStyle.None;
        root.Add(_itemRow);

        _keySlot = CreateItemSlot("stamina-key-icon", Resources.Load<VectorImage>(KeyIconResourcePath));
        _keycardSlot = CreateItemSlot("stamina-keycard-icon", Resources.Load<VectorImage>(KeycardIconResourcePath));
        _itemRow.Add(_keySlot);
        _itemRow.Add(_keycardSlot);

        _barRow = new VisualElement
        {
            name = "stamina-bar-row",
            pickingMode = PickingMode.Ignore
        };
        _barRow.style.position = Position.Absolute;
        _barRow.style.left = 0;
        _barRow.style.bottom = 0;
        _barRow.style.width = Length.Percent(100f);
        _barRow.style.height = 33;
        _barRow.style.flexDirection = FlexDirection.Row;
        _barRow.style.alignItems = Align.Center;
        _barRow.style.opacity = 0.72f;
        root.Add(_barRow);

        var accent = new VisualElement { pickingMode = PickingMode.Ignore };
        accent.style.width = 6;
        accent.style.height = 24;
        accent.style.marginRight = 11;
        accent.style.borderTopLeftRadius = 3;
        accent.style.borderTopRightRadius = 3;
        accent.style.borderBottomLeftRadius = 3;
        accent.style.borderBottomRightRadius = 3;
        accent.style.backgroundColor = new Color(0.43f, 0.98f, 0.76f, 0.9f);
        _barRow.Add(accent);

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
        _barRow.Add(frame);

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

    private VisualElement CreateItemSlot(string name, VectorImage icon)
    {
        var slot = new VisualElement
        {
            name = name,
            pickingMode = PickingMode.Ignore
        };
        slot.style.width = 28;
        slot.style.height = 28;
        slot.style.marginRight = 7;
        slot.style.alignItems = Align.Center;
        slot.style.justifyContent = Justify.Center;
        slot.style.borderTopLeftRadius = 6;
        slot.style.borderTopRightRadius = 6;
        slot.style.borderBottomLeftRadius = 6;
        slot.style.borderBottomRightRadius = 6;
        slot.style.borderLeftWidth = 1;
        slot.style.borderRightWidth = 1;
        slot.style.borderTopWidth = 1;
        slot.style.borderBottomWidth = 1;
        slot.style.borderLeftColor = new Color(1f, 1f, 1f, 0.22f);
        slot.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
        slot.style.borderTopColor = new Color(1f, 1f, 1f, 0.2f);
        slot.style.borderBottomColor = new Color(1f, 1f, 1f, 0.1f);
        slot.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
        slot.style.display = DisplayStyle.None;

        var iconImage = new Image
        {
            pickingMode = PickingMode.Ignore,
            scaleMode = ScaleMode.ScaleToFit,
            vectorImage = icon
        };
        iconImage.style.width = 18;
        iconImage.style.height = 18;
        slot.Add(iconImage);

        return slot;
    }

    private void ResolvePlayer()
    {
        _player = FindAnyObjectByType<Player_Ctrl>();
        _findTimer = Mathf.Max(findPlayerInterval, 0.1f);
    }

    private void BindGameManager()
    {
        GameManager manager = GameManager.Current != null ? GameManager.Current : FindAnyObjectByType<GameManager>();
        if (manager == null || manager == _gameManager)
        {
            return;
        }

        UnbindGameManager();
        _gameManager = manager;
        _gameManager.OnEscapeProgress += OnEscapeProgress;
        RefreshItemIcons();
    }

    private void UnbindGameManager()
    {
        if (_gameManager != null)
        {
            _gameManager.OnEscapeProgress -= OnEscapeProgress;
            _gameManager = null;
        }
    }

    private void OnEscapeProgress(GameManager.EscapeFlag _)
    {
        RefreshItemIcons();
    }

    private void RefreshItemIcons()
    {
        if (_keySlot == null || _keycardSlot == null || _itemRow == null)
        {
            return;
        }

        bool hasKey = _gameManager != null && _gameManager.KeyObtained;
        bool hasKeycard = _gameManager != null && _gameManager.KeypadObtained;

        _keySlot.style.display = hasKey ? DisplayStyle.Flex : DisplayStyle.None;
        _keycardSlot.style.display = hasKeycard ? DisplayStyle.Flex : DisplayStyle.None;
        _itemRow.style.display = (hasKey || hasKeycard) ? DisplayStyle.Flex : DisplayStyle.None;
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
        if (_barRow != null)
        {
            _barRow.style.opacity = ResolveOpacity(targetRatio);
        }
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
