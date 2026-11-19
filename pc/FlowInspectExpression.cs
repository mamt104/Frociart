using System;using System.Collections.Generic;using System.Text;
namespace Frociart {
 // One expression on a frozen inspection frame. No user functions, randomness, writes or IO.
 internal sealed class FlowInspectExpression {
  internal sealed class Result {
   internal readonly object Value;internal readonly string Preview,CopyText,Note;
   internal Result(object value,string preview,string copy,string note){Value=value;Preview=preview;CopyText=copy;Note=note;}
  }
  internal sealed class Context {
   internal readonly string FunctionName;internal readonly int Steps;internal readonly long FrameId;
   private readonly IDictionary<string,object> variables;private readonly bool eof;private readonly string eofError;
   internal Context(CallFrameSnapshot frame,int steps,Func<bool> readEof){if(frame==null)throw new ArgumentNullException("frame");FunctionName=frame.FunctionName;Steps=steps;FrameId=frame.FrameId;variables=new Dictionary<string,object>(frame.Variables,StringComparer.OrdinalIgnoreCase);try{if(readEof==null)throw new FlowException("Apri un file in lettura prima di usare EOF().");eof=readEof();}catch(FlowException ex){eofError=ex.Message;}}
   internal Result Evaluate(string text){return new FlowInspectExpression(text).Evaluate(variables,()=>{if(eofError!=null)throw new FlowException(eofError);return eof;});}
  }
  private readonly Expr expression;
  internal FlowInspectExpression(string text){
   expression=ExpressionLanguage.Parse(text);var pending=new Stack<Expr>();pending.Push(expression);
   while(pending.Count>0){var current=pending.Pop();var call=current as CallExpr;if(call!=null&&call.name=="random")throw new FlowException("La valutazione non usa numeri casuali. Leggi una variabile già calcolata dal programma.");foreach(var child in ExpressionMachine.ChildrenOf(current))pending.Push(child);}
  }
  internal Result Evaluate(IDictionary<string,object> variables,Func<bool> eof=null){var evaluation=new ExpressionMachine(expression,eof);evaluation.Advance(variables);return Present(evaluation.Result);}
  internal const int CopyLimit=262144,PreviewLimit=32768;
  internal static string Readable(Result value){return value.Value is string?Literal((string)value.Value,CopyLimit*6+2):value.CopyText??value.Preview;}
  private static void Append(StringBuilder b,string value,int limit){if(value.Length>limit-b.Length)throw new OverflowException();b.Append(value);}
  private static string Literal(string text,int limit){var b=new StringBuilder();Append(b,"\"",limit);for(int i=0;i<text.Length;i++){char c=text[i];if(c=='\\')Append(b,"\\\\",limit);else if(c=='\"')Append(b,"\\\"",limit);else if(c=='\n')Append(b,"\\n",limit);else if(c=='\r')Append(b,"\\r",limit);else if(c=='\t')Append(b,"\\t",limit);else if(Char.IsHighSurrogate(c)&&i+1<text.Length&&Char.IsLowSurrogate(text[i+1])){Append(b,text.Substring(i,2),limit);i++;}else if(Char.IsControl(c)||Char.IsSurrogate(c))Append(b,"\\u"+((int)c).ToString("x4"),limit);else Append(b,c.ToString(),limit);}Append(b,"\"",limit);return b.ToString();}
  internal static string Truncate(string value,int limit){if(value.Length<=limit)return value;int n=limit;if(n>0&&Char.IsHighSurrogate(value[n-1])&&n<value.Length&&Char.IsLowSurrogate(value[n]))n--;return value.Substring(0,n)+"…";}
  internal static Result Present(object value){
   var vector=value as VectorValue;if(vector==null){string full=ValueFormatter.Format(value),shown;bool abbreviated=false;try{shown=value is string?Literal((string)value,PreviewLimit):full;}catch(OverflowException){shown=Literal(Truncate((string)value,160),1024);abbreviated=true;}return new Result(value,Truncate(shown,PreviewLimit),full.Length<=CopyLimit?full:null,full.Length>CopyLimit?"Risultato troppo lungo da copiare (limite 262.144 caratteri).":abbreviated?"Anteprima abbreviata. Copia conserva il risultato completo.":"");}
   string copy=null;try{var all=new StringBuilder("[");for(int i=0;i<vector.Count;i++){if(i>0)Append(all,", ",CopyLimit);object item=vector.At(i);Append(all,item is string?Literal((string)item,CopyLimit):ValueFormatter.Format(item),CopyLimit);}Append(all,"]",CopyLimit);copy=all.ToString();}catch(OverflowException){}
   var preview=new StringBuilder("[");int shownCount=Math.Min(64,vector.Count);bool shortItems=false;for(int i=0;i<shownCount;i++){if(i>0)preview.Append(", ");object item=vector.At(i);if(item is string){shortItems|=((string)item).Length>160;preview.Append(Literal(Truncate((string)item,160),1024));}else preview.Append(ValueFormatter.Format(item));}if(shownCount<vector.Count)preview.Append(", …");preview.Append(']');string note=vector.Count+" elementi"+(shownCount<vector.Count?" · anteprima dei primi 64. Usa un indice per leggere un elemento.":shortItems?" · anteprima abbreviata dei testi.":".")+(copy==null?" La copia supera il limite di 262.144 caratteri.":"");return new Result(value,Truncate(preview.ToString(),PreviewLimit),copy,note);
  }
 }
}
