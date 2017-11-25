using System;
using System.Net;

namespace Apollo.NetCore.Exceptions
{
    public class RemoteStatusCodeException : Exception
    {

        public RemoteStatusCodeException(HttpStatusCode statusCode, string message)
            : base(string.Format("[status code: {0:D}] {1}", statusCode, message))
        {
            StatusCode = statusCode;
        }

        public HttpStatusCode StatusCode
        {
            get;
            private set;
        }
    }
}
