package it.frociart.app;
import android.content.Context;
import android.graphics.*;
import android.animation.*;
import android.view.View;
/** Noninteractive live preview; uses the exact canvas geometry, fonts and wrapped lines. */
public final class FlowBlockPreview extends View {
 private final FlowModel.Node node;private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);private final Path path=new Path();private float w,h;private ValueAnimator animation;
 public FlowBlockPreview(Context c,FlowModel.Node source){super(c);node=source.copy();w=FlowModel.width(node);h=FlowModel.height(node);setContentDescription("Anteprima del blocco");setImportantForAccessibility(IMPORTANT_FOR_ACCESSIBILITY_NO);setLayerType(View.LAYER_TYPE_SOFTWARE,null);}
 public void text(String value){text(value,node.comment);}
 public void text(String value,String comment){node.comment=comment;FlowModel.applyText(node,value);float fromW=w,fromH=h,toW=FlowModel.width(node),toH=FlowModel.height(node);if(animation!=null){animation.removeAllUpdateListeners();animation.cancel();}if(!Ui.motion){w=toW;h=toH;requestLayout();invalidate();return;}animation=ValueAnimator.ofFloat(0,1);animation.setDuration(320);animation.setInterpolator(Ui.SPRING);animation.addUpdateListener(a->{float f=(float)a.getAnimatedValue();w=fromW+(toW-fromW)*f;h=fromH+(toH-fromH)*f;requestLayout();invalidate();});animation.start();}
 @Override protected void onMeasure(int ws,int hs){int width=MeasureSpec.getSize(ws);float d=getResources().getDisplayMetrics().density,scale=Math.min(d,(width-24*d)/Math.max(1,w));setMeasuredDimension(width,(int)Math.ceil(Math.min(240*d,Math.max(98*d,h*scale+24*d))));}
 @Override protected void onDraw(Canvas c){super.onDraw(c);float d=getResources().getDisplayMetrics().density,s=Math.min(d,Math.min((getWidth()-24*d)/w,(getHeight()-24*d)/h));int save=c.save();c.translate((getWidth()-w*s)/2,(getHeight()-h*s)/2);c.scale(s,s);path.reset();int k=node.kind;
 if(k==FlowModel.DECISION){path.moveTo(w/2,0);path.lineTo(w,h/2);path.lineTo(w/2,h);path.lineTo(0,h/2);path.close();}
 else if(k==FlowModel.INPUT||k==FlowModel.OUTPUT){path.moveTo(19,0);path.lineTo(w,0);path.lineTo(w-19,h);path.lineTo(0,h);path.close();}else path.addRoundRect(0,0,w,h,k<2?Math.min(29,h/2):11,k<2?Math.min(29,h/2):11,Path.Direction.CW);
 p.setStyle(Paint.Style.FILL);p.setColor(Ui.surface());c.drawPath(path,p);if(FlowModel.isIo(k)){p.setColor(k==FlowModel.INPUT?0x2275BEEC:0x22814BCE);c.drawPath(path,p);}p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(1.6f);p.setColor(FlowModel.isIo(k)?(k==FlowModel.INPUT?(Ui.dark?0xFF75BEEC:0xFF388DBD):(Ui.dark?0xFFBC99FF:0xFF814BCE)):Ui.ACCENT);c.drawPath(path,p);p.setStyle(Paint.Style.FILL);int clip=c.save();c.clipPath(path);
 if(k>=2){p.setTypeface(Typeface.create("sans-serif-medium",0));p.setTextSize(9.3f);p.setColor(k==FlowModel.DECISION?0xFFE9B858:k==FlowModel.INPUT?(Ui.dark?0xFF75BEEC:0xFF388DBD):k==FlowModel.OUTPUT?(Ui.dark?0xFFBC99FF:0xFF814BCE):Ui.ACCENT);String label=FlowCanvasView.typeLabel(k);c.drawText(label,(w-p.measureText(label))/2,k==FlowModel.DECISION?h*.22f+8:23,p);}
 p.setTypeface(Typeface.create(k<2?"sans-serif-medium":"sans-serif",0));p.setTextSize(k==FlowModel.DECISION?13.3f:k<2?14:13.7f);p.setColor(Ui.ink());String[] lines=FlowModel.size(node).lines;Paint.FontMetrics fm=p.getFontMetrics();float top=k<2?8:k==FlowModel.DECISION?h*.34f:32,bottom=k<2?h-8:k==FlowModel.DECISION?h*.68f:h-12,y=top+Math.max(0,(bottom-top-((lines.length-1)*17.5f+fm.descent-fm.ascent))/2)-fm.ascent;
 for(String line:lines){float center=k<2?(49+(w-64)/2):w/2;c.drawText(line,center-p.measureText(line)/2,y,p);y+=17.5f;}c.restoreToCount(clip);c.restoreToCount(save);}
 @Override protected void onDetachedFromWindow(){if(animation!=null){animation.removeAllUpdateListeners();animation.cancel();animation=null;}super.onDetachedFromWindow();}
}
