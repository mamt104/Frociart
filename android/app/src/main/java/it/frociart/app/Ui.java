package it.frociart.app;

import android.app.*;
import android.content.*;
import android.content.res.ColorStateList;
import android.graphics.*;
import android.graphics.drawable.*;
import android.os.Build;
import android.text.TextUtils;
import android.view.*;
import android.view.accessibility.AccessibilityNodeInfo;
import android.widget.*;

/** Small native design system. No WebView, downloaded fonts or continuously running animation. */
public final class Ui {
    public static final String IO_EXPLANATION="Scrivi leggi per ricevere un dato (azzurro) o stampa per mostrarlo (viola). La forma è sempre un parallelogramma.";
    private Ui() {}
    public static boolean dark=false,motion=true;
    public static final int ACCENT=0xFF2F65D8;
    public static int accentInk(){return dark?0xFF75ACFF:0xFF2259C4;}
    public static int bg(){return dark?0xFF17181C:0xFFFAF9F6;}
    public static int surface(){return dark?0xFF202228:Color.WHITE;}
    public static int ink(){return dark?0xFFF3F4F8:0xFF17181C;}
    public static int muted(){return dark?0xFFB7BDCE:0xFF62646D;}
    public static int line(){return dark?0xFF3E424D:0xFFE7E6E1;}
    public static int tint(){return dark?0xFF283851:0xFFE7EEFC;}
    public static int danger(){return Color.parseColor(dark?"#FF92AD":"#B63350");}
    public static int success(){return dark?0xFF52D0AC:0xFF197953;}
    public static int successTint(){return dark?0xFF223C33:0xFFE4F5ED;}
    public static final android.view.animation.Interpolator SPRING=Ui::spring;
    public static float spring(float t){if(t<=0)return 0;if(t>=1)return 1;double x=t*11,d=.72,w=Math.sqrt(1-d*d);return(float)(1-Math.exp(-d*x)*(Math.cos(w*x)+d/w*Math.sin(w*x)));}
    public static boolean animations(){return motion&&android.animation.ValueAnimator.areAnimatorsEnabled();}
    /** Short, event-driven motion. Detached pages never keep their own animation loop. */
    public static void enter(View v,long delay){
        v.animate().cancel();if(!animations()){v.setAlpha(1);v.setTranslationY(0);return;}v.setAlpha(0);v.setTranslationY(dp(v.getContext(),12));
        if(v.isAttachedToWindow()&&v.isLaidOut()){startEntrance(v,delay);return;}
        // Building and laying out a page can take several frames. Start the
        // clock only when the new page can actually draw its first frame.
        final class Ready implements ViewTreeObserver.OnPreDrawListener,View.OnAttachStateChangeListener {
            private void clear(){if(v.getViewTreeObserver().isAlive())v.getViewTreeObserver().removeOnPreDrawListener(this);v.removeOnAttachStateChangeListener(this);}
            public boolean onPreDraw(){clear();startEntrance(v,delay);return true;}
            public void onViewAttachedToWindow(View view){}
            public void onViewDetachedFromWindow(View view){clear();v.animate().cancel();}
        }
        Ready ready=new Ready();v.getViewTreeObserver().addOnPreDrawListener(ready);v.addOnAttachStateChangeListener(ready);
    }
    private static void startEntrance(View v,long delay){if(!animations()){v.setAlpha(1);v.setTranslationY(0);return;}v.animate().alpha(1).translationY(0).setStartDelay(delay).setDuration(360).setInterpolator(SPRING).start();}
    public static void stagger(ViewGroup group){for(int i=0;i<group.getChildCount();i++)if(group.getChildAt(i).getVisibility()==View.VISIBLE)enter(group.getChildAt(i),Math.min(i*28,168));}
    public static void press(View v){v.setOnTouchListener((view,event)->{pressMotion(view,event);return false;});}
    private static void pressMotion(View v,MotionEvent e){
        if(!v.isEnabled())return;int action=e.getActionMasked();
        if(!animations()){v.animate().cancel();v.setScaleX(1);v.setScaleY(1);return;}
        if(action==MotionEvent.ACTION_DOWN)v.animate().scaleX(.978f).scaleY(.978f).setStartDelay(0).setDuration(70).start();
        else if(action==MotionEvent.ACTION_UP||action==MotionEvent.ACTION_CANCEL)v.animate().scaleX(1).scaleY(1).setStartDelay(0).setDuration(300).setInterpolator(SPRING).start();
    }
    public static int dp(Context c,float x){return (int)(x*c.getResources().getDisplayMetrics().density+0.5f);}
    public static GradientDrawable round(int color,float radius,Context c){GradientDrawable d=new GradientDrawable();d.setColor(color);d.setCornerRadius(dp(c,radius));return d;}
    public static void card(View v,int color,float radius){v.setBackground(round(color,radius,v.getContext()));}
    public static TextView text(Context c,String value,float size,boolean bold){TextView t=new TextView(c);t.setText(value);t.setTextSize(size);t.setTextColor(ink());t.setFontFeatureSettings("kern");t.setIncludeFontPadding(false);t.setTypeface(android.graphics.Typeface.create(bold?"sans-serif-medium":"sans-serif",0));return t;}
    public static TextView note(Context c,String value){TextView t=text(c,value,13,false);t.setTextColor(muted());t.setLineSpacing(dp(c,3),1);return t;}
    public static LinearLayout column(Context c){LinearLayout l=new LinearLayout(c);l.setOrientation(LinearLayout.VERTICAL);return l;}
    public static LinearLayout row(Context c){LinearLayout l=new LinearLayout(c);l.setGravity(Gravity.CENTER_VERTICAL);return l;}
    /** Eight dp of non-clickable space between visible actions. */
    public static LinearLayout actionRow(Context c){LinearLayout l=row(c);l.setDividerDrawable(new ColorDrawable(Color.TRANSPARENT){@Override public int getIntrinsicWidth(){return dp(c,8);}@Override public int getIntrinsicHeight(){return dp(c,8);}});l.setShowDividers(LinearLayout.SHOW_DIVIDER_MIDDLE);return l;}
    public static void pad(View v,int left,int top,int right,int bottom){Context c=v.getContext();v.setPadding(dp(c,left),dp(c,top),dp(c,right),dp(c,bottom));}
    public static void gap(LinearLayout l,int height){l.addView(new View(l.getContext()),new LinearLayout.LayoutParams(1,dp(l.getContext(),height)));}
    public static LinearLayout.LayoutParams lp(Context c,int width,int height){return new LinearLayout.LayoutParams(width<0?width:dp(c,width),height<0?height:dp(c,height));}
    public static LinearLayout.LayoutParams weight(Context c,int height){return new LinearLayout.LayoutParams(0,height<0?height:dp(c,height),1);}
    public static void announce(View v,String text){v.announceForAccessibility(text);}
    public static int parseColor(String value,int fallback){try{return Color.parseColor(value);}catch(Exception e){return fallback;}}
    /** Hold the preceding page until the replacement has actually laid out. */
    public static void installContent(Activity activity,View replacement){
        replacement.setBackgroundColor(bg());ViewGroup content=activity.findViewById(android.R.id.content);View previous=content==null||content.getChildCount()==0?null:content.getChildAt(0);Bitmap bitmap=null;
        if(animations()&&previous!=null&&previous.getWidth()>0&&previous.getHeight()>0){float ratio=Math.min(1,(float)Math.sqrt(1500000d/((double)previous.getWidth()*previous.getHeight())));try{bitmap=Bitmap.createBitmap(Math.max(1,(int)(previous.getWidth()*ratio)),Math.max(1,(int)(previous.getHeight()*ratio)),Bitmap.Config.ARGB_8888);Canvas c=new Canvas(bitmap);c.scale(ratio,ratio);previous.draw(c);}catch(OutOfMemoryError e){if(bitmap!=null)bitmap.recycle();bitmap=null;}}
        if(bitmap==null){activity.setContentView(replacement);enter(replacement,0);return;}FrameLayout host=new FrameLayout(activity);host.setBackgroundColor(bg());host.addView(replacement,new FrameLayout.LayoutParams(-1,-1));final SnapshotOverlay overlay=new SnapshotOverlay(activity,bitmap);host.addView(overlay,new FrameLayout.LayoutParams(-1,-1));activity.setContentView(host);enter(replacement,0);
        new PageReady(replacement,overlay);
    }
    private static int pendingPageFrames;
    /** Fade the old drawing only after the replacement is ready; pixels are released on detach. */
    public static void crossfade(FrameLayout host,View drawing){
        for(int i=host.getChildCount()-1;i>=0;i--)if(host.getChildAt(i) instanceof SnapshotOverlay)host.removeViewAt(i);
        if(!animations()||!drawing.isAttachedToWindow()||drawing.getWidth()<1||drawing.getHeight()<1||(long)drawing.getWidth()*drawing.getHeight()>8000000)return;
        Bitmap bitmap=Bitmap.createBitmap(drawing.getWidth(),drawing.getHeight(),Bitmap.Config.ARGB_8888);drawing.draw(new Canvas(bitmap));SnapshotOverlay overlay=new SnapshotOverlay(drawing.getContext(),bitmap);host.addView(overlay,new FrameLayout.LayoutParams(-1,-1));new PageReady(drawing,overlay);
    }
    private static final class PageReady implements ViewTreeObserver.OnPreDrawListener,View.OnAttachStateChangeListener {
        private final View page;private final SnapshotOverlay overlay;private ViewTreeObserver observer;private boolean active=true;
        PageReady(View page,SnapshotOverlay overlay){this.page=page;this.overlay=overlay;observer=page.getViewTreeObserver();pendingPageFrames++;page.addOnAttachStateChangeListener(this);observer.addOnPreDrawListener(this);}
        private void clear(){if(!active)return;active=false;pendingPageFrames--;if(observer.isAlive())observer.removeOnPreDrawListener(this);ViewTreeObserver current=page.getViewTreeObserver();if(current!=observer&&current.isAlive())current.removeOnPreDrawListener(this);page.removeOnAttachStateChangeListener(this);}
        public boolean onPreDraw(){if(!active)return true;clear();if(overlay.getParent()==null)return true;if(!animations()){((ViewGroup)overlay.getParent()).removeView(overlay);return true;}overlay.animate().alpha(0).setDuration(260).setInterpolator(new android.view.animation.DecelerateInterpolator()).withEndAction(()->{if(overlay.getParent() instanceof ViewGroup)((ViewGroup)overlay.getParent()).removeView(overlay);}).start();return true;}
        public void onViewAttachedToWindow(View view){observer=page.getViewTreeObserver();}
        public void onViewDetachedFromWindow(View view){clear();}
    }
    private static final class SnapshotOverlay extends ImageView {
        private Bitmap owned;SnapshotOverlay(Context context,Bitmap bitmap){super(context);owned=bitmap;setImageBitmap(bitmap);setScaleType(ScaleType.FIT_XY);setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);setClickable(false);setFocusable(false);}
        @Override protected void onDetachedFromWindow(){animate().cancel();setImageDrawable(null);if(owned!=null&&!owned.isRecycled())owned.recycle();owned=null;super.onDetachedFromWindow();}
    }
    public static void systemBars(Window window){
        window.setBackgroundDrawable(new ColorDrawable(bg()));
        window.setStatusBarColor(bg());window.setNavigationBarColor(bg());
        int flags=View.SYSTEM_UI_FLAG_LAYOUT_STABLE|View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN|View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION;
        if(!dark)flags|=View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR|View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR;
        window.getDecorView().setSystemUiVisibility(flags);
        if(Build.VERSION.SDK_INT>=30&&window.getInsetsController()!=null){int mask=WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS|WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS;window.getInsetsController().setSystemBarsAppearance(dark?0:mask,mask);}
        if(Build.VERSION.SDK_INT>=29){window.setStatusBarContrastEnforced(false);window.setNavigationBarContrastEnforced(false);}
    }
    public static EditText edit(Context c,String value,String hint,boolean multiline){
        EditText e=new EditText(c);e.setTextSize(16);e.setTextColor(ink());e.setHintTextColor(muted());e.setText(value);e.setHint(hint);e.setSingleLine(!multiline);e.setGravity(Gravity.TOP|Gravity.START);e.setSelectAllOnFocus(false);e.setPadding(dp(c,16),dp(c,14),dp(c,16),dp(c,14));
        GradientDrawable d=round(bg(),16,c);d.setStroke(dp(c,1),line());e.setBackground(d);e.setMinimumHeight(dp(c,54));
        if(multiline){e.setMinLines(2);e.setMaxLines(5);e.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_MULTI_LINE|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);}else e.setInputType(android.text.InputType.TYPE_CLASS_TEXT|android.text.InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        e.setImeOptions(android.view.inputmethod.EditorInfo.IME_FLAG_NO_EXTRACT_UI|(multiline?android.view.inputmethod.EditorInfo.IME_ACTION_NONE:android.view.inputmethod.EditorInfo.IME_ACTION_DONE));e.setContentDescription(hint);return e;
    }
    /** Hide the keyboard for this exact window before the input row/dialog disappears. */
    public static void dismissKeyboard(View input){
        if(input==null)return;
        android.os.IBinder token=input.getWindowToken();
        WindowInsetsController controller=Build.VERSION.SDK_INT>=30?input.getWindowInsetsController():null;
        input.clearFocus();
        android.view.inputmethod.InputMethodManager manager=(android.view.inputmethod.InputMethodManager)input.getContext().getSystemService(Context.INPUT_METHOD_SERVICE);
        if(manager!=null&&token!=null)manager.hideSoftInputFromWindow(token,0);
        if(Build.VERSION.SDK_INT>=30&&controller!=null)controller.hide(WindowInsets.Type.ime());
    }
    /** Hardware Enter is consumed on both edges and submitted once, on release. */
    public static void inputSubmission(EditText input,Runnable submit){
        input.setImeOptions(android.view.inputmethod.EditorInfo.IME_ACTION_SEND|android.view.inputmethod.EditorInfo.IME_FLAG_NO_EXTRACT_UI);
        input.setOnEditorActionListener((view,action,event)->{
            if(event!=null&&event.getKeyCode()==KeyEvent.KEYCODE_ENTER){
                if(event.getAction()==KeyEvent.ACTION_UP&&!event.isCanceled())submit.run();
                return true;
            }
            if(event==null&&(action==android.view.inputmethod.EditorInfo.IME_ACTION_SEND||action==android.view.inputmethod.EditorInfo.IME_ACTION_DONE)){submit.run();return true;}
            return false;
        });
    }
    public static class Button extends TextView {
        private String icon; private int tone; private String label;
        public Button(Context c,String label,String icon,int tone){super(c);this.label=label;this.icon=icon;this.tone=tone;setText(label);setTextSize(14);setTypeface(Typeface.create("sans-serif-medium",0));setIncludeFontPadding(false);setGravity(Gravity.CENTER);setSingleLine(true);setEllipsize(TextUtils.TruncateAt.END);setMinWidth(dp(c,48));setMinHeight(dp(c,48));int vertical=c.getResources().getConfiguration().fontScale>=1.5f?6:10;setPadding(dp(c,label.isEmpty()?10:14),dp(c,vertical),dp(c,label.isEmpty()?10:14),dp(c,vertical));setCompoundDrawablePadding(dp(c,7));setClickable(true);setFocusable(true);refresh();}
        public void setLabel(String s){if(!s.equals(label)){label=s;setText(s);}}
        public void setTone(int value){if(tone!=value){tone=value;refresh();}}
        public void setIcon(String value){if(!java.util.Objects.equals(icon,value)){icon=value;refresh();}}
        public void setChosen(boolean chosen){setSelected(chosen);setPaintFlags(chosen?getPaintFlags()|Paint.UNDERLINE_TEXT_FLAG:getPaintFlags()&~Paint.UNDERLINE_TEXT_FLAG);setTone(chosen?2:0);}
        public void refresh(){int fg=tone==1?(dark?0xFF0F1620:Color.WHITE):tone==3?danger():tone==2?accentInk():ink();int back=tone==1?(dark?accentInk():ACCENT):tone==2?tint():tone==3?(dark?0xFF422832:0xFFFCE9EE):surface();if(tone==4){fg=dark?0xFF0F2018:Color.WHITE;back=dark?0xFF52D0AC:0xFF197953;if(!isEnabled()){fg=dark?0xFF90B7A2:0xFF416453;back=dark?0xFF263E35:0xFFD5E8DE;}}setTextColor(fg);GradientDrawable shape=round(back,16,getContext());setBackground(new RippleDrawable(ColorStateList.valueOf(tone==4?0x28000000:dark?0x337CA2F2:0x285B8DEF),shape,null));if(icon!=null&&!icon.isEmpty()){android.graphics.drawable.Drawable drawable;if(icon.startsWith("png:")){try(java.io.InputStream input=getContext().getAssets().open("icons/"+icon.substring(4)+".png")){drawable=new android.graphics.drawable.BitmapDrawable(getResources(),android.graphics.BitmapFactory.decodeStream(input)){@Override public int getIntrinsicWidth(){return dp(getContext(),21);}@Override public int getIntrinsicHeight(){return dp(getContext(),21);}};drawable.setTint(fg);}catch(java.io.IOException failure){drawable=new Icon(getContext(),"center",fg,21);}}else drawable=new Icon(getContext(),icon,fg,21);setCompoundDrawablesRelativeWithIntrinsicBounds(drawable,null,null,null);}else setCompoundDrawablesRelativeWithIntrinsicBounds(null,null,null,null);setAlpha(tone==4||isEnabled()?1f:.38f);}
        @Override public void setEnabled(boolean enabled){boolean changed=enabled!=isEnabled();super.setEnabled(enabled);if(tone==4){if(changed)refresh();}else setAlpha(enabled?1f:.38f);}
        @Override public boolean onTouchEvent(android.view.MotionEvent e){pressMotion(this,e);return super.onTouchEvent(e);}
        @Override public void onInitializeAccessibilityNodeInfo(AccessibilityNodeInfo info){super.onInitializeAccessibilityNodeInfo(info);info.setClassName(android.widget.Button.class.getName());}
    }
    /** A full-width, wrapping choice for branch names and destinations on small screens. */
    public static final class Choice extends LinearLayout {
        private final TextView title,detail;private final View marker;
        public Choice(Context c){
            super(c);setOrientation(HORIZONTAL);setGravity(Gravity.CENTER_VERTICAL);setMinimumHeight(dp(c,72));pad(this,14,12,14,12);
            GradientDrawable fill=round(bg(),17,c);fill.setStroke(dp(c,1),line());setBackground(new RippleDrawable(ColorStateList.valueOf(dark?0x337CA2F2:0x285B8DEF),fill,null));
            marker=new View(c);addView(marker,lp(c,5,34));LinearLayout labels=column(c);pad(labels,12,0,8,0);
            title=text(c,"",15,true);title.setLineSpacing(dp(c,2),1);labels.addView(title,new LinearLayout.LayoutParams(-1,-2));
            detail=note(c,"");pad(detail,0,5,0,0);labels.addView(detail,new LinearLayout.LayoutParams(-1,-2));addView(labels,weight(c,-2));
            TextView arrow=text(c,"",14,false);arrow.setCompoundDrawablesWithIntrinsicBounds(new Icon(c,"right",muted(),18),null,null,null);addView(arrow,lp(c,18,20));
            setFocusable(true);setClickable(true);setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_YES);
            labels.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO_HIDE_DESCENDANTS);arrow.setImportantForAccessibility(View.IMPORTANT_FOR_ACCESSIBILITY_NO);press(this);
        }
        public void bind(String heading,String description,int color){title.setText(heading);detail.setText(description);detail.setVisibility(description==null||description.isEmpty()?GONE:VISIBLE);card(marker,color,3);setContentDescription(heading+(description==null||description.isEmpty()?"":", "+description));}
        @Override public void setSelected(boolean selected){super.setSelected(selected);GradientDrawable fill=round(selected?tint():bg(),17,getContext());fill.setStroke(dp(getContext(),selected?2:1),selected?accentInk():line());setBackground(new RippleDrawable(ColorStateList.valueOf(dark?0x337CA2F2:0x285B8DEF),fill,null));}
        @Override public void onInitializeAccessibilityNodeInfo(AccessibilityNodeInfo info){super.onInitializeAccessibilityNodeInfo(info);info.setClassName(android.widget.Button.class.getName());}
    }
    public static final class Toggle extends Button implements Checkable {
        private boolean checked;
        private final String label;
        public Toggle(Context c,String label,String icon){super(c,label,icon,0);this.label=label;}
        public boolean isChecked(){return checked;}
        public void setChecked(boolean value){checked=value;setSelected(value);setTone(value?2:0);setLabel(value?label+" ✓":label);}
        public void toggle(){setChecked(!checked);}
        @Override public void onInitializeAccessibilityNodeInfo(AccessibilityNodeInfo info){super.onInitializeAccessibilityNodeInfo(info);info.setClassName(android.widget.Switch.class.getName());info.setCheckable(true);info.setChecked(checked);}
    }
    public static final class Icon extends Drawable {
        private final Paint p=new Paint(Paint.ANTI_ALIAS_FLAG);private final String name;private final int color,size;private final Path path=new Path();
        public Icon(Context c,String name,int color,int dp){this.name=name;this.color=color;this.size=Ui.dp(c,dp);setBounds(0,0,size,size);}
        @Override public int getIntrinsicWidth(){return size;} @Override public int getIntrinsicHeight(){return size;}
        private void line(Canvas c,float...v){path.reset();path.moveTo(v[0],v[1]);for(int i=2;i<v.length;i+=2)path.lineTo(v[i],v[i+1]);c.drawPath(path,p);}
        @Override public void draw(Canvas c){c.save();Rect b=getBounds();c.translate(b.left,b.top);c.scale(b.width()/24f,b.height()/24f);p.setColor(color);p.setStyle(Paint.Style.STROKE);p.setStrokeWidth(1.8f);p.setStrokeCap(Paint.Cap.ROUND);p.setStrokeJoin(Paint.Join.ROUND);
            switch(name){
                case "brand":
                    c.scale(24f/64f,24f/64f);p.setStrokeWidth(4);
                    line(c,32,18,32,23);line(c,22,32,14,32,14,44);line(c,42,32,50,32,50,44);
                    p.setStyle(Paint.Style.FILL);c.drawRoundRect(23,8,41,18,5,5,p);
                    path.reset();path.moveTo(32,22);path.lineTo(44,32);path.lineTo(32,42);path.lineTo(20,32);path.close();c.drawPath(path,p);
                    c.drawRoundRect(6,44,22,56,4,4,p);c.drawRoundRect(42,44,58,56,4,4,p);break;
                case "home":line(c,3,11,12,3,21,11);line(c,5,10,5,21,10,21,10,15,14,15,14,21,19,21,19,10);break;
                case "back":line(c,14,5,7,12,14,19);break;
                case "learn":line(c,12,5,12,21);path.reset();path.moveTo(12,6);path.cubicTo(8,3,4,3,2,4);path.lineTo(2,19);path.cubicTo(5,18,8,18,12,21);path.cubicTo(16,18,19,18,22,19);path.lineTo(22,4);path.cubicTo(18,3,15,3,12,6);c.drawPath(path,p);break;
                case "spark":line(c,12,2,14.5f,9.5f,22,12,14.5f,14.5f,12,22,9.5f,14.5f,2,12,9.5f,9.5f,12,2);break;
                case "align":c.drawRoundRect(8,2,16,7,2,2,p);c.drawRoundRect(6,10,18,15,2,2,p);c.drawRoundRect(8,18,16,23,2,2,p);line(c,12,7,12,10);line(c,12,15,12,18);break;
                case "trophy":line(c,7,3,17,3,17,9);path.reset();path.moveTo(7,3);path.lineTo(7,9);path.cubicTo(7,15,17,15,17,9);c.drawPath(path,p);line(c,7,5,3,5,3,8,7,10);line(c,17,5,21,5,21,8,17,10);line(c,12,13,12,20);line(c,7,21,17,21);break;
                case "settings":c.drawCircle(12,12,4,p);for(int a=0;a<8;a++){c.save();c.rotate(a*45,12,12);line(c,12,2,12,5);c.restore();}c.drawCircle(12,12,8,p);break;
                case "add":line(c,12,5,12,19);line(c,5,12,19,12);break;
                case "close":line(c,6,6,18,18);line(c,18,6,6,18);break;
                case "play":p.setStyle(Paint.Style.FILL);path.reset();path.moveTo(8,4);path.lineTo(20,12);path.lineTo(8,20);path.close();c.drawPath(path,p);break;
                case "pause":line(c,8,5,8,19);line(c,16,5,16,19);break;
                case "step":line(c,18,5,18,19);path.reset();path.moveTo(6,5);path.lineTo(14,12);path.lineTo(6,19);path.close();c.drawPath(path,p);break;
                case "check":line(c,4,12,9,17,20,6);break;
                case "undo":line(c,8,5,3,10,8,15);path.reset();path.moveTo(3,10);path.lineTo(14,10);path.cubicTo(22,10,22,19,15,19);c.drawPath(path,p);break;
                case "redo":line(c,16,5,21,10,16,15);path.reset();path.moveTo(21,10);path.lineTo(10,10);path.cubicTo(2,10,2,19,9,19);c.drawPath(path,p);break;
                case "more":p.setStyle(Paint.Style.FILL);c.drawCircle(5,12,1.7f,p);c.drawCircle(12,12,1.7f,p);c.drawCircle(19,12,1.7f,p);break;
                case "down":line(c,6,9,12,15,18,9);break;
                case "right":line(c,9,5,16,12,9,19);break;
                case "up":line(c,6,15,12,9,18,15);break;
                case "minus":line(c,5,12,19,12);break;
                case "center":line(c,9,3,3,3,3,9);line(c,15,3,21,3,21,9);line(c,3,15,3,21,9,21);line(c,21,15,21,21,15,21);c.drawCircle(12,12,2,p);break;
                case "search":c.drawCircle(10,10,6,p);line(c,14.5f,14.5f,21,21);break;
                case "edit":line(c,4,20,9,19,20,8,16,4,5,15,4,20);line(c,13,7,17,11);break;
                case "link":c.drawRoundRect(3,4,13,11,3,3,p);c.drawRoundRect(11,13,21,20,3,3,p);line(c,8,8,16,16);break;
                case "copy":case "duplicate":c.drawRoundRect(8,8,21,21,3,3,p);line(c,16,4,5,4,4,5,4,16);break;
                case "trash":line(c,4,6,20,6);line(c,9,6,9,3,15,3,15,6);line(c,6,6,7,21,17,21,18,6);line(c,10,10,10,17);line(c,14,10,14,17);break;
                case "save":line(c,5,3,17,3,21,7,21,21,3,21,3,3,5,3);line(c,7,3,7,9,16,9,16,3);c.drawRect(7,14,17,21,p);break;
                case "folder":line(c,3,7,3,5,10,5,12,8,21,8,19,20,3,20,3,7);break;
                case "export":line(c,12,15,12,3);line(c,7,8,12,3,17,8);line(c,4,13,4,21,20,21,20,13);break;
                case "moon":path.reset();path.moveTo(16,3);path.cubicTo(4,0,-1,17,11,21);path.cubicTo(17,23,22,17,21,13);path.cubicTo(12,16,8,7,16,3);c.drawPath(path,p);break;
                case "sun":c.drawCircle(12,12,4,p);for(int a=0;a<8;a++){c.save();c.rotate(a*45,12,12);line(c,12,2,12,4);c.restore();}break;
                case "select":p.setPathEffect(new DashPathEffect(new float[]{3,3},0));c.drawRoundRect(3,3,21,21,3,3,p);p.setPathEffect(null);break;
                case "move":line(c,12,3,12,21);line(c,3,12,21,12);line(c,8,7,12,3,16,7);line(c,8,17,12,21,16,17);line(c,7,8,3,12,7,16);line(c,17,8,21,12,17,16);break;
                case "help":c.drawCircle(12,12,10,p);path.reset();path.moveTo(9,8);path.cubicTo(9,4,17,5,15,9);path.cubicTo(14,11,12,10,12,14);c.drawPath(path,p);p.setStyle(Paint.Style.FILL);c.drawCircle(12,18,1,p);break;
                case "start":c.drawRoundRect(3,6,21,18,6,6,p);break;
                case "end":c.drawRoundRect(3,6,21,18,6,6,p);c.drawCircle(12,12,2,p);break;
                case "call":c.drawRoundRect(3,4,21,20,3,3,p);c.drawLine(7,4,7,20,p);c.drawLine(17,4,17,20,p);break;
                case "process":c.drawRoundRect(3,4,21,20,3,3,p);break;
                case "decision":line(c,12,2,22,12,12,22,2,12,12,2);break;
                case "io":p.setShader(new LinearGradient(2,4,22,20,Ui.dark?0xFF75BEEC:0xFF388DBD,Ui.dark?0xFFBC99FF:0xFF814BCE,Shader.TileMode.CLAMP));line(c,7,4,22,4,17,20,2,20,7,4);p.setShader(null);break;
                case "input":line(c,7,4,22,4,17,20,2,20,7,4);break;
                case "output":line(c,7,4,22,4,17,20,2,20,7,4);line(c,9,12,16,12);break;
                default:line(c,12,4,12,10,5,10,5,17);line(c,12,10,19,10,19,17);c.drawCircle(12,3,2,p);c.drawRoundRect(2,17,8,22,1,1,p);c.drawRoundRect(16,17,22,22,1,1,p);break;
            }c.restore();
        }
        @Override public void setAlpha(int a){p.setAlpha(a);}@Override public void setColorFilter(ColorFilter f){p.setColorFilter(f);}@Override public int getOpacity(){return PixelFormat.TRANSLUCENT;}
    }
    /** Run controls sit beside the drawing in landscape and on wide tablets; below on narrow portrait. */
    public static final class Workspace extends LinearLayout {
        public View drawing,panel;public boolean panelExpanded,inputPriority;private int drawingVisibilityBeforeInput=View.VISIBLE;
        public Workspace(Context c){super(c);setOrientation(VERTICAL);setClipChildren(false);}
        /** Keep the existing input tree attached while giving it the short landscape IME window. */
        public void setInputPriority(boolean value){
            if(inputPriority==value)return;inputPriority=value;
            if(drawing!=null){if(value){drawingVisibilityBeforeInput=drawing.getVisibility();drawing.setVisibility(View.GONE);}else drawing.setVisibility(drawingVisibilityBeforeInput);}
            requestLayout();
        }
        @Override protected void onMeasure(int widthSpec,int heightSpec){
            if(drawing!=null&&panel!=null){
                int width=MeasureSpec.getSize(widthSpec),height=MeasureSpec.getSize(heightSpec);
                boolean wide=getResources().getConfiguration().orientation==android.content.res.Configuration.ORIENTATION_LANDSCAPE||width>=dp(getContext(),840);
                if(inputPriority)wide=false;
                int orientation=wide?HORIZONTAL:VERTICAL;if(getOrientation()!=orientation)setOrientation(orientation);
                boolean shown=panel.getVisibility()!=GONE;
                LinearLayout.LayoutParams d=(LinearLayout.LayoutParams)drawing.getLayoutParams();
                LinearLayout.LayoutParams p=(LinearLayout.LayoutParams)panel.getLayoutParams();
                d.width=wide?0:LayoutParams.MATCH_PARENT;d.height=wide?LayoutParams.MATCH_PARENT:0;d.weight=inputPriority?0:1;
                d.rightMargin=shown&&wide?dp(getContext(),8):0;d.bottomMargin=shown&&!wide?dp(getContext(),8):0;
                p.weight=0;
                if(wide){p.width=Math.max(0,Math.min(Math.min(dp(getContext(),330),(int)(width*.46f)),width-dp(getContext(),140)));p.height=LayoutParams.MATCH_PARENT;}
                else{p.width=LayoutParams.MATCH_PARENT;float extraText=80*Math.max(0,getResources().getConfiguration().fontScale-1);int desired=dp(getContext(),(panelExpanded?440:244)+extraText);p.height=inputPriority?Math.max(0,height-getPaddingTop()-getPaddingBottom()):Math.max(0,Math.min(desired,height-dp(getContext(),72)));}
            }
            super.onMeasure(widthSpec,heightSpec);
        }
    }
    public static final class BoundedScroll extends ScrollView {
        public int maxHeight;int availableHeight=Integer.MAX_VALUE;
        public BoundedScroll(Context c){super(c);maxHeight=(int)(c.getResources().getDisplayMetrics().heightPixels*.70f);setFillViewport(false);setClipToPadding(false);setVerticalScrollBarEnabled(false);}
        @Override protected void onMeasure(int w,int h){int size=MeasureSpec.getSize(h);if(MeasureSpec.getMode(h)==MeasureSpec.UNSPECIFIED)size=maxHeight;super.onMeasure(w,MeasureSpec.makeMeasureSpec(Math.max(0,Math.min(Math.min(size,maxHeight),availableHeight)),MeasureSpec.AT_MOST));}
    }
    /** Reserve the header and primary actions before giving space to scrollable fields. */
    private static final class SheetColumn extends LinearLayout {
        BoundedScroll scrolling;
        SheetColumn(Context c){super(c);setOrientation(VERTICAL);}
        @Override protected void onMeasure(int widthSpec,int heightSpec){
            if(scrolling!=null){
                int limit=MeasureSpec.getMode(heightSpec)==MeasureSpec.UNSPECIFIED?getResources().getDisplayMetrics().heightPixels:MeasureSpec.getSize(heightSpec);
                int fixed=getPaddingTop()+getPaddingBottom();
                for(int i=0;i<getChildCount();i++){
                    View child=getChildAt(i);if(child==scrolling||child.getVisibility()==GONE)continue;
                    measureChildWithMargins(child,widthSpec,0,MeasureSpec.makeMeasureSpec(0,MeasureSpec.UNSPECIFIED),0);
                    LinearLayout.LayoutParams p=(LinearLayout.LayoutParams)child.getLayoutParams();fixed+=child.getMeasuredHeight()+p.topMargin+p.bottomMargin;
                }
                scrolling.availableHeight=Math.max(0,limit-fixed);
            }
            super.onMeasure(widthSpec,heightSpec);
        }
    }
    public static final class Sheet {
        public final Dialog dialog; public final LinearLayout body,footer,root; public final BoundedScroll scroll;
        private final Activity owner;public Runnable beforeDismiss;private boolean forcedDismiss;private LinearLayout headingRow;private View handleView,headerGap;private TextView headingText;private boolean headerCompact,headingHiddenForInput;
        private boolean keyboardScroll;private EditText compactField;private int savedMinLines,savedMaxLines;
        public Sheet(Activity a,String title,String subtitle){
            owner=a;
            dialog=new Dialog(a){@Override public void dismiss(){if(beforeDismiss!=null&&!forcedDismiss){beforeDismiss.run();return;}super.dismiss();}};dialog.requestWindowFeature(Window.FEATURE_NO_TITLE);root=new SheetColumn(a);card(root,surface(),28);pad(root,20,8,20,16);
            View handle=new View(a);card(handle,line(),3);LinearLayout.LayoutParams hp=lp(a,36,4);hp.gravity=Gravity.CENTER;hp.bottomMargin=dp(a,12);root.addView(handle,hp);handleView=handle;
            LinearLayout head=row(a);headingRow=head;LinearLayout labels=column(a);TextView heading=text(a,title,24,true);headingText=heading;labels.addView(heading);if(subtitle!=null&&!subtitle.isEmpty()){TextView sub=note(a,subtitle);pad(sub,0,6,0,0);labels.addView(sub);}head.addView(labels,weight(a,-2));Button close=new Button(a,"","close",0);close.setContentDescription("Chiudi pannello");close.setOnClickListener(v->dialog.dismiss());head.addView(close,lp(a,48,48));root.addView(head);gap(root,16);headerGap=root.getChildAt(root.getChildCount()-1);
            scroll=new BoundedScroll(a);((SheetColumn)root).scrolling=scroll;body=column(a);scroll.addView(body,new ScrollView.LayoutParams(-1,-2));root.addView(scroll,new LinearLayout.LayoutParams(-1,-2));footer=column(a);root.addView(footer);dialog.setContentView(root);
            Window win=dialog.getWindow();if(win!=null){win.setBackgroundDrawable(new ColorDrawable(Color.TRANSPARENT));win.addFlags(WindowManager.LayoutParams.FLAG_DIM_BEHIND);WindowManager.LayoutParams p=win.getAttributes();p.dimAmount=.36f;p.gravity=Gravity.BOTTOM;win.setAttributes(p);win.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_ADJUST_RESIZE);}
            root.getViewTreeObserver().addOnGlobalLayoutListener(this::adaptKeyboardSpace);
        }
        public void show(){Window before=dialog.getWindow();if(before!=null)before.setWindowAnimations(animations()?R.style.FrociartSheetMotion:0);dialog.show();Window w=dialog.getWindow();if(w!=null){
                int available=root.getResources().getDisplayMetrics().widthPixels;
                if(Build.VERSION.SDK_INT>=30){WindowMetrics metrics=owner.getWindowManager().getCurrentWindowMetrics();Insets bars=metrics.getWindowInsets().getInsetsIgnoringVisibility(WindowInsets.Type.systemBars()|WindowInsets.Type.displayCutout());available=metrics.getBounds().width()-bars.left-bars.right;}
                else{Rect frame=new Rect();owner.getWindow().getDecorView().getWindowVisibleDisplayFrame(frame);if(frame.width()>0)available=frame.width();}
                int max=dp(root.getContext(),640);boolean tablet=root.getResources().getConfiguration().smallestScreenWidthDp>=600;int margin=tablet?dp(owner,48):0;w.setLayout(Math.max(1,Math.min(Math.max(1,available-margin),max)),-2);w.setGravity(tablet?Gravity.CENTER:Gravity.BOTTOM);w.setNavigationBarColor(surface());w.getDecorView().setSystemUiVisibility(dark?0:View.SYSTEM_UI_FLAG_LIGHT_NAVIGATION_BAR|View.SYSTEM_UI_FLAG_LIGHT_STATUS_BAR);if(Build.VERSION.SDK_INT>=30&&w.getInsetsController()!=null){int mask=WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS|WindowInsetsController.APPEARANCE_LIGHT_NAVIGATION_BARS;w.getInsetsController().setSystemBarsAppearance(dark?0:mask,mask);}}
            if(root.getResources().getConfiguration().screenHeightDp<440)compactHeader(true);
            stagger(body);
        }
        /** On very short IME windows, scroll the whole form rather than squeezing its fields to zero. */
        private void adaptKeyboardSpace(){
            if(!dialog.isShowing())return;Rect visible=new Rect();root.getWindowVisibleDisplayFrame(visible);View focus=dialog.getCurrentFocus();
            boolean ime=false;if(Build.VERSION.SDK_INT>=30){WindowInsets insets=root.getRootWindowInsets();ime=insets!=null&&insets.isVisible(WindowInsets.Type.ime());}else ime=owner.getResources().getDisplayMetrics().heightPixels-visible.height()>dp(owner,100);
            boolean fixed=false;for(int i=0;i<root.getChildCount();i++){View child=root.getChildAt(i);if(child!=handleView&&child!=headingRow&&child!=headerGap&&child!=scroll&&child!=footer&&child.getVisibility()!=View.GONE)fixed=true;}
            boolean tight=ime&&visible.height()<dp(owner,260)&&scroll.getVisibility()==View.VISIBLE&&!fixed&&(keyboardScroll||inside(focus,body));
            if(tight!=keyboardScroll){
                keyboardScroll=tight;
                if(tight){root.removeView(headingRow);root.removeView(footer);body.addView(headingRow,0,new LinearLayout.LayoutParams(-1,-2));body.addView(footer,new LinearLayout.LayoutParams(-1,-2));handleView.setVisibility(View.GONE);headerGap.setVisibility(View.GONE);pad(root,20,0,20,0);}
                else{body.removeView(headingRow);body.removeView(footer);root.addView(headingRow,1,new LinearLayout.LayoutParams(-1,-2));root.addView(footer,new LinearLayout.LayoutParams(-1,-2));handleView.setVisibility(headerCompact?View.GONE:View.VISIBLE);headerGap.setVisibility(headerCompact?View.GONE:View.VISIBLE);pad(root,20,headerCompact?0:8,20,headerCompact?8:16);}
                root.requestLayout();
            }
            EditText next=tight&&focus instanceof EditText?(EditText)focus:null;
            if(next!=compactField){
                if(compactField!=null){compactField.setMinLines(savedMinLines);compactField.setMaxLines(savedMaxLines);}
                compactField=next;
                if(next!=null){savedMinLines=next.getMinLines();savedMaxLines=next.getMaxLines();next.setMinLines(1);next.setMaxLines(1);next.post(()->{if(dialog.isShowing()&&compactField==next)next.requestRectangleOnScreen(new Rect(0,0,next.getWidth(),next.getHeight()),true);});}
            }
        }
        private boolean inside(View view,View ancestor){for(View current=view;current!=null;){if(current==ancestor)return true;android.view.ViewParent parent=current.getParent();current=parent instanceof View?(View)parent:null;}return false;}
        public View headerAction(String icon,String label,Runnable action){Button button=new Button(root.getContext(),"",icon,0);button.setContentDescription(label);button.setTooltipText(label);button.setOnClickListener(v->action.run());LinearLayout.LayoutParams size=lp(root.getContext(),48,48);size.setMarginEnd(dp(root.getContext(),8));headingRow.addView(button,Math.max(0,headingRow.getChildCount()-1),size);return button;}
        public void fixed(View view){root.addView(view,root.indexOfChild(scroll),new LinearLayout.LayoutParams(-1,-2));}
        public void compactHeader(boolean compact){if(headerCompact==compact)return;headerCompact=compact;if(keyboardScroll){headingText.setTextSize(compact?16:24);return;}handleView.setVisibility(compact?View.GONE:View.VISIBLE);headerGap.setVisibility(compact?View.GONE:View.VISIBLE);headingText.setTextSize(compact?16:24);pad(root,20,compact?0:8,20,compact?8:16);}
        /** Input owners may temporarily free the heading only while an extreme IME window is active. */
        public void hideHeadingForInput(boolean hidden){
            if(headingHiddenForInput==hidden)return;headingHiddenForInput=hidden;
            headingRow.setVisibility(hidden?View.GONE:View.VISIBLE);
            handleView.setVisibility(hidden||headerCompact||keyboardScroll?View.GONE:View.VISIBLE);
            headerGap.setVisibility(hidden||headerCompact||keyboardScroll?View.GONE:View.VISIBLE);
        }
        /** Reading positions only. Form owners remain responsible for preserving their actual drafts. */
        public static final class ReadingState {
            final java.util.ArrayList<int[]> selections=new java.util.ArrayList<>();int focused=-1,scrollY;
        }
        private void editFields(View view,java.util.List<EditText> fields){if(view instanceof EditText)fields.add((EditText)view);else if(view instanceof ViewGroup)for(int i=0;i<((ViewGroup)view).getChildCount();i++)editFields(((ViewGroup)view).getChildAt(i),fields);}
        public ReadingState snapshotReading(){ReadingState state=new ReadingState();state.scrollY=scroll.getScrollY();java.util.ArrayList<EditText> fields=new java.util.ArrayList<>();editFields(body,fields);for(EditText field:fields){if(field.hasFocus())state.focused=state.selections.size();state.selections.add(new int[]{field.getSelectionStart(),field.getSelectionEnd()});}return state;}
        public void restoreReading(ReadingState state){if(state==null)return;root.post(()->{if(!dialog.isShowing())return;java.util.ArrayList<EditText> fields=new java.util.ArrayList<>();editFields(body,fields);for(int i=0;i<Math.min(fields.size(),state.selections.size());i++){EditText field=fields.get(i);int[] range=state.selections.get(i);if(i==state.focused&&field.getVisibility()==View.VISIBLE)field.requestFocus();if(range[0]>=0&&range[1]>=0)field.setSelection(Math.min(field.length(),range[0]),Math.min(field.length(),range[1]));}scroll.post(()->{if(dialog.isShowing())scroll.scrollTo(0,state.scrollY);});});}
        public void dismiss(){dialog.dismiss();}
        public void dismissNow(){forcedDismiss=true;try{dialog.dismiss();}finally{forcedDismiss=false;}}
        public Button action(String label,String icon,int tone,Runnable action){Button b=new Button(root.getContext(),label,icon,tone);b.setSingleLine(false);b.setMaxLines(3);b.setEllipsize(null);b.setMinimumHeight(dp(root.getContext(),54));b.setOnClickListener(v->action.run());body.addView(b,new LinearLayout.LayoutParams(-1,-2));gap(body,8);return b;}
        public Choice choice(String title,String detail,int color,Runnable action){Choice row=new Choice(root.getContext());row.bind(title,detail,color);row.setOnClickListener(v->action.run());body.addView(row,new LinearLayout.LayoutParams(-1,-2));gap(body,8);return row;}
        public void primary(String label,String icon,Runnable action){gap(footer,12);Button b=new Button(root.getContext(),label,icon,1);b.setSingleLine(false);b.setMaxLines(3);b.setEllipsize(null);b.setMinimumHeight(dp(root.getContext(),54));b.setOnClickListener(v->action.run());footer.addView(b,new LinearLayout.LayoutParams(-1,-2));scroll.maxHeight=(int)(root.getResources().getDisplayMetrics().heightPixels*.61f);}
        public void section(String title){TextView heading=text(root.getContext(),title,11,true);heading.setTextColor(muted());pad(heading,0,12,0,8);body.addView(heading,new LinearLayout.LayoutParams(-1,-2));}
        public void paragraph(String value){TextView t=note(root.getContext(),value);body.addView(t);gap(body,14);}
    }
}
