using System.Text.RegularExpressions;

namespace Axiom.Core;

public sealed class DocumentSession
{
    private DocumentSession(string          draftKey
                          , string?         path
                          , string          text
                          , DateTimeOffset? writeUtc)
    {
        DraftKey          = draftKey;
        Path              = path;
        Text              = text;
        SavedText         = text;
        LastKnownWriteUtc = writeUtc;
    }

    public string DraftKey { get; }
    public string? Path { get; private set; }
    public string Text { get; private set; }
    public string SavedText { get; private set; }
    public string ObservedEncoding { get; private set; } = "Unsaved — writes UTF-8";
    public DateTimeOffset? LastKnownWriteUtc { get; private set; }
    public ExternalConflict? Conflict { get; private set; }
    public bool IsDirty => !string.Equals(Text, SavedText, StringComparison.Ordinal);
    public bool IsNew => Path is null;
    public Guid? DocumentId => DocumentIdCodec.Find(Text);

    public static DocumentSession New()
    {
        return new($"unsaved:{Guid.NewGuid():N}"
                 , null
                 , string.Empty
                 , null);
    }

    public static DocumentSession Open(DocumentFile file)
    {
        return new($"file:{System.IO.Path.GetFullPath(file.Path).ToUpperInvariant()}"
                 , file.Path
                 , file.Text
                 , file.LastWriteUtc) { ObservedEncoding = file.ObservedEncoding };
    }

    public static DocumentSession Recover(RecoveryDraft draft
                                        , string        savedText = "")
    {
        return new(draft.DraftKey
                 , draft.FilePath
                 , draft.Text
                 , draft.SavedFileWriteUtc)
               {
                   SavedText        = savedText
                 , ObservedEncoding = draft.FilePath is null ? "Unsaved — writes UTF-8" : "Recovered — encoding unobserved"
               };
    }

    public void UpdateText(string text)
    {
        Text = text ?? string.Empty;
    }

    public void MarkSaved(string         path
                        , DateTimeOffset writeUtc)
    {
        Path              = path;
        SavedText         = Text;
        LastKnownWriteUtc = writeUtc;
        ObservedEncoding  = "UTF-8 (no BOM)";
        Conflict          = null;
    }

    public void Reload(DocumentFile file)
    {
        Path              = file.Path;
        Text              = file.Text;
        SavedText         = file.Text;
        ObservedEncoding  = file.ObservedEncoding;
        LastKnownWriteUtc = file.LastWriteUtc;
        Conflict          = null;
    }

    public void RecordConflict(DocumentFile external)
    {
        Conflict = new ExternalConflict(Text, external.Text, external.LastWriteUtc);
    }

    public void KeepLocalAfterConflict()
    {
        if (Conflict is null)
        {
            return;
        }

        LastKnownWriteUtc = Conflict.ExternalWriteUtc;
        SavedText         = Conflict.ExternalText;
        Conflict          = null;
    }

    public void DiscardChanges()
    {
        Text     = SavedText;
        Conflict = null;
    }

    public void UpdatePath(string path) => Path = path;
}
