using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;
using UnityEngine.Events;
using UnityEngine.Analytics;
using UnityEngine.Purchasing.Security;
using System.Collections;
using System.Linq;
using Firebase.Extensions;
using Cysharp.Threading.Tasks;

[System.Serializable]
public class ShopProductCatalog
{
    public string productID;
    public ProductType productType;
    public string googlePlayID;
    public string appleStoreID;
}

public class IAPManager : BaseSystemManager<IAPManager>
{
    #region Configuration
    [Header("상품 설정")]
    private List<ShopProductCatalog> _products = new List<ShopProductCatalog>();

    [Header("디버그 설정")]
    private bool _enableTestMode = true;
    private bool _enableReceiptValidation = true;

    [Header("이벤트")]
    public UnityEvent<string> OnPurchaseSuccess = new UnityEvent<string>();
    public UnityEvent<string, string> OnPurchaseFailedEvent = new UnityEvent<string, string>();

    #endregion

    #region Internal State
    StoreController m_StoreController;
    private readonly Dictionary<string, Product> _productCache = new Dictionary<string, Product>();
    #endregion

    #region Lifecycle

    private float _initializationTime = -1f;
    private bool _isManualPurchase = false;


    protected override void OnSingletonInitialized()
    {
        base.OnSingletonInitialized();

        SDKInitiailze();
    }

    public void SDKInitiailze()
    {
        if (m_StoreController == null || IsConnected == false)
        {
            InitializeProductCatalog();

            StartCoroutine(WaitSaveDataAndInitializeIAP());
        }
    }

    #endregion

    #region Initialization
    private void InitializeProductCatalog()
    {
        var catalog = ProductCatalog.LoadDefaultCatalog();
        _products.Clear();

        foreach (var product in catalog.allProducts)
        {
            _products.Add(new ShopProductCatalog
            {
                productID = product.id,
                productType = product.type,
                googlePlayID = product.GetStoreID(GooglePlay.Name),
                appleStoreID = product.GetStoreID(AppleAppStore.Name)
            });
        }
    }

    private IEnumerator WaitSaveDataAndInitializeIAP()
    {
        yield return null;

        InitializePurchasing().Forget();
    }

    public async UniTask InitializePurchasing()
    {
#if UNITY_EDITOR
        _enableTestMode = true;
#endif

        _initializationTime = Time.realtimeSinceStartup;
        _isManualPurchase = false;

        if (m_StoreController != null)
        {
            m_StoreController.OnStoreConnected -= OnStoreConnected;
            m_StoreController.OnStoreDisconnected -= OnStoreDisconnected;
            m_StoreController.OnProductsFetched -= OnProductsFetched;
            m_StoreController.OnProductsFetchFailed -= OnProductsFetchFailed;
            m_StoreController.OnPurchasesFetched -= OnPurchasesFetched;
            m_StoreController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
            m_StoreController.OnPurchasePending -= OnPurchasePending;
            m_StoreController.OnPurchaseFailed -= OnPurchaseFailed;
        }

        m_StoreController = UnityIAPServices.StoreController();
        m_StoreController.OnStoreConnected += OnStoreConnected;
        m_StoreController.OnStoreDisconnected += OnStoreDisconnected;
        m_StoreController.OnProductsFetched += OnProductsFetched;
        m_StoreController.OnProductsFetchFailed += OnProductsFetchFailed;
        m_StoreController.OnPurchasesFetched += OnPurchasesFetched;
        m_StoreController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
        m_StoreController.OnPurchasePending += OnPurchasePending;
        m_StoreController.OnPurchaseFailed += OnPurchaseFailed;

        await m_StoreController.Connect();
    }

    public bool IsConnected = false;

    private void OnStoreConnected()
    {
        List<ProductDefinition> list = new List<ProductDefinition>();
        if (_products != null && _products.Count > 0)
        {
            foreach (var product in _products)
            {
                string storeSpecificId = ConvertToStoreSpecID(product.productID);
                list.Add(new(product.productID, storeSpecificId, product.productType));
            }
        }

        m_StoreController.FetchProducts(list);

        IsConnected = true;
    }

    private void OnProductsFetched(List<Product> products)
    {
        foreach (var product in m_StoreController.GetProducts())
        {
            _productCache[product.definition.id] = product;
        }

        // Fetch purchases for successfully retrieved products
        m_StoreController.FetchPurchases();
    }

    #endregion

    #region Core Functionality
    public void BuyProduct(string productID)
    {
        // var popup = PopupManager.Instance.GetActivePopup<PopupAlert>();
        // if (popup == null)
        // {
        //     PopupManager.Instance.ShowPopup<PopupAlert>("Processing...", "Please wait a moment.", onCreated: (popup) =>
        //     {
        //         popup.SetVisibleAllBtns(false);
        //     });
        // }
        // else
        // {
        //     popup.SetTitle("Processing...");
        //     popup.SetDescription("Please wait a moment.");
        //     popup.SetVisibleAllBtns(false);
        // }

#if UNITY_EDITOR
        // 에디터에서 테스트 모드 활성화 시 자동 성공 처리
        if (_enableTestMode)
        {
            Debug.Log($"<color=green>에디터 테스트: {productID} 구매 성공 시뮬레이션</color>");
            StartCoroutine(SimulatePurchaseSuccess(productID));
            return;
        }
#endif

        _isManualPurchase = true;

        if (!IsIAPInitialized())
        {
            OnPurchaseFailedEvent?.Invoke(productID, "STORE_NOT_INITIALIZED");
            return;
        }

        if (!_productCache.TryGetValue(productID, out Product product))
        {
            OnPurchaseFailedEvent?.Invoke(productID, "PRODUCT_NOT_FOUND");
            return;
        }

        if (!product.availableToPurchase)
        {
            OnPurchaseFailedEvent?.Invoke(productID, "PRODUCT_UNAVAILABLE");
            return;
        }

        m_StoreController.PurchaseProduct(product);
    }

    public void OnPurchasePending(PendingOrder order)
    {
        string txId = order.Info.TransactionID;
        Product product = order.CartOrdered.Items().FirstOrDefault()?.Product;
        if (product == null) return;

        // Duplicate guard
        if (HasProcessedTransaction(txId))
        {
            Debug.LogWarning($"[IAPManager] Already granted tx={txId}, confirming only.");
            m_StoreController.ConfirmPurchase(order);
            return;
        }

        if (_enableReceiptValidation && !ValidateReceipt(order.Info.Receipt))
        {
            Debug.LogWarning($"[IAPManager] Receipt validation failed. tx={txId}, product={product.definition.id}");
            OnPurchaseFailedEvent?.Invoke(product.definition.id, "RECEIPT_VALIDATION_FAILED");
            return;
        }

        // 🌟 상품 타입에 따른 로직 분기
        if (product.definition.type == ProductType.Consumable)
        {
            GrantConsumable(product);
        }
        else if (product.definition.type == ProductType.NonConsumable)
        {
            GrantNonConsumable(product);
        }
        else if (product.definition.type == ProductType.Subscription)
        {
            // 구독 상품 지급 로직이 필요할 경우 추가
        }

        MarkProcessedTransaction(txId);

        // 중요: 비소모성이든 소모성이든 승인(Confirm)은 무조건 해야 합니다.
        m_StoreController.ConfirmPurchase(order);

        string productID = product.definition.id ?? "UNKNOWN_PRODUCT";

        OnPurchaseSuccess?.Invoke(productID);

        if (!_isManualPurchase)
        {
            return;
        }

        LogPurchaseAnalytics(productID);

        SDKManager.Instance.FirebaseManager.TrackIAPPurchase(
            true,
            productID,
            decimal.ToDouble(product.metadata.localizedPrice),
            product.metadata.isoCurrencyCode,
            order.Info.TransactionID
        );

        var afParams = new Dictionary<string, object>
        {
            {"af_content_id", "forgeworld_" + product.definition.id},
            {"af_revenue", product.metadata.localizedPrice.ToString()},
            {"af_currency", product.metadata.isoCurrencyCode},
            {"af_order_id", order.Info.TransactionID}
            // 필요시 추가 파라미터
        };

        AnalyticsManager.Instance.EventLog("af_purchase", afParams);

        _isManualPurchase = false;
    }

    private bool HasProcessedTransaction(string transactionId)
    {
        return PlayerPrefs.GetInt("IAP_TX_" + transactionId, 0) == 1;
    }

    private void MarkProcessedTransaction(string transactionId)
    {
        PlayerPrefs.SetInt("IAP_TX_" + transactionId, 1);
        PlayerPrefs.Save();
    }

    #endregion

    #region Product Purchase
    public ProductType? GetProductType(string productID)
    {
        if (!IsIAPInitialized()) return null;
        var product = GetProduct(productID);
        return product != null ? (ProductType?)product.definition.type : null;
    }

    public bool IsOneTimePurchase(string productID)
    {
        var type = GetProductType(productID);
        return type == ProductType.NonConsumable;
    }

    public bool IsNoAdsProduct(string productID)
    {
        return productID == IsNoAdsProductID();
    }

    public string IsNoAdsProductID()
    {
        return "no_ads_pack";
    }

    public bool IsPurchased(string productID)
    {
#if UNITY_EDITOR
        // if (LocalSaveManager.Log != null)
        // {
        //     return LocalSaveManager.Log.GetPurchaseCount(productID) != 0;
        // }
        // else
        {
            return false;
        }
#else
        if (m_StoreController == null) return false;
     
        // 전체 결제 내역 중 '성공한 결제(ConfirmedOrder)'만 걸러냅니다.
        bool isPurchased = m_StoreController.GetPurchases()
            .OfType<ConfirmedOrder>()
            .Any(order => order.CartOrdered.Items()
                .Any(item => item.Product.definition.id == productID));

        return isPurchased;
#endif
    }

    #endregion

    #region Validation
    private bool ValidateReceipt(string receipt)
    {
#if UNITY_EDITOR
        return true;
#else

        try
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return ValidateAndroidReceipt(receipt);
            }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return ValidateiOSReceipt(receipt);
            }
            else
            {
                return true;
            }
        }
        catch (Exception)
        {
            return false;
        }
#endif
    }

    private bool ValidateAndroidReceipt(string receipt)
    {
        return true;
        // var validator = new CrossPlatformValidator(
        //     GooglePlayTangle.Data(),
        //     null,
        //     Application.identifier
        // );
        // var result = validator.Validate(receipt);
        // return result.Length > 0;
    }

    private bool ValidateiOSReceipt(string receipt)
    {
        return true;
        // var validator = new CrossPlatformValidator(
        //     null,
        //     AppleTangle.Data(),
        //     Application.identifier
        // );
        // var result = validator.Validate(receipt);
        // return result.Length > 0;
    }
    #endregion

    #region Platform Specific

    public void RestorePurchases()
    {
        if (!IsIAPInitialized()) return;

        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            UnityIAPServices.StoreController().RestoreTransactions((success, error) =>
            {
                if (success)
                {
                    Debug.LogError("Restore succeeded.");
                }
                else
                {
                    Debug.LogError("Restore failed: " + error);
                }
            });
        }
        else
        {
            Debug.LogWarning("RestorePurchases is not supported on this platform.");
        }
    }

    #endregion

    #region Helper Methods
    public bool IsIAPInitialized() => m_StoreController != null;

    public Product GetProduct(string productID)
    {
        if (IsIAPInitialized())
        {
            return m_StoreController.GetProductById(productID);
        }

        return null;
    }

    private void LogPurchaseAnalytics(string productID)
    {
        Analytics.CustomEvent("iap_purchase", new Dictionary<string, object>
        {
            {"product_id", productID},
            {"platform", Application.platform},
            {"timestamp", DateTime.Now.ToString("o")}
        });
    }

    #endregion

    #region Editor Integration

    private string ConvertToProductID(string storeProductID)
    {
        ShopProductCatalog catalogProduct = null;

#if UNITY_EDITOR
        catalogProduct = _products.Find(x => x.productID == storeProductID);
#elif UNITY_ANDROID
           catalogProduct = _products.Find(x=>x.googlePlayID == storeProductID);
#elif UNITY_IOS
           catalogProduct = _products.Find(x=>x.appleStoreID == storeProductID);
#endif

        if (catalogProduct != null)
        {
            return catalogProduct.productID;
        }
        else
        {
            return storeProductID;
        }
    }

    private string ConvertToStoreSpecID(string productID)
    {
        ShopProductCatalog catalogProduct = _products.Find(x => x.productID == productID);

        if (catalogProduct != null)
        {
#if UNITY_EDITOR
            return productID;
#elif UNITY_ANDROID
            return catalogProduct.googlePlayID;
#elif UNITY_IOS
            return catalogProduct.appleStoreID;
#endif
        }
        else
        {
            return productID;
        }
    }

    // 🌟 소모성 아이템 지급 (광고 제거 등 비소모성 분리 완료)
    private void GrantConsumable(Product product)
    {
        string productID = product.definition.id;
    }

    // 🌟 비소모성 아이템 지급 전용 함수
    private void GrantNonConsumable(Product product)
    {
        string productID = product.definition.id;

        // 구매 카운트 올리기 및 PlayerPrefs에 영구 기록

        // 광고 제거 상품일 경우 처리
        if (IsNoAdsProduct(productID))
        {
            // if (AdvertisingRootManager.Instance != null)
            //     AdvertisingRootManager.Instance.ForceNoAdsStaus(true);
        }
    }

/// <summary>
/// 보상 지급
/// </summary>
/// <param name="rewardID"></param>
/// <param name="rewardCount"></param>
    public void GrantReward(uint rewardID, int rewardCount)
    {
        if (rewardID == 0 || rewardCount <= 0)
        {
            Debug.LogWarning($"[IAPManager] Invalid reward: ID={rewardID}, Count={rewardCount}");
            return;
        }
    }

#if UNITY_EDITOR
    private IEnumerator SimulatePurchaseSuccess(string productID)
    {
        yield return new WaitForSeconds(0.5f);

        string simulatedTransactionID = "SIMULATED_" + Guid.NewGuid().ToString();
        MarkProcessedTransaction(simulatedTransactionID);

        var product = GetProduct(productID);

        // 🌟 에디터 모드에서도 타입에 맞게 지급되도록 분기 추가
        if (product.definition.type == ProductType.Consumable)
        {
            GrantConsumable(product);
        }
        else if (product.definition.type == ProductType.NonConsumable)
        {
            GrantNonConsumable(product);
        }

        OnPurchaseSuccess?.Invoke(productID);
        LogPurchaseAnalytics(productID);

        Debug.Log($"<color=green>Simulate Purchase Success : {productID}</color>");
    }

#endif

    #endregion

    #region Store Listener Implementation

    public void OnStoreDisconnected(StoreConnectionFailureDescription error)
    {
        IsConnected = false;

        OnPurchaseFailedEvent?.Invoke("INIT_ERROR", error.ToString());
    }

    public void OnProductsFetchFailed(ProductFetchFailed error)
    {
        OnPurchaseFailedEvent?.Invoke("INIT_ERROR", error.ToString());
    }

    /// <summary>
    /// Invoked when previous purchases are fetched. google Restore.
    /// </summary>
    /// <param name="orders">All active pending, completed, and deferred orders for previously fetched products.</param>
    private void OnPurchasesFetched(Orders orders)
    {
        isValidateReceiptReLoad = true;

        foreach (var order in orders.PendingOrders)
        {
            OnPurchasePending(order);
        }

        // if (AdvertisingRootManager.Instance != null)
        //     AdvertisingRootManager.Instance.LoadNoAdsStatus();
    }

    public void OnPurchasesFetchFailed(PurchasesFetchFailureDescription error)
    {
        OnPurchaseFailedEvent?.Invoke("INIT_ERROR", error.ToString());
    }

    private bool isValidateReceiptReLoad = false;
    public bool IsValidateReceiptReLoad { get => isValidateReceiptReLoad; set => isValidateReceiptReLoad = value; }

    public void OnPurchaseFailed(FailedOrder order)
    {
        Product product = order.CartOrdered.Items().FirstOrDefault()?.Product;
        string productID = product?.definition?.id ?? "UNKNOWN_PRODUCT";

        string details = order.Details ?? "";

        Debug.LogError($"[IAPManager] Purchase Failed : {productID}, message : {details} + reason : {order.FailureReason}");

        if (order.FailureReason == PurchaseFailureReason.DuplicateTransaction)
        {
            Debug.LogWarning($"[IAPManager] Already owned detected. Re-fetching purchases for recovery. productID={productID}");

            if (m_StoreController != null)
                m_StoreController.FetchPurchases();


            // PopupManager.Instance.ClosePopup<PopupAlert>();

            return;
        }

        if (product != null)
        {
            SDKManager.Instance.FirebaseManager.TrackIAPPurchase(
                false,
                product.definition.id,
                decimal.ToDouble(product.metadata.localizedPrice),
                product.metadata.isoCurrencyCode,
                order.Info.TransactionID
            );

            var failParams = new Dictionary<string, object>
        {
            {"af_product_id", product.definition.id},
            {"af_error", order.FailureReason},
            {"af_error_message", details}
        };

            AnalyticsManager.Instance.EventLog("af_purchase_fail", failParams);
        }

        // PopupManager.Instance.ClosePopup<PopupAlert>();
        OnPurchaseFailedEvent?.Invoke(productID, $"{order.FailureReason}:{details}");
    }

    #endregion
}