package it.frociart.app;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.pdf.PdfDocument;
import android.os.Bundle;
import android.os.CancellationSignal;
import android.os.Handler;
import android.os.Looper;
import android.os.OperationCanceledException;
import android.os.ParcelFileDescriptor;
import android.print.PageRange;
import android.print.PrintAttributes;
import android.print.PrintDocumentAdapter;
import android.print.PrintDocumentInfo;
import android.print.PrintManager;
import android.print.pdf.PrintedPdfDocument;
import java.io.IOException;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Snapshot-only native print adapter. No network permission, live editor mutation or physical output without system confirmation. */
public final class FlowPrintAdapter extends PrintDocumentAdapter {
    static final int MAX_PAGES=24;
    static final float NATURAL_SCALE=.75f, OVERLAP=18;
    private final Context context;
    private final FlowModel.Document snapshot;
    private final FlowCanvasView renderer;
    private final RectF bounds;
    private final boolean tiled;
    private final Handler main=new Handler(Looper.getMainLooper());
    private final ExecutorService worker=Executors.newSingleThreadExecutor();
    private PrintAttributes attributes;
    private Layout layout;
    private volatile boolean finished;
    private CancellationSignal activeWrite;

    static final class Layout {
        final Rect content;
        final float scale,stepX,stepY;
        final int columns,rows,count;
        Layout(Rect content,RectF bounds,boolean tiled) {
            // PrintedPdfDocument's page canvas already starts at the printable
            // rectangle's origin. Use its dimensions, without adding margins twice.
            this.content=new Rect(0,0,content.width(),content.height());
            float width=content.width()-36,height=content.height()-64;
            if(width<40||height<40||bounds.width()<=0||bounds.height()<=0)throw new IllegalArgumentException("Foglio o margini troppo piccoli. Scegli un altro formato.");
            float fit=Math.min(width/bounds.width(),height/bounds.height());
            // Avoid an almost empty extra page for a chart that needs only a small reduction.
            scale=tiled&&fit<NATURAL_SCALE*.85f?NATURAL_SCALE:Math.min(NATURAL_SCALE,fit);
            stepX=(width-OVERLAP)/scale;stepY=(height-OVERLAP)/scale;
            columns=tiled?tiles(bounds.width(),width/scale,stepX):1;
            rows=tiled?tiles(bounds.height(),height/scale,stepY):1;
            long total=(long)columns*rows;
            if(total>MAX_PAGES)throw new IllegalArgumentException("Servono più di "+MAX_PAGES+" pagine. Scegli una pagina unica o avvicina i blocchi.");
            count=(int)total;
        }
        private static int tiles(double length,double capacity,double step) {
            double n=length<=capacity+.01?1:1+Math.ceil((length-capacity)/step);
            return n>MAX_PAGES?MAX_PAGES+1:(int)n;
        }
    }
    public FlowPrintAdapter(Context context,FlowModel.Document document,boolean tiled) {
        this.context=context.getApplicationContext();this.snapshot=document.copy();this.tiled=tiled;
        renderer=FlowExporter.createRenderer(context,snapshot);
        bounds=renderer.getDiagramBounds();bounds.inset(-28,-28);
        if(snapshot.nodes.isEmpty())throw new IllegalArgumentException("Aggiungi almeno un blocco prima di stampare.");
    }
    public static void start(Activity activity,FlowModel.Document document,boolean tiled) {
        PrintManager manager=(PrintManager)activity.getSystemService(Context.PRINT_SERVICE);
        if(manager==null)throw new IllegalStateException("Stampa non disponibile. Puoi esportare il diagramma in PDF.");
        FlowPrintAdapter adapter=new FlowPrintAdapter(activity,document,tiled);
        try {
            PrintAttributes initial=adapter.defaults();
            if(manager.print("Frociart · "+FlowExporter.safeName(document.title),adapter,initial)==null)throw new IllegalStateException("Stampa non disponibile. Usa Esporta → PDF.");
        } catch(RuntimeException ex) {adapter.onFinish();throw ex;}
    }
    PrintAttributes defaults() {
        PrintAttributes portrait=defaults(false),landscape=defaults(true);
        Layout a=measure(portrait),b=measure(landscape);
        if(b!=null&&(a==null||b.count<a.count||b.count==a.count&&b.scale>a.scale+.00001f))return landscape;
        return portrait;
    }
    private PrintAttributes defaults(boolean landscape) {
        PrintAttributes.MediaSize media=PrintAttributes.MediaSize.ISO_A4;
        return new PrintAttributes.Builder().setMediaSize(landscape?media.asLandscape():media).setResolution(new PrintAttributes.Resolution("frociart","Frociart",300,300)).setMinMargins(new PrintAttributes.Margins(250,250,250,250)).setColorMode(PrintAttributes.COLOR_MODE_COLOR).build();
    }
    private Layout measure(PrintAttributes attrs) {
        PrintedPdfDocument doc=new PrintedPdfDocument(context,attrs);
        try {return new Layout(doc.getPageContentRect(),bounds,tiled);}catch(IllegalArgumentException ex){return null;}finally{doc.close();}
    }
    @Override public void onLayout(PrintAttributes oldAttributes,PrintAttributes newAttributes,CancellationSignal cancellation,LayoutResultCallback callback,Bundle extras) {
        layout=null;attributes=null;
        if(finished||cancellation.isCanceled()){callback.onLayoutCancelled();return;}
        try {
            PrintDocumentInfo info=prepare(newAttributes);
            if(cancellation.isCanceled()){layout=null;attributes=null;callback.onLayoutCancelled();return;}
            callback.onLayoutFinished(info,!newAttributes.equals(oldAttributes));
        } catch(RuntimeException ex) {layout=null;callback.onLayoutFailed(ex instanceof IllegalArgumentException?ex.getMessage():"Non riesco a preparare la stampa. Prova a esportare in PDF.");}
    }
    // Layout and PDF writing also serve independently of the system callback lifecycle.
    PrintDocumentInfo prepare(PrintAttributes attrs) {
        layout=null;attributes=null;
        PrintedPdfDocument measure=new PrintedPdfDocument(context,attrs);
        try {layout=new Layout(measure.getPageContentRect(),bounds,tiled);}finally{measure.close();}
        attributes=attrs;
        return new PrintDocumentInfo.Builder(FlowExporter.safeName(snapshot.title)+".pdf").setContentType(PrintDocumentInfo.CONTENT_TYPE_DOCUMENT).setPageCount(layout.count).build();
    }
    static PageRange[] selected(PageRange[] requested,int count) {
        ArrayList<PageRange> result=new ArrayList<>();int start=-1;
        for(int i=0;i<count;i++) {
            boolean found=false;if(requested!=null)for(PageRange r:requested)if(r!=null&&i>=r.getStart()&&i<=r.getEnd()){found=true;break;}
            if(found&&start<0)start=i;
            if(start>=0&&(!found||i==count-1)){result.add(new PageRange(start,found?i:i-1));start=-1;}
        }
        return result.toArray(new PageRange[0]);
    }
    @Override public void onWrite(PageRange[] requested,ParcelFileDescriptor destination,CancellationSignal cancellation,WriteResultCallback callback) {
        if(finished||cancellation.isCanceled()){close(destination);callback.onWriteCancelled();return;}
        activeWrite=cancellation;
        worker.execute(()->{
            int result=0;PageRange[] written=null;
            try {written=writePages(requested,destination,cancellation);}
            catch(OperationCanceledException ex){result=1;}
            catch(IOException|RuntimeException ex){result=2;}
            catch(OutOfMemoryError ex){result=2;}
            final int completed=result;final PageRange[] pages=written;
            main.post(()->{if(activeWrite==cancellation)activeWrite=null;if(completed==1||cancellation.isCanceled()||finished)callback.onWriteCancelled();else if(completed==2)callback.onWriteFailed("Stampa non creata. Riprova con meno pagine o esporta in PDF.");else callback.onWriteFinished(pages);});
        });
    }
    PageRange[] writePages(PageRange[] requested,ParcelFileDescriptor destination,CancellationSignal cancellation)throws IOException {
        final Layout plan=layout;final PrintAttributes attrs=attributes;
        try(OutputStream output=new ParcelFileDescriptor.AutoCloseOutputStream(destination)) {
            cancellation.throwIfCanceled();if(finished)throw new OperationCanceledException();
            if(plan==null||attrs==null)throw new IOException("Riapri l'anteprima di stampa.");
            PageRange[] pages=selected(requested,plan.count);
            if(pages.length==0)throw new IOException("Nessuna pagina selezionata.");
            PrintedPdfDocument pdf=new PrintedPdfDocument(context,attrs);
            try {
                for(PageRange range:pages)for(int i=range.getStart();i<=range.getEnd();i++) {
                    cancellation.throwIfCanceled();if(finished)throw new OperationCanceledException();
                    PdfDocument.Page page=pdf.startPage(i+1);
                    try {draw(page.getCanvas(),plan,i,attrs.getColorMode()==PrintAttributes.COLOR_MODE_MONOCHROME||renderer.getAppearance()==2);}finally{pdf.finishPage(page);}
                }
                cancellation.throwIfCanceled();if(finished)throw new OperationCanceledException();pdf.writeTo(output);
                cancellation.throwIfCanceled();return pages;
            }finally{pdf.close();}
        }
    }
    private void draw(Canvas c,Layout p,int index,boolean monochrome) {
        Rect r=p.content;float width=r.width()-36,height=r.height()-64;
        float worldX=bounds.left+(index%p.columns)*p.stepX,worldY=bounds.top+(index/p.columns)*p.stepY;
        if(p.columns==1)worldX-=(width/p.scale-bounds.width())/2;
        if(p.rows==1)worldY-=(height/p.scale-bounds.height())/2;
        c.drawColor(Color.WHITE);c.save();
        try {c.clipRect(r.left+18,r.top+32,r.right-18,r.bottom-32);c.translate(r.left+18,r.top+32);c.scale(p.scale,p.scale);c.translate(-worldX,-worldY);renderer.drawForPrint(c,monochrome);}finally{c.restore();}
        Paint text=new Paint(Paint.ANTI_ALIAS_FLAG);text.setColor(monochrome?0xff525252:0xff4b5260);text.setTextSize(10);
        String title=snapshot.title==null?"Diagramma":snapshot.title.replace('\n',' ').replace('\r',' ');
        c.drawText(fit(text,title,width),r.left+18,r.top+17,text);text.setTextSize(8);
        c.drawText("Frociart · "+(index+1)+" / "+p.count+(p.count>1?" · riga "+(index/p.columns+1)+", colonna "+(index%p.columns+1):""),r.left+18,r.bottom-14,text);
    }
    private static String fit(Paint p,String text,float width) {
        if(p.measureText(text)<=width)return text;int n=p.breakText(text,true,Math.max(0,width-p.measureText("…")),null);
        if(n>0&&n<text.length()&&Character.isHighSurrogate(text.charAt(n-1)))n--;return text.substring(0,n)+"…";
    }
    private static void close(ParcelFileDescriptor file){try{file.close();}catch(IOException ignored){}}
    @Override public void onFinish(){finished=true;if(activeWrite!=null)activeWrite.cancel();worker.shutdown();}
}
