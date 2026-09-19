using UnityEngine;
using UnityEngine.UI;

public class SoundEffectManager : MonoBehaviour
{
    private static SoundEffectManager Instance;

    private static AudioSource audioSource;
    private static AudioSource randomPitchAudioSource;
    private static AudioSource voiceAudioSource;
    [SerializeField] private AudioSource loopSource;

    private static AudioSource loopAudioSource;
    private static float currentSFXVolume = 1f;
    private static float loopVolumeMultiplier = 1f;

    private static SoundEffectLibrary soundEffectLibrary;
    [SerializeField] private Slider sfxSlider;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            AudioSource[] audioSources = GetComponents<AudioSource>();
            audioSource = audioSources[0];
            randomPitchAudioSource = audioSources[1];
            voiceAudioSource = audioSources[2];
            soundEffectLibrary = GetComponent<SoundEffectLibrary>();

            loopAudioSource = loopSource;
            // DontDestroyOnLoad(gameObject);
        }
        // else
        // {
        //     Destroy(gameObject);
        // }
    }

    public static void Play(string soundName, bool randomPitch = false)
    {
        AudioClip audioClip = soundEffectLibrary.GetRandomClip(soundName);
        if (audioClip != null)
        {
            if (randomPitch)
            {
                randomPitchAudioSource.pitch = Random.Range(1f, 1.5f);
                randomPitchAudioSource.PlayOneShot(audioClip);
            }
            else
            {
                audioSource.PlayOneShot(audioClip);
            }
        }
    }

    public static void PlayVoice(AudioClip audioClip, float pitch = 1f, bool randomPitch = false)
    {
        if (randomPitch)
        {
            voiceAudioSource.pitch = Random.Range(0.5f, 1.5f);
            voiceAudioSource.PlayOneShot(audioClip);
        }
        else
        {
            voiceAudioSource.pitch = pitch;
            voiceAudioSource.PlayOneShot(audioClip);
        }

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sfxSlider.onValueChanged.AddListener(delegate { OnValueChanged(); });
    }

    public static void SetVolume(float volume)
    {
        currentSFXVolume = volume;
        audioSource.volume = volume;
        randomPitchAudioSource.volume = volume;
        voiceAudioSource.volume = volume;
        if (loopAudioSource != null) 
            loopAudioSource.volume = volume * loopVolumeMultiplier;
    }

    public void OnValueChanged()
    {
        SetVolume(sfxSlider.value);
    }

    public static void StartLoop(string soundName, float volumeMultiplier = 1f, float pitch = 1f)
    {
        if (loopAudioSource == null) return;

        AudioClip audioClip = soundEffectLibrary.GetRandomClip(soundName);
        if (audioClip == null) return;

        loopVolumeMultiplier = volumeMultiplier;
        loopAudioSource.clip = audioClip;
        loopAudioSource.loop = true;
        loopAudioSource.pitch = pitch;
        loopAudioSource.volume = currentSFXVolume * loopVolumeMultiplier;
        loopAudioSource.Play();
    }

    public static void UpdateLoop(float volumeMultiplier, float pitch = 1f)
    {
        if (loopAudioSource == null || !loopAudioSource.isPlaying) return;

        loopVolumeMultiplier = volumeMultiplier;
        loopAudioSource.volume = currentSFXVolume * loopVolumeMultiplier;
        loopAudioSource.pitch = pitch;
    }

    public static void StopLoop()
    {
        if (loopAudioSource == null) return;

        loopAudioSource.Stop();
        loopAudioSource.clip = null;
    }

    public static bool IsLoopPlaying()
    {
        return loopAudioSource != null && loopAudioSource.isPlaying;
    }
}
