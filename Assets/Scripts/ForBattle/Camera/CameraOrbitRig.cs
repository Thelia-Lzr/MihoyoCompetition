using UnityEngine;

/// <summary>
/// 为角色提供一个“镜头轨迹球/椭球”定义：
/// - radii: 轨迹球半径（椭球 xyz 轴）
/// - centerOffset: 相对角色位置的世界偏移（例如头顶附近作为观察中心）
/// 该椭球不随角色旋转，仅围绕世界坐标轴定义。
/// </summary>
[DisallowMultipleComponent]
public class CameraOrbitRig : MonoBehaviour
{
    [Tooltip("椭球半径(单位:米)，X/Z 为水平半径，Y 为竖直半径")] 
    public Vector3 radii = new Vector3(3f, 2f, 3f);

    [Tooltip("观察中心的世界偏移(相对角色根节点位置)")] 
    public Vector3 centerOffset = new Vector3(0f, 1.2f, 0f);

    /// <summary>
    /// 计算在给定世界方位角/俯仰角下，椭球表面的局部偏移(不随角色旋转)
    /// yaw (rad), pitch (rad)
    /// </summary>
    public Vector3 GetOffset(float yaw, float pitch)
    {
        float cy = Mathf.Cos(pitch);
        float sy = Mathf.Sin(pitch);
        float sx = Mathf.Sin(yaw);
        float cz = Mathf.Cos(yaw);
        float x = radii.x * cy * sx;
        float y = radii.y * sy;
        float z = radii.z * cy * cz;
        return new Vector3(x, y, z);
    }

    /// <summary>
    /// 从一个局部偏移反推得到世界方位角/俯仰角（用于首次进入时由给定 followOffset 推导）
    /// </summary>
    public void OffsetToAngles(Vector3 offset, out float yaw, out float pitch)
    {
        // 兼容椭球：按轴向缩放到单位球后做反三角
        float nx = radii.x > 1e-4f ? offset.x / radii.x : 0f;
        float ny = radii.y > 1e-4f ? offset.y / radii.y : 0f;
        float nz = radii.z > 1e-4f ? offset.z / radii.z : 0f;
        // 规范化 (避免极端半径导致数值异常)
        var n = new Vector3(nx, ny, nz);
        if (n.sqrMagnitude > 1e-6f) n.Normalize();
        pitch = Mathf.Asin(Mathf.Clamp(n.y, -1f, 1f));
        yaw = Mathf.Atan2(n.x, n.z);
    }

    public Vector3 WorldCenter => transform.position + centerOffset;
}
