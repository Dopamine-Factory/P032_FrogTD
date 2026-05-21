using UniRx;
using UniRx.Triggers;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float Speed { get; set; } = 10f;
    public float Damage { get; set; } = 10f;

    Vector3 direction;
    bool isMoving = false;

    ResourceData projectileData;

    public void Launch(uint id, Vector3 target, float damage = 10f)
    {
        projectileData = ResourceManager.Instance.GetInstance(id);
        projectileData.gameObject.transform.SetParent(transform);
        projectileData.gameObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        
        
        Damage = damage;



        direction = (target - transform.position).normalized;
        isMoving = true;
    }

    private void Update()
    {
        if (!isMoving) return;

        transform.position += direction * Speed * Time.deltaTime;

        if (transform.position.x > 10f) // 화면 밖으로 나가면 제거
        {
            Dispose();
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        Monster monster = other.GetComponentInParent<Monster>();
        if (monster != null)
        {
            monster.Hit(Damage);

            Dispose();
        }
    }

    private void Dispose()
    {
        if (projectileData != null)
        {
            ResourceManager.Instance.ReleaseInstance(projectileData);
            projectileData = null;
            ResourceManager.Instance.ReleaseInstance(this);
        }

    }

}
