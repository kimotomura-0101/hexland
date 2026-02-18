using UnityEditor;

public class WebGLBuilder
{
    [MenuItem("Build/WebGL")]
    public static void Build()
    {
        var scenes = new string[]
        {
            "Assets/Scenes/SampleScene.unity"
        };

        BuildPipeline.BuildPlayer(scenes, "WebGLBuild", BuildTarget.WebGL, BuildOptions.None);
    }
}
