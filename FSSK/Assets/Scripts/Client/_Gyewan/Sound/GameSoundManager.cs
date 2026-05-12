using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 메인 사운드 매니저 (싱글톤)
public class GameSoundManager: MonoBehaviour
{
    [Header("3D 사운드 설정")]
    [SerializeField] private float _minDistance = 5f;  // 이 거리 안에서는 최대 볼륨으로 들림
    [SerializeField] private float _maxDistance = 50f; // 이 거리 밖으로 나가면 소리가 안 들림

    [Header("풀링 설정")]
    [SerializeField] private int _initialPoolSize = 10; // 처음에 미리 만들어둘 스피커 개수

    private List<int> _rainLevelProgression = new List<int> { 0, 0, 1, 1, 1, 2, 2, 2, 3, 3 };
    private List<int> _windLevelProgression = new List<int> { 0, 1, 1, 1, 2, 2, 2, 3, 3, 3 };

    [Header("환경음 설정")]
    [Tooltip("비, 바람처럼 항시 재생되는 2D 루프 사운드 소스 + 번개처럼 특정 상황에서 재생되는 단발성 사운드 소스")]
    [SerializeField] private AudioSource _rainSource;
    [SerializeField] private AudioSource _windSource;
    [SerializeField] private AudioSource _lightningSource;

    [Header("환경음 사운드 설정")]
    [Tooltip("웨이브 단계별로 볼륨 조절")]
    [SerializeField] private float _wave1Volume = 0.15f; // 웨이브 1단계 볼륨
    [SerializeField] private float _wave2Volume = 0.25f; // 웨이브 2단계 볼륨
    [SerializeField] private float _wave3Volume = 0.35f; // 웨이브 3단계 볼륨

    // 스피커를 보관할 창고 (큐)
    private Queue<AudioSource> _soundPool = new Queue<AudioSource>();

    private void OnEnable()
    {
        SoundEvents.Play3DSFX += HandlePlay3DSFX;
        SoundEvents.UpdateWaveAmbient += HandleUpdateWaveAmbient;
        SoundEvents.PlayLightning += HandlePlayLightning;
    }

    private void OnDisable()
    {
        SoundEvents.Play3DSFX -= HandlePlay3DSFX;
        SoundEvents.UpdateWaveAmbient -= HandleUpdateWaveAmbient;
        SoundEvents.PlayLightning -= HandlePlayLightning;
    }

    void Awake()
    {
        // 씬이 시작될 때 스피커를 미리 생성해서 풀에 넣어둡니다.
        for (int i = 0; i < _initialPoolSize; i++)
        {
            CreateNewAudioSource();
        }

        // 🟢 2. 번개 스피커 강제 제어 (시작하자마자 치는 날벼락 방지)
        if (_lightningSource != null)
        {
            _lightningSource.playOnAwake = false; // 강제로 끄기
            _lightningSource.Stop();              // 재생 중이면 멈추기
        }

        // 🟢 비와 바람만 루프 세팅! (번개는 제외)
        SetupAmbientSource(_rainSource);
        SetupAmbientSource(_windSource);

        // 처음 시작 시 볼륨은 0으로 세팅 (웨이브 시작 전)
        if (_rainSource != null) _rainSource.volume = 0f;
        if (_windSource != null) _windSource.volume = 0f;
    }

    // 🟢 환경음 스피커 기본 세팅 (무한 반복)
    private void SetupAmbientSource(AudioSource source)
    {
        if (source != null)
        {
            source.playOnAwake = false; // 코드로 강제 제어권 뺏기
            source.volume = 0f;         // 🟢 Play()를 부르기 전에 볼륨부터 0으로 확실히 막아버림
            source.loop = true; 
            
            if (!source.isPlaying) source.Play();
        }
    }

    // 🟢 웨이브 단계에 따른 볼륨 조절 로직
    private void HandleUpdateWaveAmbient(int stage)
    {
        float rainTargetVolume = 0f;
        float windTargetVolume = 0f;

        // 기획하신 단계별 볼륨 수치 (유니티 볼륨은 0.0 ~ 1.0 기준이므로 100으로 나눔)
        switch (stage)
        {
            case 0: rainTargetVolume = 0f;  windTargetVolume = 0f; break;
            case 1: rainTargetVolume = 0; windTargetVolume = _wave1Volume; break;
            case 2: case 3: rainTargetVolume = _wave1Volume; windTargetVolume = _wave1Volume; break;
            case 4: rainTargetVolume = _wave1Volume; windTargetVolume = _wave2Volume; break;
            case 5: case 6: rainTargetVolume = _wave2Volume; windTargetVolume = _wave2Volume; break;
            case 7: rainTargetVolume = _wave2Volume; windTargetVolume = _wave3Volume; break;
            default: rainTargetVolume = _wave3Volume; windTargetVolume = _wave3Volume; break;
        }

        // 볼륨 즉시 적용 (만약 서서히 커지게 하고 싶다면 이 부분에 DOTween이나 Coroutine을 사용하면 좋습니다)
        if (_rainSource != null) _rainSource.volume = rainTargetVolume;
        if (_windSource != null) _windSource.volume = windTargetVolume;
    }

    // 🟢 번개 단발성 재생 로직
    private void HandlePlayLightning(int stage)
    {
        if (_lightningSource != null && _lightningSource.clip != null)
        {
            float targetVolume = 0f; // 기본 볼륨

            switch (stage)
            {
                case 0: targetVolume = 0f; break;
                case 1: targetVolume = _wave1Volume; break;
                case 2: targetVolume = _wave2Volume; break;
                case 3: targetVolume = _wave3Volume; break;
            }

            // PlayOneShot을 사용하면 번개가 연속으로 쳐도 소리가 씹히지 않고 겹쳐서 자연스럽게 들립니다.
            _lightningSource.PlayOneShot(_lightningSource.clip, targetVolume);
            Debug.Log($"⚡ [번개] 콰쾅! (볼륨: {targetVolume})");
        }
    }

    // =================================
    // 아래는 단발성 3D 사운드 풀링 로직
    // =================================

    // 🟢 스피커를 생성하고 세팅하는 전용 함수
    private AudioSource CreateNewAudioSource()
    {
        GameObject sfxObj = new GameObject("3DSpeaker_Worker");
        sfxObj.transform.SetParent(this.transform); // 매니저의 자식으로 정리

        AudioSource source = sfxObj.AddComponent<AudioSource>();
        source.spatialBlend = 1.0f;             
        source.rolloffMode = AudioRolloffMode.Linear; 
        source.minDistance = _minDistance;
        source.maxDistance = _maxDistance;
        source.playOnAwake = false;

        sfxObj.SetActive(false); // 일단 꺼둠
        _soundPool.Enqueue(source); // 창고에 보관

        return source;
    }

    private void HandlePlay3DSFX(AudioClip clip, Vector3 position, float volume)
    {
        if (clip == null) return;

        AudioSource source = null;

        // 1. 창고에 남은 스피커가 있다면 꺼내 쓰고, 부족하면 새로 만듭니다.
        if (_soundPool.Count > 0)
        {
            source = _soundPool.Dequeue();
        }
        else
        {
            source = CreateNewAudioSource();
            // (주의) 큐에서 뺐기 때문에 여기서 Enqueue는 하지 않습니다.
        }

        // 2. 위치, 클립, 볼륨 세팅 후 재생
        source.transform.position = position;
        source.clip = clip;
        source.volume = volume;
        
        source.gameObject.SetActive(true);
        source.Play();

        // 3. 소리가 끝나면 파괴(Destroy)하는 대신 창고로 다시 반납합니다.
        StartCoroutine(ReturnToPoolCoroutine(source, clip.length));
    }

    private IEnumerator ReturnToPoolCoroutine(AudioSource source, float delay)
    {
        // 소리 길이만큼 기다림
        yield return new WaitForSeconds(delay);

        // 스피커 전원을 끄고 창고에 다시 넣음
        source.gameObject.SetActive(false);
        _soundPool.Enqueue(source);
    }
}