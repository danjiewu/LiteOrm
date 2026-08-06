using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;

namespace LiteOrm.Generators
{
    /// <summary>
    /// 代码生成辅助方法
    /// </summary>
    internal static class CodeGenHelper
    {
        internal static string FQName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        internal static string SafeName(string name) => new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());

        internal static string ProviderFullNamespace => "LiteOrm.Generated";
    }
}
