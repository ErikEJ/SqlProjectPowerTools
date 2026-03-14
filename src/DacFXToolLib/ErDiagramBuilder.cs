using System.Text;
using DacFXToolLib.Common;
using DacFXToolLib.Dab;
using DacFXToolLib.Model;

namespace DacFXToolLib
{
    public class ErDiagramBuilder
    {
        private readonly DataApiBuilderOptions dataApiBuilderOptions;

        public ErDiagramBuilder(DataApiBuilderOptions dataApiBuilderCommandOptions)
        {
            dataApiBuilderOptions = dataApiBuilderCommandOptions;
        }

        public string GetErDiagramFileName(bool createMarkdown)
        {
            var tables = GetTablesInternal();

            var creator = new DatabaseModelToMermaid(tables);

            var diagram = creator.CreateMermaid(createMarkdown);

            var extension = createMarkdown ? ".md" : ".mmd";

            var fileName = Path.Join(dataApiBuilderOptions.ProjectPath, "dbdiagram" + extension);
            File.WriteAllText(fileName, diagram, Encoding.UTF8);

            return fileName;
        }

        private List<SimpleTable> GetTablesInternal()
        {
            var tableNames = dataApiBuilderOptions.Tables
                ?.Where(t => t.ObjectType == ObjectType.Table)
                .Select(m => m.Name);

            return DacpacModelFactory.GetTables(dataApiBuilderOptions.Dacpac, tableNames);
        }
    }
}