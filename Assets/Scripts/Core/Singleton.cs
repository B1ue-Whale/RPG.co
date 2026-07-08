using UnityEngine;

/// <summary>
/// 싱글톤 패턴을 구현한 제네릭 클래스
/// <para>게임 전체 전역 싱글톤</para>
/// </summary>
/// <typeparam name="T"></typeparam>
public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    public static T Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    Debug.LogError(typeof(T) + " 싱글톤 인스턴스가 필요하지만, 씬에 없습니다.");
                }
            }
            return _instance;
        }
    }


    protected virtual void Awake()
    {
        if (_instance == null)
        {
            _instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }
}