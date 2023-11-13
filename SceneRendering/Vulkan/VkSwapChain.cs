using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan;

public unsafe class VkSwapChain : VkObject
{
    public readonly SwapchainKHR Swapchain;

    public readonly VkImage[] SwapChainImages;

    public VkSwapChain(VkContext parent) : base(parent)
    {
        QueueFamilyIndices queueFamilyIndices = Context.QueueFamilyIndices;
        SwapChainSupportDetails swapChainSupportDetails = Context.SwapChainSupportDetails;

        SurfaceFormatKHR surfaceFormat = swapChainSupportDetails.ChooseSwapSurfaceFormat();
        PresentModeKHR presentMode = swapChainSupportDetails.ChooseSwapPresentMode();
        Extent2D extent = swapChainSupportDetails.ChooseSwapExtent();
        uint imageCount = swapChainSupportDetails.GetImageCount();

        SwapchainCreateInfoKHR createInfo = new()
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = Context.Surface,
            MinImageCount = imageCount,
            ImageFormat = surfaceFormat.Format,
            ImageColorSpace = surfaceFormat.ColorSpace,
            PresentMode = presentMode,
            ImageExtent = extent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            PreTransform = swapChainSupportDetails.Capabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            Clipped = Vk.True,
            OldSwapchain = default
        };

        if (queueFamilyIndices.GraphicsFamily != queueFamilyIndices.PresentFamily)
        {
            uint[] indices = queueFamilyIndices.ToArray();

            createInfo.ImageSharingMode = SharingMode.Concurrent;
            createInfo.QueueFamilyIndexCount = (uint)indices.Length;
            createInfo.PQueueFamilyIndices = (uint*)Unsafe.AsPointer(ref indices[0]);
        }
        else
        {
            createInfo.ImageSharingMode = SharingMode.Exclusive;
        }

        fixed (SwapchainKHR* swapchain = &Swapchain)
        {
            if (Context.KhrSwapchain.CreateSwapchain(Context.Device, &createInfo, null, swapchain) != Result.Success)
            {
                throw new Exception("无法创建交换链。");
            }
        }

        Context.KhrSwapchain.GetSwapchainImages(Context.Device, Swapchain, &imageCount, null);

        Image[] images = new Image[imageCount];
        fixed (Image* image = &images[0])
        {
            Context.KhrSwapchain.GetSwapchainImages(Context.Device, Swapchain, &imageCount, image);
        }

        SwapChainImages = new VkImage[imageCount];
        for (int i = 0; i < imageCount; i++)
        {
            SwapChainImages[i] = new VkImage(Context, 1, surfaceFormat.Format, ImageAspectFlags.ColorBit, images[i]);
        }
    }

    protected override void Destroy()
    {
        foreach (VkImage image in SwapChainImages)
        {
            image.Dispose();
        }

        Context.KhrSwapchain.DestroySwapchain(Context.Device, Swapchain, null);
    }
}
