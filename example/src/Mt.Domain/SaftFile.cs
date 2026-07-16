using Mt.Results;

namespace Mt.Domain;

/// <summary>
/// The exported SAF-T file: a name plus opaque bytes. Contents don't matter to the
/// workshop — it is downloaded from Source and uploaded to Target (§6.1).
/// </summary>
public sealed record SaftFile
{
    private SaftFile(string fileName, byte[] content)
    {
        FileName = fileName;
        Content = content;
    }

    public string FileName { get; }

    public byte[] Content { get; }

    public static Result<SaftFile> Create(string fileName, byte[] content) =>
        fileName
            .FailWhen(string.IsNullOrWhiteSpace, "SAF-T file name must not be empty.")
            .Then(_ => new SaftFile(fileName, content));
}
