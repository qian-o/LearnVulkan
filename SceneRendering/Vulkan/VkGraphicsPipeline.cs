using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkGraphicsPipeline : VkObject
{
    public readonly Pipeline GraphicsPipeline;

    public VkGraphicsPipeline(VkContext parent) : base(parent)
    {
    }

    protected override void Destroy()
    {
        Vk.DestroyPipeline(Context.Device, GraphicsPipeline, null);
    }
}
