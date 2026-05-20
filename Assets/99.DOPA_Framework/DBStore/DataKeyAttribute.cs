using System;

[AttributeUsage(AttributeTargets.Class)]
public class DataKeyAttribute : Attribute
{
    public string Key { get; }
    public DataKeyAttribute(string key) => Key = key;
}