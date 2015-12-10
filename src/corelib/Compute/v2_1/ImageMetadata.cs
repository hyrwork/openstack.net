using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenStack.Serialization;

namespace OpenStack.Compute.v2_1
{
    /// <summary />
    [JsonConverterWithConstructor(typeof (RootWrapperConverter), "metadata")]
    public class ImageMetadata : Dictionary<string, string>, IHaveExtraData, IServiceResource
    {
        /// <summary />
        [JsonIgnore]
        internal protected Identifier ImageId { get; set; }

        /// <summary />
        [JsonExtensionData]
        IDictionary<string, JToken> IHaveExtraData.Data { get; set; } = new Dictionary<string, JToken>();

        object IServiceResource.Owner { get; set; }

        /// <summary />
        public async Task UpdateAsync(bool overwrite = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            var compute = this.TryGetOwner<ComputeApiBuilder>();
            var results = await compute.UpdateImageMetadataAsync<ImageMetadata>(ImageId, this, overwrite, cancellationToken);
            Clear();
            foreach (var result in results)
            {
                Add(result.Key, result.Value);
            }
        }
    }
}