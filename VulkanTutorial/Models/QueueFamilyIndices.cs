namespace VulkanTutorial.Models;

public struct QueueFamilyIndices
{
    public uint GraphicsFamily = uint.MaxValue;

    public uint PresentFamily = uint.MaxValue;

    public QueueFamilyIndices()
    {
    }

    public readonly bool IsComplete => GraphicsFamily != uint.MaxValue && PresentFamily != uint.MaxValue;

    public readonly uint[] ToArray()
    {
        if (GraphicsFamily == PresentFamily)
        {
            return new uint[] { GraphicsFamily };
        }
        else
        {
            return new uint[] { GraphicsFamily, PresentFamily };
        }
    }
}
