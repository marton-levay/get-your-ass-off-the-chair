using System.Security;
using Microsoft.Win32;

namespace BreakReminder;

public class CallDetector
{
    private const string ConsentStorePath = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore";
    private const string NonPackagedKeyName = "NonPackaged";
    private static readonly string[] DeviceKeyNames = { "microphone", "webcam" };

    private readonly IReadOnlyList<string> ignoredAppNameFragments;

    public CallDetector(IReadOnlyList<string> ignoredAppNameFragments)
    {
        this.ignoredAppNameFragments = ignoredAppNameFragments;
    }

    public static CallDetector FromIgnoredAppsFile(string ignoredAppsFilePath)
    {
        try
        {
            if (!File.Exists(ignoredAppsFilePath))
            {
                return new CallDetector(Array.Empty<string>());
            }

            List<string> ignoredAppNameFragments = File.ReadAllLines(ignoredAppsFilePath)
                .Select(ignoredAppsFileLine => ignoredAppsFileLine.Trim())
                .Where(ignoredAppNameFragment => ignoredAppNameFragment.Length > 0)
                .ToList();
            return new CallDetector(ignoredAppNameFragments);
        }
        catch (Exception ignoredAppsFileException) when (ignoredAppsFileException is IOException or UnauthorizedAccessException)
        {
            return new CallDetector(Array.Empty<string>());
        }
    }

    public bool IsCallActive()
    {
        foreach (string deviceKeyName in DeviceKeyNames)
        {
            try
            {
                if (IsDeviceInUse(deviceKeyName))
                {
                    return true;
                }
            }
            catch (Exception registryException) when (registryException is SecurityException or IOException or UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    private bool IsDeviceInUse(string deviceKeyName)
    {
        using RegistryKey? deviceKey = Registry.CurrentUser.OpenSubKey($@"{ConsentStorePath}\{deviceKeyName}");
        if (deviceKey == null)
        {
            return false;
        }

        if (HasAppInUse(deviceKey))
        {
            return true;
        }

        using RegistryKey? nonPackagedKey = deviceKey.OpenSubKey(NonPackagedKeyName);
        return nonPackagedKey != null && HasAppInUse(nonPackagedKey);
    }

    private bool HasAppInUse(RegistryKey consentKey)
    {
        foreach (string appKeyName in consentKey.GetSubKeyNames())
        {
            if (appKeyName == NonPackagedKeyName || IsIgnoredApp(appKeyName))
            {
                continue;
            }

            using RegistryKey? appKey = consentKey.OpenSubKey(appKeyName);
            if (appKey != null && IsAppKeyInUse(appKey))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsIgnoredApp(string appKeyName)
    {
        return ignoredAppNameFragments.Any(ignoredAppNameFragment => appKeyName.Contains(ignoredAppNameFragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAppKeyInUse(RegistryKey appKey)
    {
        return appKey.GetValue("LastUsedTimeStart") is long lastUsedTimeStart && lastUsedTimeStart > 0
            && appKey.GetValue("LastUsedTimeStop") is long lastUsedTimeStop && lastUsedTimeStop == 0;
    }
}
