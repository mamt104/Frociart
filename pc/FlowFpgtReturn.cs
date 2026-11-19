using System;using System.Collections.Generic;
namespace Frociart {
 // Source text only. Exported helpers validate and snapshot a function result;
 // neither this provider nor imported templates execute code in the application.
 internal static class FlowFpgtReturn {
  internal static string Method(string language,int kind){return language=="csharp"?(kind==1?"Integers":kind==2?"Reals":kind==3?"Texts":"Logicals"):(kind==1?"integers":kind==2?"reals":kind==3?"texts":"logicals");}
  internal static IEnumerable<string> Code(string language,string name,string builtins){
   if(language=="python")return new[]{
    "import builtins as "+builtins,
    "def "+name+"(values,kind):",
    "    if not "+builtins+".isinstance(values,"+builtins+".list) or "+builtins+".len(values)>1024: raise "+builtins+".ValueError('Il risultato richiede un vettore di al massimo 1024 elementi.')",
    "    result=[]",
    "    for value in values:",
    "        if kind==1 or kind==2:",
    "            if "+builtins+".isinstance(value,"+builtins+".bool) or not "+builtins+".isinstance(value,("+builtins+".int,"+builtins+".float)): raise "+builtins+".ValueError('Il risultato vettoriale richiede elementi numerici.')",
    "            number="+builtins+".float(value)",
    "            if not (-"+builtins+".float('inf')<number<"+builtins+".float('inf')): raise "+builtins+".ValueError('Il risultato vettoriale richiede numeri finiti.')",
    "            if kind==1:",
    "                if not number.is_integer(): raise "+builtins+".ValueError('Il risultato vettoriale richiede numeri interi.')",
    "                value="+builtins+".int(number)",
    "            else: value=number",
    "        elif kind==3:",
    "            if not "+builtins+".isinstance(value,"+builtins+".str): raise "+builtins+".ValueError('Il risultato vettoriale richiede elementi di testo.')",
    "        elif kind==4:",
    "            if not "+builtins+".isinstance(value,"+builtins+".bool): raise "+builtins+".ValueError('Il risultato vettoriale richiede elementi logici.')",
    "        result.append(value)",
    "    return result"};
   if(language=="java")return new[]{
    "final class "+name+" {",
    "    static int size(Object values) { if(values==null || !values.getClass().isArray())throw new java.lang.IllegalArgumentException(\"Il risultato richiede un vettore.\"); int n=java.lang.reflect.Array.getLength(values);if(n>1024)throw new java.lang.IllegalArgumentException(\"Il risultato supera 1024 elementi.\");return n; }",
    "    static double number(Object value) { if(!(value instanceof java.lang.Number))throw new java.lang.IllegalArgumentException(\"Il risultato vettoriale richiede elementi numerici.\");double n=((java.lang.Number)value).doubleValue();if(java.lang.Double.isNaN(n)||java.lang.Double.isInfinite(n))throw new java.lang.IllegalArgumentException(\"Il risultato vettoriale richiede numeri finiti.\");return n; }",
    "    static int[] integers(Object values) { int[] result=new int[size(values)];for(int i=0;i<result.length;i++){double n=number(java.lang.reflect.Array.get(values,i));if(n!=java.lang.Math.rint(n)||n<-2147483648.0||n>2147483647.0)throw new java.lang.IllegalArgumentException(\"Il risultato vettoriale richiede numeri interi da -2147483648 a 2147483647.\");result[i]=(int)n;}return result; }",
    "    static double[] reals(Object values) { double[] result=new double[size(values)];for(int i=0;i<result.length;i++)result[i]=number(java.lang.reflect.Array.get(values,i));return result; }",
    "    static String[] texts(Object values) { String[] result=new String[size(values)];for(int i=0;i<result.length;i++){Object v=java.lang.reflect.Array.get(values,i);if(!(v instanceof java.lang.String))throw new java.lang.IllegalArgumentException(\"Il risultato vettoriale richiede elementi di testo.\");result[i]=(java.lang.String)v;}return result; }",
    "    static boolean[] logicals(Object values) { boolean[] result=new boolean[size(values)];for(int i=0;i<result.length;i++){Object v=java.lang.reflect.Array.get(values,i);if(!(v instanceof java.lang.Boolean))throw new java.lang.IllegalArgumentException(\"Il risultato vettoriale richiede elementi logici.\");result[i]=(java.lang.Boolean)v;}return result; }",
    "}"};
   if(language=="csharp")return new[]{
    "internal static class "+name+" {",
    "    static int Size(global::System.Array values) { if(values==null || values.Rank!=1 || values.Length>1024)throw new global::System.InvalidOperationException(\"Il risultato richiede un vettore di al massimo 1024 elementi.\");return values.Length; }",
    "    static double Number(object value) { if(!(value is sbyte || value is byte || value is short || value is ushort || value is int || value is uint || value is long || value is ulong || value is float || value is double || value is decimal))throw new global::System.InvalidOperationException(\"Il risultato vettoriale richiede elementi numerici.\");double n=global::System.Convert.ToDouble(value,global::System.Globalization.CultureInfo.InvariantCulture);if(double.IsNaN(n)||double.IsInfinity(n))throw new global::System.InvalidOperationException(\"Il risultato vettoriale richiede numeri finiti.\");return n; }",
    "    internal static int[] Integers(global::System.Array values) { var result=new int[Size(values)];for(int i=0;i<result.Length;i++){double n=Number(values.GetValue(i));if(n!=global::System.Math.Truncate(n)||n<-2147483648.0||n>2147483647.0)throw new global::System.InvalidOperationException(\"Il risultato vettoriale richiede numeri interi da -2147483648 a 2147483647.\");result[i]=(int)n;}return result; }",
    "    internal static double[] Reals(global::System.Array values) { var result=new double[Size(values)];for(int i=0;i<result.Length;i++)result[i]=Number(values.GetValue(i));return result; }",
    "    internal static string[] Texts(global::System.Array values) { var result=new string[Size(values)];for(int i=0;i<result.Length;i++){object v=values.GetValue(i);if(!(v is string))throw new global::System.InvalidOperationException(\"Il risultato vettoriale richiede elementi di testo.\");result[i]=(string)v;}return result; }",
    "    internal static bool[] Logicals(global::System.Array values) { var result=new bool[Size(values)];for(int i=0;i<result.Length;i++){object v=values.GetValue(i);if(!(v is bool))throw new global::System.InvalidOperationException(\"Il risultato vettoriale richiede elementi logici.\");result[i]=(bool)v;}return result; }",
    "}"};
   throw new ArgumentException("Linguaggio del risultato vettoriale non supportato.");
  }
 }
}
