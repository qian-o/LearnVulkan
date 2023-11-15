using SceneRendering.Vulkan.Structs;
using SceneRendering.Vulkan.Textures;
using Silk.NET.Assimp;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Buffer = System.Buffer;

namespace SceneRendering.Vulkan;

public unsafe class VkMesh : VkObject
{
    private static readonly Dictionary<string, Tex> Cache = [];

    private readonly Tex _diffuse;
    private readonly Tex _specular;
    private readonly VkBuffer _vertexBuffer;
    private readonly VkBuffer _indexBuffer;
    private readonly uint _indexCount;

    public VkMesh(VkContext parent, string directory, Assimp assimp, Scene* scene, Mesh* mesh) : base(parent)
    {
        (Vertex[] vertices, uint[] indices, _diffuse, _specular) = ParsingMesh(directory, assimp, scene, mesh);

        _vertexBuffer = CreateVertexBuffer(vertices);
        _indexBuffer = CreateIndexBuffer(indices);
        _indexCount = (uint)indices.Length;
    }

    public Tex Diffuse => _diffuse;

    public Tex Specular => _specular;

    public VkBuffer VertexBuffer => _vertexBuffer;

    public VkBuffer IndexBuffer => _indexBuffer;

    public uint IndexCount => _indexCount;

    private (Vertex[] vertices, uint[] indices, Tex Diffuse, Tex Specular) ParsingMesh(string directory, Assimp assimp, Scene* scene, Mesh* mesh)
    {
        Vertex[] vertices = new Vertex[mesh->MNumVertices];
        List<uint> indices = [];
        Tex? diffuse = null;
        Tex? specular = null;

        for (int i = 0; i < mesh->MNumVertices; i++)
        {
            Vertex vertex = new()
            {
                Position = mesh->MVertices[i].ToGeneric(),
                Normal = mesh->MNormals[i].ToGeneric()
            };

            if (mesh->MTextureCoords[0] != null)
            {
                vertex.TexCoord = new Vector2D<float>(mesh->MTextureCoords[0][i].X, mesh->MTextureCoords[0][i].Y);
            }

            vertices[i] = vertex;
        }

        for (int i = 0; i < mesh->MNumFaces; i++)
        {
            Face face = mesh->MFaces[i];

            for (int j = 0; j < face.MNumIndices; j++)
            {
                indices.Add(face.MIndices[j]);
            }
        }

        if (mesh->MMaterialIndex >= 0)
        {
            Material* material = scene->MMaterials[mesh->MMaterialIndex];

            foreach (ImageTex texture in LoadMaterialTextures(directory, assimp, material, TextureType.Diffuse))
            {
                diffuse = texture;
            }

            foreach (ImageTex texture in LoadMaterialTextures(directory, assimp, material, TextureType.Specular))
            {
                specular = texture;
            }
        }

        diffuse ??= new LinearColorTex(Context, [Color.Blue, Color.Red], new PointF(0.0f, 0.0f), new PointF(1.0f, 1.0f));

        specular ??= new SolidColorTex(Context, Color.Black);

        return (vertices, indices.ToArray(), diffuse, specular);
    }

    private List<ImageTex> LoadMaterialTextures(string directory, Assimp assimp, Material* mat, TextureType type)
    {
        List<ImageTex> materialTextures = [];

        uint textureCount = assimp.GetMaterialTextureCount(mat, type);
        for (uint i = 0; i < textureCount; i++)
        {
            AssimpString path;
            assimp.GetMaterialTexture(mat, type, i, &path, null, null, null, null, null, null);

            if (!Cache.TryGetValue(path.AsString, out Tex? texture))
            {
                texture = new ImageTex(Context, Path.Combine(directory, path.AsString));

                Cache.Add(path.AsString, texture);
            }

            materialTextures.Add((ImageTex)texture);
        }

        return materialTextures;
    }

    private VkBuffer CreateVertexBuffer(Vertex[] vertices)
    {
        ulong size = (ulong)(vertices.Length * Marshal.SizeOf(vertices[0]));

        VkBuffer stagingBuffer = new(Context,
                                     size,
                                     BufferUsageFlags.TransferSrcBit,
                                     MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data = stagingBuffer.MapMemory();
        Buffer.MemoryCopy(Unsafe.AsPointer(ref vertices[0]), data, size, size);
        stagingBuffer.UnmapMemory();

        VkBuffer vertexBuffer = new(Context,
                                    size,
                                    BufferUsageFlags.TransferDstBit | BufferUsageFlags.VertexBufferBit,
                                    MemoryPropertyFlags.DeviceLocalBit);

        stagingBuffer.CopyBuffer(vertexBuffer, size);

        stagingBuffer.Dispose();

        return vertexBuffer;
    }

    private VkBuffer CreateIndexBuffer(uint[] indices)
    {
        ulong size = (ulong)(indices.Length * Marshal.SizeOf(indices[0]));

        VkBuffer stagingBuffer = new(Context,
                                     size,
                                     BufferUsageFlags.TransferSrcBit,
                                     MemoryPropertyFlags.HostVisibleBit | MemoryPropertyFlags.HostCoherentBit);

        void* data = stagingBuffer.MapMemory();
        Buffer.MemoryCopy(Unsafe.AsPointer(ref indices[0]), data, size, size);
        stagingBuffer.UnmapMemory();

        VkBuffer indexBuffer = new(Context,
                                   size,
                                   BufferUsageFlags.TransferDstBit | BufferUsageFlags.IndexBufferBit,
                                   MemoryPropertyFlags.DeviceLocalBit);

        stagingBuffer.CopyBuffer(indexBuffer, size);

        stagingBuffer.Dispose();

        return indexBuffer;
    }

    protected override void Destroy()
    {
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
        _specular.Dispose();
        _diffuse.Dispose();
    }
}
