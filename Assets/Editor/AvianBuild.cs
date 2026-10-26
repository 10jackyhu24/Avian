using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class AvianBuild {
    [MenuItem("Avian/Build server and Windows client")]
    public static void BuildBoth() {
        BuildServer();
        BuildClient();
    }

    public static void BuildServer() {
        Build("Assets/Scenes/GameRoon_Server.unity", "Builds/LinuxServer/AvianServer.x86_64",
            BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server);
    }

    public static void BuildClient() {
        Build("Assets/Scenes/SampleScene.unity", "Builds/WindowsClient/Avian.exe",
            BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player);
        string config = Path.Combine(Directory.GetCurrentDirectory(), "network-config.json");
        if (File.Exists(config)) {
            File.Copy(config, "Builds/WindowsClient/network-config.json", true);
        }
    }

    private static void Build(string scene, string output, BuildTarget target, StandaloneBuildSubtarget subtarget) {
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { scene },
            locationPathName = output,
            target = target,
            subtarget = (int)subtarget,
            options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) {
            throw new Exception($"[AvianBuild] {target}/{subtarget} failed: {report.summary.result}");
        }
        Debug.Log($"[AvianBuild] Built {output}, bytes={report.summary.totalSize}");
    }
}
