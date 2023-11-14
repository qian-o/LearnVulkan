using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace SceneRendering.Contracts.Scenes;

public abstract class Scene : IDisposable
{
    protected readonly IWindow _window;

    protected Scene(IWindow window)
    {
        _window = window;

        _window.Load += Load;
        _window.Update += Update;
        _window.Render += Render;
        _window.FramebufferResize += FrameBufferResize;
    }

    public void Run()
    {
        _window.Run();
    }

    public abstract void Dispose();

    protected abstract void Load();

    protected abstract void Update(double delta);

    protected abstract void Render(double delta);

    protected abstract void FrameBufferResize(Vector2D<int> size);
}
