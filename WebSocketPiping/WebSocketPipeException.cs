namespace WebSocketPiping;

public class WebSocketPipeException : Exception
{
    public WebSocketPipeException()
    {
    }

    public WebSocketPipeException(string message)
        : base(message)
    {
    }

    public WebSocketPipeException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

