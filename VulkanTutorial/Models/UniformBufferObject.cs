﻿using Silk.NET.Maths;

namespace VulkanTutorial.Models;

public struct UniformBufferObject
{
    public Matrix4X4<float> Model;

    public Matrix4X4<float> View;

    public Matrix4X4<float> Projection;
}
