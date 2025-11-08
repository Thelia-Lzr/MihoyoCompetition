using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.ForBattle.Audio
{
    /// <summary>
    /// 简单的音效播放器（单例）。
    /// 使用：在场景中新建一个 GameObject，添加 SfxPlayer组件并在 Inspector 中配置音效表。
    ///也可以通过 SfxPlayer.Instance.Play("key"); 来播放已注册的音效。
    /// </summary>
    public class SfxPlayer : MonoBehaviour
    {
        public static SfxPlayer Instance { get; private set; }

        [System.Serializable]
        public struct SoundEntry
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [Header("Sound table (key -> clip)")]
        public SoundEntry[] sounds;

        [Header("Pool settings")]
        [Tooltip("预创建的 AudioSource 数量，用于播放短音效（会根据需要自动扩展）")]
        public int initialPoolSize = 4;

        [Header("Loop settings")]
        [Tooltip("循环音效的最短播放时长（秒），防止频繁开始/停止导致断裂")]
        public float minLoopPlayTime = 0.5f;

        // internal
        private Dictionary<string, SoundEntry> soundMap = new Dictionary<string, SoundEntry>();
        private List<AudioSource> pool = new List<AudioSource>();

        // looped sources by key
        private Dictionary<string, AudioSource> loopSources = new Dictionary<string, AudioSource>();
        // record when loop started
        private Dictionary<string, float> loopStartTimes = new Dictionary<string, float>();
        // pending stop coroutines
        private Dictionary<string, Coroutine> pendingStopCoroutines = new Dictionary<string, Coroutine>();

        void Awake()
        {

            Instance = this;
            DontDestroyOnLoad(this.gameObject);

            BuildMap();
            EnsurePool(initialPoolSize);
        }

        private void BuildMap()
        {
            soundMap.Clear();
            if (sounds == null) return;
            foreach (var s in sounds)
            {
                if (string.IsNullOrEmpty(s.key) || s.clip == null) continue;
                // latest wins
                soundMap[s.key] = s;
            }
        }

        private void EnsurePool(int count)
        {
            for (int i = pool.Count; i < count; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f; //2D by default
                pool.Add(src);
            }
        }

        private AudioSource GetFreeSource()
        {
            foreach (var s in pool)
            {
                if (!s.isPlaying && !s.loop) return s; // prefer non-looping free sources
            }
            // none free, create new one
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            pool.Add(src);
            return src;
        }

        /// <summary>
        ///通过 key 播放音效（使用 Inspector 中配置的音效表）
        /// </summary>
        public void Play(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!soundMap.TryGetValue(key, out var entry))
            {
                Debug.LogWarning($"SfxPlayer: sound key not found: {key}");
                return;
            }

            PlayOneShot(entry.clip, entry.volume);
        }

        /// <summary>
        ///直接播放一个 AudioClip
        /// </summary>
        public void PlayOneShot(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            var src = GetFreeSource();
            src.volume = Mathf.Clamp01(volume);
            src.PlayOneShot(clip, src.volume);
        }

        /// <summary>
        /// 在世界坐标播放一次音效（3D 位置信息），使用 Unity 的 PlayClipAtPoint
        /// </summary>
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, Mathf.Clamp01(volume));
        }

        /// <summary>
        ///通过 key 在世界坐标播放音效
        /// </summary>
        public void PlayAtPoint(string key, Vector3 position)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!soundMap.TryGetValue(key, out var entry))
            {
                Debug.LogWarning($"SfxPlayer: sound key not found: {key}");
                return;
            }
            PlayAtPoint(entry.clip, position, entry.volume);
        }

        /// <summary>
        /// Play a looping sound by key. If already playing, does nothing. Returns the AudioSource used.
        /// </summary>
        public AudioSource PlayLoop(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!soundMap.TryGetValue(key, out var entry))
            {
                Debug.LogWarning($"SfxPlayer: sound key not found: {key}");
                return null;
            }

            if (loopSources.TryGetValue(key, out var existing) && existing != null && existing.isPlaying)
            {
                return existing;
            }

            // if a pending stop coroutine exists for this key, cancel it (we're restarting)
            if (pendingStopCoroutines.TryGetValue(key, out var pend) && pend != null)
            {
                StopCoroutine(pend);
                pendingStopCoroutines.Remove(key);
            }

            // get a free source (we'll use a dedicated source for loop)
            var src = GetFreeSource();
            src.clip = entry.clip;
            src.loop = true;
            src.volume = Mathf.Clamp01(entry.volume);
            src.spatialBlend = 0f;
            src.Play();

            loopSources[key] = src;
            loopStartTimes[key] = Time.time;
            return src;
        }

        /// <summary>
        /// Stop a looping sound by key. If played for less than minLoopPlayTime, stop is delayed until minimum elapsed.
        /// </summary>
        public void StopLoop(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!loopSources.TryGetValue(key, out var src) || src == null)
            {
                loopSources.Remove(key);
                loopStartTimes.Remove(key);
                return;
            }

            float start = 0f;
            loopStartTimes.TryGetValue(key, out start);
            float elapsed = Time.time - start;
            float remaining = minLoopPlayTime - elapsed;
            if (remaining <= 0f)
            {
                // stop immediately
                src.Stop();
                src.loop = false;
                src.clip = null;
                loopSources.Remove(key);
                loopStartTimes.Remove(key);
            }
            else
            {
                // schedule delayed stop if not already pending
                if (!pendingStopCoroutines.ContainsKey(key))
                {
                    var c = StartCoroutine(StopLoopDelayed(key, remaining));
                    pendingStopCoroutines[key] = c;
                }
            }
        }

        private System.Collections.IEnumerator StopLoopDelayed(string key, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (loopSources.TryGetValue(key, out var src) && src != null)
            {
                src.Stop();
                src.loop = false;
                src.clip = null;
            }
            loopSources.Remove(key);
            loopStartTimes.Remove(key);
            pendingStopCoroutines.Remove(key);
        }

        /// <summary>
        /// Check if a loop with key is currently playing
        /// </summary>
        public bool IsLoopPlaying(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            return loopSources.TryGetValue(key, out var src) && src != null && src.isPlaying;
        }

        /// <summary>
        ///重新加载 Inspector 中配置的音效表（运行时可调用）
        /// </summary>
        public void ReloadSounds()
        {
            BuildMap();
        }

        /// <summary>
        /// 停止所有正在播放的音效
        /// </summary>
        public void StopAll()
        {
            foreach (var s in pool)
            {
                if (s.isPlaying) s.Stop();
            }
            // clear loop map
            loopSources.Clear();
            loopStartTimes.Clear();
            // cancel pending coroutines
            foreach (var c in pendingStopCoroutines.Values)
            {
                if (c != null) StopCoroutine(c);
            }
            pendingStopCoroutines.Clear();
        }
    }
}