using Silk.NET.Vulkan;

namespace SceneRendering.Vulkan;

public unsafe class VkSurface : VkContextEntity
{
    public readonly SurfaceKHR Surface;

    public VkSurface(VkContext parent) : base(parent)
    {
        Surface = Window.VkSurface!.Create<AllocationCallbacks>(Context.Instance.ToHandle(), null).ToSurface();
    }

    protected override void Destroy()
    {
        Context.KhrSurface.DestroySurface(Context.Instance, Surface, null);
    }
}
