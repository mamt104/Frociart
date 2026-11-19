using System;using System.Collections.Generic;using System.Collections.ObjectModel;using System.Linq;
namespace Frociart {
 // A project-session list of expressions, never runtime frames or computed values.
 internal static class FlowWatchExpressions {
  internal const int Limit=8,MaxLength=8192;
  internal sealed class Session {
   readonly List<string> expressions=new List<string>();internal string Draft="";internal int DraftIndex=-1,SelectionStart=-1,SelectionEnd=-1;internal int Revision {get;private set;}
   internal ReadOnlyCollection<string> Expressions {get{return expressions.AsReadOnly();}}
   internal void Put(string expression,int index=-1){expression=(expression??"").Trim();if(expression.Length==0)throw new ArgumentException("Scrivi un’espressione da osservare.");if(expression.Length>MaxLength)throw new ArgumentException("L’espressione supera 8192 caratteri.");if(index< -1||index>=expressions.Count)throw new ArgumentOutOfRangeException("index");if(expressions.Where((s,i)=>i!=index).Contains(expression,StringComparer.Ordinal))throw new ArgumentException("Questa espressione è già nella lista.");if(index<0){if(expressions.Count>=Limit)throw new ArgumentException("Puoi osservare fino a 8 espressioni. Modifica o rimuovi una voce.");expressions.Add(expression);}else expressions[index]=expression;Revision++;Draft="";DraftIndex=SelectionStart=SelectionEnd=-1;}
   internal void Remove(int index){if(index<0||index>=expressions.Count)return;expressions.RemoveAt(index);Revision++;}
   internal void Clear(){expressions.Clear();Draft="";DraftIndex=SelectionStart=SelectionEnd=-1;Revision++;}
  }
  internal sealed class Row {
   internal readonly string Expression,Preview,Error;internal readonly FlowInspectExpression.Result Result;
   internal Row(string expression,FlowInspectExpression.Context context,bool paused){Expression=expression;if(!paused){Preview="In esecuzione · aggiornamento alla pausa";return;}if(context==null){Preview="Nessuna chiamata disponibile";return;}try{Result=context.Evaluate(expression);Preview=FlowInspectExpression.Truncate(Result.Preview,160);}catch(FlowException ex){Error=ex.Message;Preview="Errore: "+Error;}}
  }
  internal static IList<Row> Read(Session session,FlowInspectExpression.Context context,bool paused){return session.Expressions.Select(s=>new Row(s,context,paused)).ToList().AsReadOnly();}
 }
}
