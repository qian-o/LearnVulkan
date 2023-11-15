using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace SceneRendering.Vulkan;

public unsafe class VkBuffer : VkObject
{
    public readonly ulong Size;

    public readonly Buffer Buffer;

    public readonly DeviceMemory BufferMemory;

    public VkBuffer(VkContext parent, ulong size, BufferUsageFlags usage, MemoryPropertyFlags properties) : base(parent)
    {
        Size = size;

        BufferCreateInfo createInfo = new()
        {
            SType = StructureType.BufferCreateInfo,
            Size = Size,
            Usage = usage,
            SharingMode = SharingMode.Exclusive
        };

        fixed (Buffer* buffer = &Buffer)
        {
            if (Vk.CreateBuffer(Context.Device, &createInfo, null, buffer) != Result.Success)
            {
                throw new Exception("创建缓冲区失败");
            }
        }

        MemoryRequirements memRequirements;
        Vk.GetBufferMemoryRequirements(Context.Device, Buffer, &memRequirements);

        MemoryAllocateInfo allocInfo = new()
        {
            SType = StructureType.MemoryAllocateInfo,
            AllocationSize = memRequirements.Size,
            MemoryTypeIndex = Context.FindMemoryType(memRequirements.MemoryTypeBits, properties)
        };

        fixed (DeviceMemory* bufferMemory = &BufferMemory)
        {
            if (Vk.AllocateMemory(Context.Device, &allocInfo, null, bufferMemory) != Result.Success)
            {
                throw new Exception("无法分配缓冲区内存！");
            }
        }

        Vk.BindBufferMemory(Context.Device, Buffer, BufferMemory, 0);
    }

    public void* MapMemory()
    {
        void* data;

        if (Vk.MapMemory(Context.Device, BufferMemory, 0, Size, 0, &data) != Result.Success)
        {
            throw new Exception("无法映射缓冲区内存！");
        }

        return data;
    }

    public void UnmapMemory()
    {
        Vk.UnmapMemory(Context.Device, BufferMemory);
    }

    protected override void Destroy()
    {
        Vk.DestroyBuffer(Context.Device, Buffer, null);

        Vk.FreeMemory(Context.Device, BufferMemory, null);
    }
}
