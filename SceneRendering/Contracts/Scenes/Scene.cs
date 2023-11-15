using SceneRendering.Tools;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace SceneRendering.Contracts.Scenes;

public abstract class Scene : IDisposable
{
    protected const float CameraSpeed = 4.0f;
    protected const float CameraSensitivity = 0.2f;

    protected readonly IWindow _window;
    protected readonly Camera _camera;

    protected IInputContext? inputContext;
    protected IMouse? mouse;
    protected IKeyboard? keyboard;

    protected bool firstMove = true;
    protected Vector2D<float> lastPos;

    protected Scene(IWindow window)
    {
        _window = window;

        _window.Load += Load;
        _window.Update += Update;
        _window.Render += Render;
        _window.FramebufferResize += FrameBufferResize;

        _camera = new()
        {
            Position = new Vector3D<float>(0.0f, 0.0f, 3.0f),
            Fov = 45.0f
        };
    }

    public void Run()
    {
        _window.Run();
    }

    protected virtual void Load()
    {
        inputContext = _window.CreateInput();

        mouse = inputContext.Mice[0];
        keyboard = inputContext.Keyboards[0];
    }

    protected virtual void Update(double delta)
    {
        if (mouse!.IsButtonPressed(MouseButton.Middle))
        {
            Vector2D<float> vector = new(mouse.Position.X, mouse.Position.Y);

            if (firstMove)
            {
                lastPos = vector;

                firstMove = false;
            }
            else
            {
                float deltaX = vector.X - lastPos.X;
                float deltaY = vector.Y - lastPos.Y;

                _camera.Yaw += deltaX * CameraSensitivity;
                _camera.Pitch += -deltaY * CameraSensitivity;

                lastPos = vector;
            }
        }
        else
        {
            firstMove = true;
        }

        if (keyboard!.IsKeyPressed(Key.W))
        {
            _camera.Position += _camera.Front * CameraSpeed * (float)delta;
        }

        if (keyboard.IsKeyPressed(Key.A))
        {
            _camera.Position -= _camera.Right * CameraSpeed * (float)delta;
        }

        if (keyboard.IsKeyPressed(Key.S))
        {
            _camera.Position -= _camera.Front * CameraSpeed * (float)delta;
        }

        if (keyboard.IsKeyPressed(Key.D))
        {
            _camera.Position += _camera.Right * CameraSpeed * (float)delta;
        }

        if (keyboard.IsKeyPressed(Key.Q))
        {
            _camera.Position -= _camera.Up * CameraSpeed * (float)delta;
        }

        if (keyboard.IsKeyPressed(Key.E))
        {
            _camera.Position += _camera.Up * CameraSpeed * (float)delta;
        }

        _camera.Width = _window.Size.X;
        _camera.Height = _window.Size.Y;
    }

    protected abstract void Render(double delta);

    protected abstract void FrameBufferResize(Vector2D<int> size);

    public abstract void Dispose();
}
