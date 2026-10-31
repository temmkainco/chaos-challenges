using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum Sound
{
    Click, Tick, Go, Boom, Bang, Blank, Safe, Nope, Escape, RoundOver, Fanfare, Pop, Crack, Whoosh,
}

/// <summary>
/// Plays the synthesized sounds and the music loop. Lives in DontDestroyOnLoad; volumes follow <see cref="GameSettings"/>.
/// Every UI button in every loaded scene clicks.
/// </summary>
public class Sfx : MonoBehaviour
{
    private const int VOICES = 12;

    private static Sfx _instance;
    private readonly Dictionary<Sound, AudioClip> _clips = new();
    private readonly Dictionary<Sound, float> _lastPlayed = new();
    private readonly HashSet<int> _hookedButtons = new();
    private AudioSource[] _voices;
    private AudioSource _music;
    private int _nextVoice;

    public static void Initialize()
    {
        if (_instance != null) return;
        var go = new GameObject("Sfx");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<Sfx>();
    }

    public static void Play(Sound sound, float volume = 1f, float pitchJitter = 0.04f)
    {
        if (_instance != null)
            _instance.PlayInternal(sound, volume, pitchJitter);
    }

    private void Awake()
    {
        _voices = new AudioSource[VOICES];
        for (int i = 0; i < VOICES; i++)
        {
            _voices[i] = gameObject.AddComponent<AudioSource>();
            _voices[i].playOnAwake = false;
            _voices[i].spatialBlend = 0f;
        }

        _clips[Sound.Click] = SoundSynth.Click();
        _clips[Sound.Tick] = SoundSynth.Tick();
        _clips[Sound.Go] = SoundSynth.Go();
        _clips[Sound.Boom] = SoundSynth.Boom();
        _clips[Sound.Bang] = SoundSynth.Bang();
        _clips[Sound.Blank] = SoundSynth.Blank();
        _clips[Sound.Safe] = SoundSynth.Safe();
        _clips[Sound.Nope] = SoundSynth.Nope();
        _clips[Sound.Escape] = SoundSynth.Escape();
        _clips[Sound.RoundOver] = SoundSynth.RoundOver();
        _clips[Sound.Fanfare] = SoundSynth.Fanfare();
        _clips[Sound.Pop] = SoundSynth.Pop();
        _clips[Sound.Crack] = SoundSynth.Crack();
        _clips[Sound.Whoosh] = SoundSynth.Whoosh();

        _music = gameObject.AddComponent<AudioSource>();
        _music.loop = true;
        _music.playOnAwake = false;
        _music.volume = GameSettings.MusicVolume;
        PlayMusicFor(SceneManager.GetActiveScene().name);

        GameSettings.Changed += OnSettingsChanged;
        SceneManager.sceneLoaded += (scene, mode) => { HookButtons(); PlayMusicFor(scene.name); };
        HookButtons();
    }

    private readonly Dictionary<int, AudioClip> _musicClips = new();
    private int _musicStyle = -1;

    /// <summary>
    /// Every arena has its own tune (a style picked from the scene name, the same for everyone); the menus and the
    /// lobby play the show theme. Tunes are made the first time they are needed.
    /// </summary>
    private void PlayMusicFor(string sceneName)
    {
        int style = 0;
        if (sceneName.EndsWith("Minigame"))
        {
            int hash = 0;
            foreach (char c in sceneName) hash = hash * 31 + c;
            style = 1 + (hash & 0x7fffffff) % (SoundSynth.MUSIC_STYLES - 1);
        }
        if (style == _musicStyle) return;
        _musicStyle = style;
        if (!_musicClips.TryGetValue(style, out var clip))
            _musicClips[style] = clip = SoundSynth.MusicLoop(style);
        _music.clip = clip;
        _music.Play();
    }

    private void OnSettingsChanged()
    {
        _music.volume = GameSettings.MusicVolume;
    }

    private void PlayInternal(Sound sound, float volume, float pitchJitter)
    {
        if (!_clips.TryGetValue(sound, out var clip))
            return;

        // The same sound many times in one frame (e.g. several tiles cracking) is just louder noise.
        if (_lastPlayed.TryGetValue(sound, out float last) && Time.unscaledTime - last < 0.04f)
            return;
        _lastPlayed[sound] = Time.unscaledTime;

        var voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;
        voice.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        voice.PlayOneShot(clip, volume * GameSettings.SfxVolume);
    }

    private void HookButtons()
    {
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!_hookedButtons.Add(button.GetInstanceID())) continue;
            button.onClick.AddListener(() => Play(Sound.Click, 0.8f, 0f));
        }
    }
}
