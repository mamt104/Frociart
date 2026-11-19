using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Frociart
{
    internal static class TutorVariables
    {
        internal static string Rewrite(string text, IDictionary<string, string> names)
        { return FlowVariableRename.RewriteSymbols(text,names,false); }

        internal static HashSet<string> Names(Diagram document, bool definitionsOnly = false, IDictionary<string,FlowFunction> functions = null, FlowFunction owner = null)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(owner!=null){names.UnionWith(owner.Parameters.Select(p=>p.Name));if(owner.ReturnType!=FlowDataType.Void)names.Add(owner.ReturnVariable);}
            foreach (var node in document.Nodes) try { var block = Compiler.Block(node,functions,owner); names.UnionWith(block.Definitions());
                if (!definitionsOnly) { if(block.DeclarationSize!=null)block.DeclarationSize.ReadVariables(names); if (block.Expression != null) block.Expression.ReadVariables(names); foreach (var assignment in block.Assignments) assignment.Expression.ReadVariables(names); }
            } catch (FlowException) { }
            return names;
        }
        internal static int Rename(Diagram document, string oldName, string newName, IDictionary<string,FlowFunction> functions = null, FlowFunction owner = null)
        {
            if (!ExpressionLanguage.Identifier(oldName) || !ExpressionLanguage.Identifier(newName)) throw new FlowException("Scegli nomi validi: una lettera o _ iniziale, poi lettere, cifre o _. Non usare parole riservate.");
            if (!String.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase) && Names(document, true,functions,owner).Contains(oldName) && Names(document,false,functions,owner).Contains(newName))
                throw new FlowException("Il nome " + newName + " è già usato da un altro dato. Scegli un nome libero per non unire due variabili diverse.");
            int changed = 0; var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { oldName, newName } };
            foreach (var node in document.Nodes.Where(n => n.Kind != NodeKind.Start && n.Kind != NodeKind.End)) { string rewritten = FlowDeclaration.RewriteNode(node, map); if (rewritten != node.Text) { node.Text = rewritten; changed++; } }
            foreach(var node in document.Nodes)if(node.BreakpointCondition!=null){string condition=Rewrite(node.BreakpointCondition,map);if(condition!=node.BreakpointCondition){node.BreakpointCondition=condition;changed++;}}
            if(owner!=null){foreach(var p in owner.Parameters)if(String.Equals(p.Name,oldName,StringComparison.OrdinalIgnoreCase)){p.Name=newName;changed++;}if(String.Equals(owner.ReturnVariable,oldName,StringComparison.OrdinalIgnoreCase)){owner.ReturnVariable=newName;changed++;}}
            return changed;
        }
    }
}
