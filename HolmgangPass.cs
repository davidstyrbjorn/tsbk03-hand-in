using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using System.Collections.Generic;

public class HolmgangPass : ScriptableRenderPass
{
    // The profiler tag that will show up in the frame debugger.

    // We will store our pass settings in this variable.
    HolmgangFeature.PassSettings passSettings;

    RenderTargetIdentifier colorBuffer, temporaryBuffer;
    int temporaryBufferID = Shader.PropertyToID("_TemporaryBuffer");

    private Material _material;

    private bool _active = false;

    private System.Random rand = new System.Random(DateTime.Now.Second);

    // Caching shader location properties is a good idea
    static readonly Dictionary<string, int> PropertyLocations = new Dictionary<string, int>()
    {
        {"_ActivatedAt", Shader.PropertyToID("_ActivatedAt")},
        {"_CurrentTimestamp", Shader.PropertyToID("_CurrentTimestamp")},
        {"_RandomEffect", Shader.PropertyToID("_RandomEffect")},
        {"_RandomNumbers", Shader.PropertyToID("_RandomNumbers")},

        {"_EffectTime", Shader.PropertyToID("_EffectTime")},
        {"_FadeTime", Shader.PropertyToID("_FadeTime")},
        {"_SlideTime", Shader.PropertyToID("_SlideTime")},
    };

    const string ProfilerTag = "Holmgang Transition Shader";
    Texture texture;

    // The constructor of the pass. Here you can set any material properties that do not need to be updated on a per-frame basis.
    public HolmgangPass(HolmgangFeature.PassSettings passSettings)
    {
        this.passSettings = passSettings;

        // Set the render pass event.
        renderPassEvent = passSettings.renderPassEvent;

        if (_material == null) _material = CoreUtils.CreateEngineMaterial("Hidden/Transition");

        texture = Resources.Load<Texture>("transition_mask2");
        _material.SetTexture("_MaskTexture", texture);
        _material.SetInt("_EffectTime", 2);
        _material.SetInt("_FadeTime", 2);
        _material.SetInt("_SlideTime", 2);

    }

    // Gets called by the renderer before executing the pass.
    // If this method is not overriden, renderer will have a default behaviour (:
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        // We need the descriptor so we can copy the cameras color buffer description
        RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;

        // We don't use the depth buffer so no bits need to be wasted here
        descriptor.depthBufferBits = 0;

        // Get the colorBuffer (will be used and sent to our shader)
        colorBuffer = renderingData.cameraData.renderer.cameraColorTarget;

        // Create a temporary buffer we can write our results to
        cmd.GetTemporaryRT(temporaryBufferID, descriptor, FilterMode.Bilinear);
        temporaryBuffer = new RenderTargetIdentifier(temporaryBufferID);
    }

    // The actual execution of the pass. This is where custom rendering occurs. 
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!_active) return;
        // Grab a command buffer. 
        // We put the actual execution of the pass inside of a profiling scope (optional)
        CommandBuffer cmd = CommandBufferPool.Get();
        using (new ProfilingScope(cmd, new ProfilingSampler(ProfilerTag)))
        {
            // Blit from the color buffer to a temporary buffer and back. This is needed for a two-pass shader.
            Blit(cmd, colorBuffer, temporaryBuffer, _material, 0); // shader pass 0
            Blit(cmd, temporaryBuffer, colorBuffer, _material, 1); // shader pass 1
        }

        // Mark the current timestamp
        _material.SetFloat(PropertyLocations["_CurrentTimestamp"], Time.time);

        // Execute our blit commands and then release (change back and front buffer)
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // Called when the camera has finished rendering.
    // Here we release/cleanup any allocated resources that were created by this pass.
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        if (cmd == null) throw new ArgumentNullException("cmd");
        // Since we created a temporary render texture in OnCameraSetup, we need to release the memory here to avoid a leak.
        cmd.ReleaseTemporaryRT(temporaryBufferID);
    }

    // public static int currentEffet = 3;

    public void Activate()
    {
        _active = !_active;
        if (!_active) return;

        // Mark timestamp for shader to measure time passed
        _material.SetFloat(PropertyLocations["_ActivatedAt"], Time.time);

        // Upload random int to indicate effect choice
        var numberOfEffects = 7;
        var num = rand.Next(0, numberOfEffects);
        _material.SetInt(PropertyLocations["_RandomEffect"], num);
        // currentEffet++;

        // Upload 4 random floats to the shader
        float[] array = new float[4];
        for (int i = 0; i < 4; i++)
        {
            array[i] = (float)rand.NextDouble();
        }
        _material.SetFloatArray(PropertyLocations["_RandomNumbers"], array);
    }
}