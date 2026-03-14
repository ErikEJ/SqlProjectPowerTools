namespace DacFXToolLib.Model
{
    public class SimpleTable
    {
        public string Name { get; set; } = string.Empty;

        public string Schema { get; set; } = string.Empty;

        public string? Comment { get; set; }

        public List<SimpleColumn> Columns { get; set; } = [];

        public SimplePrimaryKey? PrimaryKey { get; set; }

        public List<SimpleForeignKey> ForeignKeys { get; set; } = [];
    }
}