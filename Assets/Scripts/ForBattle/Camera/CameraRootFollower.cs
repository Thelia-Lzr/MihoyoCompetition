using UnityEngine;

/// <summary>
/// Makes this CameraRoot follow a target's position without inheriting its rotation.
/// Optional yaw-follow and smoothing are provided. Use this on a CameraRoot object and
/// assign it to BattleUnit.cameraRoot so cameras can follow it without getting rotated by the unit.
/// </summary>
public class CameraRootFollower : MonoBehaviour
{
    [Tooltip("要跟随的位置目标。为空则默认使用父物体。")]
    public Transform target;

    [Tooltip("启动时把自身从父物体脱离（推荐）。脱离后通过脚本只跟随位置，不跟随父物体旋转。")]
    public bool detachOnStart = true;

    [Tooltip("世界坐标系下的跟随偏移。")]
    public Vector3 worldOffset = Vector3.zero;

    [Tooltip("是否平滑移动。")]
    public bool smooth = true;

    [Tooltip("平滑时间(秒)")]
    public float smoothTime = 0.05f;

    [Header("Rotation")]
    [Tooltip("是否跟随目标的水平朝向(Yaw)。关闭则保持自身朝向不变。")]
    public bool followYaw = false;

    [Tooltip("保持世界Up，忽略俯仰和翻滚。")]
    public bool keepWorldUp = true;

    private Vector3 _vel;
    private bool _inited;

    void Awake()
    {
        if (target == null) target = transform.parent;
        if (target != null)
        {
            // 初始偏移
            worldOffset = transform.position - target.position;
            if (detachOnStart)
            {
                transform.SetParent(null, true);
            }
            _inited = true;
        }
    }

    void LateUpdate()
    {
        if (!_inited || target == null) return;

        //位置跟随
        Vector3 desiredPos = target.position + worldOffset;
        if (smooth)
        {
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref _vel, smoothTime);
        }
        else
        {
            transform.position = desiredPos;
        }

        // 朝向处理
        if (followYaw)
        {
            float y = target.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, y, 0f);
        }
        else if (keepWorldUp)
        {
            // 保持当前yaw，去除pitch与roll
            var e = transform.rotation.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, e.y, 0f);
        }
    }
}
