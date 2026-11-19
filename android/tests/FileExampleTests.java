package it.frociart.app;
import static it.frociart.app.FlowModel.*;
import java.util.*;
public final class FileExampleTests {
 static int checks;static void check(boolean ok,String why){if(!ok)throw new AssertionError(why);checks++;}
 public static void main(String[] args){
  check(FlowExamples.all().get(7).title.equals("Copia un file di testo"),"New example appended without reordering old examples");
  for(int kind:FlowFileBlocks.KINDS){Node n=new Node(kind,FlowFileBlocks.defaultText(kind),0,0);FlowEngine.checkBlock(n);check(FlowFileBlocks.description(kind).length()>0,"Palette kind has useful description");}
  for(String input:new String[]{"","alpha\r\n\r\n è 🦊 \n","last line"})for(int pass=0;pass<3;pass++){
   Document doc=FlowExamples.copyFile();if(pass==1)doc=FlowPseudocode.decode(FlowPseudocode.encode(doc));if(pass==2)doc=FlowCodec.decode(FlowCodec.encode(doc));for(FlowEngine.Issue issue:FlowEngine.validate(doc))check(!issue.error,"Example validates: "+issue.message);
   FlowEngine.Runner runner=new FlowEngine.Runner(doc);int reads=0,saves=0;String saved=null;
   for(int i=0;i<300&&runner.state!=FlowEngine.State.COMPLETED&&runner.state!=FlowEngine.State.ERROR;i++){
    if(runner.state==FlowEngine.State.WAITING_FILE){FlowTextFiles.Request request=runner.pendingFile();if(request.kind==FlowTextFiles.Kind.READ){reads++;runner.provideFile(request,input);}else{saves++;saved=request.text;runner.provideFile(request,null);}}else runner.step();
   }
   check(runner.state==FlowEngine.State.COMPLETED,"Copy example completes: "+runner.errorMessage);
   int expected=input.isEmpty()?0:input.equals("last line")?1:3;String text=input.isEmpty()?"":input.equals("last line")?"last line\n":"alpha\n\n è 🦊 \n";
   check(reads==1&&saves==1&&Objects.equals(saved,text)&&String.join("",runner.output).equals("Righe copiate: "+expected),"Exact content and count after round trip "+pass);
  }
  String[] eof=null;for(String[] row:FlowFunctionData.ENTRIES)if(row[1].equals("EOF()"))eof=row;
  check(eof!=null&&eof[3].equals("non EOF()")&&eof[2].contains("lettura"),"EOF catalog includes executable guard and required context");
  System.out.println("FileExampleTests PASS "+checks+" assertions");
 }
}
