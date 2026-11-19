package it.frociart.app;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.pdf.PdfDocument;
import android.os.Looper;
import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.util.UUID;

/** Real project, PNG, vector SVG and PDF files. The dedicated renderer never touches the live canvas. */
public final class FlowExporter {
    private FlowExporter(){}
    public static String mime(String format){return "pdf".equals(format)?"application/pdf":"png".equals(format)?"image/png":"svg".equals(format)?"image/svg+xml":"application/x-frociart";}
    public static String safeName(String s){String n=s==null?"Diagramma":s.replaceAll("[^A-Za-z0-9_-]+","_");n=n.replaceAll("^_+|_+$","");if(n.isEmpty())n="Diagramma";return n.length()>60?n.substring(0,60):n;}

    /** Call on the UI thread with an isolated document snapshot, then hand this view to one export worker. */
    public static FlowCanvasView createRenderer(Context context,FlowModel.Document snapshot){
        if(Looper.myLooper()!=Looper.getMainLooper())throw new IllegalStateException("Prepara il renderer del diagramma sul thread principale.");
        FlowCodec.check(snapshot);
        FlowCanvasView renderer=new FlowCanvasView(context.getApplicationContext());
        renderer.setDocument(snapshot,false);renderer.setDark(false);return renderer;
    }
    /** Convenience for UI-thread callers. Background exports must use the prepared-renderer overload. */
    public static File create(Context context,FlowModel.Document doc,String format,boolean tiled)throws IOException{
        return create(context,doc,format,tiled,"frociart".equals(format)?null:createRenderer(context,doc));
    }
    public static File create(Context context,FlowModel.Document doc,String format,boolean tiled,FlowCanvasView renderer)throws IOException{
        if(!"frociart".equals(format)&&!"png".equals(format)&&!"pdf".equals(format)&&!"svg".equals(format))throw new IOException("Formato non supportato.");
        FlowCodec.check(doc);
        if(!"frociart".equals(format)&&(renderer==null||renderer.getDocument()!=doc))throw new IOException("Il diagramma da esportare non è stato preparato correttamente.");
        File folder=new File(context.getCacheDir(),"exports");if(!folder.isDirectory()&&!folder.mkdirs())throw new IOException("Non riesco a creare l'esportazione.");
        String suffix=UUID.randomUUID().toString().substring(0,8);
        File output=new File(folder,safeName(doc.title)+"_"+System.currentTimeMillis()+"_"+suffix+"."+format);
        File temporary=new File(folder,output.getName()+".tmp");
        try{
            if(format.equals("frociart"))writeText(temporary,FlowCodec.encode(doc));
            else {
                RectF bounds=renderer.getDiagramBounds();bounds.inset(-28,-28);
                if(bounds.width()<1||bounds.height()<1)bounds.set(0,0,480,640);
                if(format.equals("svg"))writeText(temporary,renderer.createSvg());
                else if(format.equals("png"))png(temporary,renderer,bounds);
                else pdf(temporary,renderer,doc,bounds,tiled);
            }
            if(!temporary.renameTo(output))throw new IOException("Non riesco a completare il file esportato.");return output;
        }catch(OutOfMemoryError ex){throw new IOException("Memoria insufficiente per esportare l'immagine. Prova il formato SVG o PDF.",ex);}
        catch(RuntimeException ex){throw new IOException("Esportazione non riuscita: "+ex.getMessage(),ex);}
        finally{if(temporary.exists())temporary.delete();}
    }
    private static void png(File output,FlowCanvasView renderer,RectF bounds)throws IOException{
        float scale=Math.min(2f,Math.min(4096f/bounds.width(),4096f/bounds.height()));
        int w=Math.max(1,(int)Math.ceil(bounds.width()*scale)),h=Math.max(1,(int)Math.ceil(bounds.height()*scale));
        if((long)w*h>12000000L){scale*=(float)Math.sqrt(12000000d/((long)w*h));w=Math.max(1,(int)(bounds.width()*scale));h=Math.max(1,(int)(bounds.height()*scale));}
        Bitmap bitmap=Bitmap.createBitmap(w,h,Bitmap.Config.ARGB_8888);
        try{Canvas c=new Canvas(bitmap);c.drawColor(Color.WHITE);c.scale(scale,scale);c.translate(-bounds.left,-bounds.top);renderer.drawForExport(c);try(OutputStream out=new FileOutputStream(output)){if(!bitmap.compress(Bitmap.CompressFormat.PNG,100,out))throw new IOException("PNG non riuscito.");}}
        finally{bitmap.recycle();}
    }
    private static void pdf(File output,FlowCanvasView renderer,FlowModel.Document doc,RectF bounds,boolean tiled)throws IOException{
        int pw=bounds.width()>bounds.height()?842:595,ph=pw==842?595:842;
        float contentW=pw-56,contentH=ph-72;
        float scale=tiled?Math.min(1f,contentW/bounds.width()):Math.min(contentW/bounds.width(),contentH/bounds.height());
        FlowPdfPages.Page[] pages;
        try{pages=tiled?FlowPdfPages.plan(doc,bounds.top,bounds.bottom,(double)contentH/scale,24,renderer.sizingProfile()):new FlowPdfPages.Page[]{new FlowPdfPages.Page(bounds.top,bounds.bottom)};}
        catch(IllegalArgumentException ex){throw new IOException(ex.getMessage(),ex);}
        int rows=pages.length;
        PdfDocument pdf=new PdfDocument();
        try{
            Paint text=new Paint(Paint.ANTI_ALIAS_FLAG);text.setColor(new FlowAppearance(renderer.getAppearance()).ink(0xFF737987,false));text.setTextSize(9);
            String title=fitText(text,"Frociart  ·  "+(doc.title==null?"Diagramma":doc.title.replace('\n',' ').replace('\r',' ')),pw-150);
            for(int i=0;i<rows;i++){
                PdfDocument.Page page=pdf.startPage(new PdfDocument.PageInfo.Builder(pw,ph,i+1).create());Canvas c=page.getCanvas();c.drawColor(Color.WHITE);
                FlowPdfPages.Page slice=pages[i];
                float pageContentH=Math.min(contentH,(float)((slice.bottom-slice.top)*scale));
                c.save();c.clipRect(28,28,pw-28,28+pageContentH);c.translate(28,28);c.scale(scale,scale);c.translate(-bounds.left,-(float)slice.top);renderer.drawForExport(c);c.restore();
                c.drawText(title,28,ph-22,text);c.drawText((i+1)+" / "+rows,pw-62,ph-22,text);pdf.finishPage(page);
            }
            try(OutputStream out=new FileOutputStream(output)){pdf.writeTo(out);}
        }finally{pdf.close();}
    }
    private static String fitText(Paint paint,String text,float width){if(paint.measureText(text)<=width)return text;int n=paint.breakText(text,true,Math.max(0,width-paint.measureText("…")),null);if(n>0&&n<text.length()&&Character.isHighSurrogate(text.charAt(n-1)))n--;return text.substring(0,n)+"…";}
    private static void writeText(File f,String text)throws IOException{try(OutputStream out=new FileOutputStream(f)){out.write(text.getBytes(StandardCharsets.UTF_8));}}
}

