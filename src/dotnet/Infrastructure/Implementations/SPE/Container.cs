using Newtonsoft.Json;
using System;

namespace Infrastructure.Implementations.SPE
{
    public class Container
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "displayName")]
        public required string DisplayName { get; set; }
        [JsonProperty(PropertyName = "description")]
        public string? Description { get; set; }
        [JsonProperty(PropertyName = "containerTypeId")]
        public required string ContainerTypeId { get; set; }
        [JsonProperty(PropertyName = "createdDateTime")]
        public DateTime CreatedDateTime { get; set; }
        [JsonProperty(PropertyName = "status")]
        public required string Status { get; set; }
        [JsonProperty(PropertyName = "size")]
        public int Size { get; set; }
    }
}
