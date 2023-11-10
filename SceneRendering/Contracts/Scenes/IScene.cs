using Silk.NET.Maths;

namespace SceneRendering.Contracts.Scenes;

public interface IScene : IDisposable
{
    public void Load();

    public void Update(double time);

    public void Render(double time);

    public void Resize(Vector2D<int> size);
}
