using UnityEngine;

// 트롤 이벤트 인터페이스
public interface ITrollEvent
{
    void ApplyEffect();
    void EndTroll();
}

// 드래그 상호작용 인터페이스
public interface IDraggable
{
    void OnDragStart();
    void OnDragging();
    void OnDragEnd();
}

// 키보드 상호작용 인터페이스
public interface IKeyInteractable
{
    void OnKeyPressed(KeyCode key);
}

// 기본 트롤 클래스 (공통 기능 포함)
public abstract class TrollBase : MonoBehaviour, ITrollEvent
{   
    protected float delay;      // 등장 타이머
    protected float duration;   // 지속 타이머
    
    public abstract void ApplyEffect();
    public abstract void EndTroll();
}