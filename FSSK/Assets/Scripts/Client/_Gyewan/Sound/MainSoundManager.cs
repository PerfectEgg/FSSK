using System;
using UnityEngine;

// 메인 사운드 매니저 (싱글톤)
public class MainSoundManager: MonoBehaviour
{
    // 싱글톤 인스턴스
    public static MainSoundManager Instance { get; private set; }

    [Header("오디오 소스")]
    [SerializeField] private AudioSource _bgmSource;    // BGM 재생용
    [SerializeField] private AudioSource _sfxSource;    // SFX 재생용

    [Header("사운드 소스")]
    [SerializeField] private AudioClip _startBGM;       // 시작 BGM

    void Awake() {
        // 싱글톤 및 씬 전환 시 유지 설정
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 변경되어도 파괴되지 않음
        }
        else
        {
            // 이미 매니저가 존재한다면 새로 생성된 것은 파괴
            Destroy(gameObject);
        }
    }

    void Start()
    {
        _bgmSource.volume = 0.5f; // BGM 기본 볼륨
        _sfxSource.volume = 0.6f; // SFX 기본 볼

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

    // 🟢 BGM 재생 (기존 BGM이 있다면 덮어씌움)
    private void HandlePlayBGM(AudioClip clip, float volume)
    {
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
        if (_sfxSource == null || clip == null) return;

        _sfxSource.PlayOneShot(clip, volume);
    }
}