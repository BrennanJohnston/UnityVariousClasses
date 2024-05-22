using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using static TankAudioManager;

public class TankAudioManager : MonoBehaviour {
    [SerializeField] private List<SoundEffect> _sounds;
    [SerializeField] private AudioSource _soundOneShotPlayerPrefab;

    [System.Serializable]
    public class SoundEffect {
        public SoundID SoundID;
        public List<AudioClip> AudioOptions;

        public SoundEffect(SoundID id, List<AudioClip> audioClips) {
            SoundID = id;
            AudioOptions = audioClips;
        }
    }

    private struct SoundTriggerData {
        private string soundID;
        private GameObject gObject;

        public string SoundID { get { return soundID; } }
        public GameObject GObject { get { return gObject; } }

        public SoundTriggerData(string _soundID, GameObject _gObject) {
            soundID = _soundID;
            gObject = _gObject;
        }
    }

    public enum SoundID {
        CannonFire,
        WreckageCollide
    }

    public static TankAudioManager Singleton { get; private set; }

    public delegate void TriggerOneShotSoundDelegate(SoundID soundID, GameObject gObject);

    private Dictionary<SoundID, List<AudioClip>> soundsDictionary = new Dictionary<SoundID, List<AudioClip>>();

    private Dictionary<TriggerOneShotSoundDelegate, SoundTriggerData> soundTriggerDictionary = new Dictionary<TriggerOneShotSoundDelegate, SoundTriggerData>();

    void Awake() {
        if(Singleton != null) {
            Destroy(this);
            return;
        }

        Singleton = this;

        ParseSoundEffects();

        RegisterAllOneShotSoundTriggers();
    }

    private void RegisterAllOneShotSoundTriggers() {
        Cannon.Fired += OnCannonFired;
        GuidedLauncher.Fired += OnGuidedLauncherFired;
        Wreckage.Collided += OnWreckageCollided;
    }

    private void OnOneShotTrigger(SoundID soundID, GameObject gObject) {
        PlaySoundOnce(soundID, gObject, 1f, 0f, false);
    }

    /// <summary>
    /// Play a sound associated with the provided soundID on the provided objectToPlayOn.
    /// If the soundID is not registered, or the objectToPlayOn does not have an AudioSource component, the sound will not play.
    /// </summary>
    /// <param name="soundID"></param>
    /// <param name="objectToPlayOn"></param>
    /// <param name="volumeScale"></param>
    private void PlaySoundOnce(SoundID soundID, GameObject objectToPlayOn, float volumeScale, float pitchRandomization, bool parentToGO) {
        if (objectToPlayOn == null) return;
        AudioClip randomAudioClip = GetRandomAudioClip(soundID);
        if (randomAudioClip == null) return;

        AudioSource audioSource = Instantiate(_soundOneShotPlayerPrefab);
        audioSource.transform.position = objectToPlayOn.transform.position;
        if (parentToGO) audioSource.transform.parent = objectToPlayOn.transform;

        pitchRandomization = Mathf.Clamp(pitchRandomization, 0f, 1f);
        audioSource.pitch = audioSource.pitch + (UnityEngine.Random.Range(-pitchRandomization, pitchRandomization));

        audioSource.PlayOneShot(randomAudioClip, volumeScale);
    }

    /// <summary>
    /// Register an AudioClip with the TankAudioManager and assign it the provided soundID.
    /// </summary>
    /// <param name="soundID"></param>
    /// <param name="sound"></param>
    private void RegisterAudioClip(SoundID soundID, AudioClip sound) {
        if (soundsDictionary.ContainsKey(soundID)) return;
        List<AudioClip> audioOptions = new List<AudioClip>();
        audioOptions.Add(sound);
        soundsDictionary.Add(soundID, audioOptions);
    }

    /// <summary>
    /// Register multiple AudioClips with the TankAudioManager and assign them the provided soundID.
    /// </summary>
    /// <param name="soundID"></param>
    /// <param name="sounds"></param>
    private void RegisterAudioClips(SoundID soundID, List<AudioClip> sounds) {
        if (soundsDictionary.ContainsKey(soundID)) return;
        soundsDictionary.Add(soundID, sounds);
    }

    private void RegisterSoundEffect(SoundEffect soundEffect) {
        RegisterAudioClips(soundEffect.SoundID, soundEffect.AudioOptions);
    }

    /// <summary>
    /// Get a random AudioClip based on the provided soundID.
    /// </summary>
    /// <param name="soundID"></param>
    /// <returns>Randomly selected AudioClip, null if invalid soundID provided.</returns>
    private AudioClip GetRandomAudioClip(SoundID soundID) {
        List<AudioClip> soundEffectOptions = GetSoundEffectOptions(soundID);
        if (soundEffectOptions == null || soundEffectOptions.Count == 0) return null;
        return GetRandomAudioClip(soundEffectOptions);
    }

    /// <summary>
    /// Get a random AudioClip from the provided List of AudioClip instances.
    /// </summary>
    /// <param name="audioClips"></param>
    /// <returns>Randomly selected AudioClip from audioClips, null if invalid List is provided (null or empty).</returns>
    private AudioClip GetRandomAudioClip(List<AudioClip> audioClips) {
        if (audioClips == null || audioClips.Count == 0) return null;
        int randomIndex = GetRandomIndex(audioClips.Count);
        return audioClips[randomIndex];
    }

    /// <summary>
    /// Get a List of options available for the provided soundID.
    /// </summary>
    /// <param name="soundID"></param>
    /// <returns>List of AudioClip options, null if soundID not valid.</returns>
    private List<AudioClip> GetSoundEffectOptions(SoundID soundID) {
        List<AudioClip> soundEffectOptions;
        soundsDictionary.TryGetValue(soundID, out soundEffectOptions);
        return soundEffectOptions;
    }

    private int GetRandomIndex(int listLength) {
        return UnityEngine.Random.Range(0, listLength);
    }

    private void ParseSoundEffects() {
        for(int i = 0; i < _sounds.Count; i++) {
            RegisterSoundEffect(_sounds[i]);
        }
    }

    private void OnCannonFired(Cannon cannon) {
        PlaySoundOnce(SoundID.CannonFire, cannon.gameObject, 0.5f, 0.2f, true);
    }

    private void OnGuidedLauncherFired(GuidedLauncher launcher) {
        // TODO: Play guided launcher fire sound
    }

    private void OnWreckageCollided(Wreckage wreck) {
        PlaySoundOnce(SoundID.WreckageCollide, wreck.gameObject, 0.2f, 0.2f, false);
    }
}