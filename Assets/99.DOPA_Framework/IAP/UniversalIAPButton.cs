using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Purchasing;
using TMPro;
using System.Linq;
using System.Collections;
using Unity.VisualScripting;


#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Button))]
public class UniversalIAPButton : MonoBehaviour
{
    #region Enums
    public enum ButtonType { Purchase, Restore }
    #endregion

    #region Serialized Fields
    [Header("Catalog Settings")]
    [SerializeField] private bool useCatalog = true;
    [SerializeField][HideInInspector] private int selectedCatalogIndex;

    [Header("Product Settings")]
    [SerializeField] private ButtonType buttonType = ButtonType.Purchase;
    [SerializeField] private string productID;
    [SerializeField] private ProductType productType = ProductType.Consumable;

    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private GameObject loadingIndicator;

    [Header("Debug Settings")]
    [SerializeField] private bool enableTestMode = true;
    #endregion

    #region Private Variables
    private UnityEngine.Purchasing.ProductCatalog catalog;
    private Button button;
    private bool isProcessing = false;
    #endregion

    #region Initialization
    private void Awake()
    {
        button = GetComponent<Button>();

    }
    void Start()
    {
        if (IAPManager.Instance == null || !IAPManager.Instance.IsFullyInitialized || !IAPManager.Instance.IsIAPInitialized())
        {
            gameObject.SetActive(false);
            return;
        }

        button.onClick.AddListener(OnButtonClicked);

        ValidatePlatformRestrictions();
        InitializeButton();
    }
    private void ValidatePlatformRestrictions()
    {
#if !UNITY_IOS
        if (buttonType == ButtonType.Restore)
        {
            gameObject.SetActive(false);
        }
#endif
    }

    private void InitializeButton()
    {
        if (useCatalog)
        {
            LoadCatalog();
            ApplyCatalogSettings();
        }
        UpdateButtonState();
    }
    #endregion

    #region Catalog Integration
    private void LoadCatalog()
    {
        catalog = UnityEngine.Purchasing.ProductCatalog.LoadDefaultCatalog();
    }

    private void ApplyCatalogSettings()
    {
        if (catalog == null || catalog.allProducts.Count == 0)
        {
            Debug.LogError("IAP Catalog is empty or not loaded!");
            return;
        }

        if (selectedCatalogIndex < 0 || selectedCatalogIndex >= catalog.allProducts.Count)
        {
            Debug.LogError("Invalid catalog index!");
            return;
        }

        var product = catalog.allProducts.ElementAt(selectedCatalogIndex);
        productID = product.id;
        productType = product.type;

        UpdateUI(product);
    }

    private void UpdateUI(ProductCatalogItem catalogItem)
    {
        if (titleText) titleText.text = catalogItem.defaultDescription.Title;
        if (descriptionText) descriptionText.text = catalogItem.defaultDescription.Description;
        if (priceText) priceText.text = catalogItem.googlePrice.value.ToString("C");
    }
    #endregion

    #region Purchase Logic
    private void OnButtonClicked()
    {
        if (isProcessing) return;

        switch (buttonType)
        {
            case ButtonType.Purchase:
                StartCoroutine(ProcessPurchase());
                break;
            case ButtonType.Restore:
                StartCoroutine(ProcessRestore());
                break;
        }
    }

    private IEnumerator ProcessPurchase()
    {
        if (string.IsNullOrEmpty(productID))
        {
            SetStatus("Error: Product ID not set");
            yield break;
        }

        isProcessing = true;
        SetStatus("Processing...");
        UpdateButtonState();

        IAPManager.Instance.BuyProduct(productID);

        float timeout = 30f;
        while (isProcessing && timeout > 0)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (isProcessing)
        {
            HandlePurchaseResult(false, "Transaction Timeout");
        }
    }

    private IEnumerator ProcessRestore()
    {
#if UNITY_IOS
        isProcessing = true;
        SetStatus("Restoring...");
        UpdateButtonState();

        IAPManager.Instance.RestorePurchases();

        yield return new WaitForSeconds(2);
        isProcessing = false;
        UpdateButtonState();
        SetStatus("Restoration Complete");
#else
        SetStatus("Restore only available on iOS");
        yield return null;
#endif
    }
    #endregion

    #region Event Handlers
    private void OnEnable()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccess.AddListener(HandlePurchaseSuccess);
            IAPManager.Instance.OnPurchaseFailedEvent.AddListener(HandlePurchaseFailed);
        }
    }

    private void OnDisable()
    {
        if (IAPManager.Instance != null)
        {
            IAPManager.Instance.OnPurchaseSuccess.RemoveListener(HandlePurchaseSuccess);
            IAPManager.Instance.OnPurchaseFailedEvent.RemoveListener(HandlePurchaseFailed);
        }
    }

    private void HandlePurchaseSuccess(string purchasedProductID)
    {
        if (purchasedProductID == productID)
        {
            HandlePurchaseResult(true, "Purchase Successful!");
        }
    }

    private void HandlePurchaseFailed(string productID, string error)
    {
        if (this.productID == productID)
        {
            HandlePurchaseResult(false, $"Failed: {error}");
        }
    }

    private void HandlePurchaseResult(bool success, string message)
    {
        isProcessing = false;
        SetStatus(message);
        UpdateButtonState();

        if (success)
        {
            onPurchaseSuccess?.Invoke();
        }
        else
        {
            onPurchaseFailed?.Invoke(message);
        }

        StartCoroutine(ClearStatus());
    }
    #endregion

    #region UI Management
    private void UpdateButtonState()
    {
        button.interactable = !isProcessing;
        if (loadingIndicator) loadingIndicator.SetActive(isProcessing);
    }

    private void SetStatus(string message)
    {
        if (statusText) statusText.text = message;
    }

    private IEnumerator ClearStatus()
    {
        yield return new WaitForSeconds(3);
        SetStatus("");
    }
    #endregion

    #region Editor Integration
#if UNITY_EDITOR
    [CustomEditor(typeof(UniversalIAPButton))]
    public class UniversalIAPButtonEditor : Editor
    {
        private SerializedProperty selectedCatalogIndexProp;
        private string[] productNames;
        private UnityEngine.Purchasing.ProductCatalog catalog;

        private void OnEnable()
        {
            selectedCatalogIndexProp = serializedObject.FindProperty("selectedCatalogIndex");
            LoadCatalog();
        }

        private void LoadCatalog()
        {
            catalog = UnityEngine.Purchasing.ProductCatalog.LoadDefaultCatalog();
            productNames = catalog.allProducts
                .Select((p, i) => $"[{i}] {p.id} ({p.type}) - {p.defaultDescription.Title}")
                .ToArray();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UniversalIAPButton button = (UniversalIAPButton)target;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Core Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("buttonType"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("useCatalog"));

            if (button.useCatalog)
            {
                DrawCatalogSelection();
            }
            else
            {
                DrawManualSettings();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("UI Configuration", EditorStyles.boldLabel);
            DrawUIReferences();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("enableTestMode"));

            serializedObject.ApplyModifiedProperties();

            if (GUI.changed && button.useCatalog)
            {
                button.ApplyCatalogSettings();
                EditorUtility.SetDirty(button);
            }
        }

        private void DrawCatalogSelection()
        {
            if (catalog.allProducts.Count == 0)
            {
                EditorGUILayout.HelpBox("No products in IAP Catalog!", MessageType.Warning);
                return;
            }

            selectedCatalogIndexProp.intValue = EditorGUILayout.Popup(
                "Catalog Product",
                selectedCatalogIndexProp.intValue,
                productNames
            );

            EditorGUILayout.HelpBox("Automatically updates Product ID and Type", MessageType.Info);
        }

        private void DrawManualSettings()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("productID"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("productType"));
        }

        private void DrawUIReferences()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("titleText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("priceText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("descriptionText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("statusText"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loadingIndicator"));

            if (GUILayout.Button("Auto Link Children"))
            {
                AutoLinkUIElements();
            }
        }

        private void AutoLinkUIElements()
        {
            UniversalIAPButton button = (UniversalIAPButton)target;
            var texts = button.GetComponentsInChildren<TMP_Text>();

            if (texts.Length >= 4)
            {
                button.titleText = texts[0];
                button.priceText = texts[1];
                button.descriptionText = texts[2];
                button.statusText = texts[3];
            }

            button.loadingIndicator = button.transform.Find("LoadingIndicator")?.gameObject;
            EditorUtility.SetDirty(button);
        }
    }
#endif
    #endregion

    #region Public Events
    public UnityEngine.Events.UnityEvent onPurchaseSuccess = new UnityEngine.Events.UnityEvent();
    public UnityEngine.Events.UnityEvent<string> onPurchaseFailed = new UnityEngine.Events.UnityEvent<string>();
    #endregion
}