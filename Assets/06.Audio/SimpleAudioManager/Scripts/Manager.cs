using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimpleAudioManager
{
    
    public class AudioManager : MonoBehaviour
    {
        [Serializable]
        public enum SFXType
        {
            None = 0,
            PickIce = 1,
            PickRock = 2,
            Avalanche = 3,
            Blizzard = 4,
            IceBreak = 5,
            
        }
        [Serializable]
        public class SFXEntry
        {
            public SFXType type;
            public List<AudioClip> clips = new List<AudioClip>();
        }
        #region PROPERTIES

        /// <summary>
        /// Singleton
        /// </summary>
        public static AudioManager instance => _instance;
        private static AudioManager _instance = null;

        /// <summary>
        /// The attached audio source
        /// </summary>
        [Header("MUSIC CONFIGURATIONS")]
        [Tooltip("The audio source prefab which will be used in the audio source pool.")] public GameObject audioSourcePrefab = null;
        private List<AudioSource> sourcePool = new List<AudioSource>();
        private int _currentSourceIndex = -1;
        private AudioSource _currentSource => (_currentSourceIndex == -1 || sourcePool == null || sourcePool.Count <= 0 || sourcePool.Count < _currentSourceIndex) ? null : sourcePool[_currentSourceIndex];

        [Tooltip("Should the current song loop?")] public bool loopCurrentSong = true;
        private Coroutine _loop;
        private Song.Data _currentSongData;
        private int _currentSongIndex = 0;
        private int _currentIntensityIndex = 0;

        /// <summary>
        /// The time before either a non-looping clip ends or the next loop of a looping clip begins
        /// </summary>
        public float clipTimeRemaining => (_nextLoopStartTime != 0f) ? ((float)(_nextLoopStartTime - AudioSettings.dspTime) + ((!loopCurrentSong) ? _currentSongData.reverbTail : 0f)) : 0f;
        private double _nextLoopStartTime = 0;
        [Tooltip("Should the manager play the first song on awake?")] public bool playOnAwake = true;
        [Tooltip("The maximum volume for the audio clips.")][Range(0f, 1f)] public float maxVolume = 1f;
        [Tooltip("The amount of time it will take for different songs to blend between one-another.")] public float defaultSongBlendDuration = 1f;
        [Tooltip("The amount of time it will take for different intensities of the same song to blend between one-another.")] public float defaultIntensityBlendDuration = 1f;

        [Space(8f)]
        /// <summary>
        /// The available songs for the manager
        /// </summary>
        [Tooltip(
            "The list of available songs for the manager to play.\n" +
            "-To create a new song:\n" +
            "  -Right Click and select:\n" +
            "    Create->SimpleAudioManager->Song\n" +
            "  -Add the intensity clips or any other desired clips\n" +
            "  -Set the reverb tail time\n" +
            "    (Seconds before the end of a clip to loop it)\n" +
            "    (Shown in parentheses on Ovani Folders)\n" +
            "  -Drag & Drop your songs onto this list")]
        [SerializeField] private List<Song> _songs = new List<Song>();
        private List<Song.Data> _data = new List<Song.Data>();
        [Tooltip("AudioSource prefab used for pooled SFX playback.")]
        [SerializeField] private AudioSource sfxObject;

        [SerializeField] private int initialSFXPoolSize = 10;
        [SerializeField] private float minPitch = 0.9f;
        [SerializeField] private float maxPitch = 1.1f;
        [SerializeField][Range(0f, 1f)] private float sfxVolume = 1f;

        [SerializeField] private List<SFXEntry> sfxEntries = new List<SFXEntry>();

        private Dictionary<SFXType, List<AudioClip>> sfxDict;
        private readonly List<AudioSource> sfxPool = new List<AudioSource>();
        #endregion

        #region PUBLIC METHODS

        /// <summary>
        /// Shorthand set Intensity
        /// </summary>
        public void SetIntensity(int pIntensity) => SetIntensity(pIntensity, defaultIntensityBlendDuration, defaultIntensityBlendDuration);

        /// <summary>
        /// Sets the intensity for the current song
        /// </summary>
        public void SetIntensity(int pIntensity, float pBlendOutDuration, float pBlendInDuration)
        {
            Debug.Log(_currentSongData.intensityClips);
            if (_currentSongData.intensityClips.Count > Mathf.Max(pIntensity, 0))
            {
                PlaySong(new PlaySongOptions()
                {
                    song = _currentSongIndex,
                    intensity = Mathf.Max(pIntensity, 0),
                    startTime = sourcePool[_currentSourceIndex].time,
                    blendOutTime = pBlendOutDuration,
                    blendInTime = pBlendInDuration
                });
            }
        }

        /// <summary>
        /// Plays the specified song and attempts to match the current intensity
        /// </summary>
        public void PlaySong(int pSong) => PlaySong(new PlaySongOptions()
        {
            song = pSong,
            intensity = _currentIntensityIndex,
            blendOutTime = defaultSongBlendDuration,
            blendInTime = defaultSongBlendDuration
        });

        /// <summary>
        /// Plays the specified song
        /// </summary>
        public void PlaySong(PlaySongOptions pOptions)
        {
            //  Updates the data collection
            _UpdateSongData();

            //  Confirm song exists, clip exists
            if (_data == null || _data.Count == 0 || _data.Count <= pOptions.song) return;
            if (_data[pOptions.song].intensityClips == null || _data[pOptions.song].intensityClips.Count == 0) return;

            //  Do our best to match intensity
            if (_data[pOptions.song].intensityClips.Count <= pOptions.intensity) pOptions.intensity = _data[pOptions.song].intensityClips.Count - 1;

            //  Get the next available audio source
            if (_currentSourceIndex != -1)
            {
                AudioSource _current = _currentSource;
                //  Passing in -1 blendOutTime will let the source play the audio out until the end of the track at its current volume
                float _endVolume = (pOptions.blendOutTime == -1f) ? _current.volume : 0f;
                float _fadeTime = (pOptions.blendOutTime == -1f) ? _currentSongData.reverbTail : pOptions.blendOutTime;
                StartCoroutine(_FadeVolume(_current, _current.volume, _endVolume, _fadeTime));
            }

            //  Change the current source to the next available source
            _currentSourceIndex = _GetNextSourceIndex();
            AudioSource _nextSource = _currentSource;

            //  Apply the provided options
            _currentSongIndex = pOptions.song;
            _currentIntensityIndex = pOptions.intensity;
            _currentSongData = _data[_currentSongIndex];
            AudioClip _clip = _currentSongData.intensityClips[_currentIntensityIndex];
            _nextSource.gameObject.name = _clip.name;

            //  Kill the previous loop and start a new loop routine with the updated song information
            if (_loop != null) StopCoroutine(_loop);
            _loop = StartCoroutine(_Loop(pOptions.startTime));
            StartCoroutine(_FadeVolume(_nextSource, 0f, maxVolume, pOptions.blendInTime));
            _nextSource.clip = _clip;
            _nextSource.time = pOptions.startTime;
            _nextSource.Play();
        }

        /// <summary>
        /// Play song options
        /// </summary>
        public struct PlaySongOptions
        {
            public int song;
            public int intensity;
            public float startTime;
            public float blendOutTime;
            public float blendInTime;
        }

        /// <summary>
        /// Stops the current song playing
        /// </summary>
        public void StopSong(float pFadeOutDuration)
        {
            AudioSource _current = _currentSource;
            StartCoroutine(_FadeVolume(_current, _current.volume, 0f, pFadeOutDuration));
        }

        public void SetMusicVolume(float pVolume)
        {
            _currentSource.volume = pVolume;
        }


        public void PlaySFX(SFXType type, Transform spawnTransform)
        {
            if (sfxDict == null || !sfxDict.TryGetValue(type, out List<AudioClip> clips) || clips == null || clips.Count == 0)
            {
                Debug.LogWarning($"No SFX found for {type}");
                return;
            }

            AudioClip clip = clips[UnityEngine.Random.Range(0, clips.Count)];
            PlaySFXInternal(clip, spawnTransform, sfxVolume);
        }

        public class PooledSFXSourceState : MonoBehaviour
        {
            [HideInInspector] public float baseVolume = 1f;
        }
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);

            for (int i = 0; i < sfxPool.Count; i++)
            {
                AudioSource source = sfxPool[i];
                if (source == null)
                    continue;

                PooledSFXSourceState state = source.GetComponent<PooledSFXSourceState>();
                float baseVolume = state != null ? state.baseVolume : 1f;

                source.volume = baseVolume * sfxVolume;
            }
        }
        #endregion


        #region PRIVATE METHODS

        /// <summary>
        /// Config
        /// </summary>
        private void Awake()
        {
            _instance = _instance ?? this;
            if (_instance != this)
            {
                DestroyImmediate(gameObject);
                return;
            }

            DontDestroyOnLoad(this.gameObject);


            if (playOnAwake) PlaySong(0);
            //if (playOnAwake) StartCoroutine(_delay());
            // IEnumerator _delay()
            // {
            //     yield return new WaitForSecondsRealtime(0.25f);
            //     PlaySong(0);
            // }

            BuildSFXDictionary();
            InitializeSFXPool();
            SetSFXVolume(sfxVolume);


        }
        private void BuildSFXDictionary()
        {
            sfxDict = new Dictionary<SFXType, List<AudioClip>>();

            foreach (var entry in sfxEntries)
            {
                if (entry == null || entry.type == SFXType.None || entry.clips == null || entry.clips.Count == 0)
                    continue;

                List<AudioClip> validClips = new List<AudioClip>();

                foreach (AudioClip clip in entry.clips)
                {
                    if (clip != null)
                        validClips.Add(clip);
                }

                if (validClips.Count == 0)
                    continue;

                if (sfxDict.ContainsKey(entry.type))
                {
                    Debug.LogWarning($"Duplicate SFX entry found for {entry.type}. Overwriting previous clip list.");
                }

                sfxDict[entry.type] = validClips;
            }
        }

        private void InitializeSFXPool()
        {
            if (sfxObject == null)
            {
                Debug.LogWarning("SFX AudioSource prefab is not assigned.");
                return;
            }

            for (int i = 0; i < Mathf.Max(1, initialSFXPoolSize); i++)
            {
                CreateSFXSource();
            }
        }

        private AudioSource CreateSFXSource()
        {
            AudioSource source = Instantiate(sfxObject, transform);
            source.playOnAwake = false;
            source.loop = false;
            source.gameObject.name = $"SFX_Source_{sfxPool.Count}";

            PooledSFXSourceState state = source.GetComponent<PooledSFXSourceState>();
            if (state == null)
                state = source.gameObject.AddComponent<PooledSFXSourceState>();

            state.baseVolume = 1f;

            sfxPool.Add(source);
            return source;
        }

        private AudioSource GetAvailableSFXSource()
        {
            for (int i = 0; i < sfxPool.Count; i++)
            {
                if (!sfxPool[i].isPlaying)
                    return sfxPool[i];
            }

            return CreateSFXSource();
        }

        private void PlaySFXInternal(AudioClip clip, Transform spawnTransform, float volume)
        {
            if (clip == null)
                return;

            if (sfxObject == null)
            {
                Debug.LogWarning("SFX AudioSource prefab is not assigned.");
                return;
            }

            AudioSource source = GetAvailableSFXSource();
            Transform target = spawnTransform != null ? spawnTransform : transform;

            source.transform.position = target.position;
            source.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            source.clip = clip;

            float clampedVolume = Mathf.Clamp01(volume);

            PooledSFXSourceState state = source.GetComponent<PooledSFXSourceState>();
            if (state != null)
                state.baseVolume = clampedVolume;

            source.volume = clampedVolume * sfxVolume;
            source.Play();
        }
        /// <summary>
        /// Clear out the pseudo-singleton
        /// </summary>
        private void OnDestroy() => _instance = _instance == this ? null : _instance;

        /// <summary>
        /// Builds the data pool for the songs
        /// </summary>
        private void _UpdateSongData()
        {
            _data.Clear();
            _songs.ForEach(s => _data.Add(s.ToSongData()));
        }

        /// <summary>
        /// Gets the next available source that is not playing
        /// </summary>
        private int _GetNextSourceIndex()
        {
            AudioSource next = sourcePool.Find(s => !s.isPlaying);
            if (next == null)
            {
                next = Instantiate(audioSourcePrefab, transform).GetComponent<AudioSource>();
                sourcePool.Add(next);
            }
            next.gameObject.SetActive(true);
            return sourcePool.IndexOf(next);
        }

        /// <summary>
        /// Force an audio source to fade in or out
        /// </summary>
        private IEnumerator _FadeVolume(AudioSource pSource, float pStart, float pEnd, float pDuration)
        {
            pDuration = Mathf.Max(pDuration, 0f);
            float duration = 0f;

            //  Perform volume fade
            while (duration < pDuration)
            {
                yield return new WaitForEndOfFrame();
                duration += Time.unscaledDeltaTime;
                pSource.volume = Mathf.SmoothStep(pStart, pEnd, duration / pDuration);
            }

            //  Ensure volume is at desired volume
            pSource.volume = pEnd;

            //  If the volume is 0 or
            //  The fade was between the same values (this is used when looping to allow for the previous clip to play out without fading)
            if (pSource.volume == 0f || pEnd == pStart)
            {
                //  If the fade is for the current audio source
                if (pSource == _currentSource)
                {
                    sourcePool.ForEach(s => s.Stop());
                    StopCoroutine(_loop);
                    _currentSourceIndex = -1;
                    _nextLoopStartTime = 0f;
                }
                pSource.volume = 0f;
                pSource.Stop();
                pSource.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Plays the provided song after a wait period
        /// </summary>
        private IEnumerator _Loop(float pStartTime)
        {
            float _fullLength = _currentSongData.intensityClips[_currentIntensityIndex].length;
            float _waitTime = _fullLength - _currentSongData.reverbTail - pStartTime;
            _nextLoopStartTime = AudioSettings.dspTime + _waitTime;
            yield return new WaitForSecondsRealtime(_waitTime);
            if (!loopCurrentSong)
            {
                //  Queue the current audio source to play out for the remainder of the duration
                AudioSource _current = _currentSource;
                StartCoroutine(_FadeVolume(_current, _current.volume, _current.volume, _currentSongData.reverbTail));
                yield break;
            }

            //  If looping, play the song
            PlaySong(new PlaySongOptions()
            {
                song = _currentSongIndex,
                intensity = _currentIntensityIndex,
                blendOutTime = -1f,
                blendInTime = 0.01f
            });
        }
        #endregion

        #region TEST CODE
        public void PlayPickIceSFX()
        {
            PlaySFX(SFXType.PickIce, transform);
        }

        public void PlayPickRockSFX()
        {
            PlaySFX(SFXType.PickRock, transform);
        }

        public void PlayAvalancheSFX()
        {
            PlaySFX(SFXType.Avalanche, transform);
        }

        public void PlayBlizzardSFX()
        {
            PlaySFX(SFXType.Blizzard, transform);
        }

        public void PlayIceBreakSFX()
        {
            PlaySFX(SFXType.IceBreak, transform);
        }
        #endregion
    }
}

/*
 * 
 * Written by Ovani Sound & Brutiful Games
 * No credit required.
 * Revision: 06/22/2023
 * 
 */