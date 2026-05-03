using System;
using System.IO;
using UnityEngine;

[Serializable]
public enum InputBindingType
{
    MouseButton = 0,
    KeyCode = 1
}

[Serializable]
public struct InputBinding
{
    public InputBindingType bindingType;
    public int code;

    public static InputBinding MouseButton(int button)
    {
        return new InputBinding
        {
            bindingType = InputBindingType.MouseButton,
            code = Mathf.Max(0, button)
        };
    }

    public static InputBinding Key(KeyCode keyCode)
    {
        return new InputBinding
        {
            bindingType = InputBindingType.KeyCode,
            code = (int)keyCode
        };
    }

    public readonly bool GetButtonDown()
    {
        return bindingType == InputBindingType.MouseButton
            ? Input.GetMouseButtonDown(code)
            : Input.GetKeyDown((KeyCode)code);
    }

    public readonly bool GetButton()
    {
        return bindingType == InputBindingType.MouseButton
            ? Input.GetMouseButton(code)
            : Input.GetKey((KeyCode)code);
    }

    public readonly bool GetButtonUp()
    {
        return bindingType == InputBindingType.MouseButton
            ? Input.GetMouseButtonUp(code)
            : Input.GetKeyUp((KeyCode)code);
    }
}

[Serializable]
public class KeybindSettings
{
    public InputBinding selection = InputBinding.MouseButton(0);
    public InputBinding contextCommand = InputBinding.MouseButton(1);
}

[Serializable]
public class LocalSettingsData
{
    public KeybindSettings keybinds = new KeybindSettings();

    public static LocalSettingsData Default()
    {
        return new LocalSettingsData
        {
            keybinds = new KeybindSettings()
        };
    }
}

public static class LocalSettingsStore
{
    private const string FileName = "settings.json";
    private static LocalSettingsData _cached;

    public static LocalSettingsData Current
    {
        get
        {
            EnsureLoaded();
            return _cached;
        }
    }

    public static void Save()
    {
        EnsureLoaded();
        string path = GetSettingsPath();
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonUtility.ToJson(_cached, true);
        File.WriteAllText(path, json);
    }

    public static void Reload()
    {
        _cached = null;
        EnsureLoaded();
    }

    public static void ResetToDefaults()
    {
        _cached = LocalSettingsData.Default();
        Save();
    }

    private static void EnsureLoaded()
    {
        if (_cached != null)
        {
            EnsureDefaults(_cached);
            return;
        }

        string path = GetSettingsPath();
        if (!File.Exists(path))
        {
            _cached = LocalSettingsData.Default();
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            _cached = JsonUtility.FromJson<LocalSettingsData>(json) ?? LocalSettingsData.Default();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to load local settings from '{path}': {ex.Message}");
            _cached = LocalSettingsData.Default();
        }

        EnsureDefaults(_cached);
    }

    private static void EnsureDefaults(LocalSettingsData data)
    {
        if (data.keybinds == null)
        {
            data.keybinds = new KeybindSettings();
        }
    }

    private static string GetSettingsPath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }
}
