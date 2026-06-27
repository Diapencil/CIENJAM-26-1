using UnityEngine;
using UnityEngine.Serialization;

public class Player_Ctrl : MonoBehaviour
{
    Rigidbody rb;

    [Header("Rotate")]
    public float mouseSpeed;
    float yRotation;
    float xRotation;
    Camera cam;
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
    [Range(0.1f, 2f)] public float sitColliderHeight = 1f;
    public float sitCameraYOffset = -0.8f;
    [Range(1f, 30f)] public float sitTransitionSpeed = 10f;
    [Range(0.1f, 1f)] public float sitSpeedMultiplier = 0.5f;
    CapsuleCollider capsuleCollider;
    float standColliderHeight;
    Vector3 standColliderCenter;
    bool isSitting;
    float sitSpeedRatio;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;   // 마우스 커서를 화면 안에서 고정
        Cursor.visible = false;                     // 마우스 커서를 보이지 않도록 설정

        rb = GetComponent<Rigidbody>();             // Rigidbody 컴포넌트 가져오기
        rb.freezeRotation = true;                   // Rigidbody의 회전을 고정하여 물리 연산에 영향을 주지 않도록 설정

        cam = Camera.main;                          // 메인 카메라를 할당
        if (cam != null)
        {
            standCameraLocalPosition = cam.transform.localPosition;
        }

        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider != null)
        {
            standColliderHeight = capsuleCollider.height;
            standColliderCenter = capsuleCollider.center;
        }

        stamina = maxStamina;
        currentSpeed = walkSpeed;
    }

    void Update()
    {
        Rotate();
        Sit();
        Move();
        
	}
    
    void Rotate()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * mouseSpeed * Time.deltaTime;
        float mouseY = Input.GetAxisRaw("Mouse Y") * mouseSpeed * Time.deltaTime;

        yRotation += mouseX;    // 마우스 X축 입력에 따라 수평 회전 값을 조정
        xRotation -= mouseY;    // 마우스 Y축 입력에 따라 수직 회전 값을 조정

        xRotation = Mathf.Clamp(xRotation, -90f, 90f);  // 수직 회전 값을 -90도에서 90도 사이로 제한

        cam.transform.localRotation = Quaternion.Euler(xRotation, 0, 0); // 카메라의 회전을 조절
        transform.rotation = Quaternion.Euler(0, yRotation, 0);             // 플레이어 캐릭터의 회전을 조절
    }

    void Move()
    {
        h = Input.GetAxisRaw("Horizontal"); // 수평 이동 입력 값
        v = Input.GetAxisRaw("Vertical");   // 수직 이동 입력 값
        if (!isSitting)
            Run();

        // 입력에 따라 이동 방향 벡터 계산
        Vector3 moveVec = transform.forward * v + transform.right * h;

        // 이동 벡터를 정규화하여 이동 속도와 시간 간격을 곱한 후 현재 위치에 더함
        transform.position += moveVec.normalized * currentSpeed * sitSpeedRatio * Time.deltaTime;
    }

    void Run()
    {
        //Debug.Log(stamina);
        bool wantsRun = Input.GetKey(KeyCode.LeftShift);
        bool canRun = wantsRun && !exhausted && stamina > 0f;

        if (canRun)
        {
            runRatio += Time.deltaTime / Mathf.Max(runAccelerationTime, Mathf.Epsilon);
            stamina -= staminaDrainPerSecond * Time.deltaTime;
            staminaRecoveryTimer = staminaRecoveryDelay;

            if (stamina <= 0f)
            {
                stamina = 0f;
                exhausted = true;
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
        isSitting = Input.GetKey(KeyCode.C);
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
}
