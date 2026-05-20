using System.Diagnostics;
using UnityEngine;

public class MyWebView : MonoBehaviour
{

    [SerializeField] private GameObject webViewGameObject;
    private WebViewObject webViewObject;
    private    string url = "https://m.naver.com/";

    public string Url { get => url; set => url = value; }

    public void StartWebView()
    {


        try
        {
            if(webViewObject == null)
            {
                webViewObject = webViewGameObject.AddComponent<WebViewObject>();
                webViewObject.Init((msg) =>
                {
                    UnityEngine.Debug.Log(string.Format("CallFromJS[{0}]", msg));
                    
                }, enableWKWebView: true);

                webViewObject.LoadURL(url);
                webViewObject.SetVisibility(true);
                webViewObject.SetMargins(100, 400, 100, 300);
            }
            else{
                webViewObject.LoadURL(url);
                webViewObject.SetVisibility(true);
            }
           
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"WebView Error : {e}");
        }
    }
    
    public void CloseWebview()
    {
        try
        {
            webViewObject.SetVisibility(false);
            //Destroy(webViewObject);
        }
        catch (System.Exception e)
        {
            print($"WebView Error : {e}");
        }
    }
}