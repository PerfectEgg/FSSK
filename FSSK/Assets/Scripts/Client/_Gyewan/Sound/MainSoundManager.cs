using UnityEngine;

// 메인 사운드 매니저 (싱글톤)
[DefaultExecutionOrder(-1000)]
public class MainSoundManager : MonoBehaviour
{
    private const string RuntimeObjectName = "Main Sound Manager (Runtime)";
    private const float DefaultBgmVolume = 0.5f;
    private const float DefaultSfxVolume = 0.6f;

    // 싱글톤 인스턴스
    public static MainSoundManager Instance { get; private set; }

    [Header("오디오 소스")]
    [SerializeField] private AudioSource _bgmSource;    // BGM 재생용
    [SerializeField] private AudioSource _sfxSource;    // SFX 재생용

    [Header("사운드 소스")]
    [SerializeField] private AudioClip _startBGM;       // 시작 BGM

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstanceAfterSceneLoad()
    {
        EnsureInstance();
    }

    public static MainSoundManager EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        MainSoundManager existing = FindFirstObjectByType<MainSoundManager>();
        if (existing != null)
        {
            Instance = existing;
            existing.EnsureAudioSources();
            DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        GameObject managerObject = new GameObject(RuntimeObjectName);
        return managerObject.AddComponent<MainSoundManager>();
    }

    private void Awake()
    {
        // 싱글톤 및 씬 전환 시 유지 설정
        if (Instance == null)
        {
            Instance = this;
            EnsureAudioSources();
            DontDestroyOnLoad(gameObject); // 씬이 변경되어도 파괴되지 않음
        }
        else
        {
            // 이미 매니저가 존재한다면 새로 생성된 것은 파괴
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        EnsureAudioSources();

        _bgmSource.volume = DefaultBgmVolume; // BGM 기본 볼륨
        _sfxSource.volume = DefaultSfxVolume; // SFX 기본 볼륨

        _bgmSource.loop = true; // BGM은 무한 반복

        // 게임 시작 시 BGM 재생
        if (_startBGM != null)
        {
            SoundEvents.PlayBGM?.Invoke(_startBGM, _bgmSource.volume);
        }
    }

    private void OnEnable()
    {
        // 이벤트 구독
        SoundEvents.PlayBGM += HandlePlayBGM;
        SoundEvents.PlaySFX += HandlePlaySFX;
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제
        SoundEvents.PlayBGM -= HandlePlayBGM;
        SoundEvents.PlaySFX -= HandlePlaySFX;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // BGM 재생 (기존 BGM이 있다면 덮어씌움)
    private void HandlePlayBGM(AudioClip clip, float volume)
    {
        EnsureAudioSources();
        if (_bgmSource == null || clip == null) return;

        // 이미 같은 음악이 재생 중이면 무시
        if (_bgmSource.clip == clip && _bgmSource.isPlaying) return;

        _bgmSource.clip = clip;
        _bgmSource.volume = volume;
        _bgmSource.loop = true; // BGM은 무한 반복
        _bgmSource.Play();
    }

    private void HandleStopBGM()
    {
        if (_bgmSource == null) return;

        _bgmSource.Stop();
        Debug.Log("🎵 [BGM 정지] BGM 정지 요청 받음");
    }

    private void HandlePlaySFX(AudioClip clip, float volume)
    {
        EnsureAudioSources();
        if (_sfxSource == null || clip == null) return;

        _sfxSource.PlayOneShot(clip, volume);
    }

    private void EnsureAudioSources()
    {
        if (_bgmSource == null)
        {
            _bgmSource = GetExistingAudioSourceExcept(null) ?? gameObject.AddComponent<AudioSource>();
        }

        if (_sfxSource == null || _sfxSource == _bgmSource)
        {
            _sfxSource = GetExistingAudioSourceExcept(_bgmSource) ?? gameObject.AddComponent<AudioSource>();
        }

        ConfigureBgmSource();
        ConfigureSfxSource();
    }

    private AudioSource GetExistingAudioSourceExcept(AudioSource excluded)
    {
        AudioSource[] sources = GetComponents<AudioSource>();
        foreach (AudioSource source in sources)
        {
            if (source != null && source != excluded)
            {
                return source;
            }
        }

        return null;
    }

    private void ConfigureBgmSource()
    {
        if (_bgmSource == null) return;

        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.spatialBlend = 0f;
        if (_bgmSource.volume <= 0f)
        {
            _bgmSource.volume = DefaultBgmVolume;
        }
    }

    private void ConfigureSfxSource()
    {
        if (_sfxSource == null) return;

        _sfxSource.playOnAwake = false;
        _sfxSource.loop = false;
        _sfxSource.spatialBlend = 0f;
        if (_sfxSource.volume <= 0f)
        {
            _sfxSource.volume = DefaultSfxVolume;
        }
    }
}
