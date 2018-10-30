using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.LightweightPipeline;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class ScreenEffectPass : MonoBehaviour, IAfterSkyboxPass
{

    private ScreenEffectPassImpl m_grabPass;

    public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle colorHandle, RenderTargetHandle depthHandle)
    {
        if (m_grabPass == null)
            m_grabPass = new ScreenEffectPassImpl(colorHandle);

        return m_grabPass;
    }

}


public class ScreenEffectPassImpl : ScriptableRenderPass
{
    const string k_RenderGrabPassTag = "Screen Effect Pass";


    private RenderTextureDescriptor m_BaseDescriptor;
    private RenderTargetHandle m_ColorHandle;

    public ScreenEffectPassImpl(RenderTargetHandle colorHandle)
    {
        m_ColorHandle = colorHandle;
    }

    public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
    {
        CommandBuffer buf = CommandBufferPool.Get(k_RenderGrabPassTag);

        using (new ProfilingSample(buf, k_RenderGrabPassTag))
        {
            
            // copy screen into temporary RT
            int screenCopyID = Shader.PropertyToID("_ScreenCopyTexture");
            RenderTextureDescriptor opaqueDesc = ScriptableRenderer.CreateRenderTextureDescriptor(ref renderingData.cameraData);
            buf.GetTemporaryRT(screenCopyID, opaqueDesc, FilterMode.Bilinear);
            buf.Blit(m_ColorHandle.Identifier(), screenCopyID);



            //Set Texture for Shader Graph
            buf.SetGlobalTexture("_GrabTexture", screenCopyID);
        }

        context.ExecuteCommandBuffer(buf);
        CommandBufferPool.Release(buf);
    }
}
