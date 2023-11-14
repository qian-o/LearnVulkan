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

        using Scene scene = new Scene1(window);

        scene.Run();
    }
}
