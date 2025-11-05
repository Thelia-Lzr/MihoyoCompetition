using UnityEngine;

/// <summary>
/// 简单宝箱：靠近按键交互后给予道具提示并禁用自身(或可选复用)。
/// 使用 ScenarioNPC 的提示风格，但作为独立交互体。
/// </summary>
public class ChestPickup : MonoBehaviour
{
    [Tooltip("交互按键")] public KeyCode interactKey = KeyCode.E;
    [Tooltip("交互半径")] public float interactRadius = 2.0f;
    [Tooltip("提示")] public string promptText = "按 E 开启宝箱";
    [Tooltip("开启后提示")] public string rewardText = "获得了 1 个 治疗剂";
    [Tooltip("是否可重复开启")] public bool reusable = false;

    public Transform player;

    private GameObject promptGO;
    private TextMesh promptTM;
    private bool opened = false;

    private void Awake()
    {
        if (player == null)
        {
            var pc = FindObjectOfType<WASDPlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) player = tagged.transform;
            }
        }

        promptGO = new GameObject("ChestPrompt");
        promptGO.transform.SetParent(transform);
        promptGO.transform.localPosition = new Vector3(0, 1.6f, 0);
        promptTM = promptGO.AddComponent<TextMesh>();
        promptTM.text = promptText;
        promptTM.fontSize = 48;
        promptTM.characterSize = 0.04f;
        promptTM.alignment = TextAlignment.Center;
        promptTM.anchor = TextAnchor.MiddleCenter;
        promptGO.SetActive(false);
    }

    private void OnDestroy()
    {
        if (promptGO != null) Destroy(promptGO);
    }

    private void Update()
    {
        // 面向相机
        FaceToCamera(promptGO);

        bool inRange = player != null && Vector3.Distance(player.position, transform.position) <= interactRadius;
        if (!opened)
        {
            promptGO.SetActive(inRange);
            if (inRange && Input.GetKeyDown(interactKey))
            {
                // 打开
                opened = true;
                ShowPopup(rewardText, Color.yellow);
                if (!reusable)
                {
                    promptGO.SetActive(false);
                    // 可改成动画/禁用外观等
                    enabled = false;
                }
            }
        }
        else if (reusable)
        {
            // 可复用：再次开启
            promptGO.SetActive(inRange);
            if (inRange && Input.GetKeyDown(interactKey))
            {
                ShowPopup(rewardText, Color.yellow);
            }
        }
    }

    private void FaceToCamera(GameObject go)
    {
        if (go == null) return;
        Camera cam = Camera.main ?? (Camera.allCamerasCount > 0 ? Camera.allCameras[0] : null);
        if (cam == null) return;
        Vector3 dir = go.transform.position - cam.transform.position;
        if (dir.sqrMagnitude > 0.0001f)
        {
            go.transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
        }
    }

    private void ShowPopup(string text, Color color)
    {
        StartCoroutine(PopupRoutine(text, color));
    }

    private System.Collections.IEnumerator PopupRoutine(string text, Color color)
    {
        GameObject go = new GameObject("ChestPopup");
        go.transform.position = transform.position + Vector3.up * 2.0f;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.color = color;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.04f;

        Camera cam = Camera.main;
        if (cam == null && Camera.allCamerasCount > 0) cam = Camera.allCameras[0];

        Vector3 startPos = go.transform.position;
        Vector3 endPos = startPos + Vector3.up * 1.0f;
        Color startColor = tm.color;
        float duration = 1.2f, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            go.transform.position = Vector3.Lerp(startPos, endPos, 1f - (1f - t) * (1f - t));
            if (cam != null)
            {
                Vector3 dir = go.transform.position - cam.transform.position;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    go.transform.rotation = Quaternion.LookRotation(dir, cam.transform.up);
                }
            }
            var c = startColor; c.a = Mathf.Lerp(1f, 0f, t);
            tm.color = c;
            yield return null;
        }
        Destroy(go);
    }
}
