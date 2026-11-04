namespace Klinova.Application.Common;

/// <summary>Raised when a requested record does not exist.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public static NotFoundException For(string entity, object key) => new($"{entity} bulunamadı (#{key}).");
}
