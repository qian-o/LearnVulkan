using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkFrameBuffers : VkReuseObject
{
    public VkImage ColorImage = null!;

    public VkImage DepthImage = null!;

    public Framebuffer[] FrameBuffers = Array.Empty<Framebuffer>();

    public VkFrameBuffers(VkContext parent) : base(parent)
    {
    }

    protected override void Core()
    {
        SwapChainSupportDetails swapChainSupportDetails = Context.SwapChainSupportDetails;

        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent();

        Format colorFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat().Format;
        Format depthFormat = Context.FindDepthFormat();

        ColorImage = new VkImage(Context,
                                 extent.Width,
                                 extent.Height,
                                 1,
                                 Context.MsaaSamples,
                                 MemoryPropertyFlags.DeviceLocalBit,
                                 colorFormat,
                                 ImageLayout.Undefined,
                                 ImageTiling.Optimal,
                                 ImageUsageFlags.TransientAttachmentBit | ImageUsageFlags.ColorAttachmentBit,
                                 ImageAspectFlags.ColorBit);

        DepthImage = new VkImage(Context,
                                 extent.Width,
                                 extent.Height,
                                 1,
                                 Context.MsaaSamples,
                                 MemoryPropertyFlags.DeviceLocalBit,
                                 depthFormat,
                                 ImageLayout.Undefined,
                                 ImageTiling.Optimal,
                                 ImageUsageFlags.DepthStencilAttachmentBit,
                                 ImageAspectFlags.DepthBit);

        FrameBuffers = new Framebuffer[Context.SwapChainImages.Length];

        for (int i = 0; i < FrameBuffers.Length; i++)
        {
            ImageView[] attachments = new ImageView[]
            {
                ColorImage.ImageView,
                DepthImage.ImageView,
                Context.SwapChainImages[i].ImageView
            };

            FramebufferCreateInfo createInfo = new()
            {
                SType = StructureType.FramebufferCreateInfo,
                RenderPass = Context.RenderPass,
                AttachmentCount = (uint)attachments.Length,
                PAttachments = (ImageView*)Unsafe.AsPointer(ref attachments[0]),
                Width = extent.Width,
                Height = extent.Height,
                Layers = 1
            };

            fixed (Framebuffer* framebuffer = &FrameBuffers[i])
            {
                if (Vk.CreateFramebuffer(Context.Device, &createInfo, null, framebuffer) != Result.Success)
                {
                    throw new Exception("无法创建帧缓冲。");
                }
            }
        }
    }

    protected override void Destroy()
    {
        foreach (Framebuffer framebuffer in FrameBuffers)
        {
            Vk.DestroyFramebuffer(Context.Device, framebuffer, null);
        }

        DepthImage.Dispose();
        ColorImage.Dispose();
    }
}
