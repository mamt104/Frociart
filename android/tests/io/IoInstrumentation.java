package it.frociart.app;

import android.app.Instrumentation;
import android.content.Context;
import android.content.ContextWrapper;
import android.database.Cursor;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.pdf.PdfRenderer;
import android.net.Uri;
import android.os.Bundle;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import android.util.AtomicFile;
import java.io.*;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.List;
import javax.xml.parsers.DocumentBuilderFactory;

/** Runs real Android framework I/O, PDF/PNG rendering and the installed content provider. */
public final class IoInstrumentation extends Instrumentation {
    private int checks;
    private File artifacts;
    private final StringBuilder log=new StringBuilder();
    private interface Action {void run()throws Exception;}
    private void check(boolean ok,String text){checks++;if(!ok)throw new AssertionError(text);log.append("PASS ").append(text).append('\n');}
    private void fails(Action action,String text)throws Exception{boolean failed=false;try{action.run();}catch(IOException|IllegalArgumentException|UnsupportedOperationException ex){failed=true;}check(failed,text);}
    @Override public void onCreate(Bundle args){super.onCreate(args);start();}
    @Override public void onStart(){
        Bundle result=new Bundle();
        try{
            Context context=getTargetContext();
            artifacts=new File(context.getExternalFilesDir(null),"io-test-artifacts");if(!artifacts.isDirectory()&&!artifacts.mkdirs())throw new IOException("Cannot create test artifact directory");
            final File isolated=new File(context.getCacheDir(),"io-test-store-"+System.currentTimeMillis());isolated.mkdirs();
            Context storeContext=new ContextWrapper(context){@Override public File getFilesDir(){return isolated;}@Override public Context getApplicationContext(){return this;}};
            stores(storeContext);
            exports(context);
            log.append("ANDROID I/O TESTS PASSED: ").append(checks).append(" checks\n");
            write(new File(artifacts,"io-tests.log"),log.toString().getBytes(StandardCharsets.UTF_8));
            result.putString("stream",log.toString()+"Artifacts: "+artifacts.getAbsolutePath()+"\n");finish(-1,result);
        }catch(Throwable ex){
            StringWriter trace=new StringWriter();ex.printStackTrace(new PrintWriter(trace));log.append("FAILED ").append(trace);
            try{if(artifacts!=null)write(new File(artifacts,"io-tests.log"),log.toString().getBytes(StandardCharsets.UTF_8));}catch(Exception ignored){}
            result.putString("stream",log.toString());finish(1,result);
        }
    }
    private void stores(Context context)throws Exception{
        FlowStore store=new FlowStore(context);String id=store.newId();FlowModel.Document d=FlowExamples.parity();d.title="Progetto originale";
        d.find("check").trueLabel="Accettato";d.find("check").trueColor="#123ABC";
        store.save(id,d);FlowModel.Document loaded=store.load(id);
        check(loaded.title.equals(d.title)&&loaded.nodes.size()==6,"local save/load with full diagram");
        check(loaded.find("check").trueLabel.equals("Accettato")&&loaded.find("check").trueColor.equals("#123ABC"),"local save preserves custom branch metadata");
        check(store.entries().size()==1&&store.entries().get(0).nodes==6,"project list reflects saved data");
        FlowModel.Document invalid=d.copy();invalid.nodes.get(0).x=Float.NaN;fails(()->store.save(id,invalid),"invalid project rejected before replacing stored data");
        check(store.load(id).title.equals("Progetto originale"),"failed save leaves prior valid project intact");
        File base=new File(context.getFilesDir(),"projects/"+id+".frociart");
        AtomicFile atomic=new AtomicFile(base);FileOutputStream unfinished=atomic.startWrite();unfinished.write("{incomplete".getBytes(StandardCharsets.UTF_8));unfinished.close();
        check(new FlowStore(context).load(id).title.equals("Progetto originale"),"unfinished AtomicFile write recovers committed version");
        File backup=new File(base.getPath()+".bak");check(base.renameTo(backup),"prepare legacy backup recovery fixture");write(base,new byte[]{'{','b','a','d'});
        check(store.load(id).title.equals("Progetto originale")&&!backup.exists(),"legacy .bak recovery precedes JSON parsing");
        check(base.renameTo(backup),"prepare backup-only list fixture");
        check(store.entries().size()==1&&base.exists(),"project list discovers and restores a backup-only project");
        fails(()->store.load("../outside"),"project identifiers reject path traversal");
        fails(()->FlowStore.readLimited(new ByteArrayInputStream(new byte[]{(byte)0xc3,(byte)0x28})),"invalid UTF8 never silently alters project text");
        fails(()->FlowStore.readLimited(new ByteArrayInputStream(new byte[FlowModel.MAX_BYTES+1])),"import read stops at 4 MB");
        byte[] bom=("\ufeff"+FlowCodec.encode(d)).getBytes(StandardCharsets.UTF_8);check(FlowCodec.decode(FlowStore.readLimited(new ByteArrayInputStream(bom))).title.equals(d.title),"BOM UTF8 project remains importable");
        write(new File(base.getPath()+".bak"),FlowCodec.encode(d).getBytes(StandardCharsets.UTF_8));write(new File(base.getPath()+".new"),new byte[]{1});
        check(store.delete(id)&&!base.exists()&&!backup.exists()&&!new File(base.getPath()+".new").exists(),"delete removes base and all recoverable temporary versions");
        check(store.entries().isEmpty(),"deleted project does not reappear via backup");
    }
    private FlowCanvasView renderer(Context context,FlowModel.Document document){
        final FlowCanvasView[] result=new FlowCanvasView[1];runOnMainSync(()->result[0]=FlowExporter.createRenderer(context,document));return result[0];
    }
    private void exports(Context context)throws Exception{
        FlowModel.Document d=FlowExamples.parity();d.title="Prova SVG & PDF 🌙";d.find("check").trueLabel="Sì <ora> &";d.find("check").falseLabel="Aspetta";
        d.find("check").trueColor="#123ABC";d.find("check").falseColor="#CA2255";
        FlowCanvasView renderer=renderer(context,d);
        File project=FlowExporter.create(context,d,"frociart",false,null);
        FlowModel.Document portable=FlowCodec.decode(FlowStore.readLimited(new FileInputStream(project)));
        check(portable.find("check").trueLabel.equals("Sì <ora> &"),"real .frociart export retains custom label");
        check(portable.find("check").trueColor.equals("#123ABC"),"real .frociart export retains custom color");
        File svg=FlowExporter.create(context,d,"svg",false,renderer);
        DocumentBuilderFactory xml=DocumentBuilderFactory.newInstance();xml.setNamespaceAware(true);org.w3c.dom.Document vector=xml.newDocumentBuilder().parse(svg);
        check(vector.getDocumentElement().getNamespaceURI().equals("http://www.w3.org/2000/svg"),"SVG parses as real namespaced XML");
        String vectorText=FlowStore.readLimited(new FileInputStream(svg));
        check(vectorText.contains("#123ABC")&&vectorText.contains("#CA2255"),"SVG preserves branch stroke colors");
        check(vector.getDocumentElement().getTextContent().contains("Sì <ora> &")&&vector.getDocumentElement().getTextContent().contains("Aspetta"),"SVG escapes XML and preserves readable branch labels");
        check(vector.getElementsByTagNameNS("http://www.w3.org/2000/svg","image").getLength()==0,"SVG remains vector, with no embedded bitmap");
        File png=FlowExporter.create(context,d,"png",false,renderer);
        Bitmap image=BitmapFactory.decodeFile(png.getPath());check(image!=null&&image.getWidth()>500&&image.getHeight()>500,"PNG decodes at useful resolution");
        check(image.getWidth()<=4096&&image.getHeight()<=4096&&(long)image.getWidth()*image.getHeight()<=12000000,"PNG stays within dimension and pixel budget");
        check(image.getPixel(0,0)==0xffffffff,"PNG background is white regardless of editor theme");
        check(colorPresent(image,0xff123abc)&&colorPresent(image,0xffca2255),"PNG contains both custom branch colors");image.recycle();
        runOnMainSync(()->renderer.setDark(true));
        File darkPng=FlowExporter.create(context,d,"png",false,renderer);
        check(Arrays.equals(read(png),read(darkPng)),"dark editor cannot change exported PNG pixels");
        File pdf=FlowExporter.create(context,d,"pdf",false,renderer);
        byte[] signature=read(pdf);check(signature.length>1000&&new String(signature,0,5,StandardCharsets.US_ASCII).equals("%PDF-"),"PDF has real signature and content");
        try(ParcelFileDescriptor pfd=ParcelFileDescriptor.open(pdf,ParcelFileDescriptor.MODE_READ_ONLY);PdfRenderer reader=new PdfRenderer(pfd)){
            check(reader.getPageCount()==1,"single-page PDF has exactly one page");
            PdfRenderer.Page page=reader.openPage(0);Bitmap pageImage=Bitmap.createBitmap(page.getWidth()*2,page.getHeight()*2,Bitmap.Config.ARGB_8888);page.render(pageImage,null,null,PdfRenderer.Page.RENDER_MODE_FOR_DISPLAY);
            check(colorPresent(pageImage,0xff123abc)&&colorPresent(pageImage,0xffca2255),"PDF renderer confirms both custom branch colors");
            try(OutputStream out=new FileOutputStream(new File(artifacts,"pdf-first-page.png"))){pageImage.compress(Bitmap.CompressFormat.PNG,100,out);}pageImage.recycle();page.close();
        }
        File parityPages=FlowExporter.create(context,d,"pdf",true,renderer);
        try(ParcelFileDescriptor pfd=ParcelFileDescriptor.open(parityPages,ParcelFileDescriptor.MODE_READ_ONLY);PdfRenderer reader=new PdfRenderer(pfd)){
            check(reader.getPageCount()>=1&&reader.getPageCount()<=24,"branching diagram exports as bounded readable A4 PDF for split-node visual regression");
        }
        FlowModel.Document tall=FlowExamples.blank();for(int i=0;i<14;i++){FlowModel.Node n=new FlowModel.Node(FlowModel.PROCESS,"x = "+i,0,200+i*190);n.id="middle"+i;tall.nodes.add(n);}tall.find("end").x=0;tall.find("end").y=2900;
        FlowCanvasView tallRenderer=renderer(context,tall);File pages=FlowExporter.create(context,tall,"pdf",true,tallRenderer);
        try(ParcelFileDescriptor pfd=ParcelFileDescriptor.open(pages,ParcelFileDescriptor.MODE_READ_ONLY);PdfRenderer reader=new PdfRenderer(pfd)){check(reader.getPageCount()>1&&reader.getPageCount()<=24,"multi-page PDF produces bounded multiple pages");}
        FlowModel.Document huge=FlowExamples.blank();huge.find("end").y=100000;FlowCanvasView hugeRenderer=renderer(context,huge);
        fails(()->FlowExporter.create(context,huge,"pdf",true,hugeRenderer),"PDF rejects layouts above 24 pages");
        fails(()->FlowExporter.create(context,d,"../outside",false,renderer),"unsupported export format cannot escape cache directory");
        fails(()->FlowExporter.create(context,d,"png",false,null),"background image export requires prepared snapshot renderer");
        check(!project.getName().equals(FlowExporter.create(context,d,"frociart",false,null).getName()),"exports receive collision-free filenames");
        File[] leftovers=new File(context.getCacheDir(),"exports").listFiles((dir,name)->name.endsWith(".tmp"));
        check(leftovers!=null&&leftovers.length==0,"successful and failed exports leave no partial file");
        provider(context,png);
        copy(project,new File(artifacts,"sample.frociart"));copy(svg,new File(artifacts,"sample.svg"));copy(png,new File(artifacts,"sample.png"));copy(pdf,new File(artifacts,"sample.pdf"));copy(parityPages,new File(artifacts,"parity-a4.pdf"));copy(pages,new File(artifacts,"multi-page.pdf"));
    }
    private void provider(Context context,File png)throws Exception{
        Uri uri=Uri.parse("content://"+context.getPackageName()+".files/"+png.getName());
        check(context.getContentResolver().getType(uri).equals("image/png"),"provider advertises correct shared MIME");
        try(Cursor cursor=context.getContentResolver().query(uri,new String[]{OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE},null,null,null)){
            check(cursor!=null&&cursor.moveToFirst()&&cursor.getString(0).equals(png.getName())&&cursor.getLong(1)==png.length(),"provider returns actual filename and byte length");
        }
        try(InputStream in=context.getContentResolver().openInputStream(uri)){byte[] sig=new byte[8];check(in!=null&&in.read(sig)==8&&sig[0]==(byte)0x89&&sig[1]=='P',"provider streams the real exported PNG");}
        fails(()->{try(ParcelFileDescriptor ignored=context.getContentResolver().openFileDescriptor(uri,"rw")){}},"shared URI cannot be opened for writing");
        fails(()->{try(InputStream ignored=context.getContentResolver().openInputStream(Uri.parse("content://"+context.getPackageName()+".files/..%2Fsecret"))){}},"provider rejects path traversal");
        fails(()->{try(InputStream ignored=context.getContentResolver().openInputStream(Uri.parse("content://"+context.getPackageName()+".files/not-an-export.frociart"))){}},"provider rejects missing or private-project paths");
        check(png.length()>0,"rejected provider operations preserve export");
    }
    private static boolean colorPresent(Bitmap bitmap,int color){int[] row=new int[bitmap.getWidth()];int hits=0;for(int y=0;y<bitmap.getHeight();y++){bitmap.getPixels(row,0,row.length,0,y,row.length,1);for(int c:row){if(Math.abs(((c>>16)&255)-((color>>16)&255))<12&&Math.abs(((c>>8)&255)-((color>>8)&255))<12&&Math.abs((c&255)-(color&255))<12&&((c>>>24)&255)>240)if(++hits>6)return true;}}return false;}
    private static byte[] read(File file)throws IOException{try(InputStream in=new FileInputStream(file);ByteArrayOutputStream out=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1)out.write(b,0,n);return out.toByteArray();}}
    private static void write(File file,byte[] bytes)throws IOException{try(OutputStream out=new FileOutputStream(file)){out.write(bytes);}}
    private static void copy(File from,File to)throws IOException{write(to,read(from));}
}

