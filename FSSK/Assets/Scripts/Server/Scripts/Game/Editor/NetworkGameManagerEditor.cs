using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NetworkGameManager))]
public class NetworkGameManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("디버깅", EditorStyles.boldLabel);

        var manager = (NetworkGameManager)target;

        // 네트워크 호출이라 Play 모드에서만 활성화
        using (new EditorGUI.DisabledGroupScope(!Application.isPlaying))
        {
            if (GUILayout.Button("방 나가기 (정상)"))
            {
                manager.ReturnToLobby();
            }

            if (GUILayout.Button("끊기 (비정상)"))
                manager.ForceDisconnect();

            if (GUILayout.Button("연결 상태 출력"))
                manager.LogNetworkState();
        }
    }
}
