using SceneRendering.Contracts.Scenes;
using SceneRendering.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace SceneRendering.Scenes;

public unsafe class Scene1 : Scene
{
    private VkContext? context;

    public Scene1(IWindow window) : base(window)
    {
    }

    protected override void Load()
    {
        context = new VkContext(_window);
        context.RecordCommandBuffer += RecordCommandBuffer;
    }

    protected override void Update(double delta)
    {

    }

    protected override void Render(double delta)
    {
        context!.DrawFrame(delta);
    }

    protected override void FrameBufferResize(Vector2D<int> size)
    {
        context!.RecreateSwapChain();
    }

    public override void Dispose()
    {
        context?.Dispose();

        GC.SuppressFinalize(this);
    }

    private void RecordCommandBuffer(Vk vk, CommandBuffer commandBuffer, double delta)
    {

    }
}
