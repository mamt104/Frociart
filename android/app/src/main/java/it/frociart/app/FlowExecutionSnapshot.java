package it.frociart.app;

import android.app.Activity;
import android.content.Context;
import android.graphics.*;
import android.text.Layout;
import android.text.StaticLayout;
import android.text.TextPaint;
import android.widget.TextView;
import java.io.*;
import java.util.*;

/** Immutable UI-thread capture; rendering never advances the program or reads mutable runner state. */
final class FlowExecutionSnapshot {
    static final class Row {final String name,value,type;final boolean heading;Row(String n,String v,boolean h){this(n,v,h,"");}Row(String n,String v,boolean h,String t){name=n;value=v;heading=h;type=t;}String displayName(){return name+(type.isEmpty()?"":"\n"+type);}}
    final String title,status;
    final ArrayList<Row> rows=new ArrayList<>();
    FlowExecutionSnapshot(FlowModel.Document doc,FlowEngine.Runner runner){
        title=doc.title==null?"Diagramma":doc.title;
        status=runner==null?"Esecuzione non avviata":runner.state==FlowEngine.State.COMPLETED?"Esecuzione completata · "+runner.steps+" passi":runner.state==FlowEngine.State.ERROR?"Esecuzione interrotta · "+runner.errorMessage:runner.state==FlowEngine.State.WAITING?"In attesa di "+runner.inputVariable:"Pausa · passo "+runner.steps;
        Map<String,String> types=runner==null?Collections.emptyMap():runner.variableTypes();
        rows.add(new Row("VARIABILI","",true));
        if(runner==null||runner.variables.isEmpty())rows.add(new Row("","Nessuna variabile ancora.",false));
        else{TreeMap<String,FlowEngine.Value> sorted=new TreeMap<>(String.CASE_INSENSITIVE_ORDER);sorted.putAll(runner.variables);for(Map.Entry<String,FlowEngine.Value> entry:sorted.entrySet()){
            FlowEngine.Value v=entry.getValue();if(!v.isVector())rows.add(new Row(entry.getKey(),v.format(),false,types.get(entry.getKey())));
            else{rows.add(new Row(entry.getKey(),"Vettore · "+v.vectorSize()+" elementi",false,types.get(entry.getKey())));for(int i=0;i<v.vectorSize();i++)rows.add(new Row("["+i+"]",v.element(i).format(),false));}
        }}
        rows.add(new Row("RISULTATO","",true));if(runner==null||runner.output.isEmpty())rows.add(new Row("","Nessun risultato prodotto.",false));else for(String line:runner.outputText().replace("\r\n","\n").replace("\r","\n").split("\n",-1))rows.add(new Row("",line.isEmpty()?"(riga vuota)":line,false));
        if(runner!=null){List<FlowConsoleLog.Entry> history=runner.consoleLog.snapshot();boolean hasInput=false;for(FlowConsoleLog.Entry e:history)if(e.kind==FlowConsoleLog.EntryKind.INPUT)hasInput=true;
            if(hasInput||runner.consoleLog.discarded()>0){rows.add(new Row("VALORI INSERITI","",true));if(runner.consoleLog.discarded()>0)rows.add(new Row("","Cronologia limitata: "+runner.consoleLog.discarded()+" messaggi precedenti rimossi.",false));
                if(!hasInput)rows.add(new Row("","Non restano valori inseriti nella cronologia conservata.",false));
                for(FlowConsoleLog.Entry e:history)if(e.kind==FlowConsoleLog.EntryKind.INPUT)rows.add(new Row(e.variable,(e.text.isEmpty()?"(testo vuoto)":e.text)+(e.textLimited?"\n[Testo limitato]":""),false,e.context()));}}
    }
    private static TextPaint paint(int size,boolean bold,boolean mono){TextPaint p=new TextPaint(Paint.ANTI_ALIAS_FLAG);p.setColor(0xFF1D2330);p.setTextSize(size);p.setTypeface(mono?Typeface.MONOSPACE:Typeface.create("sans-serif",bold?Typeface.BOLD:Typeface.NORMAL));return p;}
    private static StaticLayout layout(String text,TextPaint p,int width){return StaticLayout.Builder.obtain(text,0,text.length(),p,width).setAlignment(Layout.Alignment.ALIGN_NORMAL).setIncludePad(false).setBreakStrategy(Layout.BREAK_STRATEGY_SIMPLE).build();}
    private static void draw(Canvas c,StaticLayout text,float x,float y){c.save();c.translate(x,y);text.draw(c);c.restore();}
    Bitmap render(FlowCanvasView renderer)throws IOException{
        RectF bounds=renderer.getDiagramBounds();if(bounds.isEmpty())bounds.set(0,0,500,320);bounds.inset(-32,-32);
        final float scale=1.35f;int diagramWidth=Math.max(720,(int)Math.ceil(bounds.width()*scale)),width=diagramWidth+560;
        if(width>4096)throw new IOException("Il diagramma è troppo largo per un'immagine leggibile. Avvicina i blocchi o usa Riordina e riprova.");
        TextPaint text=paint(21,false,false),bold=paint(21,true,false),mono=paint(21,false,true),heading=paint(21,true,false);heading.setColor(0xFF2867D2);TextPaint titlePaint=paint(32,true,false);
        StaticLayout titleLayout=layout(title,titlePaint,width-96),statusLayout=layout(status,text,width-96);int top=96+titleLayout.getHeight()+statusLayout.getHeight(),rowsHeight=0;
        ArrayList<StaticLayout> names=new ArrayList<>(),values=new ArrayList<>();ArrayList<Integer> heights=new ArrayList<>();
        for(Row row:rows){StaticLayout n=layout(row.displayName(),row.heading?heading:bold,row.heading?464:158),v=layout(row.value,mono,row.name.isEmpty()?464:282);int h=row.heading?58:Math.max(38,Math.max(n.getHeight(),v.getHeight())+20);names.add(n);values.add(v);heights.add(h);rowsHeight+=h;if(rowsHeight>11000)throw new IOException("I dati sono troppo lunghi per un singolo PNG leggibile. Apri una variabile per leggerla e copiarla per intero, oppure esporta il diagramma in SVG.");}
        int height=top+Math.max((int)Math.ceil(bounds.height()*scale),rowsHeight)+72;
        if(height>12000||(long)width*height>16000000L)throw new IOException("Diagramma e dati superano le dimensioni di un singolo PNG leggibile. Riduci il contenuto della prova o esporta il diagramma in SVG.");
        Bitmap bitmap=Bitmap.createBitmap(width,height,Bitmap.Config.ARGB_8888);
        try{Canvas c=new Canvas(bitmap);c.drawColor(Color.WHITE);c.drawText("FROCIART  /  DIAGRAMMA E DATI",48,45,heading);draw(c,titleLayout,48,64);draw(c,statusLayout,48,74+titleLayout.getHeight());
            c.save();c.translate(24+(diagramWidth-48-bounds.width()*scale)/2,top);c.scale(scale,scale);c.translate(-bounds.left,-bounds.top);renderer.drawForExport(c);c.restore();
            Paint card=new Paint();card.setColor(0xFFF5F7FB);float x=diagramWidth+24,y=top;c.drawRect(x-16,top-8,x+496,top+rowsHeight+8,card);Paint line=new Paint();line.setColor(0xFFDCE2ED);
            for(int i=0;i<rows.size();i++){Row row=rows.get(i);int h=heights.get(i);draw(c,names.get(i),x,y+(row.heading?16:8));if(!row.heading){draw(c,values.get(i),x+(row.name.isEmpty()?0:182),y+8);c.drawLine(x,y+h-1,x+464,y+h-1,line);}y+=h;}
            text.setColor(0xFF505B6E);c.drawText("Frociart · realizzato da mamt104",48,height-23,text);return bitmap;
        }catch(RuntimeException|Error e){bitmap.recycle();throw e;}
    }
    File save(Context context,FlowCanvasView renderer)throws IOException{
        File folder=new File(context.getCacheDir(),"exports");if(!folder.isDirectory()&&!folder.mkdirs())throw new IOException("Cartella non disponibile.");
        File file=new File(folder,FlowExporter.safeName(title)+"_dati_"+UUID.randomUUID()+".png"),temp=new File(folder,file.getName()+".tmp");
        try{Bitmap bitmap=render(renderer);try(OutputStream stream=new FileOutputStream(temp)){if(!bitmap.compress(Bitmap.CompressFormat.PNG,100,stream))throw new IOException("Immagine non salvata.");}finally{bitmap.recycle();}if(!temp.renameTo(file))throw new IOException("Immagine non salvata.");return file;}
        catch(OutOfMemoryError e){throw new IOException("Memoria insufficiente per l'immagine. Riduci il diagramma e riprova.",e);}finally{if(temp.exists())temp.delete();}
    }
    static String summary(Map<String,FlowEngine.Value> variables){return summary(variables,java.util.Collections.emptyMap());}
    static String summary(Map<String,FlowEngine.Value> variables,Map<String,String> types){StringBuilder s=new StringBuilder();for(Map.Entry<String,FlowEngine.Value> e:variables.entrySet())s.append(e.getKey()).append(types.containsKey(e.getKey())?" · "+types.get(e.getKey()):"").append(" = ").append(e.getValue().isVector()?"Vettore · "+e.getValue().vectorSize()+" elementi":e.getValue().format()).append('\n');return s.length()==0?"Nessuna variabile ancora.":s.toString();}
    static void showVariables(Activity owner,Map<String,FlowEngine.Value> variables){showVariables(owner,variables,java.util.Collections.emptyMap());}
    static void showVariables(Activity owner,Map<String,FlowEngine.Value> variables,Map<String,String> types){
        Ui.Sheet sheet=new Ui.Sheet(owner,"Variabili","Tocca un nome per leggere e copiare il valore completo.");
        if(variables.isEmpty())sheet.paragraph("Esegui un Input o un'operazione per creare una variabile.");
        for(Map.Entry<String,FlowEngine.Value> entry:variables.entrySet()){String name=entry.getKey();FlowEngine.Value value=entry.getValue();sheet.action(name+(types.containsKey(name)?" · "+types.get(name):"")+(value.isVector()?" · "+value.vectorSize()+" elementi":""),"edit",0,()->{
            Ui.Sheet detail=new Ui.Sheet(owner,name,value.isVector()?"Indice e valore · gli indici partono da 0":null);StringBuilder contents=new StringBuilder();if(value.isVector())for(int i=0;i<value.vectorSize();i++)contents.append('[').append(i).append("]    ").append(value.element(i).format()).append('\n');else contents.append(value.format());if(value.isVector()&&value.vectorSize()==0)contents.append("Vettore vuoto");TextView text=Ui.text(owner,contents.toString(),17,false);text.setTypeface(Typeface.MONOSPACE);text.setTextIsSelectable(true);detail.body.addView(text);detail.show();
        });}sheet.show();
    }
}
