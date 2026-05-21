using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public class Hero : MonoBehaviour
{
    // ── 데이터 ──────────────────────────────────────────
    private Hero_form _data;

    // ── 참조 ────────────────────────────────────────────
    /// <summary>GameNormal이 관리하는 몬스터 목록 참조</summary>
    private List<Monster> _monsters;

    // ── 상태 ────────────────────────────────────────────
    private IDisposable _attackDisposal;
    private bool _isActive;

    // ─────────────────────────────────────────────────────

    ResourceData resourceData;
    Animator animator;

    public void Initialize(Hero_form data, List<Monster> monsters)
    {
        _data = data;
        _monsters = monsters;

        resourceData = ResourceManager.Instance.GetInstance(data.id);
        resourceData.transform.SetParent(transform);
        resourceData.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        animator = resourceData.GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError($"Animator component not found on hero prefab for {name}");
        }
    }

    public void StartAttack()
    {
        StopAttack();
        _isActive = true;
        _attackDisposal = Observable
            .Interval(TimeSpan.FromSeconds(_data.atk_interval))
            .Subscribe(_ => TryAttack())
            .AddTo(this);
    }

    public void StopAttack()
    {
        _isActive = false;
        _attackDisposal?.Dispose();
        _attackDisposal = null;
    }

    // ── 내부 ────────────────────────────────────────────
    /// <summary>가장 가까운(X 기준 왼쪽, 즉 바리케이드에 가장 가까운) 몬스터를 공격</summary>
    private void TryAttack()
    {
        if (_monsters == null || _monsters.Count == 0) return;

        Monster target = GetNearestMonster();
        if (target == null) return;

        var projectile = ResourceManager.Instance.GetInstance<Projectile>();
        projectile.transform.localScale = Vector3.one;
        projectile.transform.position = transform.position;
        projectile.Launch(300001, target.transform.position, _data.atk);
    }

    private Monster GetNearestMonster()
    {
        Monster nearest = null;
        float minX = float.MaxValue;

        foreach (var m in _monsters)
        {
            if (m == null || !m.gameObject.activeSelf) continue;

            float x = m.transform.position.x;
            if (x < minX)
            {
                minX = x;
                nearest = m;
            }
        }

        return nearest;
    }

    private void OnDestroy()
    {
        StopAttack();
    }
}
