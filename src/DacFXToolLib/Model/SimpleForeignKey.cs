namespace DacFXToolLib.Model
{
    public class SimpleForeignKey
    {
        public string? Name { get; set; }

        public SimpleTable PrincipalTable { get; set; } = null!;

        public List<SimpleColumn> Columns { get; init; } = [];

        public List<SimpleColumn> PrincipalColumns { get; init; } = [];
    }
}