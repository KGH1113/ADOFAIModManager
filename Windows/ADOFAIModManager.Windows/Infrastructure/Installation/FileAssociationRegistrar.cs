using Microsoft.Windows.AppLifecycle;

namespace ADOFAIModManager.Windows.Infrastructure.Installation;

internal static class FileAssociationRegistrar
{
    public static void Register()
    {
        if (!File.Exists(UserInstallation.InstalledExePath)) return;
        try
        {
            ActivationRegistrationManager.RegisterForFileTypeActivation(
                [".zip"], string.Empty, "ADOFAI Mod Manager로 모드 설치", ["open"],
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
