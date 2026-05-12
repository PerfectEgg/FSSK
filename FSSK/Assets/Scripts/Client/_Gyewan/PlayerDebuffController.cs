using UnityEngine;
using Photon.Pun;
using System;

public enum DebuffType { None, Rum, Octopus }

public class PlayerDebuffController : MonoBehaviourPun
{
    [Header("디버프 상태")]
    public DebuffType currentDebuff = DebuffType.None;
    private float _debuffTimer = 0f;
    private float _maxDebuffTime = 8f;

    private void OnEnable()
    {
        TrollEvents.OnHitByRum += HandleHitByRum;
        TrollEvents.OnHitByOctopus += HandleHitByOctopus;
        GameEvents.OnGameOverTriggered += HandleGameOver;
    }

    private void OnDisable()
    {
        TrollEvents.OnHitByRum -= HandleHitByRum;
        TrollEvents.OnHitByOctopus -= HandleHitByOctopus;
        GameEvents.OnGameOverTriggered -= HandleGameOver;
    }

    private void HandleHitByRum()
    {
        if (!photonView.IsMine) return; // 내 화면에서만 걸림
        ApplyDebuff(DebuffType.Rum);
        // TODO: 럼주 깨지는 소리 재생
    }

    private void HandleHitByOctopus()
    {
        if (!photonView.IsMine) return; // 내 화면에서만 걸림
        ApplyDebuff(DebuffType.Octopus);
        // TODO: 문어 철썩 소리 재생
    }

    private void HandleGameOver()
    {
        if (!photonView.IsMine) return;

        currentDebuff = DebuffType.None;
        _debuffTimer = 0f;
        UIEvents.OnDebuffEnded?.Invoke();
    }

    private void ApplyDebuff(DebuffType type)
    {
        currentDebuff = type;
        _debuffTimer = _maxDebuffTime;
        Debug.Log($"💢 [{type}] 디버프 시작! (8초)");
    }

    private void Update()
    {
        // 🟢 내 캐릭터가 아니거나, 디버프가 없으면 무시
        if (!photonView.IsMine || currentDebuff == DebuffType.None) return;

        _debuffTimer -= Time.deltaTime;

        bool isRumSuppressed = false; // 럼주 일렁임 억제 여부

        // 🟢 1. 럼주: AD 키 홀드 (누르고 있는 동안 효과 억제)
        if (currentDebuff == DebuffType.Rum)
        {
            if (Input.GetKey(KeyCode.A) && Input.GetKey(KeyCode.D))
            {
                isRumSuppressed = true; // 누르고 있는 동안 시야 정상화
            }
        }
        // 🟢 2. 문어: AD 키 연타 (누를 때마다 시간 단축)
        else if (currentDebuff == DebuffType.Octopus)
        {
            // A키나 D키를 '누르는 순간'마다 남은 시간 0.3초씩 깎아버림 (수치 조절 가능)
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
            {
                _debuffTimer -= 0.3f;
                // TODO: UI에 작게 "Hit!" 이펙트 띄우면 타격감 상승
            }
        }

        // 디버프 종료 판정
        if (_debuffTimer <= 0f)
        {
            Debug.Log($"✨ [{currentDebuff}] 디버프 해제!");
            currentDebuff = DebuffType.None;
            UIEvents.OnDebuffEnded?.Invoke(); // UI와 카메라 효과 끄기
        }
        else
        {
            // 매 프레임 UI(게이지 바)와 카메라(이펙트) 갱신을 위해 방송
            float progress = Mathf.Clamp01(_debuffTimer / _maxDebuffTime);
            UIEvents.OnDebuffUIUpdate?.Invoke(progress, isRumSuppressed);
        }
    }
}
