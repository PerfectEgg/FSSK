using UnityEngine;
using Photon.Pun;

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
    void OnDragEnd();
}

// 기본 트롤 클래스 (공통 기능 포함)
public abstract class TrollBase : MonoBehaviourPun, ITrollEvent
{   
    public abstract void ApplyEffect();
    public abstract void EndTroll();
}