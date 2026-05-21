using UnityEngine;

public class GameNormalUI : MonoBehaviour
{
    [SerializeField] MergeBoard mergeBoard;

    public void Initialize()
    {
        mergeBoard.gameObject.SetActive(true);
        mergeBoard.Initialize();
    }
}
