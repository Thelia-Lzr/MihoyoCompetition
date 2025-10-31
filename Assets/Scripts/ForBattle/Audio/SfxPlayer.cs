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

        // internal
        private Dictionary<string, SoundEntry> soundMap = new Dictionary<string, SoundEntry>();
        private List<AudioSource> pool = new List<AudioSource>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }

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
                if (!s.isPlaying) return s;
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
        }
    }
}