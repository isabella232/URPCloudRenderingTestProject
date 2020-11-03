#if PLATFORM_CLOUD_RENDERING
using System;
using System.IO;

using UnityEngine;
using UnityEditor;

using Unity.Simulation;
using Unity.Simulation.Client.ZipUtility;

namespace Unity.Simulation.Client
{
    public static class Vulkanize
    {
        [MenuItem("Simulation/Vulkanize Build")]
        public static void VulkanizeBuild()
        {
            var path = EditorUtility.OpenFolderPanel("Locate Build to Vulkanize", "", "");
            if (Directory.Exists(path))
            {
                PostProcessBuild(path);
            }
        }

        public static void Build()
        {
            var path = $"/tmp/{new GUID().ToString()}";
            Directory.CreateDirectory(path);
            Project.BuildProject(path, "test", new string[]{"Assets/Scenes/samplescene.unity"}, BuildTarget.CloudRendering, BuildOptions.Development, compress:false);
        }

        // Note: everything in the buildDirectory will be included, so create a new directory when building.
        public static void PostProcessBuild(string buildLocation)
        {
            // find XXX.x86_64 with matching XXX_Data
            var executables = Directory.GetFiles(buildLocation, "*.x86_64");
            if (executables.Length != 1)
            {
                Debug.Log($"Cannot determine which executable to use at {buildLocation}");
                return;
            }

            var buildName = Path.GetFileNameWithoutExtension(executables[0]);

            var tmpDirectory = Path.Combine(buildLocation, "tmp");
            if (Directory.Exists(tmpDirectory))
            {
                Directory.Delete(tmpDirectory, true);
            }

            var files = Directory.GetFiles(buildLocation);
            var dirs  = Directory.GetDirectories(buildLocation);

            var buildDirectory = Path.Combine(tmpDirectory, "Build");

            Directory.CreateDirectory(tmpDirectory);
            Directory.CreateDirectory(buildDirectory);
            
            foreach (var f in files)
            {
                Debug.Log($"Moving {f} to {Path.Combine(buildDirectory, Path.GetFileName(f))}");
                File.Move(f, Path.Combine(buildDirectory, Path.GetFileName(f)));
            }

            foreach (var d in dirs)
            {
                Debug.Log($"Moving {d} to {Path.Combine(buildDirectory, Path.GetFileName(d))}");
                Directory.Move(d, Path.Combine(buildDirectory, Path.GetFileName(d)));
            }
        
            // Copy the libvulkan dll to the correct place.

            var libvulkanPath = Path.Combine(buildDirectory, "libvulkan.so.1"); 
            File.Copy("Packages/com.unity.simulation.client/libvulkan~/libvulkan.so.1", libvulkanPath);

            // write the wrapper bash script to set LD_LIBRARY_PATH

            File.WriteAllLines(Path.Combine(tmpDirectory, "Launch.x86_64"), new string[]
            {
                "export LD_LIBRARY_PATH=\"/unity_build/Build\"",
               $"echo \"Launching /unity_build/Build/{buildName}.x86_64\" > /tmp/Player.Log",
                "echo \"$@\" >> /tmp/Player.Log",
               $"chmod +x /unity_build/Build/{buildName}.x86_64 >> /tmp/Player.Log",
               $"chmod +x /unity_build/Build/libvulkan.so.1 >> /tmp/Player.Log",
               $"/unity_build/Build/{buildName}.x86_64 \"$@\"",
                "exit $?"
            });

            var launchDataDirectory = Path.Combine(tmpDirectory, "Launch_Data");
            Directory.CreateDirectory(launchDataDirectory);
            File.WriteAllLines(Path.Combine(launchDataDirectory, "placeholder.txt"), new string[]{"placeholder"});

            // compress tmp directory into Build.zip

            try
            {
                Zip.DirectoryContents(tmpDirectory, "Build");
            }
            catch (Exception e)
            {
                Debug.Log($"Exception zipping or moving build. {e.ToString()}");
            }

            // Move things back

            foreach (var f in files)
            {
                Debug.Log($"Moving {Path.Combine(buildDirectory, Path.GetFileName(f))} to {f}");
                File.Move(Path.Combine(buildDirectory, Path.GetFileName(f)), f);
            }

            foreach (var d in dirs)
            {
                Debug.Log($"Moving {Path.Combine(buildDirectory, Path.GetFileName(d))} to {d}");
                Directory.Move(Path.Combine(buildDirectory, Path.GetFileName(d)), d);
            }

            Directory.Delete(tmpDirectory, true);
        }
    }
}
#endif // PLATFORM_CLOUD_RENDERING
