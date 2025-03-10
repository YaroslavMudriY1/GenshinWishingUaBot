﻿// SPDX-License-Identifier: MPL-2.0

using System.Text.Json.Serialization;

namespace TelegramUI.Strings.Items
{
    public class Items
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }
        
        [JsonPropertyName("typeId")]
        public string TypeId { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("stars")]
        public int Stars { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }
        
        [JsonPropertyName("typeDesc")]
        public string TypeDesc { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("region")]
        public string Region { get; set; }

        [JsonPropertyName("isEvent")]
        public bool IsEvent { get; set; }
    }
}