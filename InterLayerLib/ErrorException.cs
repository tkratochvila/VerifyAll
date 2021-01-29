using System;

namespace InterLayerLib
{
    public class ErrorException : Exception
    {
        public ErrorException()
        {
        }

        public ErrorException(string message)
            : base(message)
        {
        }

        public ErrorException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}