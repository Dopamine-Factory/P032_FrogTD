#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShopBase), true)]
public class ShopBaseEditor : Editor
{
    private SerializedProperty shopItemsProp;
    private int selectedTypeIndex = 0;
    private readonly string[] itemTypes = new[] { "SingleShopItem", "PackageShopItem" };

    private void OnEnable()
    {
        shopItemsProp = serializedObject.FindProperty("shopItems");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // 기본 인스펙터 표시
        DrawDefaultInspector();

        // 커스텀 UI
        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("상품 추가", EditorStyles.boldLabel);

        // 상품 타입 선택 드롭다운
        selectedTypeIndex = EditorGUILayout.Popup("상품 타입", selectedTypeIndex, itemTypes);

        // 추가 버튼
        if (GUILayout.Button("새 상품 추가"))
        {
            AddNewItem(itemTypes[selectedTypeIndex]);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void AddNewItem(string typeName)
    {
        ShopItemBase newItem = typeName switch
        {
            "SingleShopItem" => new SingleShopItem(),
            "PackageShopItem" => new PackageShopItem(),
            _ => null
        };

        if (newItem != null)
        {
            shopItemsProp.arraySize++;
            var element = shopItemsProp.GetArrayElementAtIndex(shopItemsProp.arraySize - 1);
            element.managedReferenceValue = newItem;
        }
    }
}
#endif
