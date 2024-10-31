using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentCleanUp.Helpers
{
    public class Constants
    {
        public const string COSMOS_DOCUMENTS_DATABASE_NAME = "history";
        public const string COSMOS_DOCUMENTS_CONTAINER_NAME = "documentsperthread";
        public const string COSMOS_DOCUMENTS_LEASE_CONTAINER_NAME = "docsleases";
        public const string COSMOS_THREADS_DATABASE_NAME = "history";
        public const string COSMOS_THREADS_CONTAINER_NAME = "threadhistory";
        public const string COSMOS_THREAD_LEASE_CONTAINER_NAME = "leases";
    }
}
