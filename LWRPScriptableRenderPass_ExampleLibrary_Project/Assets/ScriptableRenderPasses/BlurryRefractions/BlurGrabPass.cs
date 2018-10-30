using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Experimental.Rendering.LightweightPipeline;
using UnityEngine.Rendering;

[ExecuteInEditMode]
[ImageEffectAllowedInSceneView]
public class BlurGrabPass : MonoBehaviour, IAfterOpaquePass
{

    const string k_BasicBlitShader = "Hidden/BasicBlit";
    private Material m_BasicBlitMaterial;

    const string k_BlurShader = "Hidden/Blur";
    private Material m_BlurMaterial;

    private Vector2 currentBlurAmount;
    public Vector2 m_BlurAmount;


    private GrabPassImpl m_grabPass;

    public void OnEnable()
    {
       m_BasicBlitMaterial = CoreUtils.CreateEngineMaterial(Shader.Find(k_BasicBlitShader));
       m_BlurMaterial = CoreUtils.CreateEngineMaterial(Shader.Find(k_BlurShader));
       currentBlurAmount = m_BlurAmount;
    }

    public ScriptableRenderPass GetPassToEnqueue(RenderTextureDescriptor baseDescriptor, RenderTargetHandle colorHandle, RenderTargetHandle depthHandle)
    {
        if (m_grabPass == null)
            m_grabPass = new GrabPassImpl(m_BlurMaterial, currentBlurAmount, m_BasicBlitMaterial, colorHandle);

        return m_grabPass;
    }

    void Update()
    {
        if(m_grabPass != null)
        {
            if(currentBlurAmount != m_BlurAmount)
            {
                currentBlurAmount = m_BlurAmount;
                m_grabPass.UpdateBlurAmount(currentBlurAmount);
            }
        } 
    }
}


public class GrabPassImpl : ScriptableRenderPass
{
    const string k_RenderGrabPassTag = "Blur Refraction Pass";

    private Material m_BlurMaterial;
    
    private Material m_BlitMaterial;

    private Vector2 m_BlurAmount;

    private RenderTextureDescriptor m_BaseDescriptor;
    private RenderTargetHandle m_ColorHandle;

    public GrabPassImpl(Material blurMaterial, Vector2 blurAmount, Material blitMaterial, RenderTargetHandle colorHandle)
    {
        m_BlurMaterial = blurMaterial;
        m_ColorHandle = colorHandle;
        m_BlitMaterial = blitMaterial;
        m_BlurAmount = blurAmount;

    }

    public void UpdateBlurAmount(Vector2 newBlurAmount)
    {
        m_BlurAmount = newBlurAmount;
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

            opaqueDesc.width /= 2;
            opaqueDesc.height /= 2;

            // get two smaller RTs
            int blurredID = Shader.PropertyToID("_BlurRT1");
            int blurredID2 = Shader.PropertyToID("_BlurRT2");
            buf.GetTemporaryRT(blurredID, opaqueDesc, FilterMode.Bilinear);
            buf.GetTemporaryRT(blurredID2, opaqueDesc, FilterMode.Bilinear);

            // downsample screen copy into smaller RT, release screen RT
            buf.Blit(screenCopyID, blurredID);
            buf.ReleaseTemporaryRT(screenCopyID);
            
            // horizontal blur
            buf.SetGlobalVector("offsets", new Vector4(m_BlurAmount.x / Screen.width, 0, 0, 0));
            buf.Blit(blurredID, blurredID2, m_BlurMaterial);
            // vertical blur
            buf.SetGlobalVector("offsets", new Vector4(0, m_BlurAmount.y / Screen.height, 0, 0));
            buf.Blit(blurredID2, blurredID, m_BlurMaterial);

            // horizontal blur
            buf.SetGlobalVector("offsets", new Vector4(m_BlurAmount.x * 2 / Screen.width, 0, 0, 0));
            buf.Blit(blurredID, blurredID2, m_BlurMaterial);
            // vertical blur
            buf.SetGlobalVector("offsets", new Vector4(0, m_BlurAmount.y * 2 / Screen.height, 0, 0));
            buf.Blit(blurredID2, blurredID, m_BlurMaterial);



            //Set Texture for Shader Graph
            buf.SetGlobalTexture("_GrabBlurTexture", blurredID);
        }

        context.ExecuteCommandBuffer(buf);
        CommandBufferPool.Release(buf);
    }
}
