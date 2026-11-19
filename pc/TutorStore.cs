using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Frociart
{
    [DataContract] internal sealed class TutorDraft
    {
        [DataMember] public string Node, Text, Prompt;
    }
    [DataContract] internal sealed class TutorSession
    {
        [DataMember] public int Version = 1;
        [DataMember] public string Id;
        [DataMember] public Diagram Document;
        [DataMember] public int Step;
        [DataMember] public bool Completed;
        [DataMember] public bool Demonstrated;
        [DataMember] public string RuntimeValue = "";
        [DataMember] public List<TutorDraft> Drafts = new List<TutorDraft>();
    }
    internal sealed class TutorStore
    {
        private readonly string directory;
        private readonly HashSet<string> recovering = new HashSet<string>();
        private readonly HashSet<string> blocked = new HashSet<string>();
        internal string Error { get; private set; }
        internal TutorStore(string location = null)
        {
            string custom = Environment.GetEnvironmentVariable("FROCIART_DATA_DIR");
            directory = location ?? Path.Combine(String.IsNullOrWhiteSpace(custom) ?
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Frociart") : custom, "tutor-v1");
        }
        private string FileFor(string id)
        {
            if (id != "workshop" && Tutor.Lesson(id) == null) throw new InvalidDataException("Esercizio sconosciuto.");
            return Path.Combine(directory, id + ".json");
        }
        private static byte[] Serialize(TutorSession state)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(TutorSession)).WriteObject(stream, state); return stream.ToArray(); }
        }
        internal static TutorSession Copy(TutorSession state) { return Deserialize(Serialize(state), state.Id); }
        private static TutorSession Deserialize(byte[] bytes, string id)
        {
            if (bytes.Length == 0 || bytes.Length > 8 * 1024 * 1024) throw new InvalidDataException("Il salvataggio è vuoto o troppo grande.");
            DiagramIO.CheckJsonEnvelope(bytes);
            TutorSession result;
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, DiagramIO.JsonQuotas(8 * 1024 * 1024)))
                result = (TutorSession)new DataContractJsonSerializer(typeof(TutorSession),
                    new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 40000 }).ReadObject(reader);
            if (result == null || result.Version != 1 || result.Id != id || result.Document == null || result.Drafts == null)
                throw new InvalidDataException("Il salvataggio appartiene a una versione diversa o non è leggibile.");
            DiagramIO.CheckDocument(result.Document);
            if (result.Step < 0 || result.Step > (id == "workshop" ? 0 : Tutor.Lesson(id).steps.Length) || result.Drafts.Count > DiagramIO.MaxNodes)
                throw new InvalidDataException("I passi salvati non sono validi.");
            var ids = new HashSet<string>();
            foreach (var draft in result.Drafts)
                if (draft == null || String.IsNullOrEmpty(draft.Node) || !ids.Add(draft.Node) ||
                    Tutor.Find(result.Document, draft.Node) == null || draft.Text == null || draft.Text.Length > DiagramIO.MaxBlockText || draft.Prompt == null || draft.Prompt.Length > 1024)
                    throw new InvalidDataException("La bozza non è leggibile.");
            if (result.RuntimeValue == null || result.RuntimeValue.Length > Values.MaxStringLength) throw new InvalidDataException("Il dato salvato è troppo lungo.");
            return result;
        }
        private static byte[] ReadBytes(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("Dimensione non valida.");
                using (var buffer = new MemoryStream()) { stream.CopyTo(buffer); return buffer.ToArray(); }
            }
        }
        private static TutorSession Read(string path, string id) { return Deserialize(ReadBytes(path), id); }
        internal TutorSession Load(string id, Diagram seed)
        {
            Error = null; string file = FileFor(id);
            if (!File.Exists(file) && !File.Exists(file + ".bak")) return new TutorSession { Id = id, Document = DiagramIO.Clone(seed) };
            try { var state = Read(file, id); blocked.Remove(id); return state; }
            catch (Exception primary)
            {
                // A newer version is protected even when an older backup exists.
                try {
                    if (File.Exists(file)) {
                        string text;using(var bytes=new MemoryStream(ReadBytes(file)))using(var reader=new StreamReader(bytes))text=reader.ReadToEnd();
                        if (System.Text.RegularExpressions.Regex.IsMatch(text, "\"Version\"\\s*:\\s*([2-9]|[1-9][0-9]+)"))
                            throw new InvalidDataException("Questo esercizio è stato salvato da una versione più recente. Mantengo entrambi i file.");
                    }
                    var state = Read(file + ".bak", id); recovering.Add(id); blocked.Remove(id);
                    Error = "Ripristinata l'ultima copia valida. Il file danneggiato è ancora intatto."; return state;
                }
                catch (Exception) {
                    blocked.Add(id);
                    throw new IOException("Non riesco a riaprire questo esercizio. I file restano intatti; nessun foglio vuoto li sostituirà.", primary);
                }
            }
        }
        internal bool Save(TutorSession state)
        {
            Error = null; string temporary = null;
            try
            {
                if (blocked.Contains(state.Id)) throw new IOException("La copia esistente non è leggibile: non la sovrascrivo.");
                byte[] bytes = Serialize(state); Deserialize(bytes, state.Id);
                Directory.CreateDirectory(directory); string file = FileFor(state.Id);
                temporary = Path.Combine(directory, ".tutor-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                // Check the exact candidate before replacing the previous durable snapshot.
                Read(temporary, state.Id);
                if (File.Exists(file)) File.Replace(temporary, file, recovering.Contains(state.Id) ? null : file + ".bak");
                else File.Move(temporary, file);
                temporary = null;
                if (!File.ReadAllBytes(file).SequenceEqual(bytes)) throw new IOException("La rilettura non coincide con il salvataggio.");
                recovering.Remove(state.Id); return true;
            }
            catch (Exception ex) { Error = "Salvataggio non riuscito. Il foglio resta aperto: riprova. " + ex.Message; return false; }
            finally { if (temporary != null) try { File.Delete(temporary); } catch { } }
        }
        internal int CompletedCount()
        {
            int count = 0;
            foreach (var lesson in Tutor.Lessons)
            {
                string file = FileFor(lesson.id);
                try { if (Read(file, lesson.id).Completed) count++; }
                catch { try { if (Read(file + ".bak", lesson.id).Completed) count++; } catch { } }
            }
            return count;
        }
        internal TutorSession Peek(string id)
        {
            try { return Read(FileFor(id), id); }
            catch { try { return Read(FileFor(id) + ".bak", id); } catch { return null; } }
        }
        internal DateTime LastTouched(string id)
        { try { return File.GetLastWriteTimeUtc(FileFor(id)); } catch { return DateTime.MinValue; } }
    }
}
