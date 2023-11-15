using SceneRendering.Contracts.Scenes;
using SceneRendering.Vulkan;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;

namespace SceneRendering.Scenes;

public unsafe class Scene1(IWindow window) : Scene(window)
{
    private VkContext? context;
    private VkModel? vampire = null!;
    private VkModel? yousa = null!;

    protected override void Load()
    {
        base.Load();

        context = new VkContext(_window);
        context.RecordCommandBuffer += RecordCommandBuffer;

        vampire = new VkModel(context, "Assets/Models/Vampire/dancing_vampire.dae");
        yousa = new VkModel(context, "Assets/Models/大喜/模型/登门喜鹊泠鸢yousa-ver2.0/泠鸢yousa登门喜鹊153cm-Apose2.1完整版(2).pmx");
    }

    protected override void Update(double delta)
    {
        base.Update(delta);

        yousa!.Transform = Matrix4X4.CreateScale(new Vector3D<float>(0.1f)) * Matrix4X4.CreateTranslation(2.0f, 0.0f, 0.0f);
    }

    protected override void Render(double delta)
    {
        context!.DrawFrame(delta);
    }

    protected override void FrameBufferResize(Vector2D<int> size)
    {
        context!.RecreateSwapChain();
    }

    private void RecordCommandBuffer(Vk vk, CommandBuffer commandBuffer, double delta)
    {
        vampire!.Record(commandBuffer, _camera);
        yousa!.Record(commandBuffer, _camera);
    }

    public override void Dispose()
    {
        vampire?.Dispose();
        yousa?.Dispose();

        context?.Dispose();

        GC.SuppressFinalize(this);
    }
}
