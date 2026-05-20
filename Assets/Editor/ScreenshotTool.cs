using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

public class ScreenshotTool : EditorWindow
{
    private Camera screenshotCamera;
    private List<ScreenshotResolution> resolutions = new List<ScreenshotResolution>()
    {
        new ScreenshotResolution(new Vector2Int(1080, 1920), "Screenshot_1080x1920"),
        new ScreenshotResolution(new Vector2Int(1284, 2778), "Screenshot_1284x2778"),
        new ScreenshotResolution(new Vector2Int(2048, 2732), "Screenshot_2048x2732")
    };

    private Vector2Int newResolution = new Vector2Int(1080, 1920);
    private string newName = "Screenshot_1080x1920";
    private Canvas renderMainCanvas;
    private bool excludeCanvasEnabled = false;

    [MenuItem("Tools/Screenshot Tool")]
    public static void ShowWindow()
    {
        GetWindow<ScreenshotTool>("Screenshot Tool");
    }

    private void OnGUI()
    {
        GUILayout.Label("Screenshot Settings", EditorStyles.boldLabel);

        screenshotCamera = (Camera)EditorGUILayout.ObjectField("Camera", screenshotCamera, typeof(Camera), true);
        if (screenshotCamera == null)
        {
            screenshotCamera = Camera.main;
        }
        
        renderMainCanvas = (Canvas)EditorGUILayout.ObjectField("Render Main Canvas", renderMainCanvas, typeof(Canvas), true);
        
        excludeCanvasEnabled = EditorGUILayout.Toggle("Exclude Canvas Active", excludeCanvasEnabled);

        GUILayout.Space(10);
        GUILayout.Label("Resolutions", EditorStyles.boldLabel);
        for (int i = 0; i < resolutions.Count; i++)
        {
            GUILayout.BeginHorizontal();
            resolutions[i].resolution = EditorGUILayout.Vector2IntField("", resolutions[i].resolution);
            resolutions[i].name = EditorGUILayout.TextField(resolutions[i].name);
            if (GUILayout.Button("X", GUILayout.Width(20)))
            {
                resolutions.RemoveAt(i);
                break;
            }
            GUILayout.EndHorizontal();
        }
        
        GUILayout.Space(5);
        newResolution = EditorGUILayout.Vector2IntField("New Resolution", newResolution);
        newName = EditorGUILayout.TextField("New Name", newName);
        if (GUILayout.Button("Add Resolution"))
        {
            resolutions.Add(new ScreenshotResolution(newResolution, string.IsNullOrEmpty(newName) ? $"Screenshot_{newResolution.x}x{newResolution.y}" : newName));
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Capture Screenshots"))
        {
            CaptureScreenshots();
        }
    }

    private void CaptureScreenshots()
    {
        if (screenshotCamera == null)
        {
            return;
        }

        RenderMode originalRenderMode = RenderMode.ScreenSpaceOverlay;
        Camera originalWorldCamera = null;
        bool wasCanvasActive = false;

        if (renderMainCanvas != null)
        {
            if(renderMainCanvas.renderMode == RenderMode.ScreenSpaceOverlay )
            {
                originalRenderMode = renderMainCanvas.renderMode;
                originalWorldCamera = renderMainCanvas.worldCamera;

                // Canvas 렌더 모드를 Camera 모드로 변경
                renderMainCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                renderMainCanvas.worldCamera = screenshotCamera;
            }

            if(excludeCanvasEnabled)
            {
                wasCanvasActive = renderMainCanvas.gameObject.activeSelf;
                renderMainCanvas.gameObject.SetActive(!excludeCanvasEnabled);
            }
        }

        string folderPath = Path.Combine(Application.persistentDataPath, "Screenshots");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        float screenAspect = (float)screenWidth / screenHeight;

        // foreach (var res in resolutions)
        // {
        //     float targetAspect = (float)res.resolution.x / res.resolution.y;
        //     int captureWidth = screenWidth;
        //     int captureHeight = screenHeight;

        //     if (screenAspect > targetAspect)
        //     {
        //         captureWidth = Mathf.RoundToInt(screenHeight * targetAspect);
        //     }
        //     else if (screenAspect < targetAspect)
        //     {
        //         captureHeight = Mathf.RoundToInt(screenWidth / targetAspect);
        //     }

        //     RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);
        //     screenshotCamera.targetTexture = rt;
        //     RenderTexture.active = rt;

        //     screenshotCamera.Render();

        //     Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
        //     screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
        //     screenshot.Apply();

        //     Texture2D resizedScreenshot = new Texture2D(res.resolution.x, res.resolution.y, TextureFormat.RGB24, false);
        //     for (int y = 0; y < res.resolution.y; y++)
        //     {
        //         for (int x = 0; x < res.resolution.x; x++)
        //         {
        //             float u = (float)x / res.resolution.x;
        //             float v = (float)y / res.resolution.y;
        //             resizedScreenshot.SetPixel(x, y, screenshot.GetPixelBilinear(u, v));
        //         }
        //     }
        //     resizedScreenshot.Apply();

        //     byte[] bytes = resizedScreenshot.EncodeToPNG();
        //     string fileName = string.IsNullOrEmpty(res.name) ? 
        //         $"Screenshot_{res.resolution.x}x{res.resolution.y}.png" : $"{res.name}.png";
        //     string filePath = Path.Combine(folderPath, fileName);
        //     File.WriteAllBytes(filePath, bytes);

        //     screenshotCamera.targetTexture = null;
        //     RenderTexture.active = null;
        //     DestroyImmediate(rt);
        //     DestroyImmediate(screenshot);
        //     DestroyImmediate(resizedScreenshot);

        //     Debug.Log($"ScreedShoot: {filePath}");
        // }


        foreach (var res in resolutions)
        {
            int captureWidth = res.resolution.x;
            int captureHeight = res.resolution.y;

            RenderTexture rt = new RenderTexture(captureWidth, captureHeight, 24);
            screenshotCamera.targetTexture = rt;
            RenderTexture.active = rt;

            screenshotCamera.Render();

            Texture2D screenshot = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
            screenshot.Apply();

            byte[] bytes = screenshot.EncodeToPNG();
            string fileName = string.IsNullOrEmpty(res.name) ?
                $"Screenshot_{captureWidth}x{captureHeight}.png" : $"{res.name}.png";
            string filePath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(filePath, bytes);

            screenshotCamera.targetTexture = null;
            RenderTexture.active = null;
            DestroyImmediate(rt);
            DestroyImmediate(screenshot);

            Debug.Log($"Screenshot saved: {filePath}");
        }

        if (renderMainCanvas != null)
        {
            if(originalRenderMode == RenderMode.ScreenSpaceOverlay )
            {
                renderMainCanvas.renderMode = originalRenderMode;
                renderMainCanvas.worldCamera = originalWorldCamera;
            }

            if(excludeCanvasEnabled)
            {
                renderMainCanvas.gameObject.SetActive(wasCanvasActive);
            }
        }
    }

    private class ScreenshotResolution
    {
        public Vector2Int resolution;
        public string name;

        public ScreenshotResolution(Vector2Int resolution, string name)
        {
            this.resolution = resolution;
            this.name = name;
        }
    }
}