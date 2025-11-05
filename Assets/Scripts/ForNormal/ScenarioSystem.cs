using System;
using System.Collections.Generic;
using UnityEngine;

public class ScenarioSystem : MonoBehaviour
{
    public static ScenarioSystem Instance { get; private set; }

    [Header("初始变量(可选)")]
    [SerializeField] private List<StringKV> stringVars = new();
    [SerializeField] private List<IntKV> intVars = new();
    [SerializeField] private List<BoolKV> boolVars = new();

    private readonly Dictionary<string, string> _strings = new();
    private readonly Dictionary<string, int> _ints = new();
    private readonly Dictionary<string, bool> _bools = new();

    public event Action<string> OnVariableChanged; // 变量名

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
    }

    public bool Has(string key) => _strings.ContainsKey(key) || _ints.ContainsKey(key) || _bools.ContainsKey(key);

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

    public ScenarioContext GetContext() => new ScenarioContext(this);

    [Serializable] public struct StringKV { public string key; public string value; }
    [Serializable] public struct IntKV { public string key; public int value; }
    [Serializable] public struct BoolKV { public string key; public bool value; }
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
}
