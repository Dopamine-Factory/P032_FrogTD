using System;
using UnityEngine;

public static class TableErrorHandler
{
    public static void HandleError(string tableName, Exception e)
    {
        Debug.LogError($"[{tableName}] Error: {e.Message}");
        // 에러 리포트 시스템 연동 가능
        LoadFallbackData(tableName);
    }

    private static void LoadFallbackData(string tableName)
    {
        // 리소스 폴더의 기본 데이터 로드
        var fallback = Resources.Load<TextAsset>($"Fallback/{tableName}");
        if (fallback != null)
        {
            Debug.LogWarning($"Loaded fallback data for {tableName}");
        }
    }
}