using UnityEngine;
using Assets.Scripts.ForBattle.Audio;

[RequireComponent(typeof(CharacterController))]
public class WASDPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    [Tooltip("按住左Shift的冲刺倍速")]
    public float sprintMultiplier = 1.5f;
    public float rotationSpeed = 12f;

    [Header("Speed Control")]
    [Tooltip("是否允许运行时调节移动速度(Z/X)")]
    public bool enableDynamicSpeed = true;
    public float minMoveSpeed = 2f;
    public float maxMoveSpeed = 12f;
    public float speedStep = 0.5f;

    [Header("Jump/Gravity")]
    public bool enableJump = true;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    [Header("Camera Alignment")]
    [Tooltip("用于移动方向参考的相机(可选)。为空则使用世界坐标轴")] 
    public Transform cameraTransform;

    [Header("Camera Control (运镜)")]
    [Tooltip("启用第三人称相机跟随与鼠标旋转")] public bool enableCameraControl = true;
    [Tooltip("按住右键时旋转相机；关闭则始终可旋转")] public bool holdRightToRotate = false; // 改为默认无需按右键
    [Tooltip("旋转灵敏度(度/秒)")] public float mouseSensitivity = 180f;
    [Tooltip("俯仰角范围(度)")] public float minPitch = -35f, maxPitch = 70f;
    [Tooltip("相机距离")] public float cameraDistance = 5f;
    public float minDistance = 2f, maxDistance = 8f;
    [Tooltip("相机注视目标的偏移(角色头顶)")] public Vector3 cameraTargetOffset = new Vector3(0, 1.5f, 0);
    [Tooltip("相机平滑系数(越大越跟手)")] public float cameraFollowSharpness = 10f;
    [Tooltip("旋转平滑系数(越大越跟手)")] public float cameraRotateSharpness = 12f;
    [Tooltip("在旋转相机时锁定并隐藏鼠标")]
    public bool lockCursorWhileRotating = true;

    [Header("Animation")]
    [Tooltip("可选：用于行走/站立等动作的 Animator。如果为空，自动在自身或子物体查找。")]
    public Animator animator;
    [Tooltip("Animator中代表速度的参数名(浮点)")]
    public string animSpeedParam = "Speed";
    [Tooltip("Animator中表示是否移动的参数名(布尔，可留空)")]
    public string animMovingBoolParam = "IsMoving";
    [Tooltip("发送到Animator的速度缩放")]
    public float animSpeedScale = 1f;
    [Tooltip("设置Animator速度参数时的阻尼(秒)")]
    public float animDampTime = 0.1f;
    [Tooltip("判定为移动状态的速度阈值")]
    public float movingThreshold = 0.1f;

    [Header("Audio")]
    [Tooltip("是否启用脚步声 SFX")]
    public bool enableFootstepSfx = true;
    [Tooltip("SfxPlayer 中用于循环的脚步声键名")]
    public string footstepLoopKey = "steps";

    private CharacterController controller;
    private float verticalVelocity;

    // 相机内部状态
    private float camYaw;
    private float camPitch;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        controller = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        // 初始化相机角度
        if (cameraTransform != null)
        {
            Vector3 target = transform.position + cameraTargetOffset;
            Vector3 toCam = cameraTransform.position - target;
            if (toCam.sqrMagnitude < 0.001f) toCam = Quaternion.Euler(15f, 0f, 0f) * Vector3.back;
            camYaw = Mathf.Atan2(toCam.x, toCam.z) * Mathf.Rad2Deg;
            float horiz = new Vector2(toCam.x, toCam.z).magnitude;
            camPitch = Mathf.Atan2(toCam.y, horiz) * Mathf.Rad2Deg;
            cameraDistance = Mathf.Clamp(toCam.magnitude, minDistance, maxDistance);
        }
    }

    private void OnDisable()
    {
        // 停止脚步声循环
        if (SfxPlayer.Instance != null)
        {
            SfxPlayer.Instance.StopLoop(footstepLoopKey);
        }
    }

    private void OnDestroy()
    {
        if (SfxPlayer.Instance != null)
        {
            SfxPlayer.Instance.StopLoop(footstepLoopKey);
        }
    }

    private void Update()
    {
        // 动态速度调节(Z/X)
        if (enableDynamicSpeed)
        {
            if (Input.GetKeyDown(KeyCode.Z)) moveSpeed = Mathf.Max(minMoveSpeed, moveSpeed - speedStep);
            if (Input.GetKeyDown(KeyCode.X)) moveSpeed = Mathf.Min(maxMoveSpeed, moveSpeed + speedStep);
        }

        // 输入
        float h = Input.GetAxisRaw("Horizontal"); // A/D 或 左/右
        float v = Input.GetAxisRaw("Vertical"); // W/S 或 上/下

        // 基于相机或世界坐标的移动方向
        Vector3 desired = Vector3.zero;
        if (cameraTransform != null)
        {
            Vector3 fwd = cameraTransform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 right = cameraTransform.right; right.y = 0f; right.Normalize();
            desired = (fwd * v + right * h);
        }
        else
        {
            desired = new Vector3(h, 0f, v);
        }
        if (desired.sqrMagnitude > 1f) desired.Normalize();

        // 冲刺
        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // 重力&跳跃
        if (controller.isGrounded)
        {
            // 保持贴地
            if (verticalVelocity < 0f) verticalVelocity = -1f;
            if (enableJump && Input.GetButtonDown("Jump"))
            {
                // v = sqrt(h * -2g)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        verticalVelocity += gravity * Time.deltaTime;

        // 合成位移
        Vector3 move = desired * speed + Vector3.up * verticalVelocity;
        controller.Move(move * Time.deltaTime);

        // 朝向移动方向
        if (desired.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desired, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        // 运镜控制
        HandleCamera();

        // ??????????? + ???????
        DriveAnimatorAndFootsteps();
    }

    private void HandleCamera()
    {
        if (!enableCameraControl || cameraTransform == null) return;

        // 缩放 - 鼠标滚轮
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f)
        {
            cameraDistance = Mathf.Clamp(cameraDistance - scroll * 5f, minDistance, maxDistance);
        }

        //直接根据鼠标移动旋转相机（无需右键）
        bool rotating = true;

        // 锁定/解锁鼠标：始终锁定以便自由观镜
        if (lockCursorWhileRotating)
        {
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        //旋转 - 鼠标移动
        if (rotating)
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            camYaw += mx * mouseSensitivity * Time.deltaTime;
            camPitch = Mathf.Clamp(camPitch - my * mouseSensitivity * Time.deltaTime, minPitch, maxPitch);
        }

        //目标与相机期望位姿
        Vector3 target = transform.position + cameraTargetOffset;
        Quaternion camRot = Quaternion.Euler(camPitch, camYaw, 0f);
        Vector3 desiredPos = target + camRot * (Vector3.back * cameraDistance);
        Quaternion desiredRot = Quaternion.LookRotation(target - desiredPos, Vector3.up);

        // 平滑移动/旋转
        float posLerp = 1f - Mathf.Exp(-cameraFollowSharpness * Time.deltaTime);
        float rotLerp = 1f - Mathf.Exp(-cameraRotateSharpness * Time.deltaTime);
        cameraTransform.position = Vector3.Lerp(cameraTransform.position, desiredPos, posLerp);
        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, desiredRot, rotLerp);
    }

    private void DriveAnimatorAndFootsteps()
    {
        // ????????????????????????
        Vector3 vel = controller != null ? controller.velocity : Vector3.zero; vel.y = 0f;
        float planarSpeed = vel.magnitude * animSpeedScale;

        // Animator
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(animSpeedParam))
            {
                animator.SetFloat(animSpeedParam, planarSpeed, animDampTime, Time.deltaTime);
            }
            if (!string.IsNullOrEmpty(animMovingBoolParam))
            {
                animator.SetBool(animMovingBoolParam, planarSpeed > movingThreshold);
            }
        }

        // Footstep SFX
        if (enableFootstepSfx && SfxPlayer.Instance != null)
        {
            bool moving = planarSpeed > movingThreshold;
            if (controller != null && controller.isGrounded && moving)
            {
                SfxPlayer.Instance.PlayLoop(footstepLoopKey);
            }
            else
            {
                SfxPlayer.Instance.StopLoop(footstepLoopKey);
            }
        }
    }
}
