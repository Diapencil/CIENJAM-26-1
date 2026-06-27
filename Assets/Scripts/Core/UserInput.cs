// UserInput.cs
// 기능: 구(Legacy) Input Manager 기반의 통합 입력 매니저. 키별 리스너(Down/Up/Held) 등록과
//       마우스 정보(위치/프레임 이동량/휠/버튼별 드래그 누적량)에 대한 빠른 조회를 한 곳에서 제공한다.
// 사용법:
//   - 폴링:    if (UserInput.Instance.GetKeyDown(KeyCode.Space)) { ... }
//              Vector2 pos = UserInput.Instance.MousePosition;
//              Vector2 drag = UserInput.Instance.GetDragDelta(MouseButton.Left);
//   - 리스너:  UserInput.Instance.AddKeyListener(KeyCode.E, KeyPhase.Down, OnInteract);
//              UserInput.Instance.RemoveKeyListener(KeyCode.E, KeyPhase.Down, OnInteract);
//              UserInput.Instance.AddMouseListener(MouseButton.Left, KeyPhase.Down, OnClick);
//   - 싱글톤이므로 별도 배치 없이 UserInput.Instance 접근 시 자동 생성된다.

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>키/마우스 버튼 이벤트 단계.</summary>
public enum KeyPhase
{
    Down, // 눌린 그 프레임
    Up,   // 떼진 그 프레임
    Held  // 눌려 있는 동안 매 프레임
}

/// <summary>마우스 버튼 식별자. 값은 구 Input.GetMouseButton 인덱스와 동일.</summary>
public enum MouseButton
{
    Left = 0,
    Right = 1,
    Middle = 2
}

public class UserInput : Singleton<UserInput>
{
    // ── 키 리스너 ─────────────────────────────────────────────
    private class KeyCallbacks
    {
        public Action Down;
        public Action Up;
        public Action Held;
    }

    private readonly Dictionary<KeyCode, KeyCallbacks> keyCallbacks = new();

    // ── 마우스 리스너 ─────────────────────────────────────────
    private readonly KeyCallbacks[] mouseCallbacks =
    {
        new KeyCallbacks(), // Left
        new KeyCallbacks(), // Right
        new KeyCallbacks()  // Middle
    };

    // ── 마우스 상태 캐시 ─────────────────────────────────────
    /// <summary>현재 프레임 마우스 스크린 좌표(px).</summary>
    public Vector2 MousePosition { get; private set; }

    /// <summary>직전 프레임 대비 마우스 이동량(px).</summary>
    public Vector2 MouseDelta { get; private set; }

    /// <summary>이번 프레임 휠 스크롤량.</summary>
    public Vector2 ScrollDelta { get; private set; }

    private Vector2 prevMousePosition;

    // 버튼별 드래그 추적 (인덱스 = MouseButton)
    private readonly bool[] dragging = new bool[3];
    private readonly Vector2[] dragOrigin = new Vector2[3];

    protected override void Awake()
    {
        base.Awake();
        // Awake 시점 마우스 위치로 초기화하여 첫 프레임 MouseDelta 튐 방지.
        prevMousePosition = Input.mousePosition;
        MousePosition = prevMousePosition;
    }

    private void Update()
    {
        UpdateMouse();
        DispatchKeyListeners();
        DispatchMouseListeners();
    }

    // ── 즉시 조회 API (키) ───────────────────────────────────
    public bool GetKey(KeyCode key) => Input.GetKey(key);
    public bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
    public bool GetKeyUp(KeyCode key) => Input.GetKeyUp(key);

    // ── 즉시 조회 API (마우스 버튼) ──────────────────────────
    public bool GetMouseButton(MouseButton b) => Input.GetMouseButton((int)b);
    public bool GetMouseButtonDown(MouseButton b) => Input.GetMouseButtonDown((int)b);
    public bool GetMouseButtonUp(MouseButton b) => Input.GetMouseButtonUp((int)b);

    // ── 드래그 조회 API ──────────────────────────────────────
    /// <summary>해당 버튼이 눌린 채 유지 중인지(드래그 진행 여부).</summary>
    public bool IsDragging(MouseButton b) => dragging[(int)b];

    /// <summary>드래그 시작(버튼을 누른) 시점의 스크린 좌표. 드래그 중이 아니면 Vector2.zero.</summary>
    public Vector2 GetDragOrigin(MouseButton b) => dragging[(int)b] ? dragOrigin[(int)b] : Vector2.zero;

    /// <summary>드래그 시작점→현재 위치까지의 누적 이동 벡터. 드래그 중이 아니면 Vector2.zero.</summary>
    public Vector2 GetDragDelta(MouseButton b) =>
        dragging[(int)b] ? MousePosition - dragOrigin[(int)b] : Vector2.zero;

    // ── 리스너 등록/해제 (키) ────────────────────────────────
    public void AddKeyListener(KeyCode key, KeyPhase phase, Action callback)
    {
        if (callback == null) return;
        if (!keyCallbacks.TryGetValue(key, out var cb))
        {
            cb = new KeyCallbacks();
            keyCallbacks[key] = cb;
        }
        AddTo(cb, phase, callback);
    }

    public void RemoveKeyListener(KeyCode key, KeyPhase phase, Action callback)
    {
        if (callback == null) return;
        if (keyCallbacks.TryGetValue(key, out var cb))
            RemoveFrom(cb, phase, callback);
    }

    // ── 리스너 등록/해제 (마우스 버튼) ──────────────────────
    public void AddMouseListener(MouseButton b, KeyPhase phase, Action callback)
    {
        if (callback == null) return;
        AddTo(mouseCallbacks[(int)b], phase, callback);
    }

    public void RemoveMouseListener(MouseButton b, KeyPhase phase, Action callback)
    {
        if (callback == null) return;
        RemoveFrom(mouseCallbacks[(int)b], phase, callback);
    }

    // ── 내부 구현 ────────────────────────────────────────────
    private static void AddTo(KeyCallbacks cb, KeyPhase phase, Action callback)
    {
        switch (phase)
        {
            case KeyPhase.Down: cb.Down += callback; break;
            case KeyPhase.Up: cb.Up += callback; break;
            case KeyPhase.Held: cb.Held += callback; break;
        }
    }

    private static void RemoveFrom(KeyCallbacks cb, KeyPhase phase, Action callback)
    {
        switch (phase)
        {
            case KeyPhase.Down: cb.Down -= callback; break;
            case KeyPhase.Up: cb.Up -= callback; break;
            case KeyPhase.Held: cb.Held -= callback; break;
        }
    }

    private void UpdateMouse()
    {
        Vector2 cur = Input.mousePosition;
        MouseDelta = cur - prevMousePosition;
        MousePosition = cur;
        prevMousePosition = cur;
        ScrollDelta = Input.mouseScrollDelta;

        for (int i = 0; i < 3; i++)
        {
            if (Input.GetMouseButtonDown(i))
            {
                dragging[i] = true;
                dragOrigin[i] = cur;
            }
            else if (Input.GetMouseButtonUp(i))
            {
                dragging[i] = false;
            }
        }
    }

    private void DispatchKeyListeners()
    {
        foreach (var pair in keyCallbacks)
        {
            KeyCode key = pair.Key;
            KeyCallbacks cb = pair.Value;

            if (cb.Down != null && Input.GetKeyDown(key)) cb.Down.Invoke();
            if (cb.Up != null && Input.GetKeyUp(key)) cb.Up.Invoke();
            if (cb.Held != null && Input.GetKey(key)) cb.Held.Invoke();
        }
    }

    private void DispatchMouseListeners()
    {
        for (int i = 0; i < 3; i++)
        {
            KeyCallbacks cb = mouseCallbacks[i];
            if (cb.Down != null && Input.GetMouseButtonDown(i)) cb.Down.Invoke();
            if (cb.Up != null && Input.GetMouseButtonUp(i)) cb.Up.Invoke();
            if (cb.Held != null && Input.GetMouseButton(i)) cb.Held.Invoke();
        }
    }
}
