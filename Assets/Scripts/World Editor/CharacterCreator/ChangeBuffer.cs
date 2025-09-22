using System.Collections.Generic;

public interface ICommand { void Do(); void Undo(); string Label { get; } }

public sealed class ValueChangeCommand : ICommand
{
    readonly StatsService svc; readonly string path; readonly object before; readonly object after; public string Label { get; }
    public ValueChangeCommand(StatsService svc, string path, object before, object after, string label = null)
    { this.svc=svc; this.path=path; this.before=before; this.after=after; this.Label=label??path; }
    public void Do()   { svc.SetValue(path, after); svc.CommitAndRecompute(); }
    public void Undo() { svc.SetValue(path, before); svc.CommitAndRecompute(); }
}

public static class ChangeBuffer
{
    public static bool CanUndo => undo.Count > 0;
    public static bool CanRedo => redo.Count > 0;
    static readonly Stack<ICommand> undo = new(); static readonly Stack<ICommand> redo = new();
    static bool batching; static string label; static StatsService svc; static List<(string path, object before, object after)> edits;

    public static void BeginEdit(StatsService s, string editLabel)
    { batching=true; label=editLabel; svc=s; edits ??= new(); edits.Clear(); }

    public static void Record(string path, object before, object after)
    {
        if (!batching) return;
        // keep only the first before and latest after for a path
        for (int i=0;i<edits.Count;i++)
            if (edits[i].path==path) { edits[i]=(path, edits[i].before, after); return; }
        edits.Add((path, before, after));
    }

    public static void EndEdit()
    {
        if (!batching) return;
        if (edits.Count==1)
        {
            var e=edits[0];
            var cmd=new ValueChangeCommand(svc, e.path, e.before, e.after, label);
            cmd.Do(); undo.Push(cmd); redo.Clear();
        }
        else if (EditsCount()>1)
        {
            // simple macro: apply all, undo reverses all
            var local = new List<ValueChangeCommand>();
            foreach (var e in edits) local.Add(new ValueChangeCommand(svc, e.path, e.before, e.after, label));
            var macro = new Macro(local.ToArray(), label);
            macro.Do(); undo.Push(macro); redo.Clear();
        }
        batching=false; edits?.Clear(); label=null; svc=null;
    }

    static int EditsCount()=> edits?.Count ?? 0;

    sealed class Macro : ICommand
    {
        readonly ValueChangeCommand[] cmds; public string Label { get; }
        public Macro(ValueChangeCommand[] c, string label){cmds=c; Label=label;}
        public void Do(){ foreach (var c in cmds) c.Do(); }
        public void Undo(){ for (int i=cmds.Length-1;i>=0;i--) cmds[i].Undo(); }
    }

    public static void Undo(){ if (undo.Count==0) return; var c=undo.Pop(); c.Undo(); redo.Push(c); }
    public static void Redo(){ if (redo.Count==0) return; var c=redo.Pop(); c.Do();   undo.Push(c); }
}
