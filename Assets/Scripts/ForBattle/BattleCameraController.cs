using System.Collections;
using UnityEngine;
using Cinemachine;

/// <summary>
/// 简单的战斗相机控制器：负责在 Cinemachine 虚拟相机之间切换，以及提供聚焦/动作相机协同
/// 注意：将不同的 VirtualCamera 指到对应的 public 字段，方便在场景里关联此脚本
/// </summary>
public class BattleCameraController : MonoBehaviour
{
 public CinemachineVirtualCamera overviewCam;
 public CinemachineVirtualCamera focusCam;
 public CinemachineVirtualCamera actionCam;
 public CinemachineTargetGroup targetGroup;

 // 淡入淡出默认值
 public float blendTime =0.15f;

 // 鼠标环绕灵敏度与极限
 public float mouseSensitivity =0.12f;
 public float minPitch = -80f;
 public float maxPitch =80f;

 //过渡控制（避免切换目标时过大的角度变化）
 [Header("Transition")]
 [Tooltip("切换目标时的平滑时长（目标组权重插值）")]
 public float transitionDuration =0.5f;
 [Tooltip("切换权重插值曲线")]
 public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0,0,1,1);

 // 内部状态（环绕控制）
 private bool mouseControlEnabled = false;
 private float orbitYaw =0f; // radians
 private float orbitPitch =0f; // radians
 public float orbitRadius =3f; // exposed to inspector
 private Vector3 currentOffset;

 void Start()
 {
 // 可根据需要在 Start里默认展示总览
 ShowOverview();
 }

 void Update()
 {
 if (!mouseControlEnabled) return;

 if (focusCam == null || focusCam.Follow == null) return;

 // 使用鼠标移动来旋转环绕（当启用时）
 float dx = Input.GetAxis("Mouse X");
 float dy = Input.GetAxis("Mouse Y");

 // 无输入则不处理
 if (Mathf.Approximately(dx,0f) && Mathf.Approximately(dy,0f)) return;

 orbitYaw += dx * mouseSensitivity;
 orbitPitch -= dy * mouseSensitivity;

 orbitPitch = Mathf.Clamp(orbitPitch, minPitch * Mathf.Deg2Rad, maxPitch * Mathf.Deg2Rad);

 // 根据 yaw/pitch计算偏移
 float y = orbitRadius * Mathf.Sin(orbitPitch);
 float horiz = orbitRadius * Mathf.Cos(orbitPitch);
 float x = horiz * Mathf.Sin(orbitYaw);
 float z = horiz * Mathf.Cos(orbitYaw);

 currentOffset = new Vector3(x, y, z);

 var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
 if (transposer != null)
 {
 transposer.m_FollowOffset = currentOffset;
 }
 }

 public void ShowOverview()
 {
 if (overviewCam != null) overviewCam.Priority =30;
 if (focusCam != null) focusCam.Priority =10;
 if (actionCam != null) actionCam.Priority =5;
 }

 public IEnumerator FocusOn(Transform unit, Vector3 followOffset, float holdSeconds)
 {
 if (focusCam == null || unit == null)
 {
 yield return new WaitForSeconds(holdSeconds + blendTime);
 yield break;
 }

 focusCam.Follow = unit;
 focusCam.LookAt = unit;
 var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
 if (transposer != null)
 {
 transposer.m_FollowOffset = followOffset;
 }

 // 调整优先级触发 blend
 focusCam.Priority =50;
 if (overviewCam != null) overviewCam.Priority =10;
 if (actionCam != null) actionCam.Priority =5;

 // 等待 blend 完毕以及额外停留
 yield return new WaitForSeconds(blendTime + holdSeconds);

 // 回到 overview（或由上层再次切换）
 ShowOverview();
 }

 /// <summary>
 ///立即聚焦到指定单位（会立刻绑定 Follow/LookAt 并设置偏移）
 /// </summary>
 public void FocusImmediate(Transform unit, Vector3 followOffset)
 {
 if (focusCam == null || unit == null) return;

 focusCam.Follow = unit;
 focusCam.LookAt = unit;
 var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
 if (transposer != null)
 {
 transposer.m_FollowOffset = followOffset;
 }

 // 同步内部环绕状态
 currentOffset = followOffset;
 orbitRadius = currentOffset.magnitude;
 // initial yaw/pitch from offset
 orbitYaw = Mathf.Atan2(currentOffset.x, currentOffset.z);
 orbitPitch = Mathf.Asin(Mathf.Clamp(currentOffset.y / Mathf.Max(orbitRadius,0.0001f), -1f,1f));

 // 调整优先级触发 blend
 focusCam.Priority =50;
 if (overviewCam != null) overviewCam.Priority =10;
 if (actionCam != null) actionCam.Priority =5;
 }

 public IEnumerator PlayActionCam(Transform actor, Transform target, Vector3 offset, float actionDuration)
 {
 if (actionCam == null || actor == null)
 {
 yield return new WaitForSeconds(actionDuration);
 yield break;
 }

 actionCam.Follow = actor;
 actionCam.LookAt = target ?? actor;
 var t = actionCam.GetCinemachineComponent<CinemachineTransposer>();
 if (t != null)
 {
 t.m_FollowOffset = offset;
 }

 actionCam.Priority =60;
 if (focusCam != null) focusCam.Priority =10;
 if (overviewCam != null) overviewCam.Priority =5;

 yield return new WaitForSeconds(actionDuration);

 // 恢复到聚焦/总览
 if (focusCam != null) focusCam.Priority =50;
 if (overviewCam != null) overviewCam.Priority =10;
 if (actionCam != null) actionCam.Priority =5;
 }

 // 向目标组添加/移除（用于相机对全体的展示或平滑过渡）
 public void AddToTargetGroup(Transform unit, float weight =1f, float radius =1f)
 {
 if (targetGroup == null || unit == null) return;
 targetGroup.AddMember(unit, weight, radius);
 }

 public void RemoveFromTargetGroup(Transform unit)
 {
 if (targetGroup == null || unit == null) return;
 // CinemachineTargetGroup 无直接 RemoveMember(Transform) API，转为列表删除
 var list = new System.Collections.Generic.List<Cinemachine.CinemachineTargetGroup.Target>(targetGroup.m_Targets);
 for (int i =0; i < list.Count; i++)
 {
 if (list[i].target == unit)
 {
 list.RemoveAt(i);
 targetGroup.m_Targets = list.ToArray();
 break;
 }
 }
 }

 /// <summary>
 /// 平滑从一个目标切换到另一个目标，优先使用 TargetGroup 权重插值，避免视角瞬间大幅跳变。
 /// 若无 targetGroup，则退化为原先的“代理点 + 贝塞尔过渡”。
 /// </summary>
 public IEnumerator TransitionToTarget(Transform from, Transform to, Vector3 targetOffset, float duration)
 {
 if (focusCam == null || to == null)
 {
 FocusImmediate(to, targetOffset);
 yield break;
 }

 // 临时关闭鼠标控制，记录状态
 bool prevMouse = mouseControlEnabled;
 EnableMouseControl(false);

 var transposer = focusCam.GetCinemachineComponent<CinemachineTransposer>();
 Vector3 startOffset = transposer != null ? transposer.m_FollowOffset : Vector3.zero;

 // 优先：使用 TargetGroup进行平滑权重过渡
 if (targetGroup != null && from != null)
 {
 // 确保组里只有这两个成员（可选清理其他）
 // 简单起见：先清空再添加二者
 targetGroup.m_Targets = new CinemachineTargetGroup.Target[] { };
 targetGroup.AddMember(from,1f,1f);
 targetGroup.AddMember(to,0f,1f);

 //让 focusCam 跟随并看向组
 focusCam.Follow = targetGroup.transform;
 focusCam.LookAt = targetGroup.transform;

 float elapsed =0f;
 while (elapsed < duration)
 {
 elapsed += Time.deltaTime;
 float t = Mathf.Clamp01(elapsed / Mathf.Max(duration,0.0001f));
 float w = transitionCurve != null ? transitionCurve.Evaluate(t) : t;

 // 权重从 (1,0) -> (0,1)
 var targets = targetGroup.m_Targets;
 if (targets != null && targets.Length >=2)
 {
 targets[0].weight =1f - w; // from
 targets[1].weight = w; // to
 targetGroup.m_Targets = targets; // 写回以生效
 }

 // 同时平滑 FollowOffset
 if (transposer != null)
 {
 transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetOffset, w);
 currentOffset = transposer.m_FollowOffset;
 }

 yield return null;
 }

 //过渡结束：绑定到最终目标
 if (transposer != null)
 {
 transposer.m_FollowOffset = targetOffset;
 currentOffset = targetOffset;
 }
 focusCam.Follow = to;
 focusCam.LookAt = to;

 // 清理组成员（可选）
 targetGroup.m_Targets = new CinemachineTargetGroup.Target[] { };

 // 恢复鼠标控制
 EnableMouseControl(prevMouse);
 yield break;
 }

 // 回退：没有 targetGroup 的情况下，使用旧的代理贝塞尔过渡
 if (from == null)
 {
 // 没有 from，只能立即切换
 FocusImmediate(to, targetOffset);
 EnableMouseControl(prevMouse);
 yield break;
 }

 // 创建代理在 from->to之间插值
 GameObject proxy = new GameObject("CameraTransitionProxy");
 proxy.transform.position = from.position;

 //绑定相机到代理
 focusCam.Follow = proxy.transform;
 focusCam.LookAt = proxy.transform;

 float e =0f;
 Vector3 p0 = from.position;
 Vector3 p2 = to.position;
 Vector3 midpoint = (p0 + p2) *0.5f;
 float distance = Vector3.Distance(p0, p2);
 float height = Mathf.Max(0.5f, distance *0.3f);
 Vector3 control = midpoint + Vector3.up * height;

 while (e < duration)
 {
 if (proxy == null) break;
 e += Time.deltaTime;
 float t = Mathf.Clamp01(e / Mathf.Max(duration,0.0001f));

 // 二次贝塞尔插值 B(t) = (1-t)^2*p0 +2(1-t)t*p1 + t^2*p2
 float u =1f - t;
 Vector3 pos = u * u * p0 +2f * u * t * control + t * t * p2;
 proxy.transform.position = pos;

 if (transposer != null)
 {
 transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetOffset, t);
 currentOffset = transposer.m_FollowOffset;
 }

 yield return null;
 }

 // 确保到达终点并绑定
 if (transposer != null)
 {
 transposer.m_FollowOffset = targetOffset;
 currentOffset = targetOffset;
 }
 focusCam.Follow = to;
 focusCam.LookAt = to;

 Destroy(proxy);
 EnableMouseControl(prevMouse);
 }

 /// <summary>
 /// 启用/禁用鼠标环绕控制
 /// 启用时会根据当前相机的 FollowOffset 初始化内部状态
 /// </summary>
 public void EnableMouseControl(bool enable)
 {
 mouseControlEnabled = enable;
 if (enable)
 {
 var transposer = focusCam != null ? focusCam.GetCinemachineComponent<CinemachineTransposer>() : null;
 if (transposer != null)
 {
 currentOffset = transposer.m_FollowOffset;
 orbitRadius = currentOffset.magnitude;
 orbitYaw = Mathf.Atan2(currentOffset.x, currentOffset.z);
 orbitPitch = Mathf.Asin(Mathf.Clamp(currentOffset.y / Mathf.Max(orbitRadius,0.0001f), -1f,1f));
 }
 }
 }
}
