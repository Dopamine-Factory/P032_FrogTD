using Newtonsoft.Json;
using Newtonsoft.Json.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DopaminTable
{
    public class Table<T_Data>
    {
        public Dictionary<uint, T_Data> Data = new Dictionary<uint, T_Data>();

        public void Load(Action completeCallback)
        {
            Data.Clear();
            var handler = Addressables.LoadAssetsAsync<TextAsset>($"Tables/{this.GetType().Name}.json", (asset) =>
            {

                List<T_Data> dataList = JsonConvert.DeserializeObject<List<T_Data>>(asset.text);

                 Type tableDataType = Type.GetType($"DopaminTable.{this.GetType().Name}Data,DopaminTable");

                var property = tableDataType.GetProperty("id");

                foreach (var  data in dataList)
                {
                    Data.Add((uint)Convert.ChangeType(property.GetValue(data), typeof(uint)), data);
                }

                completeCallback?.Invoke();
            });

        }

        private void OnCompleteLoad(TextAsset asset)
        {

        }
    }
}