using Silk.NET.Maths;

namespace SceneRendering.Contracts.Scenes;

public abstract class Scene : IDisposable
{
    public abstract void Load();

    public abstract void Update(double time);

    public abstract void Render(double time);

    public abstract void Resize(Vector2D<int> size);

    public abstract void Dispose();
}
