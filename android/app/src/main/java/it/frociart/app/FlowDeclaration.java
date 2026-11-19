package it.frociart.app;
import java.util.*;
import static it.frociart.app.FlowModel.*;

/** Portable declaration syntax: name, name: TYPE or name: TYPE[size expression]. */
final class FlowDeclaration {
    String[] names;int type;String sizeText;
    boolean isArray(){return sizeText!=null;}
    static String body(String source){String text=source==null?"":source.trim();for(String prefix:new String[]{"DICHIARA ","DECLARE "})if(text.regionMatches(true,0,prefix,0,prefix.length()))return text.substring(prefix.length()).trim();return text;}
    static String rewriteVariables(String source,Map<String,String> names){
        String text=source==null?"":source,body=body(text);int prefix=text.indexOf(body);if(prefix<0)prefix=0;
        String suffix=text.substring(prefix+body.length());int colon=body.indexOf(':'),bracket=colon<0?-1:body.indexOf('[',colon);
        if(colon<0)return text.substring(0,prefix)+FlowFunctionEditing.rewrite(body,names,false)+suffix;
        return text.substring(0,prefix)+FlowFunctionEditing.rewrite(body.substring(0,colon),names,false)+(bracket<0?body.substring(colon):body.substring(colon,bracket+1)+FlowFunctionEditing.rewrite(body.substring(bracket+1),names,false))+suffix;
    }
    static String rewriteNode(Node node,String text,Map<String,String> names){if(node.kind==NOTE)return text;if(node.kind==DECLARE)return rewriteVariables(text,names);if(text==null)text="";int prefix=node.kind==OUTPUT?FlowOutput.expressionOffset(text):FlowTurtle.isTurtle(node.kind)?FlowTurtle.expressionOffset(node.kind,text):FlowTextFiles.expressionOffset(node.kind,text);return text.substring(0,prefix)+FlowFunctionEditing.rewrite(text.substring(prefix),names,false);}
    static FlowDeclaration parse(String source){
        String text=body(source);
        int colon=text.indexOf(':');if(colon<1)throw error("Scrivi nome: TIPO, per esempio totale: INTERO oppure valori: REALE[10].");
        FlowDeclaration result=new FlowDeclaration();result.names=text.substring(0,colon).split(",",-1);Set<String> seen=new TreeSet<>(String.CASE_INSENSITIVE_ORDER);
        if(result.names.length>128)throw error("Usa da 1 a 128 nomi di variabile distinti, separati da virgole.");
        for(int i=0;i<result.names.length;i++){result.names[i]=result.names[i].trim();if(!FlowEngine.identifier(result.names[i])||!seen.add(result.names[i]))throw error("Usa da 1 a 128 nomi di variabile distinti, separati da virgole.");}
        String type=text.substring(colon+1).trim();int bracket=type.indexOf('[');if(bracket>=0){if(!type.endsWith("]"))throw error("Chiudi la dimensione del vettore con ].");result.sizeText=type.substring(bracket+1,type.length()-1).trim();if(result.sizeText.isEmpty())throw error("Indica quanti elementi contiene il vettore.");type=type.substring(0,bracket).trim();}
        switch(type.toUpperCase(Locale.ROOT)){case "INTERO":case "INTEGER":result.type=INTEGER;break;case "REALE":case "REAL":result.type=REAL;break;case "TESTO":case "STRING":result.type=TEXT;break;case "LOGICO":case "BOOLEAN":result.type=BOOLEAN;break;default:throw error("Scegli INTERO, REALE, TESTO oppure LOGICO.");}return result;
    }
    FlowEngine.Value initial(int length){FlowEngine.Value value=type==TEXT?FlowEngine.Value.text(""):type==BOOLEAN?FlowEngine.Value.bool(false):FlowEngine.Value.number(0);return isArray()?FlowEngine.Value.vector(Collections.nCopies(length,value)):value;}
    static int length(FlowEngine.Value value){double n=value.asNumber();if(n<0||n>1024||n!=Math.floor(n))throw error("La dimensione del vettore deve essere un intero da 0 a 1024.");return(int)n;}
    FlowEngine.Value convert(FlowEngine.Value value,int expectedLength){if(isArray()){if(!value.isVector()||value.vectorSize()!=expectedLength)throw error("La variabile richiede un vettore di "+expectedLength+" elementi.");ArrayList<FlowEngine.Value> values=new ArrayList<>();for(int i=0;i<value.vectorSize();i++)values.add(scalar(value.element(i)));return FlowEngine.Value.vector(values);}return scalar(value);}
    FlowEngine.Value scalar(FlowEngine.Value value){if(type==INTEGER&&value.isNumber()){double n=value.asNumber();return FlowEngine.Value.number(n<0?Math.ceil(n):Math.floor(n));}FlowFunctions.checkType(value,type,false,"La variabile dichiarata");return value;}
    static String label(int type){switch(type){case INTEGER:return "Intero";case REAL:return "Reale";case TEXT:return "Testo";case BOOLEAN:return "Logico";default:return "Automatico";}}
    static String describe(FlowEngine.Value value,int type){FlowEngine.Value scalar=value.isVector()?(value.vectorSize()==0?null:value.element(0)):value;if(type==AUTO&&scalar!=null)type=scalar.isText()?TEXT:scalar.isBoolean()?BOOLEAN:scalar.asNumber()==Math.floor(scalar.asNumber())?INTEGER:REAL;return label(type)+(value.isVector()?"["+value.vectorSize()+"]":"");}
    private static FlowEngine.Problem error(String message){return new FlowEngine.Problem(message);}
}
