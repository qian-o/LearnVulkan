using SceneRendering.Contracts.Vulkan;
using SceneRendering.Vulkan.Structs;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;

namespace SceneRendering.Vulkan;

public class VkContext : VkEntity
{
    private readonly VkInstance _vkInstance;
    private readonly VkSurface _vkSurface;
    private readonly VkPhysicalDevice _vkPhysicalDevice;
    private readonly VkLogicalDevice _vkLogicalDevice;

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

    public VkContext(IWindow window) : base(Vk.GetApi(), window)
    {
        _vkInstance = new VkInstance(this);
        _vkSurface = new VkSurface(this);
        _vkPhysicalDevice = new VkPhysicalDevice(this);
        _vkLogicalDevice = new VkLogicalDevice(this);
    }

    protected override void Destroy()
    {
        _vkLogicalDevice.Dispose();
        _vkPhysicalDevice.Dispose();
        _vkSurface.Dispose();
        _vkInstance.Dispose();
    }
}
