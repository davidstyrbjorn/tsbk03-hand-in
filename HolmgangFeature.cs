using UnityEngine;
using UnityEngine.Rendering.Universal;

public class HolmgangFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class PassSettings
    {
        // Where/when the render pass should be injected during the rendering process.
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

        // Used for any potential down-sampling we will do in the pass.
        [Range(1, 4)] public int downsample = 1;

        // A variable that's specific to the use case of our pass.
    }

    // References to our pass and its settings.
    public HolmgangPass pass;
    public PassSettings passSettings = new();

    // Gets called every time serialization happens.
    // Gets called when you enable/disable the renderer feature.
    // Gets called when you change a property in the inspector of the renderer feature.
    public override void Create()
    {
        // Pass the settings as a parameter to the constructor of the pass.
        pass = new HolmgangPass(passSettings);
    }

    // Injects one or multiple render passes in the renderer.
    // Gets called when setting up the renderer, once per-camera.
    // Gets called every frame, once per-camera.
    // Will not be called if the renderer feature is disabled in the renderer inspector.
    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData _renderingData
    )
    {
        // Queue upp our HolmgangPass 
        renderer.EnqueuePass(pass);
    }
}