namespace MyPlanner.Service.Interfaces;

/// <summary>
/// Owns the disk for every stored upload. A Bill Image and a Statement File land in the same flat
/// <strong>Bucket</strong>, each under its own content hash, and nothing here knows what kind of file
/// it is holding — the Bucket has no subfolders and no notion of origin (D2, Q9/Q19).
/// </summary>
public interface IBucketStore
{
    /// <summary>
    /// Stores the bytes verbatim under a <strong>FileKey</strong> (<c>&lt;sha256-hex&gt;.&lt;ext&gt;</c>)
    /// and returns it. Identical bytes already on disk are not written again: the existing FileKey comes
    /// back, with the extension the first write gave it.
    /// </summary>
    /// <param name="content">The upload, read from its current position.</param>
    /// <param name="contentType">What the uploader declared; decides only the extension.</param>
    Task<string> SaveAsync(Stream content, string contentType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a stored upload for reading. Nothing in the product reads files back yet — this exists for
    /// tests and for the future replay/OCR-corpus paths (Q14).
    /// </summary>
    Task<Stream> OpenAsync(string fileKey, CancellationToken cancellationToken = default);
}
