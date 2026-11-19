package it.frociart.app;

import android.content.Context;
import android.media.AudioAttributes;
import android.os.Build;
import android.os.VibrationAttributes;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.provider.Settings;
import android.view.HapticFeedbackConstants;
import android.view.View;

/** Short tactile milestones synchronized with a stationary hold, never a drag loop. */
final class HoldHaptics {
    private static final float[] MARKS={.08f,.30f,.50f,.68f,.82f,.92f};
    private static final float[] STRENGTH={.08f,.13f,.21f,.33f,.50f,.75f};
    private final View view;
    private final Vibrator vibrator;
    private int stage=-1;
    private boolean playing;
    HoldHaptics(View view){this.view=view;vibrator=(Vibrator)view.getContext().getSystemService(Context.VIBRATOR_SERVICE);}
    private boolean enabled(){
        if(!view.isHapticFeedbackEnabled()||!view.isShown()||!view.hasWindowFocus())return false;
        try{return Settings.System.getInt(view.getContext().getContentResolver(),Settings.System.HAPTIC_FEEDBACK_ENABLED,1)!=0;}
        catch(SecurityException unavailable){return false;}
    }
    void progress(float amount){
        int next=-1;for(int i=0;i<MARKS.length;i++)if(amount>=MARKS[i])next=i;
        if(next<=stage)return;stage=next;
        // Skip missed milestones after a busy frame instead of firing a burst.
        if(next>=0)play(STRENGTH[next],false,next);
    }
    void confirm(){play(.95f,true,MARKS.length);}
    private void play(float strength,boolean confirm,int milestone){
        if(!enabled())return;
        try{
            if(vibrator!=null&&vibrator.hasVibrator()){
                int primitive=confirm?VibrationEffect.Composition.PRIMITIVE_CLICK:VibrationEffect.Composition.PRIMITIVE_TICK;
                VibrationEffect effect=null;
                if(Build.VERSION.SDK_INT>=31&&vibrator.areAllPrimitivesSupported(primitive))
                    effect=VibrationEffect.startComposition().addPrimitive(primitive,strength).compose();
                else if(vibrator.hasAmplitudeControl()){
                    int amplitude=Math.max(1,Math.min(240,Math.round(strength*220)));
                    effect=VibrationEffect.createWaveform(new long[]{2,7,3},new int[]{0,amplitude,0},-1);
                }
                if(effect!=null){
                    if(Build.VERSION.SDK_INT>=33)vibrator.vibrate(effect,new VibrationAttributes.Builder().setUsage(VibrationAttributes.USAGE_TOUCH).build());
                    else vibrator.vibrate(effect,new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ASSISTANCE_SONIFICATION).setContentType(AudioAttributes.CONTENT_TYPE_SONIFICATION).build());
                    playing=true;return;
                }
            }
            // Unsupported amplitude: sparse system ticks, without pretending to scale them.
            if(confirm||milestone==0||milestone==3||milestone==5)
                view.performHapticFeedback(confirm?HapticFeedbackConstants.LONG_PRESS:HapticFeedbackConstants.CLOCK_TICK);
        }catch(SecurityException unavailable){
            // A device policy can disable vibration; the visual gesture remains available.
        }
    }
    void cancel(){if(playing&&vibrator!=null){try{vibrator.cancel();}catch(SecurityException unavailable){}}playing=false;stage=-1;}
}
