package it.frociart.app;

import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import static it.frociart.app.FlowModel.*;

/** Function-aware debugger. Calls to tick are scheduled by the UI; no blocking run loop. */
public final class FlowDebugger {
    public enum Command { PAUSED, CONTINUE, INTO, OVER, OUT, TO_BLOCK }
    private static final class Point {
        FlowEngine.DebugCondition condition;String error;
        Point(String text){try{condition=new FlowEngine.DebugCondition(text);}catch(IllegalArgumentException ex){error=ex.getMessage();}}
    }
    public final FlowEngine.Runner runner;
    private Command mode=Command.PAUSED;
    public boolean ignoreBreakpoints;
    private boolean atBreakpoint,atTarget,targetNotReached,suspendedRunTo;private Document project;private String targetFunction,targetNode;
    private void clearTarget(){targetFunction=targetNode=null;suspendedRunTo=false;}
    public boolean suspendedRunTo(){return suspendedRunTo;}
    public boolean atTarget(){return atTarget;}
    public boolean targetNotReached(){return targetNotReached;}
    private String message;
    private final Map<String,Map<String,Point>> points=new HashMap<>();
    private final Map<Long,Long> checkedVisits=new HashMap<>();
    private long generation,originFrame,originVisit;
    public FlowDebugger(FlowEngine.Runner runner,Document project){if(runner==null)throw new IllegalArgumentException("Serve un'esecuzione.");this.runner=runner;generation=runner.generation();updateBreakpoints(project);}
    public Command mode(){return mode;}
    public boolean active(){return mode!=Command.PAUSED;}
    public boolean atBreakpoint(){return atBreakpoint;}
    public String message(){return message;}
    public void updateBreakpoints(Document project){
        this.project=project;points.clear();addPoints("",project);
        if(project.functions!=null)for(Function f:project.functions)addPoints(f.id,f.body);
        if(!runner.currentBlockStarted())checkedVisits.remove(runner.currentFrameId());
        atBreakpoint=false;atTarget=false;targetNotReached=false;message=null;
        if((mode==Command.TO_BLOCK||suspendedRunTo)&&!validTarget(targetFunction,targetNode)){pause();message="Il blocco da raggiungere non è più disponibile.";}
    }
    private void addPoints(String function,Document graph){Map<String,Point> set=new HashMap<>();for(Node n:graph.nodes)if(n.breakpoint)set.put(n.id,new Point(n.breakpointCondition));points.put(function,set);}
    private boolean terminal(){return runner.state==FlowEngine.State.COMPLETED||runner.state==FlowEngine.State.ERROR;}
    private void syncReset(){if(generation==runner.generation())return;generation=runner.generation();checkedVisits.clear();mode=Command.PAUSED;message=null;atBreakpoint=false;atTarget=false;targetNotReached=false;clearTarget();}
    public boolean begin(Command command){
        syncReset();if(command==Command.PAUSED){pause();return true;}if(terminal()||command==Command.TO_BLOCK)return false;
        if(command==Command.OUT&&runner.callDepth()<2)return false;
        originFrame=runner.currentFrameId();originVisit=runner.currentVisit();
        if(command!=Command.CONTINUE)checkedVisits.put(originFrame,originVisit);
        mode=command;message=null;atBreakpoint=false;atTarget=false;targetNotReached=false;clearTarget();return true;
    }
    private boolean validTarget(String function,String node){Document graph=project;if(function!=null&&!function.isEmpty()){graph=null;if(project.functions!=null)for(Function f:project.functions)if(f.id.equals(function)){graph=f.body;break;}}Node target=graph==null?null:graph.find(node);return target!=null&&target.kind!=NOTE;}
    public boolean runTo(String function,String node){syncReset();if(!validTarget(function,node)||!begin(Command.CONTINUE))return false;targetFunction=function==null?"":function;targetNode=node;mode=Command.TO_BLOCK;return true;}
    public boolean suspendRunTo(){syncReset();if(mode!=Command.TO_BLOCK&&!suspendedRunTo)return false;if(!validTarget(targetFunction,targetNode)){pause();return false;}mode=Command.PAUSED;suspendedRunTo=true;message="In pausa. Destinazione conservata: premi Esegui o Riprendi per raggiungere il blocco.";return true;}
    public boolean resumeRunTo(){syncReset();if(!suspendedRunTo)return false;if(terminal()||!validTarget(targetFunction,targetNode)){pause();return false;}suspendedRunTo=false;mode=Command.TO_BLOCK;message=null;return true;}
    public void pause(){mode=Command.PAUSED;message="In pausa.";atBreakpoint=false;atTarget=false;targetNotReached=false;clearTarget();}
    private void finishCommand(){
        if(terminal()){targetNotReached=mode==Command.TO_BLOCK&&runner.state==FlowEngine.State.COMPLETED;mode=Command.PAUSED;message=targetNotReached?"Programma terminato senza raggiungere il blocco.":null;atBreakpoint=false;atTarget=false;clearTarget();return;}
        if(runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE)return;
        boolean done=mode==Command.INTO||mode==Command.OVER&&(!runner.hasFrame(originFrame)||runner.currentFrameId()==originFrame&&runner.currentVisit()!=originVisit)||mode==Command.OUT&&!runner.hasFrame(originFrame);
        if(done){message=mode==Command.OUT?"Ritorno al chiamante. Avanti completa il blocco sospeso.":"In pausa prima del prossimo blocco.";mode=Command.PAUSED;}
    }
    public boolean tick(){
        syncReset();if(!active())return false;if(terminal()){finishCommand();return false;}if(runner.state==FlowEngine.State.WAITING||runner.state==FlowEngine.State.WAITING_FILE)return false;
        long frame=runner.currentFrameId(),visit=runner.currentVisit();Long last=checkedVisits.get(frame);
        if(mode==Command.TO_BLOCK&&!runner.currentBlockStarted()&&(runner.currentFunctionId==null?"":runner.currentFunctionId).equals(targetFunction)&&runner.currentNodeId.equals(targetNode)){checkedVisits.put(frame,visit);mode=Command.PAUSED;atTarget=true;message="Il blocco deve ancora essere eseguito.";clearTarget();return false;}
        if(last==null||last.longValue()!=visit){
            checkedVisits.put(frame,visit);Iterator<Long> it=checkedVisits.keySet().iterator();while(it.hasNext())if(!runner.hasFrame(it.next()))it.remove();
            Map<String,Point> set=points.get(runner.currentFunctionId==null?"":runner.currentFunctionId);Point point=set==null?null:set.get(runner.currentNodeId);
            if(!ignoreBreakpoints&&point!=null){
                try{
                    if(point.error!=null)throw new IllegalArgumentException(point.error);
                    if(point.condition.matches(runner.variables,runner.files::eof)){stopAtPoint("Punto di interruzione · "+runner.currentFunctionName+". Il blocco deve ancora essere eseguito.");return false;}
                }catch(IllegalArgumentException ex){stopAtPoint("Condizione di pausa non valida: "+ex.getMessage()+" Correggila oppure premi Riprendi per proseguire.");return false;}
            }
        }
        runner.step();finishCommand();return true;
    }
    private void stopAtPoint(String text){mode=Command.PAUSED;atBreakpoint=true;atTarget=false;clearTarget();message=text;}
    public void inputProvided(){syncReset();if(active())finishCommand();}
}
