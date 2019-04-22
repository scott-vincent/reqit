using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model defines a single API call, i.e. method + endpoint
    /// </summary>
    public class Api
    {
        public enum Methods { GET, PUT, POST, PATCH, DELETE };

        public Methods Method { get; }
        public Route Path { get; }
        public ApiBody Request { get; set; }
        public ApiBody Response { get; set; }
        public Persistence Persist { get; set; }

        public Api(Methods method, Route path)
        {
            Method = method;
            Path = path;
        }

        public override string ToString()
        {
            if (Persist == null)
            {
                return $"{Method} {Path}";
            }
            else
            {
                return $"{Method} {Path} (persist: {Persist.OutputDef})";
            }
        }
    }
}
