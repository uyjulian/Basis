using BasisSerializer.OdinSerializer;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class SettingsLoader : MonoBehaviour
{
    public string FilePath = Application.dataPath + "Settings.json";
    public SettingsStruct[] settings;
    public List<SettingsApply> Options = new List<SettingsApply>();
    public int SettingsCount;
    public async void Awake()
    {
        GenerateInitalData();
        await LoadSettings();
        RegisterSettings();
    }
    public void GenerateInitalData()
    {
    }
    public async Task LoadSettings()
    {
        byte[] bytes = await File.ReadAllBytesAsync(FilePath);
        settings = SerializationUtility.DeserializeValue<SettingsStruct[]>(bytes, DataFormat.JSON);
    }
    public async Task SaveSettings()
    {
        byte[] bytes = SerializationUtility.SerializeValue(settings, DataFormat.JSON);
        await File.WriteAllBytesAsync(FilePath, bytes);
    }
    public void RegisterSettings()
    {
        foreach (SettingsApply option in Options)
        {
            foreach (SettingsStruct Setting in settings)//double layer will rewrite this later to use events.
            {
                option.Setup(Setting);
            }
        }
    }
    [System.Serializable]
    abstract public class SettingsApply : MonoBehaviour
    {
        abstract public void Setup(SettingsStruct Option);
    }
}