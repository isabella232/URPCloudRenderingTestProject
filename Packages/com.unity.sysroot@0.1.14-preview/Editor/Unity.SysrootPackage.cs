using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditorInternal;
using UnityEditor;
using Unity.Sysroot.NiceIO;
#if UNITY_STANDALONE_LINUX_API

#endif

namespace UnityEditor.Il2Cpp
{
    
    /// <summary>
    /// Describe a payload location and destination
    /// </summary>
    public struct PayloadDescriptor
    {
        /// <summary>
        /// Path of payload tarball
        /// </summary>
        public NPath path;
        /// <summary>
        /// Path directory where payload is to be installed
        /// </summary>
        public NPath dir;
    }

    /// <summary>
    /// Initialization status
    /// </summary>
    enum InitializationStatus
    {
        Uninitialized,
        Failed,
        Succeeded
    }

    /// <summary>
    /// Base class for sysroot and toolchain packages
    /// </summary>
    public class SysrootPackage
#if UNITY_STANDALONE_LINUX_API
        : Sysroot
#endif
    {
        private static bool IsLinuxIL2CPPPresent()
        {
            string targetDir = $"{BuildPipeline.GetPlaybackEngineDirectory(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64, BuildOptions.None)}/Variations/il2cpp";
            if (Directory.Exists(targetDir))
                return true;

            return false;
        }

        [InitializeOnLoadMethod]
        private static void IssueWarningIfLinuxIL2CPPNotPresent()
        {
            if (!IsLinuxIL2CPPPresent())
            {
                UnityEngine.Debug.LogWarning($"Linux Compiler Toolchain package(s) present, but required Linux-IL2CPP is missing");
            }
        }

        /// <summary>
        /// Name of package
        /// </summary>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
            string Name           => "com.unity.sysroot";

        /// <summary>
        /// Name of host platform (linux, win, macos)
        /// </summary>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
        string HostPlatform   => "";

        /// <summary>
        /// Name of host architecture
        /// </summary>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
            string HostArch       => "";

        /// <summary>
        /// Name of target platform (linux, win, macos)
        /// </summary>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
            string TargetPlatform => "";

        /// <summary>
        /// Name of target architecture
        /// </summary>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
            string TargetArch     => "";

        /// <summary>
        /// Supplies arguments to il2cpp.exe
        /// </summary>
        /// <returns>Next argument to il2cpp.exe</returns>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
            IEnumerable<string> GetIl2CppArguments() { return null; }

        /// <summary>
        /// Name of payload tarball
        /// </summary>
        protected string Payload => "payload.tar.7z";

        private List<PayloadDescriptor> _payloads = new List<PayloadDescriptor>();
        private InitializationStatus _initStatus = InitializationStatus.Uninitialized;

        /// <summary>
        /// Initialize package
        /// </summary>
        /// <returns>Success or failure of initialization</returns>
        public
#if UNITY_STANDALONE_LINUX_API
            override
#else
            virtual
#endif
            bool Initialize()
        {
            if (_initStatus != InitializationStatus.Uninitialized)
                return _initStatus == InitializationStatus.Succeeded;

            foreach (PayloadDescriptor pd in _payloads)
            {
                if (!Directory.Exists(pd.dir.ToString(SlashMode.Native)) && !InstallPayload(pd))
                {
                    UnityEngine.Debug.LogError($"Failed to initialize package: {Name}");
                    _initStatus = InitializationStatus.Failed;
                    return false;
                }
            }

            _initStatus = InitializationStatus.Succeeded;
            return true;
        }

        /// <summary>
        /// Compute path of payload tarball
        /// </summary>
        /// <param name="packageName">The name of the package</param>
        /// <returns>Path of payload tarball</returns>
        public NPath PayloadPath(string packageName)
        {
            return new NPath(Path.GetFullPath($"Packages/{packageName}")).Combine("data~/payload.tar.7z");
        }

        /// <summary>
        /// Register payload tarball and destination (installed location)
        /// </summary>
        /// <param name="packageName">The name of the package</param>
        /// <param name="payloadDir">The directory to install the payload in relative to sysroot cache</param>
        public void RegisterPayload(string packageName, string payloadDir)
        {
            _payloads.Add(new PayloadDescriptor{path = PayloadPath(packageName).ToString(SlashMode.Native), dir = PayloadInstallDirectory(payloadDir).ToString(SlashMode.Native)});
        }

        private bool PreconditionsAreMet()
        {
#if UNITY_EDITOR_WIN
            if (!CanCreateSymlinks()) {
                UnityEngine.Debug.LogError("The sysroot and toolchain packages require that Windows be configured to allow creation of symlinks without elevation of privilege");
                return false;
            }
#endif
            return true;
        }

        private bool RunShellCommand(string command, string workDir = null)
        {
            var p = new Process();
            p.StartInfo.UseShellExecute = false;
#if UNITY_EDITOR_WIN
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.FileName = "cmd";
            p.StartInfo.Arguments = $"/c \"{command}\"";
#else
            p.StartInfo.FileName = "/bin/sh";
            p.StartInfo.Arguments = $"-c \"{command}\"";
#endif
            p.StartInfo.WorkingDirectory = string.IsNullOrEmpty(workDir) ? Environment.CurrentDirectory : workDir;
            p.Start();
            p.WaitForExit();
            return p.ExitCode == 0;
        }

        private bool DecompressSysroot(NPath payload, NPath workDir)
        {
            if (!RunShellCommand(CommandCreateDirectory(workDir)))
                return false;

            if (!RunShellCommand(CommandUncompressTarball(payload, workDir), workDir.ToString(SlashMode.Native)))
            {
                RunShellCommand(CommandRemoveDirectoryTree(workDir));
                return false;
            }

            return PostDecompressActions(workDir);
        }

        private bool InstallPayload(PayloadDescriptor pd)
        {
            return DecompressSysroot(pd.path, pd.dir);
        }

        private string CommandCreateDirectory(NPath dir)
        {
                    _initStatus = InitializationStatus.Failed;
#if UNITY_EDITOR_WIN
             return $"mkdir {dir.InQuotes(SlashMode.Native)}";
#else
             return $"mkdir -p {dir.InQuotes()}";
#endif
        }

        private string Get7zPath()
        {
#if UNITY_EDITOR_WIN
            string command = "7z";
#else
            string command = "7za";
#endif
            return new NPath($"{EditorApplication.applicationContentsPath}/Tools/{command}").InQuotes(SlashMode.Native);
        }

        private string CommandUncompressTarball(NPath tarball, NPath destDir)
        {
#if UNITY_EDITOR_WIN
            return $"{Get7zPath()} x -y {tarball.InQuotes(SlashMode.Native)} -so | {Get7zPath()} x -y -aoa -ttar -si";
#else
            return $"{Get7zPath()} x -y {tarball.InQuotes()} -so | tar xf - --directory={destDir.InQuotes()}";
#endif
        }

        private string CommandRemoveDirectoryTree(NPath dir)
        {
#if UNITY_EDITOR_WIN
             return $"rd /s /q {dir.InQuotes(SlashMode.Native)}";
#else
             return $"rm -rf {dir.InQuotes()}";
#endif
        }

        private bool PostDecompressActions(NPath workDir)
        {
            return true;
        }

        private string UserAppDataFolder()
        {
            return 
#if UNITY_EDITOR_OSX
                $"{Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)}/Library/Unity";
#else
                $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/unity3d";
#endif
        }

        /// <summary>
        /// Returns path of installed payload
        /// </summary>
        /// <param name="payloadDir">The directory to install the payload in relative to sysroot cache</param>
        /// <returns>Fully-qualified path of install directory</returns>
        public NPath PayloadInstallDirectory(string payloadDir)
        {
            string cacheDir = Environment.GetEnvironmentVariable("UNITY_SYSROOT_CACHE");
            if (string.IsNullOrEmpty(cacheDir))
                cacheDir = $"{UserAppDataFolder()}/cache/sysroots";
            return new NPath($"{cacheDir}/{payloadDir}");
        }

#if UNITY_EDITOR_WIN
        private bool CanCreateSymlinks()
        {
		bool result = false;
		try {
            string tempDir = Path.GetFullPath(FileUtil.GetUniqueTempPathInProject());
            Directory.CreateDirectory(tempDir);
            string targetFile = $"{tempDir}\\targetfile";
            string linkFile = $"{tempDir}\\link-to-targetFile";
            FileStream fs = File.Create(targetFile);
            fs.Close();
            /*bool*/ result = Win32Native.CreateSymbolicLink(linkFile, targetFile, Win32Native.SymbolicLinkFlags.AllowUnprivilegedCreate);
            int error = Marshal.GetLastWin32Error();
            Directory.Delete(tempDir, recursive: true);
		}
        catch (Exception e)
		{
			UnityEngine.Debug.LogError($"{e.Message}");
		}
            return result;
        }
#endif
    }

#if UNITY_EDITOR_WIN
    static class Win32Native
    {
	[Flags]
	public enum SymbolicLinkFlags
	{
		File = 0,
		Directory = 1,
		AllowUnprivilegedCreate = 2
	}

	[DllImport("kernel32.dll", SetLastError=true)]
	public static extern bool CreateSymbolicLink(string linkFile, string targetFile, SymbolicLinkFlags flags);
    }
#endif
}
