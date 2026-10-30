using System.Text.Json;
using Axiom.Core;
using Microsoft.Win32;

namespace Axiom.Infrastructure;

public sealed class FeaturePolicyStore(string stateRoot)
{
    public const string MachineKey = @"SOFTWARE\AxiomMarkdown\Policy";
    private string PreferencesPath => Path.Combine(stateRoot, "features.json");

    public FeatureDecision Evaluate(OptionalFeature feature)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using var machine = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using var policy = machine.OpenSubKey(MachineKey, writable: false);
                var machineDecision = Resolve(policy?.GetValue("Allow" + feature), true);
                if (!machineDecision.Allowed)
                {
                    return machineDecision;
                }
            }

            var preferences = ReadPreferences();
            return Resolve(null, preferences.GetValueOrDefault(feature.ToString(), true));
        }
        catch (Exception error)when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException)
        {
            return new(false, "Policy or preferences could not be read; optional operations are blocked.");
        }
    }

    public static FeatureDecision Resolve(object? machineValue
                                        , bool    userEnabled)
    {
        if (machineValue is not null && (machineValue is not int value || value != 1))
        {
            return new(false, "Disabled by organization policy.");
        }

        return userEnabled ? new(true, "Enabled; organization policy permits this feature.") : new(false
                                                                                                 , "Disabled in your feature settings.");
    }

    public void SetPreference(OptionalFeature feature
                            , bool            enabled)
    {
        var preferences = ReadPreferences();
        preferences[feature.ToString()] = enabled;
        Directory.CreateDirectory(stateRoot);
        var temporary = PreferencesPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary
                            , JsonSerializer.Serialize(preferences
                                                     , new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, PreferencesPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private Dictionary<string, bool> ReadPreferences()
    {
        return File.Exists(PreferencesPath) ? JsonSerializer.Deserialize<Dictionary<string, bool>>(File.ReadAllText(PreferencesPath)) ?? throw new JsonException("Invalid preferences.") : [];
    }
}
