using SceneRendering.Contexts;
using SceneRendering.Contracts.Scenes;
using Silk.NET.Maths;

namespace SceneRendering.Scenes;

public unsafe class MainScene : IScene
{
    private VkContext _vkContext;

    public MainScene(VkContext vkContext)
    {
        _vkContext = vkContext;
    }

    public void Load()
    {
        _vkContext.CreateInstance();
    }

    public void Update(double time)
    {
    }

    public void Render(double time)
    {
    }

    public void Resize(Vector2D<int> size)
    {
    }

    public void Dispose()
    {
        _vkContext.Dispose();

        GC.SuppressFinalize(this);
    }
}
