namespace MeshNoteLM.Interfaces;

/// <summary>
/// Interface for converting Office documents to PDF
/// </summary>
public interface IOfficeConverter
{
    /// <summary>
    /// Converts an Office document to PDF
    /// </summary>
    /// <param name="officeData">The Office document bytes</param>
    /// <param name="fileName">The file name (with extension)</param>
    /// <returns>PDF bytes, or null if conversion failed/not available</returns>
    Task<byte[]?> ConvertToPdfAsync(byte[] officeData, string fileName);

    /// <summary>
    /// Checks if the converter is currently available (e.g., user is authenticated)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets a message to display to the user if conversion is not available
    /// </summary>
    string UnavailableMessage { get; }
}
