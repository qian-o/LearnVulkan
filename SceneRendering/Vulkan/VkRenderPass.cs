using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

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
            Samples = Context.MsaaSamples,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.ColorAttachmentOptimal
        };

        AttachmentDescription depthAttachment = new()
        {
            Format = Context.FindDepthFormat(),
            Samples = Context.MsaaSamples,
            LoadOp = AttachmentLoadOp.Clear,
            StoreOp = AttachmentStoreOp.DontCare,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentDescription colorAttachmentResolve = new()
        {
            Format = surfaceFormat.Format,
            Samples = SampleCountFlags.Count1Bit,
            LoadOp = AttachmentLoadOp.DontCare,
            StoreOp = AttachmentStoreOp.Store,
            StencilLoadOp = AttachmentLoadOp.DontCare,
            StencilStoreOp = AttachmentStoreOp.DontCare,
            InitialLayout = ImageLayout.Undefined,
            FinalLayout = ImageLayout.PresentSrcKhr
        };

        AttachmentReference colorAttachmentRef = new()
        {
            Attachment = 0,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        AttachmentReference depthAttachmentRef = new()
        {
            Attachment = 1,
            Layout = ImageLayout.DepthStencilAttachmentOptimal
        };

        AttachmentReference colorAttachmentResolveRef = new()
        {
            Attachment = 2,
            Layout = ImageLayout.ColorAttachmentOptimal
        };

        SubpassDescription subpass = new()
        {
            PipelineBindPoint = PipelineBindPoint.Graphics,
            ColorAttachmentCount = 1,
            PColorAttachments = &colorAttachmentRef,
            PDepthStencilAttachment = &depthAttachmentRef,
            PResolveAttachments = &colorAttachmentResolveRef
        };

        SubpassDependency dependency = new()
        {
            SrcSubpass = Vk.SubpassExternal,
            DstSubpass = 0,
            SrcStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            SrcAccessMask = 0,
            DstStageMask = PipelineStageFlags.ColorAttachmentOutputBit | PipelineStageFlags.EarlyFragmentTestsBit,
            DstAccessMask = AccessFlags.ColorAttachmentWriteBit | AccessFlags.DepthStencilAttachmentWriteBit
        };

        AttachmentDescription[] attachments = new AttachmentDescription[]
        {
            colorAttachment,
            depthAttachment,
            colorAttachmentResolve
        };

        RenderPassCreateInfo createInfo = new()
        {
            SType = StructureType.RenderPassCreateInfo,
            AttachmentCount = (uint)attachments.Length,
            PAttachments = (AttachmentDescription*)Unsafe.AsPointer(ref attachments[0]),
            SubpassCount = 1,
            PSubpasses = &subpass,
            DependencyCount = 1,
            PDependencies = &dependency
        };

        fixed (RenderPass* renderPass = &RenderPass)
        {
            if (Vk.CreateRenderPass(Context.Device, &createInfo, null, renderPass) != Result.Success)
            {
                throw new Exception("创建渲染通道失败。");
            }
        }
    }

    protected override void Destroy()
    {
        Vk.DestroyRenderPass(Context.Device, RenderPass, null);
    }
}
