// ZoneSetupTool.cs (Editor)
// Feature: One-click in-scene setup for the escape zones (2: keypad, 3: key, 4: doorlock/ending).
//          Creates the interaction GameObjects in the OPEN scene, attaches the needed components,
//          and auto-wires onInteract UnityEvents + Door/Trigger/UIDocument references.
//          Because onInteract targets the scene GameManager, this only works in-scene (not as a prefab asset).
// Usage: Open the Play scene, then run Tools/CIENJAM/Setup All Zones (or a single-zone menu item).
//        Afterwards, position the created objects onto your real models and set:
//          - each Door's start/end local pose
//          - DoorLockKeypad.correctPassword (4 digits)
//          - DoorLockKeypad.disableWhileOpen / ClearSequenceController.disableOnClear (player move/look/flash)
//        Everything is Undo-able (single undo step per menu run).

using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

public static class ZoneSetupTool
{
    private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";
    private const string DoorLockUxmlPath = "Assets/UI/DoorLock.uxml";
    private const string ClearPanelUxmlPath = "Assets/UI/ClearPanel.uxml";

    [MenuItem("Tools/CIENJAM/Setup All Zones")]
    public static void SetupAll()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup All Zones");
        int group = Undo.GetCurrentGroup();

        SetupZone2Internal();
        SetupZone3Internal();
        SetupZone4Internal();

        Undo.CollapseUndoOperations(group);
        Debug.Log("[ZoneSetupTool] All zones created. Position the objects onto real models and set Door poses / password / disable arrays.");
    }

    [MenuItem("Tools/CIENJAM/Setup Zone 2 (Keypad)")]
    public static void SetupZone2()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Zone 2");
        SetupZone2Internal();
    }

    [MenuItem("Tools/CIENJAM/Setup Zone 3 (Key)")]
    public static void SetupZone3()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Zone 3");
        SetupZone3Internal();
    }

    [MenuItem("Tools/CIENJAM/Setup Zone 4 (Doorlock + Ending)")]
    public static void SetupZone4()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Setup Zone 4");
        SetupZone4Internal();
    }

    // ── Zone 2: 키패드 줍기 + 키패드 게이트 문 (기존 어댑터 스크립트 사용) ──
    private static void SetupZone2Internal()
    {
        GameObject root = CreateRoot("Zone2_Keypad");

        // 키패드 줍기: Interactable.onInteract -> Zone2KeypadPickup.PickUp (줍기+ObtainKeypad+비활성화+독백)
        GameObject pickup = CreateChild(root, "KeypadPickup");
        var pickupComp = AddComponent<Zone2KeypadPickup>(pickup);
        var pickupInteract = AddComponent<Interactable>(pickup);
        SetInteractable(pickupInteract, "F", true);
        WireVoid(GetOnInteract(pickupInteract), pickupComp.PickUp, pickupInteract);

        // 키패드 게이트 문: Interactable.onInteract -> Zone2KeypadDoor.TryOpen (KeypadObtained 체크+열림+UnlockGate1)
        GameObject door = CreateChild(root, "KeypadDoor");
        GameObject pivot = CreateChild(door, "DoorPivot");
        var doorComp = AddComponent<Door>(pivot);
        GameObject interact = CreateChild(door, "DoorInteract");
        var gateComp = AddComponent<Zone2KeypadDoor>(interact);
        SetObjectField(gateComp, "door", doorComp);
        var doorInteract = AddComponent<Interactable>(interact);
        SetInteractable(doorInteract, "F", false);
        WireVoid(GetOnInteract(doorInteract), gateComp.TryOpen, doorInteract);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    // ── Zone 3: 열쇠 줍기 + 열쇠 문 (전용 스크립트 없이 GameManager 이벤트 직결) ──
    private static void SetupZone3Internal()
    {
        GameManager gm = FindGameManager();
        GameObject root = CreateRoot("Zone3_Key");

        // 열쇠 줍기: onInteract -> GameManager.ObtainKey + 이 오브젝트 SetActive(false)
        GameObject pickup = CreateChild(root, "KeyPickup");
        var pickupInteract = AddComponent<Interactable>(pickup);
        SetInteractable(pickupInteract, "F", true);
        UnityEvent pickupEvt = GetOnInteract(pickupInteract);
        if (gm != null) WireVoid(pickupEvt, gm.ObtainKey, pickupInteract);
        WireBool(pickupEvt, pickup.SetActive, false, pickupInteract);

        // 열쇠 문: onInteract -> GameManager.UnlockGate2 + Door.Open
        // (주의: 게이트 조건(KeyObtained) 체크는 UnityEvent 로 불가 — 비게이트로 열림. 게이트 필요 시 별도 스크립트 필요)
        GameObject door = CreateChild(root, "KeyDoor");
        GameObject pivot = CreateChild(door, "DoorPivot");
        var doorComp = AddComponent<Door>(pivot);
        GameObject interact = CreateChild(door, "DoorInteract");
        var doorInteract = AddComponent<Interactable>(interact);
        SetInteractable(doorInteract, "F", true);
        UnityEvent doorEvt = GetOnInteract(doorInteract);
        if (gm != null) WireVoid(doorEvt, gm.UnlockGate2, doorInteract);
        WireVoid(doorEvt, doorComp.Open, doorInteract);

        if (gm == null)
            Debug.LogWarning("[ZoneSetupTool] Zone3: GameManager not found in scene. ObtainKey/UnlockGate2 wiring skipped — assign manually.");

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    // ── Zone 4: 진입 트리거 + 도어락 + 탈출문 + UI 2종 + 엔딩 시퀀스 ──
    private static void SetupZone4Internal()
    {
        PuangAI puang = FindFirst<PuangAI>();
        GameObject root = CreateRoot("Zone4_FinalEscape");

        // 진입 트리거(되돌아갈 수 없게 문 닫기 + 푸앙이 텔레포트/잠복 + TriggerFinalEscape)
        GameObject entry = CreateChild(root, "EntryTrigger");
        var box = AddComponent<BoxCollider>(entry);
        box.isTrigger = true;
        box.size = new Vector3(3f, 3f, 1f);
        GameObject entryDoorPivot = CreateChild(root, "EntryDoorPivot");
        var entryDoor = AddComponent<Door>(entryDoorPivot);
        GameObject teleport = CreateChild(root, "PuangTeleportPoint");
        var entryTrigger = AddComponent<Zone4EntryTrigger>(entry);
        SetObjectField(entryTrigger, "entryDoor", entryDoor);
        SetObjectField(entryTrigger, "teleportPoint", teleport.transform);
        if (puang != null) SetObjectField(entryTrigger, "puang", puang);

        // 탈출문(도어락 해제 시 열림)
        GameObject exitPivot = CreateChild(root, "ExitDoorPivot");
        var exitDoor = AddComponent<Door>(exitPivot);

        // UI: 도어락 키패드 + 클리어 패널
        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        var doorLockUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DoorLockUxmlPath);
        var clearUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ClearPanelUxmlPath);

        GameObject lockUiGo = CreateChild(root, "DoorLockUI");
        var lockDoc = AddComponent<UIDocument>(lockUiGo);
        lockDoc.panelSettings = panelSettings;
        lockDoc.visualTreeAsset = doorLockUxml;
        var lockPanel = AddComponent<DoorLockPanelController>(lockUiGo);
        EditorUtility.SetDirty(lockDoc);

        GameObject clearUiGo = CreateChild(root, "ClearUI");
        var clearDoc = AddComponent<UIDocument>(clearUiGo);
        clearDoc.panelSettings = panelSettings;
        clearDoc.visualTreeAsset = clearUxml;
        var clearPanel = AddComponent<ClearPanelController>(clearUiGo);
        EditorUtility.SetDirty(clearDoc);

        // 엔딩 시퀀스(Ending 진입 시 영상→페이드→클리어 패널→CompleteClear)
        GameObject clearSeqGo = CreateChild(root, "ClearSequence");
        var clearSeq = AddComponent<ClearSequenceController>(clearSeqGo);
        SetObjectField(clearSeq, "clearPanel", clearPanel);

        // 도어락: Interactable.onInteract -> DoorLockKeypad.OpenKeypad
        GameObject lockGo = CreateChild(root, "DoorLock");
        var keypad = AddComponent<DoorLockKeypad>(lockGo);
        SetObjectField(keypad, "keypadPanel", lockPanel);
        SetObjectField(keypad, "exitDoor", exitDoor);
        if (puang != null) SetObjectField(keypad, "puang", puang);
        var lockInteract = AddComponent<Interactable>(lockGo);
        SetInteractable(lockInteract, "F", false);
        WireVoid(GetOnInteract(lockInteract), keypad.OpenKeypad, lockInteract);

        if (panelSettings == null) Debug.LogWarning($"[ZoneSetupTool] Zone4: PanelSettings not found at '{PanelSettingsPath}'. Assign UIDocument PanelSettings manually.");
        if (doorLockUxml == null) Debug.LogWarning($"[ZoneSetupTool] Zone4: DoorLock.uxml not found at '{DoorLockUxmlPath}'.");
        if (clearUxml == null) Debug.LogWarning($"[ZoneSetupTool] Zone4: ClearPanel.uxml not found at '{ClearPanelUxmlPath}'.");
        if (puang == null) Debug.LogWarning("[ZoneSetupTool] Zone4: PuangAI not found in scene. Assign Zone4EntryTrigger.puang / DoorLockKeypad.puang manually.");
        Debug.LogWarning("[ZoneSetupTool] Zone4: set DoorLockKeypad.correctPassword (4 digits) and DoorLockKeypad.disableWhileOpen / ClearSequenceController.disableOnClear (player move/look/flash) manually.");

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
    }

    // ── helpers ───────────────────────────────────────────────
    private static GameObject CreateRoot(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static T AddComponent<T>(GameObject go) where T : Component
        => Undo.AddComponent<T>(go);

    private static GameManager FindGameManager() => FindFirst<GameManager>();

    private static T FindFirst<T>() where T : Object
        => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);

    // Interactable 의 직렬화 필드(promptText/interactionKey/interactOnce) 설정.
    private static void SetInteractable(Interactable inter, string promptText, bool interactOnce)
    {
        var so = new SerializedObject(inter);
        var p = so.FindProperty("promptText");
        if (p != null) p.stringValue = promptText;
        var once = so.FindProperty("interactOnce");
        if (once != null) once.boolValue = interactOnce;
        so.ApplyModifiedProperties();
    }

    private static void SetObjectField(Component comp, string field, Object value)
    {
        var so = new SerializedObject(comp);
        var prop = so.FindProperty(field);
        if (prop == null)
        {
            Debug.LogWarning($"[ZoneSetupTool] Field '{field}' not found on {comp.GetType().Name}.", comp);
            return;
        }
        prop.objectReferenceValue = value;
        so.ApplyModifiedProperties();
    }

    private static UnityEvent GetOnInteract(Interactable inter)
    {
        var f = typeof(Interactable).GetField("onInteract", BindingFlags.NonPublic | BindingFlags.Instance);
        return f?.GetValue(inter) as UnityEvent;
    }

    private static void WireVoid(UnityEvent evt, UnityAction call, Component owner)
    {
        if (evt == null || call == null) return;
        UnityEventTools.AddVoidPersistentListener(evt, call);
        int idx = evt.GetPersistentEventCount() - 1;
        evt.SetPersistentListenerState(idx, UnityEventCallState.RuntimeOnly);
        if (owner != null) EditorUtility.SetDirty(owner);
    }

    private static void WireBool(UnityEvent evt, UnityAction<bool> call, bool argument, Component owner)
    {
        if (evt == null || call == null) return;
        UnityEventTools.AddBoolPersistentListener(evt, call, argument);
        int idx = evt.GetPersistentEventCount() - 1;
        evt.SetPersistentListenerState(idx, UnityEventCallState.RuntimeOnly);
        if (owner != null) EditorUtility.SetDirty(owner);
    }
}
