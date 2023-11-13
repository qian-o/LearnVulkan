using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System.Runtime.CompilerServices;

namespace SceneRendering.Vulkan.Structs;

public readonly unsafe struct SwapChainSupportDetails
{
    private readonly VkContext _context;

    public readonly SurfaceCapabilitiesKHR Capabilities;

    public readonly SurfaceFormatKHR[] Formats;

    public readonly PresentModeKHR[] PresentModes;

    public SwapChainSupportDetails(VkContext context, PhysicalDevice physicalDevice)
    {
        _context = context;

        KhrSurface khrSurface = context.KhrSurface;
        SurfaceKHR surface = context.Surface;

        fixed (SurfaceCapabilitiesKHR* capabilities = &Capabilities)
        {
            khrSurface.GetPhysicalDeviceSurfaceCapabilities(physicalDevice, surface, capabilities);
        }

        uint formatCount;
        khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &formatCount, null);

        Formats = new SurfaceFormatKHR[formatCount];
        khrSurface.GetPhysicalDeviceSurfaceFormats(physicalDevice, surface, &formatCount, (SurfaceFormatKHR*)Unsafe.AsPointer(ref Formats[0]));

        uint presentModeCount;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, &presentModeCount, null);

        PresentModes = new PresentModeKHR[presentModeCount];
        khrSurface.GetPhysicalDeviceSurfacePresentModes(physicalDevice, surface, &presentModeCount, (PresentModeKHR*)Unsafe.AsPointer(ref PresentModes[0]));
    }

    public readonly bool IsAdequate => Formats.Length > 0 && PresentModes.Length > 0;

    /// <summary>
    /// 选择最佳的交换链格式。
    /// </summary>
    /// <returns></returns>
    public SurfaceFormatKHR ChooseSwapSurfaceFormat()
    {
        foreach (SurfaceFormatKHR availableFormat in Formats)
        {
            if (availableFormat.Format == Format.B8G8R8A8Srgb && availableFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return availableFormat;
            }
        }

        return Formats[0];
    }

    /// <summary>
    /// 选择最佳的交换链呈现模式。
    /// </summary>
    /// <returns></returns>
    public PresentModeKHR ChooseSwapPresentMode()
    {
        foreach (PresentModeKHR availablePresentMode in PresentModes)
        {
            if (availablePresentMode == PresentModeKHR.MailboxKhr)
            {
                return availablePresentMode;
            }
        }

        return PresentModeKHR.FifoKhr;
    }

    /// <summary>
    /// 选择最佳的交换范围。
    /// </summary>
    /// <returns></returns>
    public Extent2D ChooseSwapExtent()
    {
        if (Capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return Capabilities.CurrentExtent;
        }
        else
        {
            IWindow window = _context.Window;

            return new Extent2D
            {
                Width = (uint)Math.Clamp(window.FramebufferSize.X, (int)Capabilities.MinImageExtent.Width, (int)Capabilities.MaxImageExtent.Width),
                Height = (uint)Math.Clamp(window.FramebufferSize.Y, (int)Capabilities.MinImageExtent.Height, (int)Capabilities.MaxImageExtent.Height)
            };
        }
    }

    /// <summary>
    /// 获取最佳的图像数量。
    /// </summary>
    /// <returns></returns>
    public uint GetImageCount()
    {
        uint imageCount = Capabilities.MinImageCount + 1;
        if (Capabilities.MaxImageCount > 0 && imageCount > Capabilities.MaxImageCount)
        {
            imageCount = Capabilities.MaxImageCount;
        }

        return imageCount;
    }
}
