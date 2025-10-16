
    
using System;
using System.Diagnostics;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace AOULauncher.Tools;

public static class PermissionHelper
{
    public static bool GrantGameDirectoryAccess(string gamePath)
    {
        if (string.IsNullOrEmpty(gamePath)) return false;

        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return true;
            }

            DirectoryInfo dir = new DirectoryInfo(gamePath);
            DirectorySecurity security = dir.GetAccessControl();

            // Add full control for current user
            security.AddAccessRule(new FileSystemAccessRule(
                WindowsIdentity.GetCurrent().Name,
                FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            dir.SetAccessControl(security);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // If we don't have permission, use PowerShell with admin rights
            return UsePowerShellToTakeOwnership(gamePath);
        }
    }

    public static bool UsePowerShellToTakeOwnership(string path)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-Command \"Take-Ownership -Path '{path}'\"",
            Verb = "runas",
            UseShellExecute = true
        };

        try
        {
            Process.Start(psi)?.WaitForExit(10000);
            return true;
        }
        catch
        {
            return false;
        }
    }
}