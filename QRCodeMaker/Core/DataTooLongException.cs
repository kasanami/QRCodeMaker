using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QRCodeMaker.Core
{
    public class DataTooLongException : Exception
    {
        public DataTooLongException(string message) : base(message)
        {
        }
    }
}