package it.frociart.app;
import java.util.Locale;
public final class FlowOutput {
 private FlowOutput(){}
 public static final String PREFIX="continua: ";
 public static boolean newline(String text){return !(text==null?"":text.trim()).toLowerCase(Locale.ROOT).startsWith("continua:");}
 public static String expression(String text){String s=text==null?"":text.trim();return newline(s)?s:s.substring(9).trim();}
 public static String store(String expression,boolean newline){return(newline?"":PREFIX)+(expression==null?"":expression);}
 public static String editor(String text){return(newline(text)?"stampa ":"stampa_continua ")+expression(text);}
 public static String display(String text){return newline(text)?text:editor(text);}
 public static int expressionOffset(String text){String s=text==null?"":text;int lead=0;while(lead<s.length()&&Character.isWhitespace(s.charAt(lead)))lead++;String body=s.substring(lead).toLowerCase(Locale.ROOT);if(!newline(body))return lead+9;if(body.startsWith("stampa_continua "))return lead+15;return 0;}
 public static String toggle(String editor,int original,boolean newline){FlowModel.IoText parsed=new FlowModel.IoText(original,editor);String s=(newline?"stampa ":"stampa_continua ")+expression(parsed.text);if(s.length()>8192)throw new IllegalArgumentException("Il testo è troppo lungo per cambiare questa opzione. Accorcialo e riprova.");return s;}
}
