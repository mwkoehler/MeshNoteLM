using System;
using Microsoft.Extensions.DependencyInjection;

namespace MeshNoteLM.Services;

/// <summary>
/// Utility class for creating service instances with dependency injection support
/// </summary>
public static class ActivatorUtilities
{
    /// <summary>
    /// Creates an instance of a type using dependency injection
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <param name="type">The type to instantiate</param>
    /// <returns>An instance of the specified type</returns>
    public static object CreateInstance(IServiceProvider serviceProvider, Type type)
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));
        if (type == null)
            throw new ArgumentNullException(nameof(type));

        return serviceProvider.GetRequiredService(type);
    }

    /// <summary>
    /// Creates an instance of type T using dependency injection
    /// </summary>
    /// <typeparam name="T">The type to instantiate</typeparam>
    /// <param name="serviceProvider">The service provider for dependency injection</param>
    /// <returns>An instance of type T</returns>
    public static T CreateInstance<T>(IServiceProvider serviceProvider) where T : class
    {
        if (serviceProvider == null)
            throw new ArgumentNullException(nameof(serviceProvider));

        return serviceProvider.GetRequiredService<T>();
    }
}
