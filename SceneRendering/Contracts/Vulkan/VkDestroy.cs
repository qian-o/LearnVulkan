using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace SceneRendering.Contracts.Vulkan;

public abstract class VkDestroy : IDisposable
{
#if DEBUG
    public const bool EnableValidationLayers = true;
#else
    public const bool EnableValidationLayers = false;
#endif

    public const int MaxFramesInFlight = 2;

    protected VkDestroy(VkDestroy entity) : this(entity.Vk, entity.Window)
    {
    }

    protected VkDestroy(Vk vk, IWindow window)
    {
        Vk = vk;
        Window = window;
    }

    public Vk Vk { get; }

    public IWindow Window { get; }

    protected abstract void Destroy();

    public void Dispose()
    {
        Destroy();

        GC.SuppressFinalize(this);
    }
}
