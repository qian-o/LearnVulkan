﻿using VulkanTutorial.Tutorials;

namespace VulkanTutorial;

internal class Program
{
    static void Main(string[] args)
    {
        _ = args;

        using MultisamplingApplication app = new();

        app.Run();
    }
}
