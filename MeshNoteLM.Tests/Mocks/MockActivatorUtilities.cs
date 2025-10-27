using System;
using Microsoft.Extensions.DependencyInjection;

namespace MeshNoteLM.Tests.Mocks;

/// <summary>
/// Mock implementation of ActivatorUtilities for testing PluginManager
/// </summary>
public static class MockActivatorUtilities
{
    /// <summary>
    /// Creates an instance of a type using dependency injection
    /// </summary>
    public static object CreateInstance(IServiceProvider serviceProvider, Type type)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return ActivatorUtilities.CreateInstance(serviceProvider, type);
    }

    /// <summary>
    /// Creates an instance of a type using dependency injection
    /// </summary>
    public static T CreateInstance<T>(IServiceProvider serviceProvider) where T : class
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        return serviceProvider.GetRequiredService<T>();
    }
}
