using UnityEngine;


// 몬스터의 경우 (클릭 불가, 키보드로 방어)
public class MonsterTroll : TrollBase
{
    protected float _currentTime = 0f; // Update용 타이머

    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void EndTroll() { }
    public override void ApplyEffect() { }
}