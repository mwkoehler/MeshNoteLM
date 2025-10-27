using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using System;

namespace MeshNoteLM.Helpers;

public static class ServiceHelper
{
    public static T? GetService<T>() => Current.GetService<T>();

    public static IServiceProvider Current =>
        Application.Current?.Handler?.MauiContext?.Services
        ?? throw new InvalidOperationException("MauiContext not available");
}
