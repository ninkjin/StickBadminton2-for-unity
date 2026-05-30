using UnityEngine;
using UnityEngine.SceneManagement;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    private AudioSource sfxSource;
    private AudioSource musicSource;

    private AudioClip whooshClip;
    private AudioClip birdiehitClip;
    private AudioClip dingClip;
    private AudioClip cheerClip;
    private AudioClip sighClip;
    private AudioClip shotgunClip;
    private AudioClip titleMusicClip;
    private AudioClip backgroundMusicClip;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject("SoundManager");
        var sm = go.AddComponent<SoundManager>();
        sm.LoadClips();
        DontDestroyOnLoad(go);
    }

    void LoadClips()
    {
        whooshClip = Resources.Load<AudioClip>("Sounds/whoosh");
        birdiehitClip = Resources.Load<AudioClip>("Sounds/birdiehit");
        dingClip = Resources.Load<AudioClip>("Sounds/ding");
        cheerClip = Resources.Load<AudioClip>("Sounds/cheer");
        sighClip = Resources.Load<AudioClip>("Sounds/sigh");
        shotgunClip = Resources.Load<AudioClip>("Sounds/shotgun");
        titleMusicClip = Resources.Load<AudioClip>("Sounds/titlemusic");
        backgroundMusicClip = Resources.Load<AudioClip>("Sounds/backgroundmusic");
    }

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.5f;

        // 根据当前场景播放音乐
        string sceneName = SceneManager.GetActiveScene().name;
        PlayMusicForScene(sceneName);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    void PlayMusicForScene(string sceneName)
    {
        if (sceneName.Contains("Battle") || sceneName == "Battle Background")
            PlayMusicClip(backgroundMusicClip);
        else
            PlayMusicClip(titleMusicClip);
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public static void PlaySFX(string name)
    {
        if (Instance == null) return;
        AudioClip clip = Instance.GetClip(name);
        if (clip != null)
            Instance.sfxSource.PlayOneShot(clip);
    }

    public static void PlayMusic(string name)
    {
        if (Instance == null) return;
        AudioClip clip = Instance.GetClip(name);
        PlayMusicClip(clip);
    }

    static void PlayMusicClip(AudioClip clip)
    {
        if (clip == null || Instance == null) return;
        if (Instance.musicSource.clip == clip && Instance.musicSource.isPlaying) return;
        Instance.musicSource.clip = clip;
        Instance.musicSource.Play();
    }

    public static void StopMusic()
    {
        if (Instance != null)
            Instance.musicSource.Stop();
    }

    AudioClip GetClip(string name)
    {
        switch (name)
        {
            case "whoosh": return whooshClip;
            case "birdiehit": return birdiehitClip;
            case "ding": return dingClip;
            case "cheer": return cheerClip;
            case "sigh": return sighClip;
            case "shotgun": return shotgunClip;
            case "title": return titleMusicClip;
            case "background": return backgroundMusicClip;
            default: return null;
        }
    }
}
