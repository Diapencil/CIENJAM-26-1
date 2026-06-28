using UnityEngine;
using UnityEngine.Serialization;

public class Player_Ctrl : MonoBehaviour
{
    Rigidbody rb;

    [Header("Rotate")]
    public float mouseSpeed = 2f;
    [SerializeField] Camera cam;
    float yRotation;
    float xRotation;
    Vector3 standCameraLocalPosition;

    [Header("Move")]
    [FormerlySerializedAs("moveSpeed")]
    [UnityEngine.Range(1, 100)] public float walkSpeed = 5f; // 기본 걷기 속도
    [UnityEngine.Range(1, 100)] public float runSpeedMultiplier = 2f;          // 달리기 시 걷기 속도의 몇 배까지 빨라질지
    [UnityEngine.Range(1, 10)] public float runAccelerationTime = 1f;         // 몇 초에 걸쳐 최대 달리기 속도에 도달할지 (대시관성)
    [UnityEngine.Range(1, 10)] public float runDecelerationTime = 1f;         // 몇 초에 걸쳐 걷기 속도로 돌아올지 (대시종료관성)
    float h;
    float v;
    float runRatio;
    float currentSpeed;
    bool isRunning;

    public bool HasMoveInput => h != 0f || v != 0f;
    public bool IsRunning => isRunning;
    
    [Header("Stamina")]
    public float stamina;
    [UnityEngine.Range(1, 100)] public float maxStamina = 100f;                  // 최대 스태미너
    [UnityEngine.Range(1, 10)] public float staminaDrainPerSecond = 50f;       // 달리기 중 초당 스태미너 감소량
    [UnityEngine.Range(1, 10)] public float staminaRecoveryDelay = 2f;        // 달리기 종료 후 몇 초 뒤 회복 시작
    [UnityEngine.Range(1, 10)] public float staminaRecoveryPerSecond = 10f;    // 초당 스태미너 회복량

    [UnityEngine.Range(1, 100)] public float exhaustedRecoveryPercent = 30f;    // 탈진 후 최대 스태미너의 몇 % 이상 회복되어야 다시 달릴 수 있는지
    float staminaRecoveryTimer;
    bool exhausted;

    [Header("Sit")]
    [Range(0f, 0.5f)] public float standUpCheckSkin = 0.05f; // 머리 위 검사 시 벽 긁힘 방지용 반경 여유
    [Range(0.1f, 2f)] public float sitColliderHeight = 1f;
    public float sitCameraYOffset = -0.8f;
    [Range(1f, 30f)] public float sitTransitionSpeed = 10f;
    [Range(0.1f, 1f)] public float sitSpeedMultiplier = 0.5f;
    CapsuleCollider capsuleCollider;
    float standColliderHeight;
    Vector3 standColliderCenter;
    bool isSitting;
    float sitSpeedRatio = 1f;

    private void OnEnable()
    {
        CursorStateController.RequestLocked(this);
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();             // Rigidbody 컴포넌트 가져오기
        if (rb != null)
        {
            rb.freezeRotation = true;               // Rigidbody의 회전을 고정하여 물리 연산에 영향을 주지 않도록 설정
        }

        cam = ResolveCamera();
        if (cam != null)
        {
            standCameraLocalPosition = cam.transform.localPosition;
            xRotation = ClampPitch(NormalizeAngle(cam.transform.localEulerAngles.x));
        }
        yRotation = transform.eulerAngles.y;

        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            standColliderHeight = capsuleCollider.height;
            standColliderCenter = capsuleCollider.center;
        }

        stamina = maxStamina;
        currentSpeed = walkSpeed;
    }

    private void OnDisable()
    {
        CursorStateController.Release(this);
    }

    void Update()
    {
        ReadMoveInput();
        Rotate();
        Sit();
        Run(!isSitting);
    }

    void FixedUpdate()
    {
        Move();
    }
    
    void Rotate()
    {
        if (cam == null)
        {
            return;
        }

        if (CameraLookLock.IsLocked) return; // UI(도어락/앨범 등) 표시 중 시점 회전 차단

        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSpeed;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSpeed;

        yRotation += mouseX;    // 마우스 X축 입력에 따라 수평 회전 값을 조정
        xRotation -= mouseY;    // 마우스 Y축 입력에 따라 수직 회전 값을 조정

        xRotation = ClampPitch(xRotation);  // 수직 회전 값을 -90도에서 90도 사이로 제한

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0); // 카메라의 회전을 조절
        transform.rotation = Quaternion.Euler(0, yRotation, 0);             // 플레이어 캐릭터의 회전을 조절
    }

    void ReadMoveInput()
    {
        h = Input.GetAxisRaw("Horizontal"); // 수평 이동 입력 값
        v = Input.GetAxisRaw("Vertical");   // 수직 이동 입력 값
    }

    void Move()
    {
        if (rb == null)
        {
            return;
        }

        // 입력에 따라 이동 방향 벡터 계산
        Vector3 moveVec = transform.forward * v + transform.right * h;

        // 이동 벡터를 정규화하여 이동 속도와 시간 간격을 곱한 후 현재 위치에 더함
        Vector3 horizontalVelocity = moveVec.normalized * currentSpeed * sitSpeedRatio;
        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    void Run(bool canTryRun)
    {
        //Debug.Log(stamina);
        bool wantsRun = canTryRun && Input.GetKey(KeyCode.LeftShift) && (h != 0f || v != 0f);
        bool canRun = wantsRun && !exhausted && stamina > 0f;
        isRunning = canRun;

        if (canRun)
        {
            runRatio += Time.deltaTime / Mathf.Max(runAccelerationTime, Mathf.Epsilon);
            stamina -= staminaDrainPerSecond * Time.deltaTime;
            staminaRecoveryTimer = staminaRecoveryDelay;

            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
                isRunning = false;
            }
        }
        else
        {
            runRatio -= Time.deltaTime / Mathf.Max(runDecelerationTime, Mathf.Epsilon);
            staminaRecoveryTimer -= Time.deltaTime;

            if (staminaRecoveryTimer <= 0f)
            {
                stamina += staminaRecoveryPerSecond * Time.deltaTime;
            }
        }

        stamina = Mathf.Clamp(stamina, 0f, maxStamina);
        runRatio = Mathf.Clamp01(runRatio);

        if (exhausted && stamina >= maxStamina * (exhaustedRecoveryPercent * 0.01f))
        {
            exhausted = false;
        }
        
        currentSpeed = walkSpeed * Mathf.Lerp(1f, runSpeedMultiplier, runRatio);
    }

    void Sit()
    {
        bool holdSit = Input.GetKey(KeyCode.C);

        // C키를 떼서 일어나려 해도, 머리 위에 장애물이 있으면 앉은 상태를 유지한다.
        if (!holdSit && isSitting && !HasStandUpHeadroom())
        {
            isSitting = true;
        }
        else
        {
            isSitting = holdSit;
        }

        float lerpSpeed = sitTransitionSpeed * Time.deltaTime;

        if (cam != null)
        {
            Vector3 sitCameraLocalPosition = standCameraLocalPosition + Vector3.up * sitCameraYOffset;
            Vector3 targetCameraLocalPosition = isSitting ? sitCameraLocalPosition : standCameraLocalPosition;

            cam.transform.localPosition = Vector3.Lerp(cam.transform.localPosition, targetCameraLocalPosition, lerpSpeed);
        }

        if (capsuleCollider == null)
        {
            return;
        }

        float targetHeight = isSitting ? Mathf.Max(sitColliderHeight, capsuleCollider.radius * 2f) : standColliderHeight;
        Vector3 targetCenter = standColliderCenter;
        targetCenter.y -= (standColliderHeight - targetHeight) * 0.5f;

        capsuleCollider.height = Mathf.Lerp(capsuleCollider.height, targetHeight, lerpSpeed);
        capsuleCollider.center = Vector3.Lerp(capsuleCollider.center, targetCenter, lerpSpeed);

        sitSpeedRatio = isSitting ? sitSpeedMultiplier : 1f;
    }

    // 현재 앉은 콜라이더가 서있는 높이까지 늘어날 공간이 머리 위에 있는지 검사한다.
    // 플레이어 자신(자식 포함)을 제외한 모든 충돌체를 장애물로 간주한다.
    bool HasStandUpHeadroom()
    {
        if (capsuleCollider == null)
        {
            return true;
        }

        float growth = standColliderHeight - capsuleCollider.height; // 일어나면서 늘어나야 할 높이
        if (growth <= 0.01f)
        {
            return true;
        }

        float radius = Mathf.Max(capsuleCollider.radius - standUpCheckSkin, 0.01f);
        Vector3 worldCenter = transform.TransformPoint(capsuleCollider.center);
        float halfSegment = Mathf.Max(capsuleCollider.height * 0.5f - capsuleCollider.radius, 0f);
        Vector3 up = transform.up;
        Vector3 sphereTop = worldCenter + up * halfSegment;
        Vector3 sphereBottom = worldCenter - up * halfSegment;

        RaycastHit[] hits = Physics.CapsuleCastAll(
            sphereBottom, sphereTop, radius, up, growth, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == capsuleCollider)
            {
                continue;
            }
            if (hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }
            return false; // 플레이어가 아닌 장애물 감지 → 일어날 수 없음
        }

        return true;
    }

    Camera ResolveCamera()
    {
        if (cam != null)
        {
            return cam;
        }

        Camera[] childCameras = GetComponentsInChildren<Camera>(true);
        foreach (Camera childCamera in childCameras)
        {
            if (childCamera.CompareTag("MainCamera"))
            {
                return childCamera;
            }
        }

        return Camera.main;
    }

    static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f)
        {
            angle -= 360f;
        }
        return angle;
    }

    static float ClampPitch(float pitch)
    {
        return Mathf.Clamp(pitch, -90f, 90f);
    }
}
