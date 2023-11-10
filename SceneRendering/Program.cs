using SceneRendering.Contexts;
using SceneRendering.Contracts.Scenes;
using SceneRendering.Scenes;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace SceneRendering;

internal class Program
{
    static void Main(string[] args)
    {
        _ = args;

        WindowOptions options = WindowOptions.DefaultVulkan;
        options.Size = new Vector2D<int>(800, 600);
        options.Title = "Scene Rendering";

        IWindow window = Window.Create(options);

        using IScene scene = new MainScene(new VkContext(window));

        window.Load += scene.Load;
        window.Update += scene.Update;
        window.Render += scene.Render;
        window.FramebufferResize += scene.Resize;

        window.Run();
    }
}
