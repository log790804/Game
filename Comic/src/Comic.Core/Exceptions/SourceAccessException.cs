namespace Comic.Core.Exceptions;

public sealed class SourceAccessException : Exception
{
    public SourceAccessException(string message)
        : base(message)
    {
    }
}

