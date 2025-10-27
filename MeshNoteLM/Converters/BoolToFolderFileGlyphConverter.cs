using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Globalization;

// File: Converters/BoolToFolderFileGlyphConverter.cs

namespace MeshNoteLM.Converters;

public sealed class BoolToFolderFileGlyphConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? "\uE8B7" /*folder*/ : "\uE8A5" /*page*/;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

