using SceneRendering.Contracts.Scenes;
using SceneRendering.Contracts.Vulkan;
using SceneRendering.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace SceneRendering.Scenes;

public unsafe class Scene1 : Scene
{
    private readonly IWindow _window;

    private VkContext? context;
    private Vk? vk;
    private uint currentFrame = 0;
    private bool framebufferResized = true;

    public Scene1(IWindow window)
    {
        _window = window;
    }

    public override void Load()
    {
        context = new VkContext(_window);
        vk = context.Vk;
    }

    public override void Update(double time)
    {
    }

    public override void Render(double time)
    {
        if (!framebufferResized)
        {
            return;
        }

        currentFrame = (currentFrame + 1) % VkDestroy.MaxFramesInFlight;
    }

    public override void Resize(Vector2D<int> size)
    {
        context!.RecreateSwapChain();

        framebufferResized = true;
    }

    public override void Dispose()
    {
        context?.Dispose();

        GC.SuppressFinalize(this);
    }
}
