package it.frociart.app;

import java.io.StringReader;
import java.io.StringWriter;
import java.util.HashMap;
import java.util.Locale;
import java.util.regex.Pattern;
import javax.xml.parsers.DocumentBuilderFactory;
import javax.xml.transform.OutputKeys;
import javax.xml.transform.TransformerFactory;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.StreamResult;
import org.w3c.dom.*;
import org.xml.sax.InputSource;

/** Immutable style snapshot: no project, history or execution changes. */
public final class FlowAppearance {
    public final int mode;
    public final FlowDesign customDesign;
    public FlowDesign design(){return mode==3?customDesign:FlowDesign.DEFAULT;}
    private final HashMap<Long,Integer> colors=new HashMap<>();
    private static int current;
    private static FlowDesign currentDesign=FlowDesign.DEFAULT;
    private static final Pattern HEX=Pattern.compile("#[0-9A-Fa-f]{6}");
    private static final Pattern SURROGATE_REFERENCES=Pattern.compile("&#([0-9]{5});&#([0-9]{5});");
    public static final String[] NAMES={"Standard","Contorni marcati","Bianco e nero","Personalizzato"};
    public static final String[] DESCRIPTIONS={"Stile originale, con i colori dei blocchi e dei rami.","Linee più evidenti e colori più leggibili. Le caselle restano neutre.","Diagramma monocromatico con linee evidenti. I rami si distinguono dal nome.","Il tuo carattere, la dimensione del testo e i colori dei titoli. Le caselle restano neutre."};
    public static int valid(int mode){return mode>=0&&mode<=3?mode:0;}
    public static void setCurrent(int mode){current=valid(mode);}
    public static int current(){return current;}
    public static FlowDesign currentDesign(){return currentDesign;}
    public static void setSettings(int mode,FlowDesign design){setCurrent(mode);currentDesign=design==null?FlowDesign.DEFAULT:design;}
    public static FlowDesign activeDesign(){return current==3?currentDesign:FlowDesign.DEFAULT;}
    public FlowAppearance(int mode){this(mode,currentDesign);}
    public FlowAppearance(int mode,FlowDesign design){this.mode=valid(mode);customDesign=design==null?FlowDesign.DEFAULT:design;}
    public int kind(int kind,int original,boolean dark){return ink(mode==3?design().colorFor(kind,original):original,dark);}
    public float width(float value){return mode==0?value:value*1.5f;}
    public int neutral(int color){if(mode!=2)return color;int gray=(77*((color>>>16)&255)+150*((color>>>8)&255)+29*(color&255)+128)>>8;return(color&0xff000000)|(gray<<16)|(gray<<8)|gray;}
    public int border(boolean dark,int original){return mode==0?original:neutral(dark?0xffa7afbc:0xff626b79);}
    public int ink(int color,boolean dark) {
        if(mode==0)return color;long key=((color&0xffffffffL)<<1)|(dark?1:0);Integer cached=colors.get(key);if(cached!=null)return cached;
        int value=neutral(color),background=dark?0xff202228:0xffffffff;
        if(contrast(value,background)<4.5) {
            int target=dark?0xffffffff:0xff000000;double low=0,high=1;
            for(int i=0;i<10;i++){double middle=(low+high)/2;int candidate=blend(value,target,middle);if(contrast(candidate,background)>=4.5)high=middle;else low=middle;}
            value=blend(value,target,high);
        }
        if(colors.size()>=4096)colors.clear();colors.put(key,value);return value;
    }
    private static int blend(int a,int b,double t){int r=(int)Math.floor(((a>>>16)&255)+(((b>>>16)&255)-((a>>>16)&255))*t+.5),g=(int)Math.floor(((a>>>8)&255)+(((b>>>8)&255)-((a>>>8)&255))*t+.5),blue=(int)Math.floor((a&255)+((b&255)-(a&255))*t+.5);return(a&0xff000000)|(r<<16)|(g<<8)|blue;}
    private static double channel(int c){double x=c/255d;return x<=.04045?x/12.92:Math.pow((x+.055)/1.055,2.4);}
    private static double light(int c){return .2126*channel((c>>>16)&255)+.7152*channel((c>>>8)&255)+.0722*channel(c&255);}
    public static double contrast(int a,int b){double x=light(a),y=light(b);return(Math.max(x,y)+.05)/(Math.min(x,y)+.05);}
    public String svg(String source) {
        if(mode==0)return source;
        try {
            if(source.contains("<!DOCTYPE"))throw new IllegalArgumentException("SVG con DTD non supportato.");
            DocumentBuilderFactory factory=DocumentBuilderFactory.newInstance();factory.setNamespaceAware(true);factory.setExpandEntityReferences(false);
            javax.xml.parsers.DocumentBuilder builder=factory.newDocumentBuilder();builder.setEntityResolver((publicId,systemId)->new InputSource(new StringReader("")));
            Document doc=builder.parse(new InputSource(new StringReader(source)));
            NodeList elements=doc.getElementsByTagName("*");
            for(int i=0;i<elements.getLength();i++) {
                Element element=(Element)elements.item(i);
                for(String attr:new String[]{"fill","stroke","stop-color"}) {
                    String text=element.getAttribute(attr);if(!HEX.matcher(text).matches())continue;int rgb=Integer.parseInt(text.substring(1),16);
                    if(attr.equals("fill")&&rgb==0xffffff)continue;
                    int mapped=ink(0xff000000|rgb,false);element.setAttribute(attr,String.format(Locale.ROOT,"#%06X",mapped&0xffffff));
                }
                String stroke=element.getAttribute("stroke"),width=element.getAttribute("stroke-width");
                if(!stroke.isEmpty()&&!stroke.equals("none")&&!width.isEmpty())element.setAttribute("stroke-width",String.format(Locale.ROOT,"%.3f",Double.parseDouble(width)*1.5).replaceAll("0+$","").replaceAll("\\.$",""));
            }
            StringWriter result=new StringWriter();javax.xml.transform.Transformer transform=TransformerFactory.newInstance().newTransformer();transform.setOutputProperty(OutputKeys.ENCODING,"UTF-8");transform.transform(new DOMSource(doc),new StreamResult(result));return xmlCodepoints(result.toString());
        }catch(Exception ex){throw new IllegalArgumentException("Non riesco ad applicare lo stile SVG. Prova PNG o PDF.",ex);}
    }
    // Some Android XML transformers serialize a supplementary character as two
    // UTF-16 numeric references. XML 1.0 requires one Unicode scalar reference.
    private static String xmlCodepoints(String source){
        java.util.regex.Matcher matcher=SURROGATE_REFERENCES.matcher(source);if(!matcher.find())return source;
        StringBuilder result=new StringBuilder(source.length());int offset=0;
        do{result.append(source,offset,matcher.start());int high=Integer.parseInt(matcher.group(1)),low=Integer.parseInt(matcher.group(2));
            if(high>=0xD800&&high<=0xDBFF&&low>=0xDC00&&low<=0xDFFF)result.append("&#").append(0x10000+((high-0xD800)<<10)+(low-0xDC00)).append(';');
            else result.append(matcher.group());offset=matcher.end();
        }while(matcher.find());return result.append(source,offset,source.length()).toString();
    }
}
