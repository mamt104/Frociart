using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Frociart {
    // The embedded DataContract serializer populates these fields.
#pragma warning disable 0649
    [DataContract] internal sealed class LearningUnit {
        [DataMember] public string id, title, subtitle;
    }
    [DataContract] internal sealed class LearningSource {
        [DataMember] public string id, title, url;
    }
    [DataContract] internal sealed class LearningQuestion {
        [DataMember] public string id, prompt, code;
        [DataMember] public string[] options, feedback;
        [DataMember] public int correct;
    }
    [DataContract] internal sealed class LearningLesson {
        [DataMember] public string id, unitId, title, goal, explanation, tip, counterexample, practiceExample;
        [DataMember] public LearningSource[] sources;
        [DataMember] public LearningQuestion[] questions;
    }
    [DataContract] internal sealed class LearningExample {
        [DataMember] public string title, json;
    }
    [DataContract] internal sealed class LearningContent {
        [DataMember] public int contentVersion;
        [DataMember] public string intro, pseudocodeNote;
        [DataMember] public LearningUnit[] units;
        [DataMember] public LearningLesson[] lessons;
        [DataMember] public LearningExample[] examples;
        private static LearningContent current;
        internal static LearningContent Current {
            get {
                if(current==null)using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Frociart.Learning.lessons.json")){
                    if(stream==null)throw new InvalidDataException("Il percorso Impara non è incluso in questa build.");
                    current=(LearningContent)new DataContractJsonSerializer(typeof(LearningContent)).ReadObject(stream);
                    current.Validate();
                }
                return current;
            }
        }
        internal LearningLesson Lesson(string id){return lessons.FirstOrDefault(l=>l.id==id);}
        internal Diagram Practice(string title){
            var example=examples.FirstOrDefault(e=>e.title==title);
            if(example==null)return DiagramIO.CreateBlank();
            using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(example.json))){
                var diagram=(Diagram)new DataContractJsonSerializer(typeof(Diagram)).ReadObject(stream);
                return DiagramIO.Clone(diagram);
            }
        }
        private void Validate(){
            if(contentVersion!=1||units==null||lessons==null||examples==null||units.Length!=6||lessons.Length!=24)
                throw new InvalidDataException("Percorso Impara incompleto.");
            var ids=new HashSet<string>();
            foreach(var lesson in lessons){
                if(!ids.Add(lesson.id)||!units.Any(u=>u.id==lesson.unitId)||lesson.questions==null||lesson.questions.Length!=3)
                    throw new InvalidDataException("Lezione non valida: "+lesson.id);
                foreach(var q in lesson.questions)
                    if(!ids.Add(q.id)||q.options==null||q.feedback==null||q.options.Length!=q.feedback.Length||q.correct<0||q.correct>=q.options.Length)
                        throw new InvalidDataException("Esercizio non valido: "+q.id);
                foreach(var source in lesson.sources??new LearningSource[0]){
                    Uri uri;if(!Uri.TryCreate(source.url,UriKind.Absolute,out uri)||uri.Scheme!="https")
                        throw new InvalidDataException("Fonte non valida.");
                }
                if(!String.IsNullOrEmpty(lesson.practiceExample)&&!examples.Any(e=>e.title==lesson.practiceExample))
                    throw new InvalidDataException("Esempio della lezione non disponibile.");
            }
        }
    }
#pragma warning restore 0649
    [DataContract] internal sealed class LearningState {
        [DataMember] public int Version=2;
        [DataMember] public bool Grid=true;
        [DataMember] public List<string> Completed=new List<string>();
        [DataMember] public List<string> Review=new List<string>();
        [DataMember] public string Active="";
        [DataMember] public int Question=-1, Answer=-1, FirstTry;
        [DataMember] public bool Mistake;
        [OnDeserializing] private void Defaults(StreamingContext context){Version=1;Grid=true;Completed=new List<string>();Review=new List<string>();Active="";Question=Answer=-1;}
        internal LearningLesson ReviewLesson {get{return LearningContent.Current.lessons.FirstOrDefault(l=>l.questions.Any(q=>Review.Contains(q.id)));}}
        internal bool Done(string id){return Completed.Contains(id);}
        internal int Xp {get{return Completed.Count*30;}}
        internal LearningLesson Lesson {get{return LearningContent.Current.Lesson(Active);}}
        internal bool Finished {get{return Lesson!=null&&Question>=Lesson.questions.Length;}}
        internal bool Correct {get{return Lesson!=null&&Question>=0&&Question<Lesson.questions.Length&&Answer==Lesson.questions[Question].correct;}}
        internal LearningLesson Recommended {
            get {return Lesson!=null&&!Finished?Lesson:LearningContent.Current.lessons.FirstOrDefault(l=>!Done(l.id))??LearningContent.Current.lessons[0];}
        }
        internal void Begin(string id,bool restart){
            if(LearningContent.Current.Lesson(id)==null)return;
            if(!restart&&Active==id&&!Finished)return;
            Active=id;Question=Answer=-1;FirstTry=0;Mistake=false;
        }
        internal void Start(){if(Lesson!=null&&Question==-1)Question=0;}
        internal bool Choose(int index){
            if(Lesson==null||Question<0||Finished||Answer!=-1||index<0||index>=Lesson.questions[Question].options.Length)return false;
            Answer=index;string id=Lesson.questions[Question].id;
            if(!Correct){Mistake=true;if(!Review.Contains(id))Review.Add(id);}
            else if(!Mistake)Review.Remove(id);
            return true;
        }
        internal void Retry(){if(Answer>=0&&!Correct)Answer=-1;}
        internal bool Advance(){
            if(!Correct)return false;if(!Mistake)FirstTry++;
            Question++;Answer=-1;Mistake=false;if(Finished&&!Done(Active))Completed.Add(Active);return true;
        }
        internal void Normalize(){
            if(Version<1||Version>2)throw new NotSupportedException("La versione dei progressi non è supportata da questa build.");
            Version=2;
            var questionIds=new HashSet<string>(LearningContent.Current.lessons.SelectMany(l=>l.questions).Select(q=>q.id));
            Review=(Review??new List<string>()).Where(questionIds.Contains).Distinct().ToList();
            Completed=(Completed??new List<string>()).Where(id=>LearningContent.Current.Lesson(id)!=null).Distinct().ToList();
            if(Lesson==null){Active="";Question=Answer=-1;FirstTry=0;Mistake=false;return;}
            Question=Math.Max(-1,Math.Min(Question,Lesson.questions.Length));
            if(Finished&&!Done(Active))Question=-1;
            FirstTry=Math.Max(0,Math.Min(FirstTry,Math.Max(0,Question)));
            if(Question<0||Finished)Answer=-1;
            else if(Answer<0||Answer>=Lesson.questions[Question].options.Length)Answer=-1;
            if(Answer>=0&&!Correct)Mistake=true;
        }
    }
    internal sealed class LearningStore {
        internal readonly string PathName;
        internal LearningState State=new LearningState();
        internal string Error;
        private bool recovered,unreadable;
        internal LearningStore(string path=null){
            string data=Environment.GetEnvironmentVariable("FROCIART_DATA_DIR");
            PathName=path??Path.Combine(String.IsNullOrEmpty(data)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Frociart"):data,"impara.json");
            if(!File.Exists(PathName))return;
            try{State=Read(PathName);}
            catch(NotSupportedException){Error="I progressi appartengono a un'altra versione. Il file è conservato; questa sessione resta in memoria.";unreadable=true;}
            catch{
                try{State=Read(PathName+".bak");recovered=true;Error="Progressi recuperati dalla copia di sicurezza.";}
                catch{Error="Non riesco a leggere i progressi precedenti. Il file è conservato; questa sessione resta in memoria.";unreadable=true;}
            }
        }
        private static LearningState Read(string path){
            using(var stream=File.OpenRead(path)){
                if(stream.Length>65536)throw new InvalidDataException();
                byte[] bytes;using(var buffer=new MemoryStream()){stream.CopyTo(buffer);bytes=buffer.ToArray();}DiagramIO.CheckJsonEnvelope(bytes);
                using(var reader=JsonReaderWriterFactory.CreateJsonReader(bytes,DiagramIO.JsonQuotas(65536))){
                    var state=(LearningState)new DataContractJsonSerializer(typeof(LearningState),
                        new DataContractJsonSerializerSettings{MaxItemsInObjectGraph=40000}).ReadObject(reader);state.Normalize();return state;
                }
            }
        }
        internal bool Save(){
            if(unreadable){Error="I progressi precedenti non sono leggibili. Questa sessione resta in memoria.";return false;}
            string temp=PathName+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                Directory.CreateDirectory(Path.GetDirectoryName(PathName));
                using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                    new DataContractJsonSerializer(typeof(LearningState)).WriteObject(stream,State);stream.Flush(true);
                }
                if(File.Exists(PathName))File.Replace(temp,PathName,recovered?null:PathName+".bak");else File.Move(temp,PathName);
                recovered=false;Error=null;return true;
            }catch{Error="Progressi non salvati: restano in memoria. Riprova con la prossima risposta.";return false;}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch{}}
        }
    }
}
