using NUnit.Framework;
using System.Collections;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

public class UniversalProjectEditorTests
{
    private static UniversalRenderPipelineAsset currentAsset;

    [Test]
    public void GetCurrentAsset()
    {
        GetUniversalAsset();
    }

    [UnityTest]
    public IEnumerator GetDefaultRenderer()
    {
        GetUniversalAsset();

        yield return null;

        Assert.IsNotNull(currentAsset.scriptableRenderer, "Current ScriptableRenderer is null.");
    }

    //Utilities
    void GetUniversalAsset()
    {
        var renderpipelineAsset = GraphicsSettings.currentRenderPipeline;

        if(renderpipelineAsset == null)
            Assert.Fail("No Render Pipeline Asset assigned.");

        if (renderpipelineAsset.GetType() == typeof(UniversalRenderPipelineAsset))
        {
            currentAsset = renderpipelineAsset as UniversalRenderPipelineAsset;
        }
        else
        {
            Assert.Inconclusive("Project not setup for Universal RP.");
        }
    }
}
