using System;
using System.Collections.Generic;
using System.Linq;

namespace Frociart
{
    internal sealed class FlowDeclaration
    {
        internal string[] Names;
        internal FlowDataType Type;
        internal string SizeText;
        internal bool IsArray {get{return SizeText!=null;}}
        internal static string Body(string source)
        {
            string text=(source??"").Trim();
            foreach(string prefix in new[]{"DICHIARA ","DECLARE "})if(text.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return text.Substring(prefix.Length).Trim();
            return text;
        }
        internal static string RewriteVariables(string source,IDictionary<string,string> names)
        {
            string text=source??"",body=Body(text);int prefix=text.IndexOf(body,StringComparison.Ordinal);if(prefix<0)prefix=0;
            string suffix=text.Substring(prefix+body.Length);int colon=body.IndexOf(':'),bracket=colon<0?-1:body.IndexOf('[',colon);
            if(colon<0)return text.Substring(0,prefix)+TutorVariables.Rewrite(body,names)+suffix;
            return text.Substring(0,prefix)+TutorVariables.Rewrite(body.Substring(0,colon),names)+
                (bracket<0?body.Substring(colon):body.Substring(colon,bracket-colon+1)+TutorVariables.Rewrite(body.Substring(bracket+1),names))+suffix;
        }
        internal static string RewriteNode(FlowNode node,IDictionary<string,string> names)
        {
            if(node.Kind==NodeKind.Note)return node.Text;
            if(node.Kind==NodeKind.Declare)return RewriteVariables(node.Text,names);
            string text=node.Text??"";int prefix=node.Kind==NodeKind.Output?FlowOutput.ExpressionOffset(text):FlowTurtle.IsTurtle(node.Kind)?FlowTurtle.ExpressionOffset(node.Kind,text):FlowTextFiles.ExpressionOffset(node.Kind,text);
            return text.Substring(0,prefix)+TutorVariables.Rewrite(text.Substring(prefix),names);
        }
        internal static FlowDeclaration Parse(string source)
        {
            string text=Body(source);
            int colon=text.IndexOf(':');if(colon<1)throw new FlowException("Scrivi nome: TIPO, per esempio totale: INTERO oppure valori: REALE[10].");
            var result=new FlowDeclaration();result.Names=text.Substring(0,colon).Split(',').Select(n=>n.Trim()).ToArray();
            if(result.Names.Length>128||result.Names.Any(n=>!ExpressionLanguage.Identifier(n))||result.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count()!=result.Names.Length)throw new FlowException("Usa da 1 a 128 nomi di variabile distinti, separati da virgole.");
            string type=text.Substring(colon+1).Trim();int bracket=type.IndexOf('[');
            if(bracket>=0){if(!type.EndsWith("]",StringComparison.Ordinal))throw new FlowException("Chiudi la dimensione del vettore con ].");result.SizeText=type.Substring(bracket+1,type.Length-bracket-2).Trim();if(result.SizeText.Length==0)throw new FlowException("Indica quanti elementi contiene il vettore.");type=type.Substring(0,bracket).Trim();}
            switch(type.ToUpperInvariant()){
                case "INTERO":case "INTEGER":result.Type=FlowDataType.Integer;break;
                case "REALE":case "REAL":result.Type=FlowDataType.Real;break;
                case "TESTO":case "STRING":result.Type=FlowDataType.Text;break;
                case "LOGICO":case "BOOLEAN":result.Type=FlowDataType.Boolean;break;
                default:throw new FlowException("Scegli INTERO, REALE, TESTO oppure LOGICO.");
            }
            return result;
        }
        internal object Initial(int length){object value=Type==FlowDataType.Text?(object)"":Type==FlowDataType.Boolean?(object)false:0d;return IsArray?new VectorValue(Enumerable.Repeat(value,length)):value;}
        internal static int Length(object value){double n=Values.Number(value);if(n<0||n>VectorValue.MaxLength||n!=Math.Truncate(n))throw new FlowException("La dimensione del vettore deve essere un intero da 0 a 1024.");return(int)n;}
        internal object ConvertValue(object value,int expectedLength)
        {
            if(IsArray){var vector=value as VectorValue;if(vector==null||vector.Count!=expectedLength)throw new FlowException("La variabile richiede un vettore di "+expectedLength+" elementi.");return new VectorValue(Enumerable.Range(0,vector.Count).Select(i=>Scalar(vector.At(i))));}
            return Scalar(value);
        }
        internal object Scalar(object value){if(Type==FlowDataType.Integer&&Values.Numeric(value))return Math.Truncate(Values.Number(value));FunctionModel.CheckType(value,Type,false,"La variabile dichiarata");return value;}
        internal static string Label(FlowDataType type){switch(type){case FlowDataType.Integer:return "Intero";case FlowDataType.Real:return "Reale";case FlowDataType.Text:return "Testo";case FlowDataType.Boolean:return "Logico";default:return "Automatico";}}
        internal static string Describe(object value,FlowDataType type=FlowDataType.Auto){var vector=value as VectorValue;object scalar=vector==null?value:vector.Count==0?null:vector.At(0);if(type==FlowDataType.Auto&&scalar!=null)type=scalar is string?FlowDataType.Text:scalar is bool?FlowDataType.Boolean:Math.Truncate(Values.Number(scalar))==Values.Number(scalar)?FlowDataType.Integer:FlowDataType.Real;return Label(type)+(vector==null?"":"["+vector.Count+"]");}
    }
}
