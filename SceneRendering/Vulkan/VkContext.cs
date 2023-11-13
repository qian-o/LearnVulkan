using SceneRendering.Contracts.Vulkan;
using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace SceneRendering.Vulkan;

public unsafe class VkContext : VkDestroy
{
    private readonly VkInstance _vkInstance;
    private readonly VkSurface _vkSurface;
    private readonly VkPhysicalDevice _vkPhysicalDevice;
    private readonly VkLogicalDevice _vkLogicalDevice;
    private readonly VkSwapChain _vkSwapChain;
    private readonly VkRenderPass _vkRenderPass;

    public VkContext(IWindow window) : base(Vk.GetApi(), window)
    {
        _vkInstance = new VkInstance(this);
        _vkSurface = new VkSurface(this);
        _vkPhysicalDevice = new VkPhysicalDevice(this);
        _vkLogicalDevice = new VkLogicalDevice(this);
        _vkSwapChain = new VkSwapChain(this);
        _vkRenderPass = new VkRenderPass(this);
    }

    #region VkInstance
    public Instance Instance => _vkInstance.Instance;

    public ExtDebugUtils DebugUtils => _vkInstance.DebugUtils;

    public DebugUtilsMessengerEXT Messenger => _vkInstance.Messenger;

    public KhrSurface KhrSurface => _vkInstance.KhrSurface;
    #endregion

    #region VkSurface
    public SurfaceKHR Surface => _vkSurface.Surface;
    #endregion

    #region VkPhysicalDevice
    public PhysicalDevice PhysicalDevice => _vkPhysicalDevice.PhysicalDevice;

    public SampleCountFlags SampleCountFlags => _vkPhysicalDevice.SampleCountFlags;

    public QueueFamilyIndices QueueFamilyIndices => _vkPhysicalDevice.GetQueueFamilyIndices();

    public SwapChainSupportDetails SwapChainSupportDetails => _vkPhysicalDevice.GetSwapChainSupportDetails();
    #endregion

    #region VkLogicalDevice
    public Device LogicalDevice => _vkLogicalDevice.LogicalDevice;

    public Queue GraphicsQueue => _vkLogicalDevice.GraphicsQueue;

    public Queue PresentQueue => _vkLogicalDevice.PresentQueue;

    public KhrSwapchain KhrSwapchain => _vkLogicalDevice.KhrSwapchain;
    #endregion

    #region VkSwapChain
    public SwapchainKHR Swapchain => _vkSwapChain.Swapchain;

    public VkImage[] SwapChainImages => _vkSwapChain.SwapChainImages;
    #endregion

    #region VkRenderPass
    public RenderPass RenderPass => _vkRenderPass.RenderPass;
    #endregion

    /// <summary>
    /// 查找合适的内存类型。
    /// </summary>
    /// <param name="typeFilter">typeFilter</param>
    /// <param name="properties">properties</param>
    /// <returns></returns>
    public uint FindMemoryType(uint typeFilter, MemoryPropertyFlags properties)
    {
        PhysicalDeviceMemoryProperties memProperties;
        Vk.GetPhysicalDeviceMemoryProperties(PhysicalDevice, &memProperties);

        for (uint i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << (int)i)) != 0 && memProperties.MemoryTypes[(int)i].PropertyFlags.HasFlag(properties))
            {
                return i;
            }
        }

        throw new Exception("无法找到合适的内存类型！");
    }

    /// <summary>
    /// 查找合适的格式。
    /// </summary>
    /// <param name="candidates">candidates</param>
    /// <param name="tiling">tiling</param>
    /// <param name="features">features</param>
    /// <returns></returns>
    public Format FindSupportedFormat(Format[] candidates, ImageTiling tiling, FormatFeatureFlags features)
    {
        foreach (Format format in candidates)
        {
            FormatProperties props;
            Vk.GetPhysicalDeviceFormatProperties(PhysicalDevice, format, &props);

            if (tiling == ImageTiling.Linear && props.LinearTilingFeatures.HasFlag(features))
            {
                return format;
            }
            else if (tiling == ImageTiling.Optimal && props.OptimalTilingFeatures.HasFlag(features))
            {
                return format;
            }
        }

        throw new Exception("无法找到合适的格式！");
    }

    /// <summary>
    /// 查找合适的深度格式。
    /// </summary>
    /// <returns></returns>
    public Format FindDepthFormat() => FindSupportedFormat(new[] { Format.D32Sfloat, Format.D32SfloatS8Uint, Format.D24UnormS8Uint },
                                                           ImageTiling.Optimal,
                                                           FormatFeatureFlags.DepthStencilAttachmentBit);

    protected override void Destroy()
    {
        _vkSwapChain.Dispose();
        _vkLogicalDevice.Dispose();
        _vkPhysicalDevice.Dispose();
        _vkSurface.Dispose();
        _vkInstance.Dispose();
    }
}
