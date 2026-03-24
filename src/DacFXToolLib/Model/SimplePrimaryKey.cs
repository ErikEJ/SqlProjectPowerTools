namespace DacFXToolLib.Model
{
    public class SimplePrimaryKey
    {
        public string? Name { get; set; }

        public List<SimpleColumn> Columns { get; init; } = [];
    }
}