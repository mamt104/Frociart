using System;using System.Collections.Generic;
namespace Frociart {
 // Source-only dimension checks for passive graph-array allocation.
 internal static class FlowFpgtStorage {
  internal static IEnumerable<string> Code(string language,string name,string builtins){
   if(language=="python")return Replace(new[]{
    "import builtins as __BUILTINS__",
    "def __NAME__(value):",
    "    if __BUILTINS__.isinstance(value,__BUILTINS__.bool) or not __BUILTINS__.isinstance(value,(__BUILTINS__.int,__BUILTINS__.float)) or not (0<=value<=1024) or value!=__BUILTINS__.int(value): raise __BUILTINS__.ValueError('La dimensione del vettore deve essere un intero da 0 a 1024.')",
    "    return __BUILTINS__.int(value)"},name,builtins);
   if(language=="java")return Replace(new[]{
    "final class __NAME__ {",
    "    static int length(Object value) {",
    "        if(!(value instanceof java.lang.Number))throw new java.lang.IllegalArgumentException(\"La dimensione del vettore richiede un numero.\");",
    "        double n=((java.lang.Number)value).doubleValue();",
    "        if(!(n>=0 && n<=1024) || n!=java.lang.Math.rint(n))throw new java.lang.IllegalArgumentException(\"La dimensione del vettore deve essere un intero da 0 a 1024.\");",
    "        return (int)n;",
    "    }",
    "}"},name,builtins);
   if(language=="csharp")return Replace(new[]{
    "internal static class __NAME__ {",
    "    internal static int Length(object value) {",
    "        if(!(value is byte || value is sbyte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal))throw new global::System.ArgumentException(\"La dimensione del vettore richiede un numero.\");",
    "        double n=global::System.Convert.ToDouble(value,global::System.Globalization.CultureInfo.InvariantCulture);",
    "        if(!(n>=0 && n<=1024) || n!=global::System.Math.Truncate(n))throw new global::System.ArgumentException(\"La dimensione del vettore deve essere un intero da 0 a 1024.\");",
    "        return (int)n;",
    "    }",
    "}"},name,builtins);
   return new string[0];
  }
  static IEnumerable<string> Replace(IEnumerable<string> rows,string name,string builtins){foreach(string row in rows)yield return row.Replace("__NAME__",name).Replace("__BUILTINS__",builtins??"");}
 }
}
