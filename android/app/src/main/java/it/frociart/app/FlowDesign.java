package it.frociart.app;

import java.util.Arrays;
import java.util.Locale;

/** Immutable device preference. Font families are supplied by the operating system. */
public final class FlowDesign {
    public static final String[] FAMILIES={"Moderno","Classico","Monospazio"};
    public static final String[] ROLES={"Operazioni e altri blocchi","Lettura","Stampa","Decisione","Inizio","Fine"};
    public static final FlowDesign DEFAULT=new FlowDesign(0,100,new String[]{"#5B8DEF","#22A5B8","#9771DC","#D68A18","#39B985","#7A719C"});
    public final int family,percent;
    private final String[] colors;
    public FlowDesign(int family,int percent,String[] colors){
        if(family<0||family>=FAMILIES.length||percent<80||percent>200||percent%10!=0)throw new IllegalArgumentException("Scegli un carattere e una dimensione tra 80% e 200%.");
        if(colors==null||colors.length!=6)throw new IllegalArgumentException("Servono sei colori per i titoli dei blocchi.");
        this.family=family;this.percent=percent;this.colors=colors.clone();for(int i=0;i<this.colors.length;i++)this.colors[i]=normalize(this.colors[i]);
    }
    public float scale(){return percent/100f;}
    public int fontKey(){return family*1000+percent;}
    public String fontFamily(){return family==1?"serif":family==2?"monospace":"sans-serif";}
    public String svgFamily(){return family==1?"serif":family==2?"monospace":"Arial, sans-serif";}
    public String hex(int role){return colors[role];}
    public String[] palette(){return colors.clone();}
    public int colorFor(int kind,int fallback){if(kind==FlowModel.NOTE||kind==FlowModel.PAUSE)return fallback;int role=kind==FlowModel.INPUT?1:kind==FlowModel.OUTPUT?2:kind==FlowModel.DECISION?3:kind==FlowModel.START?4:kind==FlowModel.END?5:0;return 0xff000000|Integer.parseInt(colors[role].substring(1),16);}
    public static String normalize(String color){if(color==null||!color.matches("#[0-9A-Fa-f]{6}"))throw new IllegalArgumentException("Usa un colore nel formato #RRGGBB, per esempio #5B8DEF.");return color.toUpperCase(Locale.ROOT);}
    public String encode(){return "FROCIART_DESIGN_1\n"+family+"\n"+percent+"\n"+String.join("\n",colors);}
    public static FlowDesign decode(String text){if(text==null||text.length()>256)throw new IllegalArgumentException("Preferenza di aspetto non valida.");String[] p=text.replace("\r\n","\n").split("\n",-1);if(p.length!=9||!p[0].equals("FROCIART_DESIGN_1")||!p[1].matches("[0-9]+")||!p[2].matches("[0-9]+"))throw new IllegalArgumentException("Preferenza di aspetto non valida.");return new FlowDesign(Integer.parseInt(p[1]),Integer.parseInt(p[2]),Arrays.copyOfRange(p,3,9));}
    public boolean same(FlowDesign other){return other!=null&&encode().equals(other.encode());}
}
