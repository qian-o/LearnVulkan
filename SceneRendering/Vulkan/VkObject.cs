using SceneRendering.Contracts.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace SceneRendering.Vulkan;

public abstract class VkObject : VkDestroy
{
    public static readonly string[] ValidationLayers = new string[]
    {
        "VK_LAYER_KHRONOS_validation"
    };

    public static readonly string[] DeviceExtensions = new string[]
    {
        KhrSwapchain.ExtensionName
    };

    protected VkObject(VkContext parent) : base(parent)
    {
        Context = parent;
    }

    public VkContext Context { get; }
}
