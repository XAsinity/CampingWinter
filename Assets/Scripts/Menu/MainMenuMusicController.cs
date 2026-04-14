using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class MainMenuMusicController : MonoBehaviour
{
    [Header("Main Menu Music")]
    [SerializeField] string mainMenuSceneName = "MainMenu";
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioClip musicClip;
    [SerializeField] bool playOnStart = true;

    void Awake()
    {
        if (musicSource == null)
            musicSource = GetComponent<AudioSource>();

        if (musicSource == null)
            return;

        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;

        if (musicClip != null)
            musicSource.clip = musicClip;
    }

    void Start()
    {
        if (musicSource == null)
            return;

        if (!IsInMainMenuScene())
        {
            musicSource.Stop();
            return;
        }

        PlayerSettings settings = PlayerSettings.Instance != null ? PlayerSettings.Instance : FindFirstObjectByType<PlayerSettings>();
        if (settings != null)
            SetMenuMusicVolume(settings.MenuMusicVolume);

        if (playOnStart && musicSource.clip != null)
            musicSource.Play();
    }

    public void SetMenuMusicVolume(float volume01)
    {
        if (musicSource == null)
            return;

        musicSource.volume = Mathf.Clamp01(volume01);
    }

    bool IsInMainMenuScene()
    {
        if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            return true;

        return string.Equals(SceneManager.GetActiveScene().name, mainMenuSceneName, System.StringComparison.Ordinal);
    }
}
