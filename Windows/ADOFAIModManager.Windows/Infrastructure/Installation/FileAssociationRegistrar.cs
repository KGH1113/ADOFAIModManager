using Microsoft.Windows.AppLifecycle;
using ADOFAIModManager.Windows.Application.Localization;

namespace ADOFAIModManager.Windows.Infrastructure.Installation;

internal static class FileAssociationRegistrar
{
    public static void Register(ILocalizationService localization)
    {
        if (!File.Exists(UserInstallation.InstalledExePath)) return;
        try
        {
            ActivationRegistrationManager.RegisterForFileTypeActivation(
                [".zip"], string.Empty, localization["Shell_FileAssociation"], ["open"],
                UserInstallation.InstalledExePath);
        }
        catch { }
    }

    public static void Unregister()
    {
        try
        {
            ActivationRegistrationManager.UnregisterForFileTypeActivation(
                [".zip"], UserInstallation.InstalledExePath);
        }
        catch { }
    }
}
