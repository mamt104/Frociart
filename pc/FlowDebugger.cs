using System;
using System.Collections.Generic;
using System.Linq;

namespace Frociart
{
    /// <summary>Execution control is separate from the algorithm and its step/memory limits.</summary>
    public sealed class FlowDebugger
    {
        public enum Command { Paused, Continue, Into, Over, Out, ToBlock }
        private sealed class Point
        {
            internal DebugCondition Condition;
            internal string Error;
            internal Point(string text){try{Condition=new DebugCondition(text);}catch(FlowException ex){Error=ex.Message;}}
        }
        public sealed class DebugCondition
        {
            private readonly Expr expression;
            public DebugCondition(string text)
            {
                if(String.IsNullOrWhiteSpace(text))return;
                // No function catalog: conditions cannot call user code or request input.
                expression=ExpressionLanguage.Parse(text);
                var pending=new Stack<Expr>();pending.Push(expression);
                while(pending.Count>0)
                {
                    var current=pending.Pop();var call=current as CallExpr;
                    if(call!=null&&call.name=="random")throw new FlowException("Una condizione di pausa non può usare random. Usa un valore già calcolato nel diagramma.");
                    foreach(var child in ExpressionMachine.ChildrenOf(current))pending.Push(child);
                }
            }
            public bool Matches(IDictionary<string,object> variables,Func<bool> eof=null)
            {
                if(expression==null)return true;
                var evaluation=new ExpressionMachine(expression,eof);evaluation.Advance(variables);object value=evaluation.Result;
                if(!(value is bool))throw new FlowException("La condizione di pausa deve restituire vero o falso.");
                return (bool)value;
            }
        }
        public Interpreter Runner {get;private set;}
        public Command Mode {get;private set;}
        public bool IgnoreBreakpoints {get;set;}
        public bool Active {get{return Mode!=Command.Paused;}}
        public string Message {get;private set;}
        public bool AtBreakpoint {get;private set;}
        public bool AtTarget {get;private set;}
        public bool TargetNotReached {get;private set;}
        public bool SuspendedRunTo {get;private set;}
        private Diagram project;private string targetFunction,targetNode;
        private void ClearTarget(){targetFunction=targetNode=null;SuspendedRunTo=false;}
        private readonly Dictionary<string,Dictionary<string,Point>> points=new Dictionary<string,Dictionary<string,Point>>(StringComparer.Ordinal);
        private readonly Dictionary<long,long> checkedVisits=new Dictionary<long,long>();
        private long generation,originFrame,originVisit;

        public FlowDebugger(Interpreter runner,Diagram project)
        {
            if(runner==null)throw new ArgumentNullException("runner");
            Runner=runner;generation=runner.Generation;UpdateBreakpoints(project);
        }
        public void UpdateBreakpoints(Diagram project)
        {
            this.project=project;points.Clear();AddPoints("",project);
            if(project.Functions!=null)foreach(var f in project.Functions)AddPoints(f.Id,f.Body);
            // A not-yet-started block can be rechecked after editing its condition.
            if(!Runner.CurrentBlockStarted)checkedVisits.Remove(Runner.CurrentFrameId);
            AtBreakpoint=false;AtTarget=false;TargetNotReached=false;Message=null;
            if((Mode==Command.ToBlock||SuspendedRunTo)&&!ValidTarget(targetFunction,targetNode)){Pause();Message="Il blocco da raggiungere non è più disponibile.";}
        }
        private void AddPoints(string function,Diagram graph)
        {
            var set=new Dictionary<string,Point>(StringComparer.Ordinal);
            foreach(var n in graph.Nodes)if(n.Breakpoint)set.Add(n.Id,new Point(n.BreakpointCondition));
            points.Add(function,set);
        }
        private bool Terminal {get{return Runner.State==RuntimeState.Completed||Runner.State==RuntimeState.Error;}}
        private void SyncReset()
        {
            if(generation==Runner.Generation)return;
            generation=Runner.Generation;checkedVisits.Clear();Mode=Command.Paused;Message=null;AtBreakpoint=false;AtTarget=false;TargetNotReached=false;ClearTarget();
        }
        public bool Begin(Command command)
        {
            SyncReset();if(command==Command.Paused){Pause();return true;}
            if(Terminal||command==Command.ToBlock)return false;
            if(command==Command.Out&&Runner.CallDepth<2)return false;
            originFrame=Runner.CurrentFrameId;originVisit=Runner.CurrentVisit;
            if(command!=Command.Continue)checkedVisits[originFrame]=originVisit;
            Mode=command;Message=null;AtBreakpoint=false;AtTarget=false;TargetNotReached=false;ClearTarget();return true;
        }
        private bool ValidTarget(string function,string node){var graph=String.IsNullOrEmpty(function)?project:project.Functions==null?null:project.Functions.Where(f=>f.Id==function).Select(f=>f.Body).FirstOrDefault();return graph!=null&&graph.Nodes.Any(n=>n.Id==node&&n.Kind!=NodeKind.Note);}
        public bool RunTo(string function,string node){SyncReset();if(!ValidTarget(function,node)||!Begin(Command.Continue))return false;targetFunction=function??"";targetNode=node;Mode=Command.ToBlock;return true;}
        public bool SuspendRunTo(){SyncReset();if(Mode!=Command.ToBlock&&!SuspendedRunTo)return false;if(!ValidTarget(targetFunction,targetNode)){Pause();return false;}Mode=Command.Paused;SuspendedRunTo=true;Message="In pausa. Destinazione conservata: premi Esegui o Riprendi per raggiungere il blocco.";return true;}
        public bool ResumeRunTo(){SyncReset();if(!SuspendedRunTo)return false;if(Terminal||!ValidTarget(targetFunction,targetNode)){Pause();return false;}SuspendedRunTo=false;Mode=Command.ToBlock;Message=null;return true;}
        public void Pause(){Mode=Command.Paused;Message="In pausa.";AtBreakpoint=false;AtTarget=false;TargetNotReached=false;ClearTarget();}
        private void FinishCommand()
        {
            if(Terminal){TargetNotReached=Mode==Command.ToBlock&&Runner.State==RuntimeState.Completed;Mode=Command.Paused;Message=TargetNotReached?"Programma terminato senza raggiungere il blocco.":null;AtBreakpoint=false;AtTarget=false;ClearTarget();return;}
            if(Runner.State==RuntimeState.WaitingForInput||Runner.State==RuntimeState.WaitingForFile)return;
            bool done=Mode==Command.Into || Mode==Command.Over&&(!Runner.HasFrame(originFrame)||Runner.CurrentFrameId==originFrame&&Runner.CurrentVisit!=originVisit) || Mode==Command.Out&&!Runner.HasFrame(originFrame);
            if(done)
            {
                Message=Mode==Command.Out?"Ritorno al chiamante. Avanti completa il blocco sospeso.":"In pausa prima del prossimo blocco.";
                Mode=Command.Paused;
            }
        }
        /// <summary>One bounded interpreter step. The UI schedules batches and yields to input/paint.</summary>
        public bool Tick()
        {
            SyncReset();if(!Active)return false;
            if(Terminal){FinishCommand();return false;}
            if(Runner.State==RuntimeState.WaitingForInput||Runner.State==RuntimeState.WaitingForFile)return false;
            long frame=Runner.CurrentFrameId,visit=Runner.CurrentVisit,last;
            if(Mode==Command.ToBlock&&!Runner.CurrentBlockStarted&&(Runner.CurrentFunctionId??"")==targetFunction&&Runner.CurrentNodeId==targetNode){checkedVisits[frame]=visit;Mode=Command.Paused;AtTarget=true;Message="Il blocco deve ancora essere eseguito.";ClearTarget();return false;}
            if(!checkedVisits.TryGetValue(frame,out last)||last!=visit)
            {
                checkedVisits[frame]=visit;
                foreach(long dead in checkedVisits.Keys.Where(id=>!Runner.HasFrame(id)).ToArray())checkedVisits.Remove(dead);
                Dictionary<string,Point> set;Point point;
                if(!IgnoreBreakpoints&&points.TryGetValue(Runner.CurrentFunctionId??"",out set)&&set.TryGetValue(Runner.CurrentNodeId,out point))
                {
                    try
                    {
                        if(point.Error!=null)throw new FlowException(point.Error);
                        if(point.Condition.Matches(Runner.Variables,Runner.Files.Eof)){StopAtPoint("Punto di interruzione · "+Runner.CurrentFunctionName+". Il blocco deve ancora essere eseguito.");return false;}
                    }
                    catch(FlowException ex){StopAtPoint("Condizione di pausa non valida: "+ex.Message+" Correggila oppure premi Riprendi per proseguire.");return false;}
                }
            }
            Runner.Step();FinishCommand();return true;
        }
        private void StopAtPoint(string message){Mode=Command.Paused;AtBreakpoint=true;AtTarget=false;ClearTarget();Message=message;}
        public void InputProvided(){SyncReset();if(Active)FinishCommand();}
    }
}
