using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PonscripterParser
{
    class SegmentException : Exception
    {
        public SegmentException()
        {
        }

        public SegmentException(string message) : base(message)
        {
        }

        public SegmentException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    class PonscripterWrongNumArguments : Exception
    {
        public PonscripterWrongNumArguments()
        {
        }

        public PonscripterWrongNumArguments(string message) : base(message)
        {
        }

        public PonscripterWrongNumArguments(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
