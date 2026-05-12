using System;
using UnityEngine;

// 사운드 관련 이벤트
public static class SoundEvents
{
    #region MainSoundManager 이벤트
    // BGM 관련 이벤트 (클립, 볼륨)
    public static Action<AudioClip, float> PlayBGM;
    public static Action StopBGM;

    // 공통 SFX 관련 이벤트 (클립, 볼륨) - 버튼 클릭음, 게임오버 효과음 등
    public static Action<AudioClip, float> PlaySFX;
    #endregion

    #region GameSoundManager 이벤트
    // 인게임 3D SFX (GameSoundManager가 수신)
    // 매개변수: 오디오 클립, 발생 위치(Vector3), 볼륨
    public static Action<AudioClip, Vector3, float> Play3DSFX;
    public static Action<AudioClip, Vector3, float, float, float> Play3DSFX_Cut;

    // 환경음 (비, 바람) 웨이브 단계 조절 이벤트
    public static Action<int> UpdateWaveAmbient;

    // 번개 재생 이벤트 (원하는 볼륨값 전달)
    public static Action<int> PlayLightning;
    #endregion
}
