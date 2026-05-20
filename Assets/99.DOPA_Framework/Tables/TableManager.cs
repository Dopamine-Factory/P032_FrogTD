using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;

public class TableManager : MonoBehaviour
{
    public static string TableJsonFilesPath = "05.TableData";
    public static string TablesPath = "99.DOPA_Framework/Tables";
    public static string TableFilesPath = "99.DOPA_Framework/Tables/Tables";

    public static string firstFieldName = "";
    public static string firstFieldType = "";


    public static bool GenerateDataClass(DataTable table)
    {
        firstFieldName = "";
        firstFieldType = "";

        StringBuilder sb = new StringBuilder();
        // 네임스페이스 추가
        sb.AppendLine("using System.Security.Cryptography;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine("using UnityEngine.Networking;");
        sb.AppendLine("using Newtonsoft.Json;");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using UnityEngine.AddressableAssets;");
        // 데이터 폼 클래스
        sb.AppendLine("[Serializable]");
        sb.AppendLine($"public struct {table.TableName}_form");
        sb.AppendLine("{");
        for (int col = 0; col < table.Columns.Count; col++)
        {
            string fieldName = table.Rows[1][col].ToString();
            string fieldType = table.Rows[2][col].ToString();


            if (string.IsNullOrEmpty(fieldName) || string.IsNullOrEmpty(fieldType))
                continue;

            if (IsFirstCharAlphabet(fieldName[0]) == false)
                continue;

            if (IsFirstCharAlphabet(fieldType[0]) == false)
                continue;


            if (firstFieldName == "") firstFieldName = fieldName;
            if (firstFieldType == "") firstFieldType = fieldType;

            sb.AppendLine($"    public {fieldType} {fieldName};");
        }
        sb.AppendLine("}");


        if (string.IsNullOrEmpty(firstFieldName) || string.IsNullOrEmpty(firstFieldType))
            return false;

        // 데이터 관리 클래스
        sb.AppendLine("[Serializable]");
        sb.AppendLine($"public class {table.TableName}Table : IDataManager<{firstFieldType}, {table.TableName}_form>");
        sb.AppendLine("{");
        sb.AppendLine($"    private List<{table.TableName}_form> _data = new();");
        sb.AppendLine($"    private Dictionary<{firstFieldType}, {table.TableName}_form> _dataDic = new();");
        sb.AppendLine("    public string TableName => \"" + table.TableName + "\";");

        // 암호화 관련 초기화
        sb.AppendLine(@"    private static byte[] _encryptionKey = 
            Convert.FromBase64String(""5/lEYNAr/m3rVNk5LKWHUsha+ssdLbHishaEkHsxilU="");");

        // 데이터 로드 메서드
        sb.AppendLine("    public async Task LoadDataAsync(Action<float> onProgress)");
        sb.AppendLine("    {");

        sb.AppendLine($"    TextAsset binAsset = await Addressables.LoadAssetAsync<TextAsset>(\"Tables/{table.TableName}.bytes\").Task;");

        sb.AppendLine("    byte[] encrypted = binAsset.bytes;");
        sb.AppendLine("    byte[] decrypted = CryptoUtility.Decrypt(binAsset.bytes, _encryptionKey);");
        sb.AppendLine("    string json = GZipCompression.Decompress(decrypted);");
        sb.AppendLine($"    _data = JsonConvert.DeserializeObject<List<{table.TableName}_form>>(json);");
        sb.AppendLine("    BuildDictionary();");

        sb.AppendLine("    onProgress?.Invoke(1);");
        sb.AppendLine("    }");

        // 딕셔너리 빌드 메서드
        sb.AppendLine("    private void BuildDictionary()");
        sb.AppendLine("    {");
        sb.AppendLine("        _dataDic.Clear();");
        sb.AppendLine("        foreach (var item in _data)");
        sb.AppendLine("        {");
        sb.AppendLine("            try");
        sb.AppendLine("            {");
        sb.AppendLine($"                _dataDic.Add(item.{firstFieldName}, item);");
        sb.AppendLine("            }");
        sb.AppendLine("            catch (ArgumentException)");
        sb.AppendLine("            {");
        sb.AppendLine($"                Debug.LogError($\"중복 키 발견: {table.TableName} - item : {firstFieldName} {{item.{firstFieldName}}}\");");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");

        sb.AppendLine($"    public {table.TableName}_form GetData({firstFieldType} key)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_dataDic.TryGetValue(key, out var value))");
        sb.AppendLine("            return value;");
        sb.AppendLine("");
        sb.AppendLine($"        Debug.LogWarning(${'"'} Data X : {"{k"}ey{"}"}{'"'});");
        sb.AppendLine($"        return default;");
        sb.AppendLine("    }");


        // 데이터 접근 메서드
        sb.AppendLine("    public bool TryGetData(" + firstFieldType + " key, out " + table.TableName + "_form value)");
        sb.AppendLine("    {");
        sb.AppendLine("        return _dataDic.TryGetValue(key, out value);");
        sb.AppendLine("    }");
        sb.AppendLine("    ");
        sb.AppendLine("    public Dictionary<" + firstFieldType + ", " + table.TableName + "_form> GetDatas() => _dataDic;");
        sb.AppendLine("}");

        // 파일 저장 로직
        string filePath = Path.Combine(Application.dataPath, TableFilesPath);
        EnsureOutputDirectoryExists(filePath);
        File.WriteAllText($"{filePath}/{table.TableName}Table.cs", sb.ToString());

        return true;
    }


    public static void GenerateTablesClass(List<string> sheetNames = null)
    {
        Debug.Log("GenerateTablesClass Start");


        StringBuilder sb = new StringBuilder();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using System;");
        sb.AppendLine("public static class Tables");
        sb.AppendLine("{");
        sb.AppendLine("    private static List<IDataManager> _managers = new List<IDataManager>();");
        sb.AppendLine("    private static readonly Dictionary<Type, IDataManager> _managerLookup = new();");
        sb.AppendLine("    static Tables()");
        sb.AppendLine("    {");

        if (sheetNames != null)
        {
            foreach (string name in sheetNames)
            {
                sb.AppendLine($"        Register(new {name}Table());");
            }
        }


        sb.AppendLine("    }");
        sb.AppendLine("    private static void Register(IDataManager manager)");
        sb.AppendLine("    {");
        sb.AppendLine("        _managers.Add(manager);");
        sb.AppendLine("        _managerLookup[manager.GetType()] = manager;");
        sb.AppendLine("    }");

        sb.AppendLine("    public static async Task LoadAllAsync(System.Action<float> onProgress)");
        sb.AppendLine("    {");
        sb.AppendLine("        int totalManagers        = _managers.Count;");
        sb.AppendLine("        int completedManagers    = 0;");
        sb.AppendLine("");
        sb.AppendLine("        foreach (var manager in _managers)");
        sb.AppendLine("        {");
        sb.AppendLine("            await manager.LoadDataAsync(progress =>");
        sb.AppendLine("            {");
        sb.AppendLine("                float totalProgress = (completedManagers + progress) / totalManagers;");
        sb.AppendLine("                onProgress?.Invoke(totalProgress);");
        sb.AppendLine("            });");
        sb.AppendLine("");
        sb.AppendLine("            completedManagers++;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");


        sb.AppendLine("    public static T GetTable<T>() where T : class, IDataManager");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_managerLookup.TryGetValue(typeof(T), out var manager))");
        sb.AppendLine("        {");
        sb.AppendLine("            return manager as T;");
        sb.AppendLine("        }");
        sb.AppendLine("    ");
        sb.AppendLine("        throw new InvalidOperationException($\"Fail : {typeof(T).Name}\");");
        sb.AppendLine("    }");
        sb.AppendLine("");
        sb.AppendLine("    public static bool GetTable<T>(out T table) where T : class, IDataManager");
        sb.AppendLine("    {");
        sb.AppendLine("        if (_managerLookup.TryGetValue(typeof(T), out var manager))");
        sb.AppendLine("        {");
        sb.AppendLine("            table = manager as T;");
        sb.AppendLine("    ");
        sb.AppendLine("            return true;");
        sb.AppendLine("        }");
        sb.AppendLine("    ");
        sb.AppendLine("        table = null;");
        sb.AppendLine("    ");
        sb.AppendLine("        return false;");


        sb.AppendLine("    }");

        sb.AppendLine("}");

        string filePath = Path.Combine(Application.dataPath, TablesPath);
        Debug.Log("Path.Combine : " + filePath);

        EnsureOutputDirectoryExists(filePath);

        File.WriteAllText($"{filePath}/Tables.cs", sb.ToString());
    }

    private static int headerRow = 2; // 헤더 행 (1-based)
    private static int typeRow = 3;   // 데이터 타입 행 (1-based)
    private static int dataStartRow = 5; // 데이터 시작 행 (1-based)

    public static void GenerateJsonFile(DataTable table)
    {
        Debug.LogWarning($"{table.TableName} == table.Rows.Count : {table.Rows.Count}");

        if (table.Rows.Count == 0)
            return;

        int keyIdx = 1;
        int validIdx = 0;

        int[] validDataColIdxs = new int[50];

        string[] keys = table.Rows[headerRow - 1].ItemArray
                        .Cast<object>()
                        .Skip(1)
                        .Where(x =>
                        {
                            string s = x.ToString();
                            if (s.Length > 0)
                            {
                                if (IsFirstCharAlphabet(s[0]))
                                {
                                    validDataColIdxs[validIdx] = keyIdx;

                                    ++keyIdx;
                                    ++validIdx;

                                    return true;
                                }
                                else
                                {
                                    ++keyIdx;
                                }
                            }
                            else
                            {
                                ++keyIdx;
                            }

                            return false;
                        })
                        .Select(x => x.ToString())
                        .ToArray();

        // 2. 데이터 타입 추출
        string[] types = table.Rows[typeRow - 1].ItemArray
                        .Cast<object>()
                        .Skip(1)
                        .Where(x =>
                        {
                            string s = x.ToString();
                            if (s.Length > 0)
                                return IsFirstCharAlphabet(s[0]);

                            return false;
                        })
                        .Select(x => x.ToString())
                        .ToArray();

        // 3. 데이터 파싱
        var parsedData = ParseTableData(table, keys, types, validDataColIdxs);
        SaveJsonFilEncording(table.TableName, parsedData);
    }
    private static List<Dictionary<string, object>> ParseTableData(DataTable table, string[] keys, string[] types, int[] vaildDataColIdx)
    {
        List<Dictionary<string, object>> dataList = new List<Dictionary<string, object>>();

        for (int i = dataStartRow - 1; i < table.Rows.Count; i++)
        {
            DataRow row = table.Rows[i];
            Dictionary<string, object> entry = new Dictionary<string, object>();

            // 첫 번째 컬럼("Unnamed: 0") 건너뛰기
            for (int j = 0; j < row.ItemArray.Length; j++)
            {
                int col = vaildDataColIdx[j];

                string cellValue = row[col].ToString();
                if (string.IsNullOrEmpty(cellValue)) continue;

                string currentKey = keys[j]; // j=1부터 시작하므로 -1 적용
                object parsedValue = ParseCellValue(cellValue, types[j]);
                entry[currentKey] = parsedValue;
            }

            dataList.Add(entry);
        }

        return dataList;
    }

    private static object ParseCellValue(string value, string type)
    {
        Debug.LogWarning($"ParseCellValue Start type: {type}, value: {value}");

        try
        {
            switch (type.ToLower())
            {
                case "int":
                    return int.Parse(value);
                case "uint":
                    return uint.Parse(value);
                case "float":
                    return float.Parse(value);
                case "bool":
                    return bool.Parse(value);
                case "list<ushort>":
                    Debug.LogWarning($"List< ParseCellValue value: {value}");

                    return value.Trim('[', ']')
                                  .Split(',')
                                  .Select(ushort.Parse)
                                  .ToList();
                case "list<int>":
                    Debug.LogWarning($"List< ParseCellValue value: {value}");

                    return value.Trim('[', ']')
                                  .Split(',')
                                  .Select(int.Parse)
                                  .ToList();
                case "list<uint>":
                    Debug.LogWarning($"List< ParseCellValue value: {value}");

                    return value.Trim('[', ']')
                                  .Split(',')
                                  .Select(uint.Parse)
                                  .ToList();
                case "list<string>":
                    Debug.LogWarning($"List< ParseCellValue value: {value}");

                    return value.Trim('[', ']')
                                  .Split(',')
                                  .ToList();
                default:
                    return value;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"값 파싱 실패: {value} (타입: {type}) → {ex.Message}");
            return null;
        }
    }

    // SaveJsonFile 메서드 수정
    private static void SaveJsonFilEncording(string sheetName, List<Dictionary<string, object>> data)
    {
        string json = JsonConvert.SerializeObject(data);

        // 압축 → 암호화 → 파일 저장
        byte[] compressed = GZipCompression.Compress(json);
        byte[] key = GetEncryptionKey(); // 키 가져오는 메서드
        byte[] encrypted = CryptoUtility.Encrypt(compressed, key);


        string filePath = Path.Combine(
            Application.dataPath,
            TableJsonFilesPath,
            $"{sheetName}.bytes" // 확장자 변경
        );

        File.WriteAllBytes(filePath, encrypted);
    }


    public static void EnsureOutputDirectoryExists(string checkPath)
    {
        // Debug.Log("EnsureOutputDirectoryExists : " + Application.dataPath);
        // Debug.Log("EnsureOutputDirectoryExists : " + checkPath);

        if (!Directory.Exists(checkPath))
        {
            try
            {
                Directory.CreateDirectory(checkPath);
                // Debug.Log("Directory Created Successfully: " + checkPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to Create Directory: " + ex.Message);
            }
        }
    }

    private static byte[] GetEncryptionKey()
    {
        // 안전한 키 저장소에서 키 로드
        return Convert.FromBase64String("5/lEYNAr/m3rVNk5LKWHUsha+ssdLbHishaEkHsxilU="); // 256-bit 키
    }

    public static bool IsFirstCharAlphabet(char c)
    {
        int convertAscii = c;
        return ((convertAscii >= 65 && convertAscii <= 90) || (convertAscii >= 97 && convertAscii <= 122));
    }
}
