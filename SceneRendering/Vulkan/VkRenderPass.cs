using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkRenderPass : VkObject
{
    public readonly RenderPass RenderPass;

    public VkRenderPass(VkContext parent) : base(parent)
    {
        SwapChainSupportDetails swapChainSupportDetails = Context.SwapChainSupportDetails;

        SurfaceFormatKHR surfaceFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat();

        AttachmentDescription colorAttachment = new()
        {
            Format = surfaceFormat.Format,
            Samples = Context.SampleCountFlags,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal
        };
    }

    protected override void Destroy()
    {
        Vk.DestroyRenderPass(Context.LogicalDevice, RenderPass, null);
    }
}
