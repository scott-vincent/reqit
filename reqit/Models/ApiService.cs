using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model defines a complete API service as defined in the
    /// YAML file, i.e. all individual entities and API calls. Note
    /// that an alias is just stored as an entity of type REF as
    /// they are equivalent.
    /// </summary>
    public class ApiService
    {
        public Entity EntityRoot { get; set; }
        public List<Api> Apis { get; set; }

        public ApiService()
        {
            // Root entity has null name so it does not get indexed by entity search
            EntityRoot = new Entity(null, Entity.Types.PARENT);
            Apis = new List<Api>();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(EntityRoot.ToString());

            sb.AppendLine("api:");
            foreach (var api in Apis)
            {
                sb.AppendLine("  " + api.ToString());
            }

            return sb.ToString();
        }
    }
}
