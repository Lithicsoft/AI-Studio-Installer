using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Lithicsoft_AI_Studio_Installer
{
    public static class DirectoryPermissionHelper
    {
        public static void SetFullControlPermissions(string folderPath)
        {
            try
            {
                DirectoryInfo directoryInfo = new DirectoryInfo(folderPath);

                DirectorySecurity directorySecurity = directoryInfo.GetAccessControl();

                SecurityIdentifier everyoneSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                FileSystemAccessRule accessRule = new FileSystemAccessRule(
                    everyoneSid,
                    FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                    PropagationFlags.None,
                    AccessControlType.Allow);

                directorySecurity.AddAccessRule(accessRule);

                directoryInfo.SetAccessControl(directorySecurity);
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"'{folderPath}': {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fail: {ex.Message}");
            }
        }
    }
}