using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class StartMenuController : MonoBehaviour
{
    [Header("Scene")]
    [Tooltip("SceneController loads this scene when Start is clicked.")]
    [SerializeField] private string startTargetScene;

    [Header("Options")]
    [Tooltip("Reusable OptionsPanel.uxml. It is cloned into this document at runtime.")]
    [SerializeField] private VisualTreeAsset optionsPanelAsset;

    [Header("Intro")]
    [SerializeField] private float titlePulseInterval = 2f;
    [SerializeField] private float buttonsRevealDelay = 1f;

    private const string RootName = "start-root";
    private const string TitleLabelName = "title-label";
    private const string TitleGlowName = "title-glow";
    private const string RevealedClass = "revealed";
    private const string ButtonsReadyClass = "buttons-ready";
    private const string TitlePulseClass = "title-pulse";
    private const string StartButtonName = "btn-start";
    private const string OptionButtonName = "btn-option";
    private const string ExitButtonName = "btn-exit";

    private VisualElement _root;
    private Label _titleLabel;
    private Label _titleGlow;
    private Button _startButton;
    private Button _optionButton;
    private Button _exitButton;
    private VisualElement _optionsPanelRoot;
    private OptionsPanelController _options;
    private Coroutine _titlePulseRoutine;
    private Coroutine _buttonsRevealRoutine;
    private bool _revealed;
    private bool _starting;

    private void OnEnable()
    {
        var documentRoot = GetComponent<UIDocument>().rootVisualElement;
        if (documentRoot == null)
        {
            Debug.LogError("[StartMenuController] UIDocument rootVisualElement is missing.", this);
            return;
        }

        _root = documentRoot.Q<VisualElement>(RootName);
        if (_root == null)
        {
            Debug.LogError($"[StartMenuController] '{RootName}' element is missing.", this);
            return;
        }

        _revealed = false;
        _starting = false;
        _root.RemoveFromClassList(RevealedClass);
        _root.RemoveFromClassList(ButtonsReadyClass);

        _titleLabel = _root.Q<Label>(TitleLabelName);
        _titleGlow = _root.Q<Label>(TitleGlowName);
        ApplyTitlePulseTiming();
        SetupOptionsPanel(_root);
        BindButtons(_root);
        SetButtonsEnabled(false);

        _titlePulseRoutine = StartCoroutine(PulseTitle());
    }

    private void OnDisable()
    {
        if (_titlePulseRoutine != null)
        {
            StopCoroutine(_titlePulseRoutine);
            _titlePulseRoutine = null;
        }

        if (_buttonsRevealRoutine != null)
        {
            StopCoroutine(_buttonsRevealRoutine);
            _buttonsRevealRoutine = null;
        }

        if (_startButton != null) _startButton.clicked -= OnStart;
        if (_optionButton != null) _optionButton.clicked -= OnOption;
        if (_exitButton != null) _exitButton.clicked -= OnExit;

        _optionsPanelRoot?.RemoveFromHierarchy();
        _optionsPanelRoot = null;
        _options = null;
    }

    private void Update()
    {
        if (_revealed || _starting) return;
        if (Input.anyKeyDown) RevealMenu();
    }

    private void SetupOptionsPanel(VisualElement root)
    {
        if (optionsPanelAsset == null)
        {
            Debug.LogWarning("[StartMenuController] optionsPanelAsset is empty; options popup is unavailable.", this);
            return;
        }

        _optionsPanelRoot = optionsPanelAsset.CloneTree();
        _optionsPanelRoot.style.position = Position.Absolute;
        _optionsPanelRoot.style.left = 0;
        _optionsPanelRoot.style.top = 0;
        _optionsPanelRoot.style.right = 0;
        _optionsPanelRoot.style.bottom = 0;
        root.Add(_optionsPanelRoot);

        _options = new OptionsPanelController(_optionsPanelRoot);
        _options.Hide();
    }

    private void BindButtons(VisualElement root)
    {
        _startButton = root.Q<Button>(StartButtonName);
        if (_startButton != null) _startButton.clicked += OnStart;

        _optionButton = root.Q<Button>(OptionButtonName);
        if (_optionButton != null) _optionButton.clicked += OnOption;

        _exitButton = root.Q<Button>(ExitButtonName);
        if (_exitButton != null) _exitButton.clicked += OnExit;
    }

    private void OnStart()
    {
        if (string.IsNullOrEmpty(startTargetScene))
        {
            Debug.LogError("[StartMenuController] startTargetScene is empty.", this);
            return;
        }

        _starting = true;
        SetButtonsEnabled(false);
        SceneController.Instance.LoadScene(startTargetScene);
    }

    private void OnOption() => _options?.Show();

    private void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private IEnumerator PulseTitle()
    {
        while (!_revealed && !_starting)
        {
            if (_titleLabel != null)
                _titleLabel.ToggleInClassList(TitlePulseClass);
            if (_titleGlow != null)
                _titleGlow.ToggleInClassList(TitlePulseClass);

            yield return new WaitForSecondsRealtime(titlePulseInterval);
        }
    }

    private void ApplyTitlePulseTiming()
    {
        var duration = new StyleList<TimeValue>(
            new List<TimeValue> { new TimeValue(titlePulseInterval, TimeUnit.Second) });

        if (_titleLabel != null)
            _titleLabel.style.transitionDuration = duration;
        if (_titleGlow != null)
            _titleGlow.style.transitionDuration = duration;
    }

    private void RevealMenu()
    {
        _revealed = true;
        _root.AddToClassList(RevealedClass);

        if (_titleLabel != null)
            _titleLabel.RemoveFromClassList(TitlePulseClass);
        if (_titleGlow != null)
            _titleGlow.RemoveFromClassList(TitlePulseClass);

        SetButtonsEnabled(false);
        _buttonsRevealRoutine = StartCoroutine(ShowButtonsAfterDelay());
    }

    private IEnumerator ShowButtonsAfterDelay()
    {
        yield return new WaitForSecondsRealtime(buttonsRevealDelay);
        if (_starting) yield break;

        _root.AddToClassList(ButtonsReadyClass);
        SetButtonsEnabled(true);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _startButton?.SetEnabled(enabled);
        _optionButton?.SetEnabled(enabled);
        _exitButton?.SetEnabled(enabled);
    }
}
