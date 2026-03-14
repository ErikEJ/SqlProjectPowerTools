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

    public class SimpleColumn
    {
        public string Name { get; set; } = string.Empty;

        public string? StoreType { get; set; }

        public bool IsNullable { get; set; }

        public string? Comment { get; set; }
    }

    public class SimplePrimaryKey
    {
        public string? Name { get; set; }

        public List<SimpleColumn> Columns { get; set; } = [];
    }

    public class SimpleForeignKey
    {
        public string? Name { get; set; }

        public SimpleTable PrincipalTable { get; set; } = null!;

        public List<SimpleColumn> Columns { get; set; } = [];

        public List<SimpleColumn> PrincipalColumns { get; set; } = [];
    }

    public class SimpleStoredProcedure
    {
        public string Name { get; set; } = string.Empty;

        public string Schema { get; set; } = string.Empty;
    }
}