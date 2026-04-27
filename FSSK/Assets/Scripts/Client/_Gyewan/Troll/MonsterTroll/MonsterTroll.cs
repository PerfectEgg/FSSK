using UnityEngine;


// 몬스터의 경우 (클릭 불가, 키보드로 방어)
public class MonsterTroll : TrollBase, IKeyInteractable 
{
    // --- TrollBase(추상 클래스)의 메서드 구현 ---
    public override void EndTroll() { }
    public override void ApplyEffect() { }

    // --- IKeyInteractable(인터페이스)의 메서드 구현 ---
    public virtual void OnKeyPressed(KeyCode key) 
    {
        // 예: 세이렌일 때 AD 키 입력 체크
        // 조건이 충족되면 EndTroll() 호출하여 스턴 해제
    }
}