using System;

namespace InterLayerLib
{
    public class WarningException : Exception
    {
        public WarningException()
        {
        }

        public WarningException(string message)
            : base(message)
        {
        }

        public WarningException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
