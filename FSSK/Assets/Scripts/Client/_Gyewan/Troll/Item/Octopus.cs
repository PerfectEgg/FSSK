using UnityEngine;

// 아이템의 경우 (마우스로 던질 수 있음)
public class Octopus : ItemTroll
{
    void Start()
    {
        _grabbedScale = new Vector3(2f, 2f, 2f); // 🟢 잡았을 때 원래 크기
    }
    public override void ApplyDebuff(GameObject target)
    {
        
    }
}

