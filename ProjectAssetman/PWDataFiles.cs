using System.Text.Json.Serialization;
class PWDataTable
{
    public PWDataTable() { }
    String Name;
    List<PWDataEntry> DataEntries;
}

class PWDataEntry
{
    public PWDataEntry() { }
    [JsonConstructor]
    public PWDataEntry(String Name, Dictionary<String, String> DataAssets, Dictionary<String, DataTableProperty> DataProperties, Dictionary<String, String>? AssetPackages)
    {
        this.Name = Name;
        this.DataAssets = DataAssets;
        this.DataProperties = DataProperties;
        this.AssetPackages = AssetPackages;
    }
    [JsonPropertyName("Name")]
    public String? Name { get; set; }
    [JsonPropertyName("DataAssets")]
    public Dictionary<String, String>? DataAssets { get; set; }
    [JsonPropertyName("AssetPackages")]
    public Dictionary<String, String>? AssetPackages { get; set; }
    [JsonPropertyName("DataProperties")]
    public Dictionary<String, DataTableProperty>? DataProperties { get; set; }
}
class DataTableProperty
{
    public DataTableProperty() { }
    public DataTableProperty(String
        name, String type, String? value = null, Dictionary<String, DataTableProperty>? SubProperties = null, String? Tag = null)
    {
        Name = Name;
        Type = Type;
        if (Value != null) Value = value;
        if (SubProperties != null) SubProperties = SubProperties;
        if (Tag != null) Tag = Tag;
    }
    public String Name { get; set; }
    public String Type { get; set; }
    public String? Value { get; set; }
    public Dictionary<String, DataTableProperty>? SubProperties { get; set; }
    public String? Tag { get; set; }
}
