namespace Klinova.Domain.Common;

/// <summary>
/// Raised when an operation would violate a business rule. The message is
/// intended to be shown to the end user and is therefore written in Turkish.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException()
    {
    }

    public DomainException(string message)
        : base(message)
    {
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
