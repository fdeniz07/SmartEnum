namespace SmartEnum;

/// <summary>
/// Thrown when Smart Enum Registry validation fails during initialization.
/// </summary>
public sealed class SmartEnumValidationException : Exception
{
    /// <summary>
    /// Initializes a new validation exception.
    /// </summary>
    /// <param name="message">Failure description.</param>
    public SmartEnumValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new validation exception with an inner exception.
    /// </summary>
    public SmartEnumValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
