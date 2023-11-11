using SceneRendering.Contracts.Scenes;
using SceneRendering.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace SceneRendering.Scenes;

public unsafe class Scene1 : Scene
{
    private readonly IWindow _window;

    private VkContext? context;

    public Scene1(IWindow window)
    {
        _window = window;
    }

    public override void Load()
    {
        context = new VkContext(_window);
    }

    public override void Update(double time)
    {
    }

    public override void Render(double time)
    {
    }

    public override void Resize(Vector2D<int> size)
    {
    }

    public override void Dispose()
    {
        context?.Dispose();

        GC.SuppressFinalize(this);
    }
}
