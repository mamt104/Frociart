package it.frociart.app;

import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.Set;

/** Learning state independent from Android. A completed lesson earns XP only once. */
public final class FlowLearningProgress {
    private final LinkedHashSet<String> completed=new LinkedHashSet<>();
    private final LinkedHashSet<String> review=new LinkedHashSet<>();
    private final LinkedHashSet<String> practices=new LinkedHashSet<>();
    public String activeId="";
    public int question=-1,answer=-1,firstTry=0;
    public boolean mistake=false;

    public Set<String> completed(){return Collections.unmodifiableSet(completed);}
    public boolean done(String id){return completed.contains(id);}
    public Set<String> review(){return Collections.unmodifiableSet(review);}
    public FlowLearning.Lesson reviewLesson(){for(FlowLearning.Lesson l:FlowLearning.lessons())for(FlowLearning.Question q:l.questions)if(review.contains(q.id))return l;return null;}
    public void restoreReview(Set<String> saved){review.clear();if(saved!=null)for(FlowLearning.Lesson l:FlowLearning.lessons())for(FlowLearning.Question q:l.questions)if(saved.contains(q.id))review.add(q.id);}
    public Set<String> practices(){return Collections.unmodifiableSet(practices);}
    public boolean practiceDone(String id){return practices.contains(id);}
    public boolean completePractice(String id){return FlowPractice.mission(id)!=null&&practices.add(id);}
    public void restorePractices(Set<String> saved){practices.clear();if(saved!=null)for(FlowPractice.Mission m:FlowPractice.all())if(saved.contains(m.id))practices.add(m.id);}
    public int xp(){return completed.size()*30+practices.size()*20;}
    public FlowLearning.Lesson active(){return FlowLearning.lesson(activeId);}
    public boolean finished(){FlowLearning.Lesson l=active();return l!=null&&question>=l.questions.length;}
    public boolean correct(){FlowLearning.Lesson l=active();return l!=null&&question>=0&&question<l.questions.length&&answer==l.questions[question].correct;}
    public FlowLearning.Lesson recommended(){
        FlowLearning.Lesson current=active();if(current!=null&&!finished())return current;
        for(FlowLearning.Lesson l:FlowLearning.lessons())if(!done(l.id))return l;
        return FlowLearning.lessons()[0];
    }
    public void begin(String id,boolean restart){
        if(FlowLearning.lesson(id)==null)return;
        if(!restart&&id.equals(activeId)&&!finished())return;
        activeId=id;question=-1;answer=-1;firstTry=0;mistake=false;
    }
    public void start(){if(active()!=null&&question==-1)question=0;}
    public boolean choose(int option){
        FlowLearning.Lesson l=active();if(l==null||question<0||question>=l.questions.length||answer!=-1)return false;
        FlowLearning.Question q=l.questions[question];if(option<0||option>=q.options.length)return false;
        answer=option;if(option!=q.correct){mistake=true;review.add(q.id);}else if(!mistake)review.remove(q.id);
        return true;
    }
    public void retry(){if(answer>=0&&!correct())answer=-1;}
    public boolean advance(){
        if(!correct())return false;
        if(!mistake)firstTry++;
        question++;answer=-1;mistake=false;
        if(finished())completed.add(activeId);
        return true;
    }
    public void restore(Set<String> saved,String id,int step,int chosen,boolean wrong,int clean){
        completed.clear();review.clear();if(saved!=null)for(FlowLearning.Lesson l:FlowLearning.lessons())if(saved.contains(l.id))completed.add(l.id);
        activeId="";question=-1;answer=-1;mistake=false;firstTry=0;
        FlowLearning.Lesson l=FlowLearning.lesson(id);if(l==null)return;
        activeId=id;question=Math.max(-1,Math.min(step,l.questions.length));
        // A result screen is meaningful only for a lesson already committed as complete.
        if(question==l.questions.length&&!done(id))question=-1;
        firstTry=Math.max(0,Math.min(clean,Math.max(0,question)));
        if(question>=0&&question<l.questions.length){
            answer=chosen>=0&&chosen<l.questions[question].options.length?chosen:-1;
            mistake=wrong||(answer>=0&&answer!=l.questions[question].correct);
        }
    }
}
