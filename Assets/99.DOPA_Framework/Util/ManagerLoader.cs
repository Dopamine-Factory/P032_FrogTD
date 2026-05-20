// using UnityEngine;
// using System;
// using System.Linq;
// using System.Reflection;

// public class ManagerLoader : MonoBehaviour
// {
//     private void Awake()
//     {
//         CreateDataManagers();
//     }

//     private void CreateDataManagers()
//     {
//         var dataTypes = Assembly.GetExecutingAssembly().GetTypes()
//             .Where(t => t.Namespace == "DynamicTables" && t.Name.EndsWith("Data"));

//         foreach (Type dataType in dataTypes)
//         {
//             Type managerType = typeof(DataManager<>).MakeGenericType(dataType);
//             GameObject managerObj = new GameObject($"{dataType.Name}Manager");
//             managerObj.AddComponent(managerType);
//         }
//     }
// }