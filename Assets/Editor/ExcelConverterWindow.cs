using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using ExcelDataReader;
using System.Data;
using Newtonsoft.Json;
using System.Text;
using System;

public class ExcelConverterWindow : EditorWindow
{
    private List<string> sheetNames = new List<string>();
    private Vector2 scrollPos;
    private string excelPath = "Assets/Data/GameData.xlsx";

    [MenuItem("Tools/Excel Converter")]
    public static void ShowWindow()
    {
        GetWindow<ExcelConverterWindow>("Excel Converter ON");
    }

    private void OnGUI()
    {
        // 엑셀 파일 선택 UI
        excelPath = EditorGUILayout.TextField("Excel Path", excelPath);
        if (GUILayout.Button("Browse Excel"))
            excelPath = EditorUtility.OpenFilePanel("Select Excel", "", "xlsx");

        if (GUILayout.Button("Load Sheets"))
        {
            LoadSheetList();
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        foreach (var sheet in sheetNames)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(sheet);
            if (GUILayout.Button("Generate", GUILayout.Width(80)))
                ProcessSingleSheet(sheet);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        if (GUILayout.Button("Generate Crypto key"))
        {
            string pathKey = Path.Combine(Application.streamingAssetsPath, "Encryption");
            TableManager.EnsureOutputDirectoryExists(pathKey);
            File.WriteAllText(Path.Combine(pathKey, "key"), CryptoUtility.GenerateBase64Key());
        }

        if (GUILayout.Button("Generate All"))
            ConvertExcel();
    }

    private void LoadSheetList()
    {
        sheetNames.Clear();
        try
        {
            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read))
            {
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration()
                    {
                        ConfigureDataTable = _ => new ExcelDataTableConfiguration()
                        {
                            UseHeaderRow = false
                        }
                    });

                    foreach (DataTable table in dataSet.Tables)
                    {
                        if (TableManager.IsFirstCharAlphabet(table.TableName[0]))
                        {
                            sheetNames.Add(table.TableName);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ExcelConverter] Load failed: {ex.Message}");
        }
    }

    private void ProcessSingleSheet(string sheetName)
    {
        try
        {
            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet();
                foreach (DataTable table in dataSet.Tables)
                {
                    if (table.TableName == sheetName && TableManager.GenerateDataClass(table))
                    {
                        TableManager.GenerateJsonFile(table);
                        Debug.Log($"[ExcelConverter] Generated: {sheetName}");
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ExcelConverter] Single sheet failed: {sheetName} - {ex.Message}");
        }
    }

    private void ConvertExcel()
    {
        List<string> processedSheets = new List<string>();

        try
        {
            using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                DataSet dataSet = reader.AsDataSet();

                foreach (DataTable table in dataSet.Tables)
                {
                    if (TableManager.IsFirstCharAlphabet(table.TableName[0]))
                    {
                        if (TableManager.GenerateDataClass(table))
                        {
                            processedSheets.Add(table.TableName);
                            TableManager.GenerateJsonFile(table);
                        }
                    }
                }
            }

            TableManager.GenerateTablesClass(processedSheets);
            AssetDatabase.Refresh();
            Debug.Log($"[ExcelConverter] Completed: {processedSheets.Count} sheets");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ExcelConverter] Convert failed: {ex.Message}");
        }
    }
}