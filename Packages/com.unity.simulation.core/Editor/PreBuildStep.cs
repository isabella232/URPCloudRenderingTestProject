#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;
using System.Linq;
using System.Diagnostics;

namespace Unity.Simulation
{
    [InitializeOnLoad]
    class PreBuildStep: IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

#pragma warning disable 414
        private static int maxElapsedTimeForImport = 60000;
#pragma warning restore 414

        private static GraphicsDeviceType[] _supportedGraphicsApis =
        {
            GraphicsDeviceType.OpenGLES2,
            GraphicsDeviceType.OpenGLES3,
            GraphicsDeviceType.OpenGLCore,
            GraphicsDeviceType.Vulkan
        };
        
        private static string[] _packagesToImport =
        {
#if UNITY_EDITOR_WIN
            "com.unity.toolchain.win-x86_64-linux-x86_64@0.1.9-preview",
#elif UNITY_EDITOR_OSX
            "com.unity.toolchain.macos-x86_64-linux-x86_64@0.1.15-preview"
#endif
            //"com.unity.sysroot.linux-x86_64@0.1.9-preview",
            //"com.unity.sysroot@0.1.11-preview"
        };

        static PreBuildStep()
        {
#if (UNITY_EDITOR_OSX || UNITY_EDITOR_WIN) && PLATFORM_CLOUD_RENDERING
            Log.I("Importing the toolchain packages");
            var timer = Stopwatch.StartNew();

            foreach (var package in _packagesToImport)
            {
                Log.I("Importing toolchain package: " + package);
                var req = UnityEditor.PackageManager.Client.Add(package);
                while (!req.IsCompleted)
                {
                    if (timer.ElapsedMilliseconds > maxElapsedTimeForImport)
                    {
                        timer.Stop();
                        Log.E("Failed to import the toolchain. Cannot crosscompile, quitting..");
                        EditorApplication.Exit(1);
                    }
                }

                Log.I("Done Importing toolchain packages.");
            }
#endif
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            CheckUSimRunCompatibility();
        }

        private void CheckUSimRunCompatibility()
        {
#if !PLATFORM_CLOUD_RENDERING
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneLinux64)
                Log.W("In order to run on Unity Simulation, you need to build a linux player");
            
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Vulkan)
                Log.W("In order to run with Vulkan in Unity Simulation, you need to build the player with CloudRendering Build target.");
#endif
            
            if (!_supportedGraphicsApis.Contains(SystemInfo.graphicsDeviceType))
                Log.W("The current GraphicsAPI is not supported in Unity Simulation.");
            
        }
    }
}
#endif