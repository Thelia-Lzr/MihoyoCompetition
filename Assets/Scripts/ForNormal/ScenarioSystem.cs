using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.ForBattle.Audio;

public class ScenarioSystem : MonoBehaviour
{
    public static ScenarioSystem Instance { get; private set; }

    [Header("初始变量(可选)")]
    [SerializeField] private List<StringKV> stringVars = new();
    [SerializeField] private List<IntKV> intVars = new();
    [SerializeField] private List<BoolKV> boolVars = new();
    [SerializeField] private List<GameObjectKV> gameObjectRefs = new();

    private readonly Dictionary<string, string> _strings = new();
    private readonly Dictionary<string, int> _ints = new();
    private readonly Dictionary<string, bool> _bools = new();
    private readonly Dictionary<string, GameObject> _gameObjects = new();

    public event Action<string> OnVariableChanged; // 变量名

    // Fate Vision settings
    [Header("Fate Vision")]
    [Tooltip("持续时间（秒）")]
    public float fateVisionDuration = 5f;

    private ScenarioIndicatorManager _indicatorManager;

    private class ActiveVisionLink
    {
        public string id;
        public string fromKey;
        public string toKey;
        public string colorKey;
        public GameObject lineObj;
        public float expiry;
    }

    private readonly Dictionary<string, ActiveVisionLink> _activeVisionLinks = new Dictionary<string, ActiveVisionLink>();

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var kv in stringVars) if (!string.IsNullOrEmpty(kv.key)) _strings[kv.key] = kv.value;
        foreach (var kv in intVars) if (!string.IsNullOrEmpty(kv.key)) _ints[kv.key] = kv.value;
        foreach (var kv in boolVars) if (!string.IsNullOrEmpty(kv.key)) _bools[kv.key] = kv.value;
        foreach (var kv in gameObjectRefs) if (!string.IsNullOrEmpty(kv.key) && kv.value != null) _gameObjects[kv.key] = kv.value;
    }

    void Start()
    {
        // try to find indicator manager
        _indicatorManager = FindObjectOfType<ScenarioIndicatorManager>();
        if (_indicatorManager == null)
        {
            Debug.LogWarning("ScenarioSystem: ScenarioIndicatorManager not found in scene.");
        }

        // Start looping village background music if SfxPlayer is available
        if (SfxPlayer.Instance != null)
        {
            SfxPlayer.Instance.PlayLoop("Villiage");
        }
    }

    void Update()
    {
        // input G to show fate vision
        if (Input.GetKeyDown(KeyCode.G))
        {
            // play change choice sound if available
            if (SfxPlayer.Instance != null)
            {
                // try common key; user requested "ChangeChoice" specifically
                SfxPlayer.Instance.Play("ChangeChoice");
            }

            ShowFateVision();
        }

        // expire links
        if (_activeVisionLinks.Count > 0)
        {
            var now = Time.time;
            var keys = new List<string>(_activeVisionLinks.Keys);
            foreach (var k in keys)
            {
                var link = _activeVisionLinks[k];
                if (link.expiry <= now)
                {
                    RemoveVisionLink(k);
                }
            }
        }
    }

    private void ShowFateVision()
    {
        if (_indicatorManager == null) return;

        // Keys used in inspector: "Fayt", "Chest", "Weapon", "Lily", "Guard"
        // 1) Fayt - Chest (Yellow), disappears when chest opened
        EnsureVisionLink("Fayt", "Chest", "Yellow", Color.yellow);

        // 2) Fayt - Weapon (Blue)
        EnsureVisionLink("Fayt", "Weapon", "Blue", Color.cyan);

        // 3) Flower quest
        string questKey = "Quest.DeliverFlower";
        int state = GetInt(questKey, 0);
        string targetKey = null;
        switch (state)
        {
            case 0: targetKey = "Lily"; break; // 未接取
            case 1: targetKey = "Guard"; break; // 已接取
            case 2: targetKey = "Lily"; break; // 已完成
            case 3: targetKey = null; break; // 任务结束: 无
            default: targetKey = null; break;
        }
        if (!string.IsNullOrEmpty(targetKey))
        {
            EnsureVisionLink("Fayt", targetKey, "Green", Color.green);
        }
        else
        {
            // ensure any existing green link for flower quest removed
            string idPrefix = MakeLinkId("Fayt", "", "Green");
            var toRemove = new List<string>();
            foreach (var kv in _activeVisionLinks)
            {
                if (kv.Key.StartsWith(idPrefix)) toRemove.Add(kv.Key);
            }
            foreach (var id in toRemove) RemoveVisionLink(id);
        }
    }

    private string MakeLinkId(string fromKey, string toKey, string colorKey)
    {
        return fromKey + "|" + toKey + "|" + colorKey;
    }

    private void EnsureVisionLink(string fromKey, string toKey, string colorKey, Color fallback)
    {
        if (_indicatorManager == null) return;
        if (string.IsNullOrEmpty(fromKey) || string.IsNullOrEmpty(toKey)) return;

        // If this link involves the Chest, and the chest is already opened, do not recreate it
        if (string.Equals(fromKey, "Chest", StringComparison.OrdinalIgnoreCase) || string.Equals(toKey, "Chest", StringComparison.OrdinalIgnoreCase))
        {
            var chestObj = GetObject("Chest");
            if (chestObj != null)
            {
                var chestComp = chestObj.GetComponent<ChestPickup>();
                if (chestComp != null && chestComp.IsOpened)
                {
                    // chest has been opened -> do not recreate chest-related links
                    return;
                }
            }
        }

        GameObject fromObj = GetObject(fromKey);
        GameObject toObj = GetObject(toKey);
        if (fromObj == null || toObj == null) return;

        string id = MakeLinkId(fromKey, toKey, colorKey);
        if (_activeVisionLinks.TryGetValue(id, out var existing))
        {
            // refresh expiry
            existing.expiry = Time.time + fateVisionDuration;
            return;
        }

        // create link via indicator manager using color key
        GameObject line = _indicatorManager.CreateLink(fromKey, toKey, colorKey, fallback);
        if (line == null)
        {
            // fallback: try create with direct color
            line = _indicatorManager.CreateLink(fromKey, toKey, fallback);
        }
        if (line == null) return;

        var link = new ActiveVisionLink { id = id, fromKey = fromKey, toKey = toKey, colorKey = colorKey, lineObj = line, expiry = Time.time + fateVisionDuration };
        _activeVisionLinks[id] = link;

        // special-case: chest open should remove this link when chest opened
        if (toKey == "Chest" || fromKey == "Chest")
        {
            var chestObj = GetObject("Chest");
            if (chestObj != null)
            {
                var chestComp = chestObj.GetComponent<ChestPickup>();
                if (chestComp != null)
                {
                    // subscribe once
                    chestComp.OnOpened -= OnChestOpened; // avoid duplicates
                    chestComp.OnOpened += OnChestOpened;
                }
            }
        }
    }

    private void OnChestOpened(ChestPickup chest)
    {
        // remove any active vision links that reference Chest (both directions), especially Fayt-Chest Yellow
        var toRemove = new List<string>();
        foreach (var kv in _activeVisionLinks)
        {
            var link = kv.Value;
            if (string.Equals(link.fromKey, "Chest", StringComparison.OrdinalIgnoreCase)
                || string.Equals(link.toKey, "Chest", StringComparison.OrdinalIgnoreCase))
            {
                toRemove.Add(kv.Key);
            }
        }

        foreach (var id in toRemove)
        {
            RemoveVisionLink(id);
        }

        // unsubscribe from this chest only if it's not reusable (we want reusable chests to still trigger again)
        if (chest != null && !chest.reusable)
        {
            chest.OnOpened -= OnChestOpened;
        }
    }

    private void RemoveVisionLink(string id)
    {
        if (!_activeVisionLinks.TryGetValue(id, out var link)) return;
        if (link.lineObj != null) _indicatorManager.RemoveLink(link.lineObj);
        _activeVisionLinks.Remove(id);
    }

    // expose to other systems
    public bool Has(string key) => _strings.ContainsKey(key) || _ints.ContainsKey(key) || _bools.ContainsKey(key) || _gameObjects.ContainsKey(key);

    // 字符串
    public string GetString(string key, string def = "") => _strings.TryGetValue(key, out var v) ? v : def;
    public void SetString(string key, string value) { _strings[key] = value; OnVariableChanged?.Invoke(key); }

    // 整数
    public int GetInt(string key, int def = 0) => _ints.TryGetValue(key, out var v) ? v : def;
    public void SetInt(string key, int value) { _ints[key] = value; OnVariableChanged?.Invoke(key); }
    public void AddInt(string key, int delta) { SetInt(key, GetInt(key) + delta); }

    // 布尔
    public bool GetBool(string key, bool def = false) => _bools.TryGetValue(key, out var v) ? v : def;
    public void SetBool(string key, bool value) { _bools[key] = value; OnVariableChanged?.Invoke(key); }

    // GameObject 引用 API
    public GameObject GetObject(string key) => _gameObjects.TryGetValue(key, out var v) ? v : null;
    public void SetObject(string key, GameObject value) { if (string.IsNullOrEmpty(key)) return; if (value == null) _gameObjects.Remove(key); else _gameObjects[key] = value; OnVariableChanged?.Invoke(key); }
    public bool HasObject(string key) => _gameObjects.ContainsKey(key);

    public ScenarioContext GetContext() => new ScenarioContext(this);

    [Serializable] public struct StringKV { public string key; public string value; }
    [Serializable] public struct IntKV { public string key; public int value; }
    [Serializable] public struct BoolKV { public string key; public bool value; }
    [Serializable] public struct GameObjectKV { public string key; public GameObject value; }
}

public readonly struct ScenarioContext
{
    public static ScenarioContext Current => ScenarioSystem.Instance != null ? new ScenarioContext(ScenarioSystem.Instance) : default;

    private readonly ScenarioSystem _sys;
    public bool IsValid => _sys != null;

    public ScenarioContext(ScenarioSystem sys) { _sys = sys; }

    public string GetString(string key, string def = "") => _sys != null ? _sys.GetString(key, def) : def;
    public void SetString(string key, string value) { if (_sys != null) _sys.SetString(key, value); }

    public int GetInt(string key, int def = 0) => _sys != null ? _sys.GetInt(key, def) : def;
    public void SetInt(string key, int value) { if (_sys != null) _sys.SetInt(key, value); }
    public void AddInt(string key, int delta) { if (_sys != null) _sys.AddInt(key, delta); }

    public bool GetBool(string key, bool def = false) => _sys != null ? _sys.GetBool(key, def) : def;
    public void SetBool(string key, bool value) { if (_sys != null) _sys.SetBool(key, value); }

    // GameObject accessors
    public GameObject GetObject(string key) => _sys != null ? _sys.GetObject(key) : null;
    public void SetObject(string key, GameObject value) { if (_sys != null) _sys.SetObject(key, value); }
    public bool HasObject(string key) => _sys != null && _sys.HasObject(key);
}
