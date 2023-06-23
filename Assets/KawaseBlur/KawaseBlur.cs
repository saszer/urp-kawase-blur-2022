using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class KawaseBlur : ScriptableRendererFeature
{
    [System.Serializable]
    public class KawaseBlurSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material blurMaterial = null;

        [Range(2, 15)]
        public int blurPasses = 1;

        [Range(1, 4)]
        public int downsample = 1;
        public bool copyToFramebuffer;
        public string targetName = "_blurTexture";
    }

    public KawaseBlurSettings settings = new KawaseBlurSettings();

    class CustomRenderPass : ScriptableRenderPass
    {
        public Material blurMaterial;
        public int passes;
        public int downsample;
        public bool copyToFramebuffer;
        public string targetName;

        public CustomRenderPass(Material blurMaterial, int passes, int downsample, bool copyToFramebuffer, string targetName)
        {
            this.blurMaterial = blurMaterial;
            this.passes = passes;
            this.downsample = downsample;
            this.copyToFramebuffer = copyToFramebuffer;
            this.targetName = targetName;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get("KawaseBlur");

            RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;

            int tmpId1 = Shader.PropertyToID("tmpBlurRT1");
            int tmpId2 = Shader.PropertyToID("tmpBlurRT2");
            var width = renderingData.cameraData.cameraTargetDescriptor.width / downsample;
            var height = renderingData.cameraData.cameraTargetDescriptor.height / downsample;

            cmd.GetTemporaryRT(tmpId1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
            cmd.GetTemporaryRT(tmpId2, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

            var tmpRT1 = new RenderTargetIdentifier(tmpId1);
            var tmpRT2 = new RenderTargetIdentifier(tmpId2);

            cmd.SetGlobalFloat("_offset", 1.5f);
            cmd.Blit(renderingData.cameraData.renderer.cameraColorTarget, tmpRT1, blurMaterial);

            for (var i = 1; i < passes - 1; i++)
            {
                cmd.SetGlobalFloat("_offset", 0.5f + i);
                cmd.Blit(tmpRT1, tmpRT2, blurMaterial);

                var rttmp = tmpRT1;
                tmpRT1 = tmpRT2;
                tmpRT2 = rttmp;
            }

            cmd.SetGlobalFloat("_offset", 0.5f + passes - 1f);
            if (copyToFramebuffer)
            {
                cmd.Blit(tmpRT1, renderingData.cameraData.renderer.cameraColorTarget, blurMaterial);
            }
            else
            {
                cmd.Blit(tmpRT1, tmpRT2, blurMaterial);
                cmd.SetGlobalTexture(targetName, tmpRT2);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }

    CustomRenderPass scriptablePass;

    public override void Create()
    {
        scriptablePass = new CustomRenderPass(settings.blurMaterial, settings.blurPasses, settings.downsample, settings.copyToFramebuffer, settings.targetName);
        scriptablePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(scriptablePass);
    }
}
