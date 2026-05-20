using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유니티 게임 오브젝트 범용 Generic 객체 풀링 클래스
/// T: MonoBehaviour를 상속받은 컴포넌트 타입
/// </summary>
public class GenericObjectPool<T> where T : MonoBehaviour
{
    private readonly T prefab;
    private readonly Transform parentContainer;
    private readonly Queue<T> poolQueue;

    private readonly int maxPoolSize;

    /// <summary>
    /// 생성자 - 미리 생성할 초기 크기와 최대 크기 지정 가능
    /// </summary>
    /// <param name="prefab">풀에서 사용할 프리팹</param>
    /// <param name="initialSize">초기 생성할 오브젝트 개수</param>
    /// <param name="maxPoolSize">풀 최대 보유 개수 (0 또는 음수면 제한 없음)</param>
    /// <param name="parent">생성된 오브젝트들의 부모 Transform</param>
    public GenericObjectPool(T prefab, int initialSize = 10, int maxPoolSize = 0, Transform parent = null)
    {
        if (prefab == null)
            throw new ArgumentNullException(nameof(prefab), "Prefab must not be null");

        this.prefab = prefab;
        this.parentContainer = parent;
        this.maxPoolSize = maxPoolSize;
        poolQueue = new Queue<T>(initialSize);

        for (int i = 0; i < initialSize; i++)
        {
            T obj = CreateNewObject();
            poolQueue.Enqueue(obj);
        }
    }

    /// <summary>
    /// 새로운 인스턴스 생성 및 비활성화 후 반환
    /// </summary>
    private T CreateNewObject()
    {
        T newObj = GameObject.Instantiate(prefab, parentContainer);
        newObj.gameObject.SetActive(false);
        return newObj;
    }

    /// <summary>
    /// 풀에서 오브젝트 획득: 없으면 새로 생성
    /// </summary>
    public T Get()
    {
        T obj;
        if (poolQueue.Count == 0)
        {
            obj = CreateNewObject();
        }
        else
        {
            obj = poolQueue.Dequeue();
        }
        obj.gameObject.SetActive(true);
        return obj;
    }

    /// <summary>
    /// 사용이 끝난 오브젝트를 풀에 반환
    /// </summary>
    /// <param name="obj">반환할 오브젝트</param>
    public void ReturnToPool(T obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // 이미 풀에 있거나 최대 초과한 경우 즉시 파괴하거나 무시 가능
        if (maxPoolSize > 0 && poolQueue.Count >= maxPoolSize)
        {
            GameObject.Destroy(obj.gameObject);
            return;
        }

        obj.gameObject.SetActive(false);
        obj.transform.SetParent(parentContainer, false);
        poolQueue.Enqueue(obj);
    }

    /// <summary>
    /// 현재 풀에 남아있는 오브젝트 개수
    /// </summary>
    public int Count => poolQueue.Count;

    /// <summary>
    /// 모든 풀 오브젝트 강제 제거 (예: 씬 전환 시)
    /// </summary>
    public void ClearPool()
    {
        while (poolQueue.Count > 0)
        {
            T obj = poolQueue.Dequeue();
            if (obj != null)
                GameObject.Destroy(obj.gameObject);
        }
    }
}