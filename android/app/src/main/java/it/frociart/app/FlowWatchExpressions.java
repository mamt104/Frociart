package it.frociart.app;
import java.util.*;
/** Project-session expression strings only; never retain runners or computed values. */
final class FlowWatchExpressions {
 static final int LIMIT=8,MAX_LENGTH=8192;
 static final class Session {
  private final ArrayList<String> values=new ArrayList<>();String draft="";int draftIndex=-1,selectionStart=-1,selectionEnd=-1;private int revision;
  List<String> expressions(){return Collections.unmodifiableList(values);}int revision(){return revision;}
  void put(String expression,int index){expression=expression==null?"":expression.trim();if(expression.isEmpty())throw new IllegalArgumentException("Scrivi un’espressione da osservare.");if(expression.length()>MAX_LENGTH)throw new IllegalArgumentException("L’espressione supera 8192 caratteri.");if(index< -1||index>=values.size())throw new IllegalArgumentException("La voce non esiste più.");for(int i=0;i<values.size();i++)if(i!=index&&values.get(i).equals(expression))throw new IllegalArgumentException("Questa espressione è già nella lista.");if(index<0){if(values.size()>=LIMIT)throw new IllegalArgumentException("Puoi osservare fino a 8 espressioni. Modifica o rimuovi una voce.");values.add(expression);}else values.set(index,expression);revision++;draft="";draftIndex=selectionStart=selectionEnd=-1;}
  void remove(int index){if(index<0||index>=values.size())return;values.remove(index);revision++;}
  Session copy(){Session s=new Session();s.restore(this);return s;}void restore(Session other){if(other==null||other==this)return;values.clear();values.addAll(other.values);draft=other.draft;draftIndex=other.draftIndex;selectionStart=other.selectionStart;selectionEnd=other.selectionEnd;revision++;}
 }
 static final class Row {
  final String expression,preview,error;final FlowInspectExpression.Result result;
  Row(String expression,FlowInspectExpression.Context context,boolean paused){this.expression=expression;FlowInspectExpression.Result value=null;String message=null,text;if(!paused)text="In esecuzione · aggiornamento alla pausa";else if(context==null)text="Nessuna chiamata disponibile";else try{value=context.evaluate(expression);text=FlowInspectExpression.truncate(value.preview,160);}catch(IllegalArgumentException ex){message=ex.getMessage();text="Errore: "+message;}result=value;error=message;preview=text;}
 }
 static List<Row> read(Session session,FlowInspectExpression.Context context,boolean paused){ArrayList<Row> rows=new ArrayList<>();for(String expression:session.expressions())rows.add(new Row(expression,context,paused));return Collections.unmodifiableList(rows);}
}
