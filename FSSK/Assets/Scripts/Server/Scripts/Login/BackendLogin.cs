using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 뒤끝 SDK namespace 추가
using BackEnd;

public class BackendLogin
{
    private static BackendLogin _instance = null;

    public static BackendLogin Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new BackendLogin();
            }

            return _instance;
        }
    }

    // 성공 여부 반환. 성공 시 nickname 에 서버에 저장된 닉네임(없으면 "") 을 담아 반환
    public bool CustomSignUp(string id, string pw, out string errorMsg)
    {
        errorMsg = "";
        Debug.Log("회원가입을 요청합니다.");

        var bro = Backend.BMember.CustomSignUp(id, pw);

        if (bro.IsSuccess())
        {
            Debug.Log("회원가입에 성공했습니다. : " + bro);
            return true;
        }
        else
        {
            errorMsg = bro.ToString();
            Debug.LogError("회원가입에 실패했습니다. : " + bro);
            return false;
        }
    }

    // 로그인만 수행. 닉네임 조회는 LoginUIManager 에서 직접 처리
    public bool CustomLogin(string id, string pw, out string errorMsg)
    {
        errorMsg = "";
        Debug.Log("로그인을 요청합니다.");

        var bro = Backend.BMember.CustomLogin(id, pw);

        if (bro.IsSuccess())
        {
            Debug.Log("로그인이 성공했습니다. : " + bro);
            return true;
        }
        else
        {
            errorMsg = bro.ToString();
            Debug.LogError("로그인이 실패했습니다. : " + bro);
            return false;
        }
    }

    public bool UpdateNickname(string nickname, out string errorMsg)
    {
        errorMsg = "";
        Debug.Log("닉네임 변경을 요청합니다.");

        var bro = Backend.BMember.UpdateNickname(nickname);

        if (bro.IsSuccess())
        {
            Debug.Log("닉네임 변경에 성공했습니다 : " + bro);
            return true;
        }
        else
        {
            errorMsg = bro.ToString();
            Debug.LogError("닉네임 변경에 실패했습니다 : " + bro);
            return false;
        }
    }
}