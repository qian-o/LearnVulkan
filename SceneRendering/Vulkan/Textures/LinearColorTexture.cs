using Silk.NET.Vulkan;
using SkiaSharp;
using System.Drawing;

namespace SceneRendering.Vulkan.Textures;

public class LinearColorTexture(VkContext context, Color[] colors, PointF begin, PointF end) : Texture(context)
{
    public override unsafe void* GetMappedData(out uint texWidth, out uint texHeight, out int texChannels, out Format format)
    {
        texWidth = 1024;
        texHeight = 1024;
        texChannels = 4;
        format = Format.R8G8B8A8Srgb;

        byte[] bytes = new byte[1024 * 1024 * 4];

        fixed (byte* ptr = bytes)
        {
            using SKSurface surface = SKSurface.Create(new SKImageInfo(1024, 1024, SKColorType.Rgba8888), (nint)ptr);

            using SKPaint paint = new()
            {
                IsAntialias = true,
                IsDither = true,
                FilterQuality = SKFilterQuality.High,
                Shader = SKShader.CreateLinearGradient(new SKPoint(begin.X * 1024, begin.Y * 1024), new SKPoint(end.X * 1024, end.Y * 1024), colors.Select(c => new SKColor(c.R, c.G, c.B, c.A)).ToArray(), null, SKShaderTileMode.Repeat)
            };
            surface.Canvas.DrawRect(0, 0, 1024, 1024, paint);

            return ptr;
        }
    }
}
