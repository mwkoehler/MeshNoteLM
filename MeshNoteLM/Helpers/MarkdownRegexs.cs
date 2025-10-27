using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace MeshNoteLM.Helpers
{
    // Uses source-generated regex (best for AOT/MAUI)
    internal static partial class MarkdownRegexes
    {
        [GeneratedRegex(@"^\s*\d+\.\s+", RegexOptions.Compiled)]
        private static partial Regex NumberedListItemGeneratedRegex();

        public static Regex NumberedListItemRegex() =>
            NumberedListItemGeneratedRegex();
    }
}

