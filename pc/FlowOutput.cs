using System;
namespace Frociart {
 // Explicit instruction syntax keeps files/drafts/clipboard lossless without a new schema.
 // Older interpreters reject the colon form instead of silently adding unwanted line breaks.
 internal static class FlowOutput {
  internal const string Prefix="continua: ";
  internal static bool Newline(string text){return !((text??"").TrimStart().StartsWith("continua:",StringComparison.OrdinalIgnoreCase));}
  internal static string Expression(string text){string s=(text??"").Trim();return Newline(s)?s:s.Substring(9).Trim();}
  internal static string Store(string expression,bool newline){return (newline?"":Prefix)+(expression??"");}
  internal static string Editor(string text){return (Newline(text)?"stampa ":"stampa_continua ")+Expression(text);}
  internal static string Display(string text){return Newline(text)?text:Editor(text);}
  internal static int ExpressionOffset(string text){string s=(text??"").TrimStart();int lead=(text??"").Length-s.Length;if(!Newline(s))return lead+9;if(s.StartsWith("stampa_continua ",StringComparison.OrdinalIgnoreCase))return lead+15;return 0;}
  internal static string Toggle(string editor,NodeKind original,bool newline){NodeKind kind;string text;IoInstruction.Parse(original,editor,out kind,out text);string value=(newline?"stampa ":"stampa_continua ")+Expression(text);if(value.Length>8192)throw new FlowException("Il testo è troppo lungo per cambiare questa opzione. Accorcialo e riprova.");return value;}
 }
}
