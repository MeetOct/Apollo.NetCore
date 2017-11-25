using System;
using System.Collections.Generic;
using System.Text;

namespace Apollo.NetCore.Exceptions
{
    public class RemoteException : Exception
    {
        public RemoteException(string message) : base(message) { }
        public RemoteException(string message, Exception ex) : base(message, ex) { }
    }
}
